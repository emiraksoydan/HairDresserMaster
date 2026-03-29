using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Business.Concrete
{
    public class AIAssistantManager : IAIAssistantService
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IFavoriteService _favoriteService;
        private readonly IBarberStoreService _barberStoreService;
        private readonly IFreeBarberService _freeBarberService;
        private readonly IUserService _userService;
        private readonly IServiceOfferingDal _serviceOfferingDal;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIAssistantManager> _logger;
        private readonly HttpClient _httpClient;

        private const string CHAT_COMPLETIONS_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
        private const double NEARBY_RADIUS_KM = 10.0;

        public AIAssistantManager(
            IAppointmentService appointmentService,
            IFavoriteService favoriteService,
            IBarberStoreService barberStoreService,
            IFreeBarberService freeBarberService,
            IUserService userService,
            IServiceOfferingDal serviceOfferingDal,
            IConfiguration configuration,
            ILogger<AIAssistantManager> logger,
            IHttpClientFactory httpClientFactory)
        {
            _appointmentService = appointmentService;
            _favoriteService = favoriteService;
            _barberStoreService = barberStoreService;
            _freeBarberService = freeBarberService;
            _userService = userService;
            _serviceOfferingDal = serviceOfferingDal;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("AI");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Entry point
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IDataResult<AIAssistantResponseDto>> ProcessRequestAsync(
            Guid userId, string userMessage, string language = "tr", double? latitude = null, double? longitude = null)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return new ErrorDataResult<AIAssistantResponseDto>("Mesaj boş olamaz.");

            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                return new ErrorDataResult<AIAssistantResponseDto>("AI asistanı şu anda kullanılamıyor.");

            // 1) Aktif randevular
            var activeRes = await _appointmentService.GetAllAppointmentByFilter(userId, AppointmentFilter.Active);
            var appointments = activeRes.Success ? activeRes.Data ?? new() : new();

            // 2) Yakın çevredeki berberler ve dükkanlar (konum varsa 10km, yoksa favoriler)
            List<FreeBarberGetDto> nearbyFreeBarbers = new();
            List<BarberStoreGetDto> nearbyStores = new();
            bool hasLocation = latitude.HasValue && longitude.HasValue;

            if (hasLocation)
            {
                var fbRes = await _freeBarberService.GetNearbyFreeBarberAsync(latitude!.Value, longitude!.Value, NEARBY_RADIUS_KM, userId);
                if (fbRes.Success) nearbyFreeBarbers = (fbRes.Data ?? new()).Where(f => f.IsAvailable == true).ToList();

                var stRes = await _barberStoreService.GetNearbyStoresAsync(latitude!.Value, longitude!.Value, NEARBY_RADIUS_KM, userId);
                if (stRes.Success) nearbyStores = stRes.Data ?? new();
            }

            // 3) Kullanıcı rolü + kendi mağazaları (store ise)
            var userRole = DetermineUserRole(userId, appointments);
            List<BarberStoreMineDto> myStores = new();
            if (userRole == "store")
            {
                var storesRes = await _barberStoreService.GetByCurrentUserAsync(userId);
                if (storesRes.Success) myStores = storesRes.Data ?? new();
            }

            // 4) Kullanıcı profili (isim + müşteri no)
            var profileRes = await _userService.GetMe(userId);
            var profile = profileRes.Success ? profileRes.Data : null;

            // 5) System prompt oluştur
            var systemPrompt = BuildSystemPrompt(userId, userRole, appointments, nearbyFreeBarbers, nearbyStores, myStores, profile, language, hasLocation);

            // 6) Gemini'ya gönder
            IntentResponse? intent;
            try
            {
                intent = await CallGpt4oAsync(apiKey, systemPrompt, userMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API call failed");
                return new ErrorDataResult<AIAssistantResponseDto>("AI asistanı yanıt vermedi. Lütfen tekrar deneyin.");
            }

            if (intent == null)
                return new ErrorDataResult<AIAssistantResponseDto>("AI asistanından geçersiz yanıt alındı.");

            // 7) Intent'e göre aksiyon
            var result = new AIAssistantResponseDto
            {
                Intent = intent.Intent ?? "unknown",
                Response = intent.Response ?? "Anlaşılamadı.",
            };

            // --- Çoklu karar (bulk_decide) — dükkan sahibi için ---
            if (intent.Intent?.ToLower() == "bulk_decide" && intent.Decisions?.Count > 0)
                return await HandleBulkDecideAsync(userId, userRole, intent, result);

            // --- Tekil randevu yönetimi ---
            if (!string.IsNullOrEmpty(intent.AppointmentId) &&
                Guid.TryParse(intent.AppointmentId, out var appointmentId))
            {
                result.AffectedAppointmentId = appointmentId;
                switch (intent.Intent?.ToLower())
                {
                    case "approve_appointment":
                        var ap = await ExecuteDecisionAsync(userId, userRole, appointmentId, true);
                        result.ActionTaken = ap.Success;
                        if (!ap.Success) result.Response = ap.Message ?? result.Response;
                        break;
                    case "reject_appointment":
                        var rj = await ExecuteDecisionAsync(userId, userRole, appointmentId, false);
                        result.ActionTaken = rj.Success;
                        if (!rj.Success) result.Response = rj.Message ?? result.Response;
                        break;
                    case "cancel_appointment":
                        var cn = await _appointmentService.CancelAsync(userId, appointmentId);
                        result.ActionTaken = cn.Success;
                        if (!cn.Success) result.Response = cn.Message ?? result.Response;
                        break;
                }
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            // --- Randevu oluşturma ---
            switch (intent.Intent?.ToLower())
            {
                case "create_c2fb":
                    return await HandleCreateC2FBAsync(userId, intent, nearbyFreeBarbers, result);
                case "create_c2s":
                    return await HandleCreateC2SAsync(userId, intent, nearbyStores, result);
                case "create_fb2s":
                    return await HandleCreateFB2SAsync(userId, intent, nearbyStores, result);
                case "create_s2fb":
                    return await HandleCreateS2FBAsync(userId, intent, myStores, nearbyFreeBarbers, result);
            }

            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Create handlers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Müşteri → Serbest Berber randevusu oluştur.</summary>
        private async Task<IDataResult<AIAssistantResponseDto>> HandleCreateC2FBAsync(
            Guid userId, IntentResponse intent, List<FreeBarberGetDto> nearbyFreeBarbers, AIAssistantResponseDto result)
        {
            var (fb, fbMultiple) = await FindFreeBarberByIdentifierAsync(nearbyFreeBarbers, intent.TargetIdentifier);

            if (fb == null)
            {
                result.Response = BuildNotFoundMessage(intent.TargetIdentifier, "serbest berber", nearbyFreeBarbers.Select(f => f.FullName), fbMultiple);
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            // Hizmet ID'lerini eşleştir
            var serviceIds = MatchServiceIds(fb.Offerings, intent.ServiceNames);

            // Tarih/saat parse
            var date = ParseDateOnly(intent.Date);
            var start = ParseTimeSpan(intent.StartTime);
            var end = ParseTimeSpan(intent.EndTime);

            var req = new CreateAppointmentRequestDto
            {
                FreeBarberUserId = fb.FreeBarberUserId,
                StoreSelectionType = StoreSelectionType.CustomRequest,
                AppointmentDate = date,
                StartTime = start,
                EndTime = end,
                ServiceOfferingIds = serviceIds,
                Note = intent.Note,
            };

            var createRes = await _appointmentService.CreateCustomerToFreeBarberAsync(userId, req);
            result.ActionTaken = createRes.Success;
            if (createRes.Success)
            {
                result.AffectedAppointmentId = createRes.Data;
                result.Response = BuildCreateSuccessMessage(intent, fb.FullName, serviceIds.Count, date, start, end, language: intent.Response);
            }
            else
            {
                result.Response = createRes.Message ?? result.Response;
            }
            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        /// <summary>Müşteri → Dükkan randevusu oluştur.</summary>
        private async Task<IDataResult<AIAssistantResponseDto>> HandleCreateC2SAsync(
            Guid userId, IntentResponse intent, List<BarberStoreGetDto> nearbyStores, AIAssistantResponseDto result)
        {
            var (store, storeMultiple) = await FindStoreByIdentifierAsync(nearbyStores, intent.TargetIdentifier);

            if (store == null)
            {
                result.Response = BuildNotFoundMessage(intent.TargetIdentifier, "dükkan", nearbyStores.Select(s => s.StoreName), storeMultiple);
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            var storeId = store.Id;

            var date = ParseDateOnly(intent.Date);
            if (date == null)
            {
                result.Response = "Randevu için tarih belirtmelisiniz. Örnek: 'Cuma günü' veya '2 Nisan'";
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            var start = ParseTimeSpan(intent.StartTime);
            var end = ParseTimeSpan(intent.EndTime);

            // Müsaitlik kontrolü — uygun koltuk+slot bul
            var (chairId, resolvedStart, resolvedEnd, slotError) =
                await FindAvailableChairSlotAsync(storeId, date.Value, start, end);

            if (slotError != null)
            {
                result.Response = slotError;
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            // Hizmetleri dükkan üzerinden al
            var storeServices = await _serviceOfferingDal.GetServiceOfferingsByIdAsync(storeId);
            var storeOfferings = storeServices.Select(s => new ServiceOfferingGetDto
            {
                Id = s.Id,
                ServiceName = s.ServiceName,
                Price = s.Price
            }).ToList();
            var serviceIds = MatchServiceIds(storeOfferings, intent.ServiceNames);

            var req = new CreateAppointmentRequestDto
            {
                StoreId = storeId,
                ChairId = chairId,
                AppointmentDate = date,
                StartTime = resolvedStart,
                EndTime = resolvedEnd,
                ServiceOfferingIds = serviceIds,
                Note = intent.Note,
            };

            var createRes = await _appointmentService.CreateCustomerToStoreControlAsync(userId, req);
            result.ActionTaken = createRes.Success;
            if (createRes.Success)
            {
                result.AffectedAppointmentId = createRes.Data;
                result.Response = BuildCreateSuccessMessage(intent, store.StoreName, serviceIds.Count, date, resolvedStart, resolvedEnd, language: intent.Response);
            }
            else
            {
                result.Response = createRes.Message ?? result.Response;
            }
            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        /// <summary>Serbest Berber → Dükkan randevusu oluştur.</summary>
        private async Task<IDataResult<AIAssistantResponseDto>> HandleCreateFB2SAsync(
            Guid userId, IntentResponse intent, List<BarberStoreGetDto> nearbyStores, AIAssistantResponseDto result)
        {
            var (store, storeFb2sMultiple) = await FindStoreByIdentifierAsync(nearbyStores, intent.TargetIdentifier);

            if (store == null)
            {
                result.Response = BuildNotFoundMessage(intent.TargetIdentifier, "dükkan", nearbyStores.Select(s => s.StoreName), storeFb2sMultiple);
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            var storeId = store.Id;
            var date = ParseDateOnly(intent.Date);

            if (date == null)
            {
                result.Response = "Randevu için tarih belirtmelisiniz.";
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            var start = ParseTimeSpan(intent.StartTime);
            var end = ParseTimeSpan(intent.EndTime);

            var (chairId, resolvedStart, resolvedEnd, slotError) =
                await FindAvailableChairSlotAsync(storeId, date.Value, start, end);

            if (slotError != null)
            {
                result.Response = slotError;
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            var storeServices = await _serviceOfferingDal.GetServiceOfferingsByIdAsync(storeId);
            var storeOfferings = storeServices.Select(s => new ServiceOfferingGetDto
            {
                Id = s.Id,
                ServiceName = s.ServiceName,
                Price = s.Price
            }).ToList();
            var serviceIds = MatchServiceIds(storeOfferings, intent.ServiceNames);

            var req = new CreateAppointmentRequestDto
            {
                StoreId = storeId,
                ChairId = chairId,
                AppointmentDate = date,
                StartTime = resolvedStart,
                EndTime = resolvedEnd,
                ServiceOfferingIds = serviceIds,
                Note = intent.Note,
            };

            var createRes = await _appointmentService.CreateFreeBarberToStoreAsync(userId, req);
            result.ActionTaken = createRes.Success;
            if (createRes.Success)
            {
                result.AffectedAppointmentId = createRes.Data;
                result.Response = BuildCreateSuccessMessage(intent, store.StoreName, serviceIds.Count, date, resolvedStart, resolvedEnd, language: intent.Response);
            }
            else
            {
                result.Response = createRes.Message ?? result.Response;
            }
            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        /// <summary>Dükkan → Serbest Berber daveti oluştur.</summary>
        private async Task<IDataResult<AIAssistantResponseDto>> HandleCreateS2FBAsync(
            Guid userId, IntentResponse intent, List<BarberStoreMineDto> myStores,
            List<FreeBarberGetDto> nearbyFreeBarbers, AIAssistantResponseDto result)
        {
            if (!myStores.Any())
            {
                result.Response = "Kayıtlı dükkanınız bulunamadı.";
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            // Hangi mağaza? İlk mağaza veya isimle eşleşen
            var myStore = myStores.Count == 1
                ? myStores[0]
                : myStores.FirstOrDefault(s =>
                    !string.IsNullOrEmpty(intent.MyStoreName) &&
                    s.StoreName.Contains(intent.MyStoreName, StringComparison.OrdinalIgnoreCase))
                  ?? myStores[0];

            // Hedef serbest berberi yakın çevreden bul
            var (fb, fbS2FBMultiple) = await FindFreeBarberByIdentifierAsync(nearbyFreeBarbers, intent.TargetIdentifier);

            if (fb == null)
            {
                result.Response = BuildNotFoundMessage(intent.TargetIdentifier, "serbest berber", nearbyFreeBarbers.Select(f => f.FullName), fbS2FBMultiple);
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            var req = new CreateStoreToFreeBarberRequestDto
            {
                StoreId = myStore.Id,
                FreeBarberUserId = fb.FreeBarberUserId,
            };

            var createRes = await _appointmentService.CreateStoreToFreeBarberAsync(userId, req);
            result.ActionTaken = createRes.Success;
            if (createRes.Success)
            {
                result.AffectedAppointmentId = createRes.Data;
                result.Response = intent.Response ?? $"{fb.FullName} adlı serbest berbere davet gönderildi.";
            }
            else
            {
                result.Response = createRes.Message ?? result.Response;
            }
            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        /// <summary>Tek komutla birden fazla randevuya karar ver (dükkan sahibi).</summary>
        private async Task<IDataResult<AIAssistantResponseDto>> HandleBulkDecideAsync(
            Guid userId, string userRole, IntentResponse intent, AIAssistantResponseDto result)
        {
            var successIds = new List<Guid>();
            var failedMessages = new List<string>();

            foreach (var decision in intent.Decisions!)
            {
                if (!Guid.TryParse(decision.AppointmentId, out var apptId))
                    continue;

                IResult decisionResult = decision.Action?.ToLower() switch
                {
                    "approve" => await ExecuteDecisionAsync(userId, userRole, apptId, true),
                    "reject"  => await ExecuteDecisionAsync(userId, userRole, apptId, false),
                    "cancel"  => await _appointmentService.CancelAsync(userId, apptId),
                    _         => new ErrorResult("Bilinmeyen aksiyon")
                };

                if (decisionResult.Success)
                    successIds.Add(apptId);
                else
                    failedMessages.Add(decisionResult.Message ?? apptId.ToString());
            }

            result.ActionTaken = successIds.Count > 0;
            result.AffectedAppointmentIds = successIds;

            if (!string.IsNullOrWhiteSpace(intent.Response))
                result.Response = intent.Response;
            else if (failedMessages.Count == 0)
                result.Response = $"{successIds.Count} randevu işlemi tamamlandı.";
            else
                result.Response = $"{successIds.Count} işlem başarılı, {failedMessages.Count} işlem başarısız: {string.Join("; ", failedMessages)}";

            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  System prompt builder
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildSystemPrompt(
            Guid userId,
            string userRole,
            List<AppointmentGetDto> appointments,
            List<FreeBarberGetDto> nearbyFreeBarbers,
            List<BarberStoreGetDto> nearbyStores,
            List<BarberStoreMineDto> myStores,
            UserProfileDto? profile,
            string language,
            bool hasLocation)
        {
            var langLabel = language switch
            {
                "en" => "English",
                "de" => "Deutsch",
                "ar" => "العربية",
                _ => "Türkçe"
            };

            var roleLabel = userRole switch
            {
                "store" => "Dükkan Sahibi",
                "freebarber" => "Serbest Berber",
                _ => "Müşteri"
            };

            var now = DateTime.Now;
            var dayNames = new[] { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };

            var sb = new StringBuilder();
            sb.AppendLine("Sen bir berber randevu asistanısın. Kullanıcının sesli/yazılı komutlarını yorumlar ve randevu işlemlerini gerçekleştirirsin.");
            sb.AppendLine($"Yanıt dili: {langLabel}");
            sb.AppendLine();

            // Kullanıcı bilgileri
            sb.AppendLine("# Kullanıcı Bilgileri");
            sb.AppendLine($"Rol: {roleLabel}");
            if (profile != null)
            {
                sb.AppendLine($"İsim: {profile.FirstName} {profile.LastName}");
                sb.AppendLine($"Müşteri No: {profile.CustomerNumber}");
                sb.AppendLine($"Telefon: {profile.PhoneNumber}");
            }
            sb.AppendLine();

            // Bugünün tarihi
            sb.AppendLine("# Bugünün Tarihi");
            sb.AppendLine($"{now:dd.MM.yyyy} ({dayNames[(int)now.DayOfWeek]})");
            sb.AppendLine("Göreli tarih ifadelerini (bugün, yarın, öbür gün, pazartesi, cuma gibi) ISO formatına (YYYY-MM-DD) çevir.");
            sb.AppendLine();

            // Yakın çevredeki serbest berberler (10 km)
            if (nearbyFreeBarbers.Any())
            {
                sb.AppendLine(hasLocation ? "# Yakın Çevredeki Serbest Berberler (10 km)" : "# Serbest Berberler");
                foreach (var fb in nearbyFreeBarbers)
                {
                    var fbNumStr = !string.IsNullOrEmpty(fb.CustomerNumber) ? $" | BerberNo: #{fb.CustomerNumber}" : "";
                    var distStr = hasLocation ? $" | Mesafe: {fb.DistanceKm:0.#}km" : "";
                    sb.AppendLine($"- İsim: {fb.FullName}{fbNumStr}{distStr} | FreeBarberUserId: {fb.FreeBarberUserId} | FreeBarberEntityId: {fb.Id}");
                    if (fb.Offerings?.Count > 0)
                    {
                        var offeringStr = string.Join(", ", fb.Offerings.Select(o => $"{o.ServiceName} ({o.Price:0.##}₺) [ID:{o.Id}]"));
                        sb.AppendLine($"  Hizmetler: {offeringStr}");
                    }
                    else
                    {
                        sb.AppendLine("  Hizmetler: (tanımlı hizmet yok)");
                    }
                }
                sb.AppendLine();
            }

            // Yakın çevredeki dükkanlar (10 km)
            if (nearbyStores.Any())
            {
                sb.AppendLine(hasLocation ? "# Yakın Çevredeki Dükkanlar (10 km)" : "# Dükkanlar");
                foreach (var store in nearbyStores)
                {
                    var storeNoStr = !string.IsNullOrEmpty(store.StoreNo) ? $" | DükkanNo: #{store.StoreNo}" : "";
                    var distStr = hasLocation ? $" | Mesafe: {store.DistanceKm:0.#}km" : "";
                    sb.AppendLine($"- İsim: {store.StoreName}{storeNoStr}{distStr} | StoreId: {store.Id}");
                }
                sb.AppendLine();
            }

            // Kullanıcının kendi mağazaları (dükkan sahibi ise)
            if (myStores.Any())
            {
                sb.AppendLine("# Kendi Mağazalarım");
                for (int i = 0; i < myStores.Count; i++)
                {
                    var noStr = !string.IsNullOrEmpty(myStores[i].StoreNo) ? $" | DükkanNo: #{myStores[i].StoreNo}" : "";
                    sb.AppendLine($"- [{i + 1}. Dükkan] {myStores[i].StoreName}{noStr} | StoreId: {myStores[i].Id}");
                }
                sb.AppendLine("Kullanıcı '1. dükkan', '2. dükkan', dükkan ismi veya DükkanNo (#XXXX) ile referans verebilir.");
                sb.AppendLine();
            }

            // Aktif randevular — dükkan sahibi için mağazaya göre grupla
            sb.AppendLine("# Aktif Randevular");
            if (!appointments.Any())
            {
                sb.AppendLine("(Aktif randevu yok)");
            }
            else
            {
                // Dükkan sahibi: mağazaya göre grupla ve her gruba 1-based index ver
                var storeAppointments = appointments.Where(a => a.StoreUserId == userId).ToList();
                var otherAppointments = appointments.Where(a => a.StoreUserId != userId).ToList();

                if (storeAppointments.Any())
                {
                    var grouped = storeAppointments
                        .GroupBy(a => a.StoreName ?? "Bilinmeyen Dükkan")
                        .ToList();

                    int storeIdx = 1;
                    foreach (var group in grouped)
                    {
                        // Hangi mağaza numarası?
                        var matchedStore = myStores.FirstOrDefault(s =>
                            s.StoreName.Equals(group.Key, StringComparison.OrdinalIgnoreCase));
                        var storeNoLabel = matchedStore != null && !string.IsNullOrEmpty(matchedStore.StoreNo)
                            ? $" | DükkanNo: #{matchedStore.StoreNo}"
                            : "";
                        var storeLabel = matchedStore != null
                            ? $"[{myStores.IndexOf(matchedStore) + 1}. Dükkan] {group.Key}{storeNoLabel}"
                            : $"[Dükkan {storeIdx}] {group.Key}";

                        sb.AppendLine($"## {storeLabel}");
                        foreach (var a in group)
                        {
                            sb.AppendLine("---");
                            sb.AppendLine($"RandevuId: {a.Id} | Durum: {a.Status}");
                            if (a.AppointmentDate.HasValue) sb.AppendLine($"Tarih: {a.AppointmentDate.Value:dd.MM.yyyy}");
                            if (a.StartTime.HasValue) sb.AppendLine($"Saat: {a.StartTime.Value:hh\\:mm}–{a.EndTime!.Value:hh\\:mm}");
                            if (!string.IsNullOrEmpty(a.FreeBarberName)) sb.AppendLine($"SerberstBerber: {a.FreeBarberName}");
                            if (!string.IsNullOrEmpty(a.CustomerName)) sb.AppendLine($"Müşteri: {a.CustomerName}");
                            if (a.Services?.Count > 0)
                                sb.AppendLine($"Hizmetler: {string.Join(", ", a.Services.Select(s => $"{s.ServiceName} ({s.Price:0.##}₺)"))} | Toplam: {a.TotalPrice:0.##}₺");
                            if (a.StoreDecision.HasValue) sb.AppendLine($"DükkanKararı: {a.StoreDecision.Value}");
                            if (a.FreeBarberDecision.HasValue) sb.AppendLine($"BerberKararı: {a.FreeBarberDecision.Value}");
                        }
                        storeIdx++;
                    }
                }

                foreach (var a in otherAppointments)
                {
                    sb.AppendLine("---");
                    sb.AppendLine($"RandevuId: {a.Id} | Durum: {a.Status}");
                    if (a.AppointmentDate.HasValue) sb.AppendLine($"Tarih: {a.AppointmentDate.Value:dd.MM.yyyy}");
                    if (a.StartTime.HasValue) sb.AppendLine($"Saat: {a.StartTime.Value:hh\\:mm}–{a.EndTime!.Value:hh\\:mm}");
                    if (!string.IsNullOrEmpty(a.StoreName)) sb.AppendLine($"Dükkan: {a.StoreName}");
                    if (!string.IsNullOrEmpty(a.FreeBarberName)) sb.AppendLine($"SerberstBerber: {a.FreeBarberName}");
                    if (!string.IsNullOrEmpty(a.CustomerName)) sb.AppendLine($"Müşteri: {a.CustomerName}");
                    if (a.Services?.Count > 0)
                        sb.AppendLine($"Hizmetler: {string.Join(", ", a.Services.Select(s => $"{s.ServiceName} ({s.Price:0.##}₺)"))} | Toplam: {a.TotalPrice:0.##}₺");
                    if (a.StoreDecision.HasValue) sb.AppendLine($"DükkanKararı: {a.StoreDecision.Value}");
                    if (a.FreeBarberDecision.HasValue) sb.AppendLine($"BerberKararı: {a.FreeBarberDecision.Value}");
                    if (a.CustomerDecision.HasValue) sb.AppendLine($"MüşteriKararı: {a.CustomerDecision.Value}");
                    if (a.FreeBarberUserId == userId) sb.AppendLine("Rolüm: SerberstBerber");
                    else if (a.CustomerUserId == userId) sb.AppendLine("Rolüm: Müşteri");
                }
            }
            sb.AppendLine();

            // Intent kuralları
            sb.AppendLine("# Intent Kuralları");
            sb.AppendLine("- list_appointments: randevuları özetle");
            sb.AppendLine("- approve_appointment / reject_appointment / cancel_appointment: TEK randevuya aksiyon, appointmentId zorunlu");
            sb.AppendLine("- bulk_decide: birden fazla randevuya tek komutla karar ver. decisions dizisini doldur. Örnekler:");
            sb.AppendLine("    '1. dükkanın tüm randevularını onayla' → decisions = o dükkanın pending randevuları için approve");
            sb.AppendLine("    '1. dükkanı onayla 2. dükkanı reddet' → decisions = her dükkanın randevuları için ilgili action");
            sb.AppendLine("    'tüm randevuları onayla' → decisions = tüm pending randevular için approve");
            sb.AppendLine("    Karar verilecek randevularda önce Pending/bekleyen olanları seç (StoreDecision=null veya bekliyor).");
            sb.AppendLine("- get_appointment_details: belirli randevu detayı");
            sb.AppendLine("- create_c2fb: Müşteri → SerberstBerber (CustomRequest; tarih/saat/hizmetler opsiyonel)");
            sb.AppendLine("- create_c2s: Müşteri → Dükkan (tarih zorunlu, saat opsiyonel, sistem uygun slot bulur)");
            sb.AppendLine("- create_fb2s: SerberstBerber → Dükkan (tarih zorunlu)");
            sb.AppendLine("- create_s2fb: Dükkan → SerberstBerber daveti (tarih/saat opsiyonel)");
            sb.AppendLine("- unknown: anlaşılamadı veya yetersiz bilgi");
            sb.AppendLine();
            sb.AppendLine("Kullanıcı isim veya rastgele oluşturulan müşteri/berber numarası ile kişi/dükkan tanımlayabilir.");
            sb.AppendLine("Müşteri numaraları 'C-XXXX' veya 'B-XXXX' formatındadır (örn. 'C-0042', 'B-0017'). Kullanıcı bu numaralardan birini söylerse targetIdentifier'a olduğu gibi yaz.");
            sb.AppendLine("targetIdentifier: hedefin adı veya müşteri/berber numarası. Birden fazla eşleşme varsa hepsini listele (response'ta) ve unknown dön.");
            if (!hasLocation)
                sb.AppendLine("NOT: Kullanıcı konumunu paylaşmadı. Randevu oluşturmak için yakın çevre listesi boş. Konum paylaşmasını iste.");
            sb.AppendLine();

            // Yanıt formatı
            sb.AppendLine("# Yanıt Formatı — YALNIZCA JSON döndür");
            sb.AppendLine("""
{
  "intent": "<intent>",
  "appointmentId": "<uuid veya null — tekil aksiyon için>",
  "decisions": [
    {"appointmentId": "<uuid>", "action": "approve|reject|cancel"}
  ],
  "targetIdentifier": "<hedefin adı/telefonu/nosu veya null>",
  "myStoreName": "<kullanıcının kendi mağazası varsa adı, create_s2fb için; genellikle null>",
  "date": "<YYYY-MM-DD veya null>",
  "startTime": "<HH:mm veya null>",
  "endTime": "<HH:mm veya null>",
  "serviceNames": ["<hizmet adı>"],
  "note": "<randevu notu veya null>",
  "response": "<kullanıcıya gösterilecek açıklayıcı yanıt>"
}
""");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string DetermineUserRole(Guid userId, List<AppointmentGetDto> appointments)
        {
            foreach (var a in appointments)
            {
                if (a.StoreUserId == userId) return "store";
                if (a.FreeBarberUserId == userId) return "freebarber";
                if (a.CustomerUserId == userId) return "customer";
            }
            return "customer";
        }

        private async Task<(FreeBarberGetDto? result, bool multipleMatches)> FindFreeBarberByIdentifierAsync(
            List<FreeBarberGetDto> list, string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return (list.FirstOrDefault(), false);

            // 1) Tam isim eşleşmesi
            var exact = list.Where(f => f.FullName.Equals(identifier, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return (exact[0], false);
            if (exact.Count > 1) return (null, true);

            // 2) Kısmi isim içerme
            var partial = list.Where(f => f.FullName.Contains(identifier, StringComparison.OrdinalIgnoreCase)).ToList();
            if (partial.Count == 1) return (partial[0], false);
            if (partial.Count > 1) return (null, true);

            // 3) CustomerNumber ile eşleşme (örn. "B-0017")
            var cleanId = identifier.TrimStart('#');
            var byNum = list.FirstOrDefault(f => f.CustomerNumber?.Equals(cleanId, StringComparison.OrdinalIgnoreCase) == true);
            if (byNum != null) return (byNum, false);

            // 4) IUserService ile müşteri numarasından userId çöz
            var customerNumRes = await _userService.GetByCustomerNumber(identifier);
            if (customerNumRes.Success && customerNumRes.Data != null)
            {
                var resolvedUserId = customerNumRes.Data.Id;
                return (list.FirstOrDefault(f => f.FreeBarberUserId == resolvedUserId), false);
            }

            return (null, false);
        }

        private async Task<(BarberStoreGetDto? result, bool multipleMatches)> FindStoreByIdentifierAsync(
            List<BarberStoreGetDto> list, string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return (list.FirstOrDefault(), false);

            // 1) Tam isim eşleşmesi
            var exact = list.Where(s => s.StoreName.Equals(identifier, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return (exact[0], false);
            if (exact.Count > 1) return (null, true);

            // 2) Kısmi isim içerme
            var partial = list.Where(s => s.StoreName.Contains(identifier, StringComparison.OrdinalIgnoreCase)).ToList();
            if (partial.Count == 1) return (partial[0], false);
            if (partial.Count > 1) return (null, true);

            // 3) StoreNo ile eşleşme (örn. "#A1B2C3")
            var cleanId = identifier.TrimStart('#');
            var byStoreNo = list.FirstOrDefault(s => s.StoreNo?.Equals(cleanId, StringComparison.OrdinalIgnoreCase) == true);
            if (byStoreNo != null) return (byStoreNo, false);

            // 4) IUserService ile owner userId çöz (mağaza sahibinin müşteri no ile arama)
            var customerNumRes = await _userService.GetByCustomerNumber(identifier);
            if (customerNumRes.Success && customerNumRes.Data != null)
            {
                var resolvedUserId = customerNumRes.Data.Id;
                var byOwner = list.FirstOrDefault(s => s.BarberStoreOwnerId == resolvedUserId);
                return (byOwner, false);
            }

            return (null, false);
        }

        private static List<Guid> MatchServiceIds(
            List<ServiceOfferingGetDto>? offerings, List<string>? requestedNames)
        {
            if (offerings == null || offerings.Count == 0) return new();
            if (requestedNames == null || requestedNames.Count == 0) return new();

            var result = new List<Guid>();
            foreach (var name in requestedNames)
            {
                var matched = offerings.FirstOrDefault(o =>
                    o.ServiceName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(o.ServiceName, StringComparison.OrdinalIgnoreCase));
                if (matched != null && !result.Contains(matched.Id))
                    result.Add(matched.Id);
            }
            return result;
        }

        private async Task<(Guid? chairId, TimeSpan? start, TimeSpan? end, string? error)>
            FindAvailableChairSlotAsync(Guid storeId, DateOnly date, TimeSpan? requestedStart, TimeSpan? requestedEnd)
        {
            var availability = await _appointmentService.GetAvailibity(storeId, date);
            if (!availability.Success || availability.Data == null || !availability.Data.Any())
                return (null, null, null, $"{date:dd.MM.yyyy} tarihinde müsait koltuk bulunamadı.");

            // İstenen saatte uygun slot ara
            foreach (var chair in availability.Data)
            {
                if (requestedStart.HasValue && requestedEnd.HasValue)
                {
                    // Hem başlangıç hem bitiş belirtilmiş — tam aralık eşleşmesi
                    var slot = chair.Slots.FirstOrDefault(s =>
                        !s.IsBooked && !s.IsPast &&
                        TimeSpan.TryParse(s.Start, out var sStart) &&
                        TimeSpan.TryParse(s.End, out var sEnd) &&
                        sStart <= requestedStart.Value && sEnd >= requestedEnd.Value);

                    if (slot != null)
                        return (chair.ChairId, requestedStart, requestedEnd, null);
                }
                else if (requestedStart.HasValue)
                {
                    // Sadece başlangıç saati belirtilmiş — o saatten itibaren ilk müsait slotu al
                    var slot = chair.Slots.FirstOrDefault(s =>
                        !s.IsBooked && !s.IsPast &&
                        TimeSpan.TryParse(s.Start, out var sStart) &&
                        sStart >= requestedStart.Value);

                    if (slot != null &&
                        TimeSpan.TryParse(slot.Start, out var fStart) &&
                        TimeSpan.TryParse(slot.End, out var fEnd))
                        return (chair.ChairId, fStart, fEnd, null);
                }
                else
                {
                    // Hiç saat belirtilmemiş — ilk müsait slotu al
                    var firstFree = chair.Slots.FirstOrDefault(s => !s.IsBooked && !s.IsPast);
                    if (firstFree != null &&
                        TimeSpan.TryParse(firstFree.Start, out var fStart) &&
                        TimeSpan.TryParse(firstFree.End, out var fEnd))
                        return (chair.ChairId, fStart, fEnd, null);
                }
            }

            // İstenen saatte uygun slot yok — müsait saatleri listele
            var freeSlots = availability.Data
                .SelectMany(c => c.Slots.Where(s => !s.IsBooked && !s.IsPast).Select(s => s.Start))
                .Distinct().Take(5).ToList();

            if (!freeSlots.Any())
                return (null, null, null, $"{date:dd.MM.yyyy} tarihinde tüm slotlar dolu.");

            var slotsText = string.Join(", ", freeSlots);
            return (null, null, null,
                $"{date:dd.MM.yyyy} tarihinde {(requestedStart.HasValue ? requestedStart.Value:default):hh\\:mm} için uygun slot yok. " +
                $"Müsait saatler: {slotsText}. Yeni bir saat belirtin.");
        }

        private static DateOnly? ParseDateOnly(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            return DateOnly.TryParse(dateStr, out var d) ? d : null;
        }

        private static TimeSpan? ParseTimeSpan(string? timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return null;
            return TimeSpan.TryParse(timeStr, out var t) ? t : null;
        }

        private static string BuildNotFoundMessage(string? identifier, string targetType, IEnumerable<string?> available, bool multipleMatches = false)
        {
            var list = available.Where(n => !string.IsNullOrEmpty(n)).ToList();
            var sb = new StringBuilder();

            if (multipleMatches)
            {
                sb.Append($"'{identifier}' ifadesiyle birden fazla {targetType} eşleşti. Lütfen tam isim veya numara belirtin.");
                if (list.Any())
                    sb.Append($" Eşleşenler: {string.Join(", ", list)}.");
            }
            else
            {
                sb.Append(string.IsNullOrWhiteSpace(identifier)
                    ? $"Lütfen bir {targetType} belirtin."
                    : $"'{identifier}' adlı {targetType} yakın çevrenizde bulunamadı.");

                if (list.Any())
                    sb.Append($" Yakın çevrenizdeki {targetType}lar: {string.Join(", ", list)}.");
                else
                    sb.Append($" Yakın çevrenizde {targetType} bulunamadı.");
            }

            return sb.ToString();
        }

        private static string BuildCreateSuccessMessage(
            IntentResponse intent, string targetName, int serviceCount,
            DateOnly? date, TimeSpan? start, TimeSpan? end, string? language)
        {
            // GPT zaten bir response üretmiş — onu kullan
            if (!string.IsNullOrWhiteSpace(intent.Response) && intent.Response.Length > 10)
                return intent.Response;

            var sb = new StringBuilder($"{targetName} için randevu isteği gönderildi.");
            if (date.HasValue) sb.Append($" Tarih: {date.Value:dd.MM.yyyy}.");
            if (start.HasValue && end.HasValue) sb.Append($" Saat: {start.Value:hh\\:mm}–{end.Value:hh\\:mm}.");
            if (serviceCount > 0) sb.Append($" {serviceCount} hizmet eklendi.");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gemini call
        // ─────────────────────────────────────────────────────────────────────

        private async Task<IntentResponse?> CallGpt4oAsync(string apiKey, string systemPrompt, string userMessage)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, CHAT_COMPLETIONS_ENDPOINT);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model = "gemini-2.0-flash",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                response_format = new { type = "json_object" },
                max_tokens = 1000,
                temperature = 0.15
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.SendAsync(request);
            var raw = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini error: {Status} - {Body}", httpResponse.StatusCode, raw);
                return null;
            }

            var gpt = JsonSerializer.Deserialize<GptChatResponse>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var content = gpt?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content)) return null;

            return JsonSerializer.Deserialize<IntentResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Whisper transcription
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IDataResult<string>> TranscribeAudioAsync(Stream audioStream, string fileName)
        {
            var apiKey = _configuration["Groq:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                return new ErrorDataResult<string>("AI servisi şu anda kullanılamıyor.");

            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(audioStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
            content.Add(streamContent, "file", fileName);
            content.Add(new StringContent("whisper-large-v3-turbo"), "model");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Whisper error: {Status} - {Body}", response.StatusCode, body);
                return new ErrorDataResult<string>("Ses metne dönüştürülemedi.");
            }

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("text").GetString() ?? "";
            return new SuccessDataResult<string>(text);
        }

        private async Task<IResult> ExecuteDecisionAsync(Guid userId, string userRole, Guid appointmentId, bool approve)
        {
            return userRole switch
            {
                "store" => await _appointmentService.StoreDecisionAsync(userId, appointmentId, approve),
                "freebarber" => await _appointmentService.FreeBarberDecisionAsync(userId, appointmentId, approve),
                _ => await _appointmentService.CustomerDecisionAsync(userId, appointmentId, approve),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private response models
        // ─────────────────────────────────────────────────────────────────────

        private class GptChatResponse
        {
            public GptChoice[]? Choices { get; set; }
        }

        private class GptChoice
        {
            public GptMessage? Message { get; set; }
        }

        private class GptMessage
        {
            public string? Content { get; set; }
        }

        private class IntentResponse
        {
            [JsonPropertyName("intent")]
            public string? Intent { get; set; }

            [JsonPropertyName("appointmentId")]
            public string? AppointmentId { get; set; }

            /// <summary>bulk_decide için: her bir randevu için { appointmentId, action } listesi</summary>
            [JsonPropertyName("decisions")]
            public List<BulkDecisionItem>? Decisions { get; set; }

            [JsonPropertyName("targetIdentifier")]
            public string? TargetIdentifier { get; set; }

            [JsonPropertyName("myStoreName")]
            public string? MyStoreName { get; set; }

            [JsonPropertyName("date")]
            public string? Date { get; set; }

            [JsonPropertyName("startTime")]
            public string? StartTime { get; set; }

            [JsonPropertyName("endTime")]
            public string? EndTime { get; set; }

            [JsonPropertyName("serviceNames")]
            public List<string>? ServiceNames { get; set; }

            [JsonPropertyName("note")]
            public string? Note { get; set; }

            [JsonPropertyName("response")]
            public string? Response { get; set; }
        }

        private class BulkDecisionItem
        {
            [JsonPropertyName("appointmentId")]
            public string? AppointmentId { get; set; }

            /// <summary>approve | reject | cancel</summary>
            [JsonPropertyName("action")]
            public string? Action { get; set; }
        }
    }
}
