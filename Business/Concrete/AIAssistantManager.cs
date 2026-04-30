using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
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
        private readonly IServicePackageDal _servicePackageDal;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIAssistantManager> _logger;
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private const string CHAT_COMPLETIONS_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
        private const double NEARBY_RADIUS_KM = 10.0;
        private static readonly ConcurrentDictionary<Guid, PendingCreateIntentState> _pendingCreateIntents = new();
        private static readonly TimeSpan PendingIntentTtl = TimeSpan.FromMinutes(30);

        public AIAssistantManager(
            IAppointmentService appointmentService,
            IFavoriteService favoriteService,
            IBarberStoreService barberStoreService,
            IFreeBarberService freeBarberService,
            IUserService userService,
            IServiceOfferingDal serviceOfferingDal,
            IServicePackageDal servicePackageDal,
            IConfiguration configuration,
            ILogger<AIAssistantManager> logger,
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            _appointmentService = appointmentService;
            _favoriteService = favoriteService;
            _barberStoreService = barberStoreService;
            _freeBarberService = freeBarberService;
            _userService = userService;
            _serviceOfferingDal = serviceOfferingDal;
            _servicePackageDal = servicePackageDal;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("AI");
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Log'lar için kullanıcı kimliği getirir. Auth context yoksa "anonymous" döner.
        /// Hata fırlatmaz; sadece logger'a ek bilgi olarak kullanılır.
        /// </summary>
        private string GetCurrentUserIdForLog()
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return "anonymous";
            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrWhiteSpace(id) ? "anonymous" : id;
        }

        /// <summary>
        /// Verilen owner id'lerin her biri için paket listesini paralel şekilde çeker.
        /// Auth bypass'li (DAL doğrudan) — AI context'i için read-only. Hata olursa
        /// boş liste döner, prompt inşası aksamaz.
        /// </summary>
        private async Task<Dictionary<Guid, List<ServicePackageGetDto>>> FetchPackagesByOwnersAsync(
            IEnumerable<Guid> ownerIds)
        {
            var dict = new Dictionary<Guid, List<ServicePackageGetDto>>();
            var ids = ownerIds.Distinct().Where(id => id != Guid.Empty).ToList();
            if (ids.Count == 0) return dict;

            try
            {
                var tasks = ids.Select(async id =>
                {
                    try
                    {
                        var list = await _servicePackageDal.GetPackagesByOwnerIdAsync(id);
                        return (id, list ?? new List<ServicePackageGetDto>());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[AI context] FetchPackages failed for ownerId={OwnerId}", id);
                        return (id, new List<ServicePackageGetDto>());
                    }
                });

                var results = await Task.WhenAll(tasks);
                foreach (var (id, list) in results)
                    dict[id] = list;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AI context] FetchPackagesByOwnersAsync unexpected error");
            }

            return dict;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Entry point
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IDataResult<AIAssistantResponseDto>> ProcessRequestAsync(
            Guid userId, string userMessage, string language = "tr", double? latitude = null, double? longitude = null)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return new ErrorDataResult<AIAssistantResponseDto>("empty_message");

            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("[Assistant] UserId={UserId} - Gemini API key missing in configuration.", userId);
                return new ErrorDataResult<AIAssistantResponseDto>("ai_unavailable");
            }

            // 1) Aktif randevular — AI context için son 10 yeterli; kullanıcı geçmişi büyüdükçe
            // tam liste çekmek anlamsızca büyük bir prompt ve memory yükü yaratıyordu.
            var activeRes = await _appointmentService.GetAllAppointmentByFilter(userId, AppointmentFilter.Active, limit: 10);
            var appointments = activeRes.Success ? activeRes.Data ?? new() : new();

            // 2) Yakın çevredeki berberler ve dükkanlar (konum varsa 10km, yoksa favoriler)
            List<FreeBarberGetDto> nearbyFreeBarbers = new();
            List<BarberStoreGetDto> nearbyStores = new();
            bool hasLocation = latitude.HasValue && longitude.HasValue;

            if (hasLocation)
            {
                var fbRes = await _freeBarberService.GetNearbyFreeBarberAsync(latitude!.Value, longitude!.Value, NEARBY_RADIUS_KM, userId);
                if (fbRes.Success) nearbyFreeBarbers = (fbRes.Data ?? new()).Where(f => f.IsAvailable == true).ToList();
                else _logger.LogWarning("AI context: GetNearbyFreeBarber failed for user {UserId}: {Message}", userId, fbRes.Message);

                var stRes = await _barberStoreService.GetNearbyStoresAsync(latitude!.Value, longitude!.Value, NEARBY_RADIUS_KM, userId);
                if (stRes.Success) nearbyStores = stRes.Data ?? new();
                else _logger.LogWarning("AI context: GetNearbyStores failed for user {UserId}: {Message}", userId, stRes.Message);
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

            // 4.5) Hizmet paketleri — her berber / dükkan sahibinin paketlerini bir sözlükte topla.
            // Böylece prompt'ta her kart altına paketleri yazabiliriz ve model "1. paket", "VIP paketi"
            // gibi ifadeleri doğru ID'ye eşleyebilir.
            var ownerIdsForPackages = new HashSet<Guid>();
            foreach (var fb in nearbyFreeBarbers) ownerIdsForPackages.Add(fb.FreeBarberUserId);
            foreach (var st in nearbyStores) if (st.BarberStoreOwnerId.HasValue) ownerIdsForPackages.Add(st.BarberStoreOwnerId.Value);
            if (myStores.Any()) ownerIdsForPackages.Add(userId); // kendi mağaza(ları) — owner current user
            var packagesByOwner = await FetchPackagesByOwnersAsync(ownerIdsForPackages);

            // 5) System prompt oluştur
            var systemPrompt = BuildSystemPrompt(userId, userRole, appointments, nearbyFreeBarbers, nearbyStores, myStores, profile, language, hasLocation, packagesByOwner);

            // 6) Gemini'ya gönder (kullanıcının yarım bıraktığı create komutu varsa bağlamı mesaja ekle)
            var pendingState = GetPendingCreateIntent(userId);
            var effectiveUserMessage = BuildUserMessageWithPendingContext(userMessage, pendingState);
            IntentResponse? intent;
            try
            {
                intent = await CallGpt4oAsync(apiKey, systemPrompt, effectiveUserMessage);
            }
            catch (GeminiRateLimitException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[Assistant] UserId={UserId} ErrorCode={ErrorCode} — Gemini free-tier quota or API rate limit exceeded.",
                    userId,
                    "ai_rate_limit");
                return new ErrorDataResult<AIAssistantResponseDto>("ai_rate_limit");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Assistant] UserId={UserId} Gemini API call failed.", userId);
                return new ErrorDataResult<AIAssistantResponseDto>("ai_error");
            }

            if (intent == null)
                return new ErrorDataResult<AIAssistantResponseDto>("ai_invalid_response");

            // Kullanıcı eksik bilgi ile devam ediyorsa pending create state ile birleştir.
            intent = MergeWithPendingCreateIntent(intent, pendingState);

            // 7) Intent'e göre aksiyon
            var result = new AIAssistantResponseDto
            {
                Intent = intent.Intent ?? "unknown",
                Response = intent.Response ?? "Anlaşılamadı.",
            };

            // --- Create intent eksik alan kontrolü (multi-turn tamamlama) ---
            if (IsCreateIntent(intent.Intent))
            {
                var missingFields = GetMissingCreateFields(intent);
                if (missingFields.Count > 0)
                {
                    SavePendingCreateIntent(userId, intent);
                    result.Intent = intent.Intent ?? "unknown";
                    result.Response = BuildMissingFieldPrompt(intent, missingFields, language, fallbackResponse: intent.Response);
                    return new SuccessDataResult<AIAssistantResponseDto>(result);
                }
            }
            else if (pendingState != null && ContainsPotentialCreateDetails(intent))
            {
                // Model unknown döndü ama içinde tarih/saat/hedef vb. var; pending ile devam.
                var resumed = MergeIntentFields(pendingState.Intent, intent);
                var missingFields = GetMissingCreateFields(resumed);
                if (missingFields.Count > 0)
                {
                    SavePendingCreateIntent(userId, resumed);
                    result.Intent = resumed.Intent ?? "unknown";
                    result.Response = BuildMissingFieldPrompt(resumed, missingFields, language, fallbackResponse: intent.Response);
                    return new SuccessDataResult<AIAssistantResponseDto>(result);
                }
                intent = resumed;
                result.Intent = intent.Intent ?? "unknown";
            }

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
                {
                    var create = await HandleCreateC2FBAsync(userId, intent, nearbyFreeBarbers, packagesByOwner, result);
                    HandlePendingStateAfterCreate(userId, create.Data);
                    return create;
                }
                case "create_c2s":
                {
                    var create = await HandleCreateC2SAsync(userId, intent, nearbyStores, packagesByOwner, result);
                    HandlePendingStateAfterCreate(userId, create.Data);
                    return create;
                }
                case "create_fb2s":
                {
                    var create = await HandleCreateFB2SAsync(userId, intent, nearbyStores, packagesByOwner, result);
                    HandlePendingStateAfterCreate(userId, create.Data);
                    return create;
                }
                case "create_s2fb":
                {
                    var create = await HandleCreateS2FBAsync(userId, intent, myStores, nearbyFreeBarbers, result);
                    HandlePendingStateAfterCreate(userId, create.Data);
                    return create;
                }
            }

            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Create handlers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Müşteri → Serbest Berber randevusu oluştur.</summary>
        private async Task<IDataResult<AIAssistantResponseDto>> HandleCreateC2FBAsync(
            Guid userId, IntentResponse intent, List<FreeBarberGetDto> nearbyFreeBarbers,
            Dictionary<Guid, List<ServicePackageGetDto>> packagesByOwner,
            AIAssistantResponseDto result)
        {
            var (fb, fbMultiple) = await FindFreeBarberByIdentifierAsync(nearbyFreeBarbers, intent.TargetIdentifier);

            if (fb == null)
            {
                result.Response = BuildNotFoundMessage(intent.TargetIdentifier, "serbest berber", nearbyFreeBarbers.Select(f => f.FullName), fbMultiple);
                return new SuccessDataResult<AIAssistantResponseDto>(result);
            }

            // Hizmet + paket ID'lerini eşleştir
            var serviceIds = MatchServiceIds(fb.Offerings, intent.ServiceNames);
            var packageIds = MatchPackageIds(packagesByOwner, fb.FreeBarberUserId, intent.PackageNames);

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
                PackageIds = packageIds,
                Note = intent.Note,
            };

            var createRes = await _appointmentService.CreateCustomerToFreeBarberAsync(userId, req);
            result.ActionTaken = createRes.Success;
            if (createRes.Success)
            {
                result.AffectedAppointmentId = createRes.Data;
                result.Response = BuildCreateSuccessMessage(intent, fb.FullName, serviceIds.Count + packageIds.Count, date, start, end, language: intent.Response);
            }
            else
            {
                result.Response = createRes.Message ?? result.Response;
            }
            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        /// <summary>Müşteri → Dükkan randevusu oluştur.</summary>
        private async Task<IDataResult<AIAssistantResponseDto>> HandleCreateC2SAsync(
            Guid userId, IntentResponse intent, List<BarberStoreGetDto> nearbyStores,
            Dictionary<Guid, List<ServicePackageGetDto>> packagesByOwner,
            AIAssistantResponseDto result)
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
            // Paketler dükkan sahibinin (BarberStoreOwnerId) paket listesinden çözülür
            var packageIds = store.BarberStoreOwnerId.HasValue
                ? MatchPackageIds(packagesByOwner, store.BarberStoreOwnerId.Value, intent.PackageNames)
                : new List<Guid>();

            var req = new CreateAppointmentRequestDto
            {
                StoreId = storeId,
                ChairId = chairId,
                AppointmentDate = date,
                StartTime = resolvedStart,
                EndTime = resolvedEnd,
                ServiceOfferingIds = serviceIds,
                PackageIds = packageIds,
                Note = intent.Note,
            };

            var createRes = await _appointmentService.CreateCustomerToStoreControlAsync(userId, req);
            result.ActionTaken = createRes.Success;
            if (createRes.Success)
            {
                result.AffectedAppointmentId = createRes.Data;
                result.Response = BuildCreateSuccessMessage(intent, store.StoreName, serviceIds.Count + packageIds.Count, date, resolvedStart, resolvedEnd, language: intent.Response);
            }
            else
            {
                result.Response = createRes.Message ?? result.Response;
            }
            return new SuccessDataResult<AIAssistantResponseDto>(result);
        }

        /// <summary>Serbest Berber → Dükkan randevusu oluştur.</summary>
        private async Task<IDataResult<AIAssistantResponseDto>> HandleCreateFB2SAsync(
            Guid userId, IntentResponse intent, List<BarberStoreGetDto> nearbyStores,
            Dictionary<Guid, List<ServicePackageGetDto>> packagesByOwner,
            AIAssistantResponseDto result)
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
            var packageIds = store.BarberStoreOwnerId.HasValue
                ? MatchPackageIds(packagesByOwner, store.BarberStoreOwnerId.Value, intent.PackageNames)
                : new List<Guid>();

            var req = new CreateAppointmentRequestDto
            {
                StoreId = storeId,
                ChairId = chairId,
                AppointmentDate = date,
                StartTime = resolvedStart,
                EndTime = resolvedEnd,
                ServiceOfferingIds = serviceIds,
                PackageIds = packageIds,
                Note = intent.Note,
            };

            var createRes = await _appointmentService.CreateFreeBarberToStoreAsync(userId, req);
            result.ActionTaken = createRes.Success;
            if (createRes.Success)
            {
                result.AffectedAppointmentId = createRes.Data;
                result.Response = BuildCreateSuccessMessage(intent, store.StoreName, serviceIds.Count + packageIds.Count, date, resolvedStart, resolvedEnd, language: intent.Response);
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
            bool hasLocation,
            Dictionary<Guid, List<ServicePackageGetDto>> packagesByOwner)
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

                    // Paketler — varsa yazdır
                    AppendPackagesBlock(sb, packagesByOwner, fb.FreeBarberUserId);
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

                    // Dükkanın sahibinin paketleri (dükkanın servisleri ServiceOfferingDal üzerinden
                    // ayrıca çekiliyor; paketleri de owner üzerinden al).
                    if (store.BarberStoreOwnerId.HasValue)
                        AppendPackagesBlock(sb, packagesByOwner, store.BarberStoreOwnerId.Value);
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
                // Kendi mağaza paketleri — owner = userId
                AppendPackagesBlock(sb, packagesByOwner, userId, indent: "  ");
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
            sb.AppendLine("Numara/No değerini ASLA yeniden biçimlendirme: boşluk ekleme, tire kaldırma, gruplama yapma. Kartta nasıl görünüyorsa targetIdentifier'a aynen kopyala.");
            sb.AppendLine("Örn: '643357' -> '643 357' yapma. 'B-0017' -> 'B 0017' yapma.");
            sb.AppendLine("targetIdentifier: hedefin adı veya müşteri/berber numarası. Birden fazla eşleşme varsa hepsini listele (response'ta) ve unknown dön.");
            sb.AppendLine();
            sb.AppendLine("# Paket Kuralları");
            sb.AppendLine("- Her berber/dükkan kartının altında varsa 'Paketler:' satırı bulunur. Her paket için ad, fiyat ve içerdiği hizmet adları listelenir.");
            sb.AppendLine("- Kullanıcı 'VIP paketi', 'kombo paket', '1. paket' gibi ifadeler kullanırsa → packageNames dizisine paketin ADINI ekle (örn. [\"VIP Paket\"]). ID'yi yazma, sadece ad.");
            sb.AppendLine("- Paket seçildiğinde paket içindeki hizmetleri AYRICA serviceNames'e ekleme; paket zaten kendi hizmetlerini kapsar.");
            sb.AppendLine("- Paket + ek hizmet: kullanıcı 'VIP paket + sakal' gibi söylerse packageNames=[\"VIP Paket\"] + serviceNames=[\"sakal\"] olabilir.");
            sb.AppendLine("- Hedef berber/dükkanda söylenen paket yoksa intent=\"unknown\" dön ve response'ta mevcut paketleri kısaca listele.");
            if (!hasLocation)
                sb.AppendLine("NOT: Kullanıcı konumunu paylaşmadı. Randevu oluşturmak için yakın çevre listesi boş. Konum paylaşmasını iste.");
            sb.AppendLine();

            // Kesin kurallar — modelin tutarlılığını artırır
            sb.AppendLine("# Kesin Kurallar");
            sb.AppendLine("- SADECE yukarıda listelenen ID'leri (RandevuId / StoreId / FreeBarberUserId / FreeBarberEntityId) kullan; ASLA uydurma veya tahmin etme.");
            sb.AppendLine("- Hedef (berber/dükkan/randevu) yukarıdaki listelerde yoksa intent=\"unknown\" dön ve response'ta kullanıcıdan yardım iste.");
            sb.AppendLine($"- response alanı HER ZAMAN {langLabel} dilinde olmalı. Sistem mesajı Türkçe olsa bile response'u {langLabel} yaz.");
            sb.AppendLine("- Tarih/saat/hizmet belirsizse tahmin YAPMA; response'ta kullanıcıdan net bilgi iste ve intent=\"unknown\" dön.");
            sb.AppendLine("- Yanıtı yalnızca tek bir JSON nesnesi olarak ver. Markdown (```), açıklama, selamlama veya JSON dışı metin EKLEME.");
            sb.AppendLine("- Yanıt dilini kullanıcı mesajına göre DEĞİŞTİRME; her zaman ayarlanmış yanıt dilini kullan.");
            sb.AppendLine("- Para birimi formatı: '250₺' gibi Türk lirası sembolü kullan. Tarihleri konuşma diline uygun yaz (örn. '15 Nisan Pazartesi').");
            sb.AppendLine();

            // Response (konuşma) stili
            sb.AppendLine("# Yanıt (response) Stili");
            sb.AppendLine("- Uzunluk: 1-4 cümle arası. Gerekirse kısa bir madde listesi kullanabilirsin, ancak özetleyici ve doğal kal.");
            sb.AppendLine("- Ton: Sıcak, net, profesyonel; konuşma diline uygun. Emoji KULLANMA.");
            sb.AppendLine("- Başarı durumunda: ne yapıldığını (kim/ne zaman/hangi hizmet veya paket) kısaca özetle. Paket varsa paket adını ve fiyatını da belirt.");
            sb.AppendLine("- Hata/belirsizlikte: kısa bir açıklama + bir sonraki adım için somut öneri ver.");
            sb.AppendLine("- Liste isteklerinde: önce kısa özet (toplam X randevu), sonra en fazla 5 öğeyi sırala; daha fazlası varsa 've diğerleri' de.");
            sb.AppendLine("- Kullanıcıya asistan olduğunu veya teknik ayrıntıları (JSON, ID, intent gibi) AÇIKLAMA.");
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
  "packageNames": ["<paket adı>"],
  "note": "<randevu notu veya null>",
  "response": "<kullanıcıya gösterilecek açıklayıcı yanıt>"
}
""");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static bool IsCreateIntent(string? intent)
        {
            if (string.IsNullOrWhiteSpace(intent)) return false;
            var i = intent.Trim().ToLowerInvariant();
            return i is "create_c2fb" or "create_c2s" or "create_fb2s" or "create_s2fb";
        }

        private static bool ContainsPotentialCreateDetails(IntentResponse? intent)
        {
            if (intent == null) return false;
            return !string.IsNullOrWhiteSpace(intent.TargetIdentifier) ||
                   !string.IsNullOrWhiteSpace(intent.Date) ||
                   !string.IsNullOrWhiteSpace(intent.StartTime) ||
                   !string.IsNullOrWhiteSpace(intent.EndTime) ||
                   !string.IsNullOrWhiteSpace(intent.Note) ||
                   (intent.ServiceNames?.Count > 0) ||
                   (intent.PackageNames?.Count > 0);
        }

        private static List<string> GetMissingCreateFields(IntentResponse intent)
        {
            var missing = new List<string>();
            var type = intent.Intent?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(type)) return missing;

            // Dört create akışında da yanlış hedefe gitmemek için hedef zorunlu tutulur.
            if (string.IsNullOrWhiteSpace(intent.TargetIdentifier))
                missing.Add("targetIdentifier");

            // Dükkan randevularında tarih zorunlu.
            if ((type == "create_c2s" || type == "create_fb2s") && string.IsNullOrWhiteSpace(intent.Date))
                missing.Add("date");

            return missing;
        }

        private static string BuildMissingFieldPrompt(
            IntentResponse intent,
            List<string> missingFields,
            string language,
            string? fallbackResponse = null)
        {
            if (!string.IsNullOrWhiteSpace(fallbackResponse) && fallbackResponse!.Length > 8)
                return fallbackResponse;

            var isTr = string.Equals(language, "tr", StringComparison.OrdinalIgnoreCase);
            var labels = missingFields.Select(m => m switch
            {
                "targetIdentifier" => isTr ? "hedef berber/dükkan" : "target barber/store",
                "date" => isTr ? "tarih" : "date",
                _ => m
            }).ToList();

            var missingText = string.Join(", ", labels);
            if (isTr)
                return $"Devam edebilmem için şu bilgi(ler) eksik: {missingText}. Sadece bu bilgileri yazmanız yeterli, kaldığımız yerden devam ederim.";

            return $"I need the following missing info to continue: {missingText}. You can just send those details, and I will continue from where we left off.";
        }

        private static PendingCreateIntentState? GetPendingCreateIntent(Guid userId)
        {
            if (!_pendingCreateIntents.TryGetValue(userId, out var state))
                return null;

            if (DateTime.UtcNow - state.UpdatedAtUtc > PendingIntentTtl)
            {
                _pendingCreateIntents.TryRemove(userId, out _);
                return null;
            }
            return state;
        }

        private static void SavePendingCreateIntent(Guid userId, IntentResponse intent)
        {
            if (!IsCreateIntent(intent.Intent))
                return;

            _pendingCreateIntents[userId] = new PendingCreateIntentState
            {
                Intent = CloneIntent(intent),
                UpdatedAtUtc = DateTime.UtcNow
            };
        }

        private static void ClearPendingCreateIntent(Guid userId)
        {
            _pendingCreateIntents.TryRemove(userId, out _);
        }

        private static void HandlePendingStateAfterCreate(Guid userId, AIAssistantResponseDto? data)
        {
            if (data?.ActionTaken == true)
                ClearPendingCreateIntent(userId);
        }

        private static string BuildUserMessageWithPendingContext(string userMessage, PendingCreateIntentState? state)
        {
            if (state?.Intent == null)
                return userMessage;

            var pending = state.Intent;
            var sb = new StringBuilder();
            sb.AppendLine("Devam eden randevu taslağı:");
            sb.AppendLine($"intent={pending.Intent ?? "unknown"}");
            sb.AppendLine($"targetIdentifier={pending.TargetIdentifier ?? "null"}");
            sb.AppendLine($"date={pending.Date ?? "null"}");
            sb.AppendLine($"startTime={pending.StartTime ?? "null"}");
            sb.AppendLine($"endTime={pending.EndTime ?? "null"}");
            if (pending.ServiceNames?.Count > 0) sb.AppendLine($"serviceNames={string.Join(", ", pending.ServiceNames)}");
            if (pending.PackageNames?.Count > 0) sb.AppendLine($"packageNames={string.Join(", ", pending.PackageNames)}");
            if (!string.IsNullOrWhiteSpace(pending.Note)) sb.AppendLine($"note={pending.Note}");
            sb.AppendLine();
            sb.AppendLine("Kullanıcının yeni mesajı (eksik bilgileri tamamlıyor olabilir):");
            sb.AppendLine(userMessage);
            return sb.ToString();
        }

        private static IntentResponse MergeWithPendingCreateIntent(IntentResponse current, PendingCreateIntentState? pendingState)
        {
            if (pendingState?.Intent == null)
                return current;

            // Yeni mesaj doğrudan create intent ise yine de alanları pending ile tamamla.
            if (IsCreateIntent(current.Intent))
                return MergeIntentFields(pendingState.Intent, current);

            // unknown ama create detayları içeriyorsa pending intent tipini koru ve alanları birleştir.
            if ((current.Intent ?? "").Equals("unknown", StringComparison.OrdinalIgnoreCase) &&
                ContainsPotentialCreateDetails(current))
            {
                return MergeIntentFields(pendingState.Intent, current);
            }

            // Kullanıcı apayrı bir işlem yaptıysa pending state'i bozmadan current'ı dön.
            return current;
        }

        private static IntentResponse MergeIntentFields(IntentResponse baseIntent, IntentResponse patch)
        {
            var merged = CloneIntent(baseIntent);
            merged.Intent = FirstNonEmpty(patch.Intent, merged.Intent);
            merged.TargetIdentifier = FirstNonEmpty(patch.TargetIdentifier, merged.TargetIdentifier);
            merged.MyStoreName = FirstNonEmpty(patch.MyStoreName, merged.MyStoreName);
            merged.Date = FirstNonEmpty(patch.Date, merged.Date);
            merged.StartTime = FirstNonEmpty(patch.StartTime, merged.StartTime);
            merged.EndTime = FirstNonEmpty(patch.EndTime, merged.EndTime);
            merged.Note = FirstNonEmpty(patch.Note, merged.Note);
            merged.AppointmentId = FirstNonEmpty(patch.AppointmentId, merged.AppointmentId);
            merged.Response = FirstNonEmpty(patch.Response, merged.Response);
            merged.ServiceNames = MergeStringLists(merged.ServiceNames, patch.ServiceNames);
            merged.PackageNames = MergeStringLists(merged.PackageNames, patch.PackageNames);
            merged.Decisions = (patch.Decisions != null && patch.Decisions.Count > 0) ? patch.Decisions : merged.Decisions;
            return merged;
        }

        private static IntentResponse CloneIntent(IntentResponse source)
        {
            return new IntentResponse
            {
                Intent = source.Intent,
                AppointmentId = source.AppointmentId,
                Decisions = source.Decisions != null ? new List<BulkDecisionItem>(source.Decisions) : null,
                TargetIdentifier = source.TargetIdentifier,
                MyStoreName = source.MyStoreName,
                Date = source.Date,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
                ServiceNames = source.ServiceNames != null ? new List<string>(source.ServiceNames) : null,
                PackageNames = source.PackageNames != null ? new List<string>(source.PackageNames) : null,
                Note = source.Note,
                Response = source.Response
            };
        }

        private static string? FirstNonEmpty(string? preferred, string? fallback)
            => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

        private static List<string>? MergeStringLists(List<string>? a, List<string>? b)
        {
            var source = new List<string>();
            if (a != null) source.AddRange(a.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (b != null) source.AddRange(b.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (source.Count == 0) return null;
            return source
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Prompt'a bir berber/dükkan kartı altında "Paketler:" satırı ekler.
        /// Paketi olmayan ownerlar için hiçbir şey yazmaz (gürültü olmaması için).
        /// Format: "  Paketler: VIP Paket (350₺) [PackageId:GUID] { saç, sakal }, Combo (200₺) [PackageId:GUID] { ... }"
        /// </summary>
        private static void AppendPackagesBlock(
            StringBuilder sb,
            Dictionary<Guid, List<ServicePackageGetDto>> packagesByOwner,
            Guid ownerId,
            string indent = "  ")
        {
            if (!packagesByOwner.TryGetValue(ownerId, out var list) || list is null || list.Count == 0)
                return;

            var parts = list.Select(p =>
            {
                var items = p.Items?.Count > 0
                    ? $" {{ {string.Join(", ", p.Items.Select(i => i.ServiceName))} }}"
                    : "";
                return $"{p.PackageName} ({p.TotalPrice:0.##}₺) [PackageId:{p.Id}]{items}";
            });

            sb.AppendLine($"{indent}Paketler: {string.Join(", ", parts)}");
        }

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

            // 3.1) Normalize numara eşleşmesi (örn. "643 357" == "643357", "B 0017" == "B-0017")
            var normalizedInput = NormalizeIdentifierForMatch(identifier);
            if (!string.IsNullOrEmpty(normalizedInput))
            {
                var normalizedMatches = list
                    .Where(f => NormalizeIdentifierForMatch(f.CustomerNumber) == normalizedInput)
                    .ToList();
                if (normalizedMatches.Count == 1) return (normalizedMatches[0], false);
                if (normalizedMatches.Count > 1) return (null, true);
            }

            // 4) IUserService ile müşteri numarasından userId çöz
            var customerNumRes = await _userService.GetByCustomerNumber(identifier);
            if (customerNumRes.Success && customerNumRes.Data != null)
            {
                var resolvedUserId = customerNumRes.Data.Id;
                return (list.FirstOrDefault(f => f.FreeBarberUserId == resolvedUserId), false);
            }

            // 4.1) Normalize edilmiş hali ile tekrar dene
            var normalizedIdentifier = NormalizeIdentifierForMatch(identifier);
            if (!string.IsNullOrEmpty(normalizedIdentifier) &&
                !string.Equals(normalizedIdentifier, identifier, StringComparison.OrdinalIgnoreCase))
            {
                var customerNumNormalizedRes = await _userService.GetByCustomerNumber(normalizedIdentifier);
                if (customerNumNormalizedRes.Success && customerNumNormalizedRes.Data != null)
                {
                    var resolvedUserId = customerNumNormalizedRes.Data.Id;
                    return (list.FirstOrDefault(f => f.FreeBarberUserId == resolvedUserId), false);
                }
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

            // 3.1) Normalize no eşleşmesi (örn. "643 357" == "643357")
            var normalizedInput = NormalizeIdentifierForMatch(identifier);
            if (!string.IsNullOrEmpty(normalizedInput))
            {
                var normalizedMatches = list
                    .Where(s => NormalizeIdentifierForMatch(s.StoreNo) == normalizedInput)
                    .ToList();
                if (normalizedMatches.Count == 1) return (normalizedMatches[0], false);
                if (normalizedMatches.Count > 1) return (null, true);
            }

            // 4) IUserService ile owner userId çöz (mağaza sahibinin müşteri no ile arama)
            var customerNumRes = await _userService.GetByCustomerNumber(identifier);
            if (customerNumRes.Success && customerNumRes.Data != null)
            {
                var resolvedUserId = customerNumRes.Data.Id;
                var byOwner = list.FirstOrDefault(s => s.BarberStoreOwnerId == resolvedUserId);
                return (byOwner, false);
            }

            // 4.1) Normalize edilmiş müşteri numarası ile tekrar dene
            var normalizedIdentifier = NormalizeIdentifierForMatch(identifier);
            if (!string.IsNullOrEmpty(normalizedIdentifier) &&
                !string.Equals(normalizedIdentifier, identifier, StringComparison.OrdinalIgnoreCase))
            {
                var customerNumNormalizedRes = await _userService.GetByCustomerNumber(normalizedIdentifier);
                if (customerNumNormalizedRes.Success && customerNumNormalizedRes.Data != null)
                {
                    var resolvedUserId = customerNumNormalizedRes.Data.Id;
                    var byOwner = list.FirstOrDefault(s => s.BarberStoreOwnerId == resolvedUserId);
                    return (byOwner, false);
                }
            }

            return (null, false);
        }

        /// <summary>
        /// Numara/no eşleşmesini model kaynaklı format farklarına dayanıklı hale getirir.
        /// Örn: "643 357", "643-357" ve "643357" aynı kabul edilir.
        /// </summary>
        private static string NormalizeIdentifierForMatch(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var chars = input
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray();
            return new string(chars);
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

        /// <summary>
        /// Kullanıcının istediği paket adlarını, ownerId'ye ait mevcut paketlerden seçer.
        /// "1. paket", "vip paketi" gibi ifadeleri, tam/kısmi ad eşleşmesi ile çözer.
        /// </summary>
        private static List<Guid> MatchPackageIds(
            Dictionary<Guid, List<ServicePackageGetDto>> packagesByOwner,
            Guid ownerId,
            List<string>? requestedNames)
        {
            if (requestedNames == null || requestedNames.Count == 0) return new();
            if (!packagesByOwner.TryGetValue(ownerId, out var packages) || packages == null || packages.Count == 0) return new();

            var result = new List<Guid>();
            foreach (var rawName in requestedNames)
            {
                var name = (rawName ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name)) continue;

                // "N. paket" / "N.paket" formatı → sıra numarası olarak çöz
                var ordinal = TryParseOrdinalPackageIndex(name);
                if (ordinal.HasValue && ordinal.Value >= 1 && ordinal.Value <= packages.Count)
                {
                    var byIndex = packages[ordinal.Value - 1];
                    if (!result.Contains(byIndex.Id)) result.Add(byIndex.Id);
                    continue;
                }

                // Tam eşleşme
                var exact = packages.FirstOrDefault(p =>
                    p.PackageName.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    if (!result.Contains(exact.Id)) result.Add(exact.Id);
                    continue;
                }

                // Kısmi eşleşme (içerir / içerilir)
                var partial = packages.FirstOrDefault(p =>
                    p.PackageName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(p.PackageName, StringComparison.OrdinalIgnoreCase));
                if (partial != null && !result.Contains(partial.Id))
                    result.Add(partial.Id);
            }
            return result;
        }

        /// <summary>"1. paket", "2 paket", "3.paket" gibi ifadelerden sıra numarasını çıkarır.</summary>
        private static int? TryParseOrdinalPackageIndex(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var normalized = text.Trim().ToLowerInvariant();
            if (!normalized.Contains("paket")) return null;
            var digits = new string(normalized.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digits)) return null;
            return int.TryParse(digits, out var n) ? n : null;
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

        /// <summary>
        /// Gemini API kota/rate-limit hatalarını (429) diğer ağ hatalarından ayırt etmek için.
        /// Üst katman bu durumda kullanıcıya "servis yoğun" mesajı dönebilir, loglar spam'lenmez.
        /// </summary>
        private sealed class GeminiRateLimitException : Exception
        {
            public GeminiRateLimitException(string message) : base(message) { }
        }

        /// <summary>
        /// Aktif Gemini modeli. `Gemini:Model` config ile override edilebilir.
        /// Varsayılan: gemini-2.5-flash — ücretsiz tier'da 10 RPM / 250 RPD kotası vardır.
        /// Eski gemini-2.0-flash modelinin ücretsiz kotası yeni projelerde kapatıldığı için
        /// bu default'u tercih ediyoruz. Alternatifler: gemini-2.5-flash-lite, gemini-1.5-flash.
        /// </summary>
        private string ResolveGeminiModel()
        {
            var configured = _configuration["Gemini:Model"];
            return string.IsNullOrWhiteSpace(configured) ? "gemini-2.5-flash" : configured;
        }

        private async Task<IntentResponse?> CallGpt4oAsync(string apiKey, string systemPrompt, string userMessage)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, CHAT_COMPLETIONS_ENDPOINT);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var model = ResolveGeminiModel();
            var payload = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                response_format = new { type = "json_object" },
                max_tokens = 4000,
                temperature = 0.15
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.SendAsync(request);
            var raw = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                // 429 = kota doldu / rate limit. Bu özel durumda üst katmanı
                // bilgilendirecek özel exception fırlat.
                // Ayrıca body'de "RESOURCE_EXHAUSTED" / "quota" geçiyorsa da aynı muamele.
                var is429 = httpResponse.StatusCode == HttpStatusCode.TooManyRequests;
                var isQuota = raw.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                              raw.Contains("quota", StringComparison.OrdinalIgnoreCase);

                if (is429 || isQuota)
                {
                    _logger.LogWarning("Gemini free-tier quota/rate-limit: Model={Model}, Status={Status}", model, httpResponse.StatusCode);
                    throw new GeminiRateLimitException($"Gemini rate limit ({httpResponse.StatusCode}) model={model}");
                }

                _logger.LogError("Gemini error: Model={Model}, Status={Status}, Body={Body}", model, httpResponse.StatusCode, raw);
                return null;
            }

            var gpt = JsonSerializer.Deserialize<GptChatResponse>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var content = gpt?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content)) return null;

            // Model bazen JSON'ı markdown code block içinde döner (```json ... ```).
            // Ayrıca max_tokens truncation veya literal newline içeren string değerleri
            // JsonSerializer'ı patlatabilir. Temizleyip tekrar dene.
            var json = ExtractJsonFromContent(content);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("[AI] IntentResponse: JSON çıkarılamadı. Content={Content}", content);
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<IntentResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("[AI] IntentResponse deserialize başarısız. Message={Message} JSON={Json}", ex.Message, json);
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Whisper transcription
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IDataResult<string>> TranscribeAudioAsync(Stream audioStream, string fileName, string? contentType = null, string? language = null)
        {
            var userIdLog = GetCurrentUserIdForLog();

            var apiKey = _configuration["Groq:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("[Transcribe] UserId={UserId} - Groq API key missing in configuration.", userIdLog);
                return new ErrorDataResult<string>("whisper_unavailable");
            }

            var groqMime = ResolveGroqAudioContentType(fileName, contentType);

            // Dil kodu normalleştir: "tr-TR" → "tr", desteklenmeyenler → null (Whisper otomatik algılar)
            var groqLang = NormalizeWhisperLanguage(language);

            try
            {
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(audioStream);
                streamContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(groqMime);
                content.Add(streamContent, "file", fileName);
                content.Add(new StringContent("whisper-large-v3-turbo"), "model");
                if (!string.IsNullOrEmpty(groqLang))
                    content.Add(new StringContent(groqLang), "language");

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[Transcribe] UserId={UserId} Whisper error: {Status} - {Body}", userIdLog, response.StatusCode, body);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        body.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("quota", StringComparison.OrdinalIgnoreCase))
                        return new ErrorDataResult<string>("whisper_rate_limit");
                    return new ErrorDataResult<string>("whisper_failed");
                }

                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var hasTextProp = doc.RootElement.TryGetProperty("text", out var textEl);
                var rawText = hasTextProp ? (textEl.GetString() ?? "") : "";
                // Groq bazen yalnızca boşluk / görünmez Unicode döndürebilir; Trim sonrası anlamlı metin kalmalı.
                var text = rawText.Trim();

                _logger.LogInformation(
                    "[Transcribe] UserId={UserId} Groq OK: FileName={FileName}, GroqMime={GroqMime}, ResponseBodyLength={BodyLen}, RawLength={RawLen}, TrimmedLength={TrimLen}, HasTextJsonProperty={HasTextProp}",
                    userIdLog,
                    fileName,
                    groqMime,
                    body.Length,
                    rawText.Length,
                    text.Length,
                    hasTextProp);

                if (string.IsNullOrWhiteSpace(text))
                {
                    const int maxLen = 512;
                    var truncated = body.Length <= maxLen ? body : body[..maxLen] + "…";
                    _logger.LogWarning(
                        "[Transcribe] UserId={UserId} Groq empty text. FileName={FileName}, GroqMime={GroqMime}, Lang={Lang}, ResponseBodyTruncated={Body}",
                        userIdLog,
                        fileName,
                        groqMime,
                        groqLang ?? "auto",
                        truncated);
                    return new ErrorDataResult<string>("transcription_empty");
                }

                // DİKKAT: SuccessDataResult<T>'de T=string olduğunda
                // `SuccessDataResult(T data)` ve `SuccessDataResult(string message)`
                // overload'ları çakışır; C# overload resolution non-generic
                // `string message` versiyonunu seçer ve Data=null, Message=text olur.
                // Bu yüzden iki parametreli `(T data, string message)` overload'ını
                // açıkça çağırıp Data = text olmasını garantiliyoruz.
                return new SuccessDataResult<string>(text, string.Empty);
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("[Transcribe] UserId={UserId} Whisper timeout for file {FileName}", userIdLog, fileName);
                return new ErrorDataResult<string>("whisper_timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transcribe] UserId={UserId} Whisper unexpected error for file {FileName}", userIdLog, fileName);
                return new ErrorDataResult<string>("whisper_unavailable");
            }
        }

        /// <summary>
        /// "tr-TR" → "tr", "en-US" → "en" gibi kısalt; Whisper desteklemediği kodları null bırak.
        /// Groq Whisper desteklenen diller: https://console.groq.com/docs/speech-text
        /// </summary>
        private static string? NormalizeWhisperLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language)) return null;
            // "tr-TR" → "tr"
            var code = language.Split('-')[0].ToLowerInvariant().Trim();
            // Whisper'ın desteklediği yaygın kodlar; bilinmeyenleri geçme
            HashSet<string> supported = ["af", "ar", "hy", "az", "be", "bs", "bg", "ca", "zh", "hr",
                "cs", "da", "nl", "en", "et", "fi", "fr", "gl", "de", "el", "he", "hi", "hu", "is",
                "id", "it", "ja", "kn", "kk", "ko", "lv", "lt", "mk", "ms", "mr", "mi", "ne", "no",
                "fa", "pl", "pt", "ro", "ru", "sr", "sk", "sl", "es", "sw", "sv", "tl", "ta", "th",
                "tr", "uk", "ur", "vi", "cy"];
            return supported.Contains(code) ? code : null;
        }

        /// <summary>
        /// Groq Whisper multipart dosya parçası için MIME. Expo/ iOS kayıtları çoğunlukla .m4a (audio/mp4);
        /// önceki sabit audio/mpeg Groq tarafında hatalı işlemeye yol açabiliyordu.
        /// </summary>
        private static string ResolveGroqAudioContentType(string fileName, string? uploadContentType)
        {
            if (!string.IsNullOrWhiteSpace(uploadContentType) &&
                uploadContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) &&
                uploadContentType.IndexOf("octet-stream", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Mobile istemciler bazı cihazlarda m4a için audio/x-m4a gönderir.
                // Groq tarafında en stabil karşılığı audio/mp4.
                if (string.Equals(uploadContentType, "audio/m4a", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(uploadContentType, "audio/x-m4a", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(uploadContentType, "audio/mp4a-latm", StringComparison.OrdinalIgnoreCase))
                    return "audio/mp4";
                return uploadContentType;
            }

            var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            return ext switch
            {
                ".m4a" => "audio/mp4",
                ".mp4" => "audio/mp4",
                ".mp3" => "audio/mpeg",
                ".mpeg" => "audio/mpeg",
                ".mpga" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".webm" => "audio/webm",
                ".ogg" => "audio/ogg",
                ".flac" => "audio/flac",
                _ => "audio/mp4"
            };
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

        /// <summary>
        /// Model yanıtından JSON nesnesini çıkarır.
        /// Markdown code block (```json...```) varsa içini alır.
        /// İlk '{' ile son '}' arasını kesip literal newline'ları temizler.
        /// </summary>
        private static string? ExtractJsonFromContent(string content)
        {
            var trimmed = content.Trim();

            // Markdown code block: ```json\n{...}\n``` veya ```\n{...}\n```
            var codeBlockMatch = System.Text.RegularExpressions.Regex.Match(
                trimmed, @"```(?:json)?\s*([\s\S]*?)\s*```");
            if (codeBlockMatch.Success)
                trimmed = codeBlockMatch.Groups[1].Value.Trim();

            // JSON nesnesini { ... } arasından kes
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start < 0 || end <= start) return null;

            return trimmed.Substring(start, end - start + 1);
        }

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

            [JsonPropertyName("packageNames")]
            public List<string>? PackageNames { get; set; }

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

        private sealed class PendingCreateIntentState
        {
            public IntentResponse Intent { get; set; } = new();
            public DateTime UpdatedAtUtc { get; set; }
        }
    }
}
