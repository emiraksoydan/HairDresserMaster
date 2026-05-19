using Business.Abstract;
using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Linq;

namespace Business.Concrete
{
    public class AppointmentNotifyManager(
        IAppointmentDal appointmentDal,
        IBarberStoreDal barberStoreDal,
        IBarberStoreChairDal chairDal,
        IManuelBarberDal manuelBarberDal,
        IImageDal imageDal,
        IUserSummaryService userSummarySvc,
        INotificationService notificationSvc,
        IAppointmentServiceOffering appointmentServiceOfferingDal,
        IServicePackageDal servicePackageDal,
        IFavoriteService favoriteService,
        IFreeBarberDal freeBarberDal,
        Business.Helpers.BlockedHelper blockedHelper
    ) : IAppointmentNotifyService
    {
        // Overload 1: AppointmentId ile (mevcut randevular için - transaction dışında)
        public async Task<IResult> NotifyAsync(
            Guid appointmentId,
            NotificationType type,
            Guid? actorUserId = null,
            object? extra = null)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null)
            {
                return new ErrorResult(Messages.AppointmentNotFound);
            }
            
            return await NotifyAsyncInternal(appt, type, actorUserId, extra, recipientUserIds: null);
        }

        // Appointment entity ile (yeni oluşturulan randevular için - transaction içinde)
        public async Task<IResult> NotifyWithAppointmentAsync(
            Entities.Concrete.Entities.Appointment appointment,
            NotificationType type,
            Guid? actorUserId = null,
            object? extra = null)
        {
            if (appointment is null)
            {
                return new ErrorResult(Messages.AppointmentNotFound);
            }
            
            return await NotifyAsyncInternal(appointment, type, actorUserId, extra, recipientUserIds: null);
        }

        public async Task<IResult> NotifyToRecipientsAsync(
            Guid appointmentId,
            NotificationType type,
            IReadOnlyCollection<Guid> recipientUserIds,
            Guid? actorUserId = null,
            object? extra = null)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null)
            {
                return new ErrorResult(Messages.AppointmentNotFound);
            }

            return await NotifyAsyncInternal(appt, type, actorUserId, extra, recipientUserIds);
        }

        public async Task<IResult> NotifyWithAppointmentToRecipientsAsync(
            Entities.Concrete.Entities.Appointment appointment,
            NotificationType type,
            IReadOnlyCollection<Guid> recipientUserIds,
            Guid? actorUserId = null,
            object? extra = null)
        {
            if (appointment is null)
            {
                return new ErrorResult(Messages.AppointmentNotFound);
            }

            return await NotifyAsyncInternal(appointment, type, actorUserId, extra, recipientUserIds);
        }

        // Internal method: Ortak bildirim gönderme mantığı
        private async Task<IResult> NotifyAsyncInternal(
            Entities.Concrete.Entities.Appointment appt,
            NotificationType type,
            Guid? actorUserId = null,
            object? extra = null,
            IReadOnlyCollection<Guid>? recipientUserIds = null)
        {
            // ÖNEMLİ: Randevu oluşturulduğunda status Pending olmalı, Unanswered olmamalı
            // Eğer AppointmentCreated notification'ı gönderiliyorsa ve status Unanswered ise,
            // bu bir hata - status Pending olmalı
            if (type == NotificationType.AppointmentCreated && appt.Status == AppointmentStatus.Unanswered)
            {
                // Status'u Pending'e çevir (eğer yanlışlıkla Unanswered ise)
                appt.Status = AppointmentStatus.Pending;
                await appointmentDal.Update(appt);
            }

            // BİLDİRİM AKIŞI MANTIĞI:
            // 1. Randevu oluşturulduğunda: Randevu alan kişi (actor) → Karşı tarafa bildirim gönderir
            //    Örnek: Customer randevu oluşturdu → Store/FreeBarber'a bildirim gider (Customer'a gitmez)
            // 2. Cevap verildiğinde: Cevap veren kişi (actor) → Randevu alan kişiye geri bildirim gönderir
            //    Örnek: Store onayladı → Customer'a bildirim gider (Store'a gitmez)
            //
            // actorUserId: İşlemi yapan kişi (randevu oluşturan veya cevap veren)
            // Bu kişi recipient listesinden çıkarılır çünkü kendi yaptığı işlem için bildirim almamalı
            // ÖNEMLİ: AppointmentUnanswered durumunda TÜM ilgili kişilere bildirim gitmeli (actor dahil)
            // ÖNEMLİ: actorUserId null olabilir (örn: background service'ten gelen AppointmentUnanswered)
            var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var recipients = (recipientUserIds ?? participantUserIds)
                .Where(participantUserIds.Contains)
                .Distinct()
                .ToList();

            // Actor'ı recipient listesinden çıkar (kendi yaptığı işlem için bildirim almamalı)
            // AppointmentUnanswered hariç - bu durumda herkese gitmeli
            if (type != NotificationType.AppointmentUnanswered && actorUserId.HasValue)
            {
                recipients = recipients
                    .Where(x => x != actorUserId.Value)
                    .ToList();
            }

            // Engelleme filtresi: alıcı ile diğer katılımcılar arasında çift yönlü block varsa
            // push gönderme. Timeout/worker akışlarında actor null olsa da uygulanır.
            var filteredRecipients = new List<Guid>(recipients.Count);
            foreach (var recipientId in recipients)
            {
                var hasAnyBlock = false;
                foreach (var otherParticipantId in participantUserIds)
                {
                    if (otherParticipantId == recipientId) continue;
                    if (await blockedHelper.HasBlockBetweenAsync(recipientId, otherParticipantId))
                    {
                        hasAnyBlock = true;
                        break;
                    }
                }
                if (!hasAnyBlock)
                    filteredRecipients.Add(recipientId);
            }
            recipients = filteredRecipients;

            // Eğer hiç recipient yoksa hata döndür
            if (recipients.Count == 0)
            {
                return new ErrorResult(Messages.AppointmentRecipientNotFound);
            }

            // ÖNEMLİ: Payload için TÜM ilgili kullanıcıların bilgilerini çek (recipients değil)
            // Çünkü payload'da customer, store owner, free barber bilgileri olmalı
            // Örneğin customer appointment oluşturduğunda, store owner customer bilgisini görmeli
            var allRelevantUserIds = participantUserIds;

            // tek seferde summary çek - TÜM ilgili kullanıcılar için
            var userMapRes = await userSummarySvc.GetManyAsync(allRelevantUserIds);
            var userMap = (userMapRes.Success && userMapRes.Data is not null)
                ? userMapRes.Data
                : new Dictionary<Guid, UserNotifyDto>();

            UserNotifyDto? GetUser(Guid? id)
                => id.HasValue && userMap.TryGetValue(id.Value, out var u) ? u : null;

            var customerInfo = GetUser(appt.CustomerUserId);
            var freeBarberInfo = GetUser(appt.FreeBarberUserId);

            // Customer için snapshot konum: Appointment.RequestLatitude/Longitude
            // (Müşteri randevuyu açarken set edilir; sonra değişmez. "Haritada Göster" için.)
            if (customerInfo != null && appt.RequestLatitude.HasValue && appt.RequestLongitude.HasValue)
            {
                customerInfo.Latitude = appt.RequestLatitude;
                customerInfo.Longitude = appt.RequestLongitude;
            }

            // --- Entity'leri çek (sıralı, koşullu) ---
            FreeBarber? freeBarberEntity = null;
            if (appt.FreeBarberUserId.HasValue)
                freeBarberEntity = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);

            BarberStore? store = null;
            if (appt.BarberStoreUserId.HasValue)
                store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);

            var chair = appt.ChairId.HasValue
                ? await chairDal.Get(c => c.Id == appt.ChairId.Value)
                : null;

            var manuelBarberFromChair = (chair?.ManuelBarberId.HasValue == true)
                ? await manuelBarberDal.Get(x => x.Id == chair.ManuelBarberId!.Value)
                : null;

            // Snapshot durumu: slot serbest bırakıldıktan sonra ChairId null olabilir,
            // appointment satırındaki ChairName/ManuelBarberId snapshot'tan devam eder.
            bool useSnapshot = chair is null && (!string.IsNullOrWhiteSpace(appt.ChairName) || appt.ManuelBarberId.HasValue);
            var manuelBarberFromSnapshot = (useSnapshot && appt.ManuelBarberId.HasValue)
                ? await manuelBarberDal.Get(x => x.Id == appt.ManuelBarberId!.Value)
                : null;

            // --- TEK BATCH IMAGE SORGUSU (3 ayrı sorgu → 1 sorgu) ---
            var imageRequests = new List<(Guid OwnerId, ImageOwnerType OwnerType)>(3);
            if (freeBarberEntity != null)
                imageRequests.Add((freeBarberEntity.Id, ImageOwnerType.FreeBarber));
            if (store != null)
                imageRequests.Add((store.Id, ImageOwnerType.Store));
            var mbForImage = manuelBarberFromChair ?? manuelBarberFromSnapshot;
            if (mbForImage != null)
                imageRequests.Add((mbForImage.Id, ImageOwnerType.ManuelBarber));

            var imageMap = await imageDal.GetLatestImagesAsync(imageRequests);
            string? GetImageUrl(Guid ownerId, ImageOwnerType ownerType)
                => imageMap.TryGetValue((ownerId, ownerType), out var url) ? url : null;

            // --- DTO'ları image map'ten doldur ---
            if (freeBarberEntity != null && freeBarberInfo != null)
            {
                freeBarberInfo.AvatarUrl = GetImageUrl(freeBarberEntity.Id, ImageOwnerType.FreeBarber);
                freeBarberInfo.BarberType = freeBarberEntity.Type;
                // FreeBarber canlı konumu — "Haritada Göster" ekranı 10sn'de bir live polling yapar.
                freeBarberInfo.Latitude = freeBarberEntity.Latitude;
                freeBarberInfo.Longitude = freeBarberEntity.Longitude;
            }

            StoreNotifyDto? storeInfo = null;
            if (store is not null)
            {
                string? storeOwnerNumber = null;
                if (userMap.TryGetValue(store.BarberStoreOwnerId, out var storeOwner))
                    storeOwnerNumber = storeOwner.CustomerNumber;

                storeInfo = new StoreNotifyDto
                {
                    StoreId = store.Id,
                    StoreOwnerUserId = store.BarberStoreOwnerId,
                    StoreName = store.StoreName,
                    ImageUrl = GetImageUrl(store.Id, ImageOwnerType.Store),
                    Type = store.Type,
                    AddressDescription = store.AddressDescription,
                    StoreOwnerNumber = storeOwnerNumber,
                    StoreNo = store.StoreNo,
                    Latitude = store.Latitude,
                    Longitude = store.Longitude
                };
            }

            ChairNotifyDto? chairInfo = null;
            if (chair is not null)
            {
                chairInfo = new ChairNotifyDto
                {
                    ChairId = chair.Id,
                    ChairName = chair.Name,
                    ManuelBarberId = chair.ManuelBarberId
                };
                if (manuelBarberFromChair is not null)
                {
                    chairInfo.ManuelBarberName = manuelBarberFromChair.FullName;
                    chairInfo.ManuelBarberImageUrl = GetImageUrl(manuelBarberFromChair.Id, ImageOwnerType.ManuelBarber);
                    chairInfo.ManuelBarberType = store?.Type;
                }
            }
            else if (useSnapshot)
            {
                chairInfo = new ChairNotifyDto
                {
                    ChairId = appt.ChairId ?? Guid.Empty,
                    ChairName = appt.ChairName,
                    ManuelBarberId = appt.ManuelBarberId
                };
                if (manuelBarberFromSnapshot is not null)
                {
                    chairInfo.ManuelBarberName = manuelBarberFromSnapshot.FullName;
                    chairInfo.ManuelBarberType = store?.Type;
                    chairInfo.ManuelBarberImageUrl = GetImageUrl(manuelBarberFromSnapshot.Id, ImageOwnerType.ManuelBarber);
                }
            }

            // FreeBarber varsa manuel barber alanlarını temizle (defensive)
            if (appt.FreeBarberUserId.HasValue && chairInfo is not null)
            {
                chairInfo.ManuelBarberId = null;
                chairInfo.ManuelBarberName = null;
                chairInfo.ManuelBarberImageUrl = null;
            }

            // Service Offerings - Appointment'a ait hizmetleri al
            // ÖNEMLİ: Transaction içinde çağrılıyorsa (NotifyWithAppointmentAsync), 
            // AppointmentServiceOffering kayıtları henüz commit edilmemiş olabilir.
            // Bu durumda appointmentServiceOfferingDal.GetAll boş dönebilir.
            // Çözüm: Veritabanından al, eğer boşsa transaction commit edilene kadar birkaç kez dene
            var serviceOfferings = new List<ServiceOfferingGetDto>();
            var appointmentServiceOfferings = await appointmentServiceOfferingDal.GetAll(x => x.AppointmentId == appt.Id);
            
            
            // Service offerings'i her zaman payload'a ekle (boş olsa bile null olarak)
            if (appointmentServiceOfferings != null && appointmentServiceOfferings.Any())
            {
                serviceOfferings = appointmentServiceOfferings
                    .Select(aso => new ServiceOfferingGetDto
                    {
                        Id = aso.ServiceOfferingId,
                        ServiceName = aso.ServiceName,
                        Price = aso.Price
                    })
                    .ToList();
            }

            var appointmentPackages = await servicePackageDal.GetPackagesByAppointmentIdAsync(appt.Id);

            foreach (var userId in recipients)
            {
                var role =
                    appt.CustomerUserId == userId ? "customer" :
                    appt.BarberStoreUserId == userId ? "store" :
                    appt.FreeBarberUserId == userId ? "freebarber" : "other";

                var title = EnhanceAppointmentPushTitle(
                    BuildTitle(type, role, appt.Status, userId, appt),
                    role,
                    customerInfo,
                    storeInfo,
                    freeBarberInfo);

                // PAYLOAD OPTİMİZASYONU: Gereksiz bilgileri çıkar
                // 1. AppointmentCreated durumunda: İsteği gönderen kişi kendi bilgisini alıcıya göndermesin
                // 2. Geri dönüşlerde ve cevapsız durumlarda: Alıcı kendi bilgisini görmesin
                
                UserNotifyDto? payloadCustomer = customerInfo;
                StoreNotifyDto? payloadStore = storeInfo;
                UserNotifyDto? payloadFreeBarber = freeBarberInfo;

                // AppointmentCreated durumunda: İsteği gönderen kişi kendi bilgisini alıcıya göndermesin
                if (type == NotificationType.AppointmentCreated)
                {
                    // Müşteri dükkana göndermişse → dükkana giden bildirimde dükkan bilgisi olmasın
                    if (appt.RequestedBy == AppointmentRequester.Customer && role == "store")
                    {
                        payloadStore = null;
                    }
                    // Dükkan serbest berbere göndermişse → serbest berbere giden bildirimde serbest berber bilgisi olmasın
                    else if (appt.RequestedBy == AppointmentRequester.Store && role == "freebarber")
                    {
                        payloadFreeBarber = null;
                    }
                    // Serbest berber dükkana göndermişse → dükkana giden bildirimde dükkan bilgisi olmasın
                    else if (appt.RequestedBy == AppointmentRequester.FreeBarber && role == "store")
                    {
                        payloadStore = null;
                    }
                }
                // Geri dönüşlerde (Approved, Rejected, Cancelled, Completed) veya cevapsız durumlarda (Unanswered):
                // Alıcı kendi bilgisini görmesin
                else if (type == NotificationType.AppointmentApproved ||
                         type == NotificationType.AppointmentRejected ||
                         type == NotificationType.AppointmentCancelled ||
                         type == NotificationType.AppointmentCompleted ||
                         type == NotificationType.AppointmentUnanswered)
                {
                    // Customer'a gidiyorsa → customer bilgisi göndermeye gerek yok
                    if (role == "customer")
                    {
                        payloadCustomer = null;
                    }
                    // Store'a gidiyorsa → store bilgisi göndermeye gerek yok
                    else if (role == "store")
                    {
                        payloadStore = null;
                    }
                    // FreeBarber'a gidiyorsa → freebarber bilgisi göndermeye gerek yok
                    else if (role == "freebarber")
                    {
                        payloadFreeBarber = null;
                    }
                }

                // FAVORİ KONTROLLERİ: Her recipient için favori durumunu kontrol et
                bool? isCustomerFavorite = null;
                bool? isStoreFavorite = null;
                bool? isFreeBarberFavorite = null;

                string? note = appt.Note;
                if (role == "store" && appt.StoreSelectionType == StoreSelectionType.StoreSelection)
                {
                    note = null;
                }

                // Customer favorilerde mi? (Customer UserId ile kontrol et)
                if (payloadCustomer != null && appt.CustomerUserId.HasValue && role != "customer")
                {
                    var customerFavoriteResult = await favoriteService.IsFavoriteAsync(userId, appt.CustomerUserId.Value);
                    isCustomerFavorite = customerFavoriteResult.Success && customerFavoriteResult.Data;
                    payloadCustomer.IsInFavorites = isCustomerFavorite;
                }

                // Store favorilerde mi? (Store ID ile kontrol et)
                if (payloadStore != null && store != null && role != "store")
                {
                    var storeFavoriteResult = await favoriteService.IsFavoriteAsync(userId, store.Id);
                    isStoreFavorite = storeFavoriteResult.Success && storeFavoriteResult.Data;
                    payloadStore.IsInFavorites = isStoreFavorite;
                }

                // FreeBarber favorilerde mi? 
                // ÖNEMLİ: IsFavoriteAsync FreeBarber ID'yi de kabul eder ve FreeBarber User ID'ye çevirir
                // Frontend'de freeBarber.userId (User ID) kullanıldığı için tutarlılık için FreeBarber User ID kullanıyoruz
                if (payloadFreeBarber != null && freeBarberEntity != null && role != "freebarber")
                {
                    // FreeBarber User ID ile kontrol et (frontend ile uyumlu - freeBarber.userId)
                    var freeBarberFavoriteResult = await favoriteService.IsFavoriteAsync(userId, freeBarberEntity.FreeBarberUserId);
                    isFreeBarberFavorite = freeBarberFavoriteResult.Success && freeBarberFavoriteResult.Data;
                    payloadFreeBarber.IsInFavorites = isFreeBarberFavorite;
                }

                var payload = new AppointmentNotifyPayloadDto
                {
                    AppointmentId = appt.Id,
                    RecipientRole = role,
                    Date = appt.AppointmentDate,
                    StartTime = appt.StartTime,
                    EndTime = appt.EndTime,

                    Customer = payloadCustomer,
                    Store = payloadStore,
                    FreeBarber = payloadFreeBarber,
                    Chair = chairInfo,

                    // Status bilgileri - Frontend'de filtreleme için
                    Status = appt.Status,
                    StoreDecision = appt.StoreDecision,
                    FreeBarberDecision = appt.FreeBarberDecision,
                    CustomerDecision = appt.CustomerDecision,
                    StoreSelectionType = appt.StoreSelectionType,
                    PendingExpiresAt = appt.PendingExpiresAt,
                    Note = note,
                    CancellationReason = string.IsNullOrWhiteSpace(appt.CancellationReason) ? null : appt.CancellationReason.Trim(),

                    // Service offerings - Frontend'de hizmet butonlarını göstermek için
                    ServiceOfferings = serviceOfferings.Any() ? serviceOfferings : null,

                    Packages = appointmentPackages.Count > 0 ? appointmentPackages : null,

                    // Favori durumları (backward compatibility için - nested object'lerde de var)
                    IsCustomerInFavorites = isCustomerFavorite,
                    IsStoreInFavorites = isStoreFavorite,
                    IsFreeBarberInFavorites = isFreeBarberFavorite,
                };

                // Push body: tek şablon — kısa olay etiketi + tarih / taraflar / koltuk / iptal nedeni (varsa).
                var notifyBody = BuildBody(role, type, appt, customerInfo, storeInfo, freeBarberInfo, chairInfo);

                // role bazlı "kimleri dahil edelim?"
                // Global exception middleware hataları yakalayacak
                await notificationSvc.CreateAndPushAsync(
                    userId: userId,
                    type: type,
                    appointmentId: appt.Id,
                    title: title,
                    payload: payload,
                    body: notifyBody
                );
            }

            // Badge count güncellemesi NotificationManager.CreateAndPushAsync içinde yapılıyor
            // Her notification oluşturulduğunda otomatik olarak badge schedule ediliyor (NotificationManager satır 163, 204, 227)
            // Burada tekrar schedule etmeye gerek yok (HashSet duplicate'ları filtreler ama gereksiz)

            return new SuccessResult();
        }

        /// <summary>FCM / sistem tepsisi için gövde uzunluğunu güvenli sınıra indirger.</summary>
        private static string? TruncateNotificationText(string? text, int maxLen = 320)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length <= maxLen) return text;
            return text[..(maxLen - 1)] + "…";
        }

        /// <summary>Kullanıcı satırı: görünen ad + varsa 6 haneli (veya diğer) müşteri numarası.</summary>
        private static string? FormatUserWithCustomerNumber(UserNotifyDto? u)
        {
            if (u is null) return null;
            var name = u.DisplayName?.Trim();
            var num = u.CustomerNumber?.Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(num)) return null;
            if (string.IsNullOrWhiteSpace(name)) return string.IsNullOrWhiteSpace(num) ? null : $"No:{num}";
            if (string.IsNullOrWhiteSpace(num)) return name;
            return $"{name} · No:{num}";
        }

        /// <summary>Dükkan satırı: işletme adı + dükkan numarası (StoreNo).</summary>
        private static string? FormatStoreNameAndNumber(StoreNotifyDto? s)
        {
            if (s is null) return null;
            var name = s.StoreName?.Trim();
            var storeNo = s.StoreNo?.Trim();
            var ownerNo = s.StoreOwnerNumber?.Trim();
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(name)) parts.Add(name);
            if (!string.IsNullOrWhiteSpace(storeNo)) parts.Add($"Dükkan No:{storeNo}");
            if (!string.IsNullOrWhiteSpace(ownerNo)) parts.Add($"İşletme No:{ownerNo}");
            var joined = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }

        /// <summary>Başlığa kısa karşı taraf özeti ekler (kilit ekranında boş hissi azaltır).</summary>
        private static string EnhanceAppointmentPushTitle(
            string baseTitle,
            string role,
            UserNotifyDto? customer,
            StoreNotifyDto? store,
            UserNotifyDto? freeBarber)
        {
            if (string.IsNullOrWhiteSpace(baseTitle)) baseTitle = "Bildirim";
            var counterparty = role switch
            {
                "customer" => FormatStoreNameAndNumber(store) ?? FormatUserWithCustomerNumber(freeBarber),
                "store" => FormatUserWithCustomerNumber(customer),
                "freebarber" => FormatUserWithCustomerNumber(customer),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(counterparty)) return baseTitle;
            var shortCp = counterparty.Length > 44 ? counterparty[..41] + "…" : counterparty;
            var full = $"{baseTitle} ({shortCp})";
            return full.Length > 110 ? full[..107] + "…" : full;
        }

        private static string BuildTitle(NotificationType type, string role, AppointmentStatus status, Guid recipientUserId, Entities.Concrete.Entities.Appointment appt)
        {
            return type switch
            {
                NotificationType.AppointmentCreated =>
                    role == "store" ? Messages.NotificationNewAppointmentRequestForStore :
                    role == "freebarber" ? Messages.NotificationNewAppointmentRequest :
                    Messages.AppointmentCreatedNotification,

                NotificationType.AppointmentApproved => 
                    "Randevu Onaylandı",

                NotificationType.AppointmentRejected => 
                    "Randevu Reddedildi",

                NotificationType.AppointmentCancelled => 
                    "Randevu İptal Edildi",
                    
                NotificationType.AppointmentCompleted => 
                    "Randevu Tamamlandı",
                
                NotificationType.AppointmentDecisionUpdated =>
                    "Randevu Durumu Güncellendi",
                
                // 3'lü sistem bildirimleri
                NotificationType.FreeBarberRejectedInitial => 
                    "Serbest Berber Randevuyu Reddetti",
                    
                NotificationType.StoreRejectedSelection => 
                    "Dükkan Randevuyu Reddetti",
                    
                NotificationType.StoreApprovedSelection => 
                    "Dükkan Randevuyu Onayladı",
                    
                NotificationType.StoreSelectionTimeout => 
                    "Dükkan Süresinde Cevap Vermedi",
                    
                NotificationType.CustomerRejectedFinal => 
                    "Müşteri Randevuyu Reddetti",
                    
                NotificationType.CustomerApprovedFinal => 
                    "Müşteri Randevuyu Onayladı",
                    
                NotificationType.CustomerFinalTimeout => 
                    "Müşteri Süresinde Cevap Vermedi",

                NotificationType.AppointmentReminder =>
                    "Randevu Hatırlatması",

                NotificationType.AppointmentCompletionReminder =>
                    "Randevuyu Tamamlamayı Unutmayın",
                
                NotificationType.AppointmentUnanswered =>
                    // Karar vermesi gereken kişiye "Randevuyu cevaplamadınız", diğerlerine "Randevunuz cevaplanamadı"
                    ((role == "store" && (appt.StoreDecision == DecisionStatus.Pending || appt.StoreDecision == DecisionStatus.NoAnswer)) ||
                     (role == "freebarber" && (appt.FreeBarberDecision == DecisionStatus.Pending || appt.FreeBarberDecision == DecisionStatus.NoAnswer)))
                        ? "Randevuyu Cevaplamadınız"
                        : "Randevunuz Cevaplanamadı",

                _ => "Bildirim"
            };
        }

        /// <summary>
        /// Tarih + saat: "02.05 14:30-15:00" (elde olan alanlar kadar).
        /// </summary>
        private static string? BuildWhenPart(Entities.Concrete.Entities.Appointment appt)
        {
            string when = string.Empty;
            if (appt.AppointmentDate.HasValue)
            {
                when = appt.AppointmentDate.Value.ToString("dd.MM");
            }
            if (appt.StartTime.HasValue)
            {
                var start = appt.StartTime.Value;
                var startStr = $"{start.Hours:D2}:{start.Minutes:D2}";
                when = string.IsNullOrEmpty(when) ? startStr : $"{when} {startStr}";
                if (appt.EndTime.HasValue)
                {
                    var end = appt.EndTime.Value;
                    when = $"{when}-{end.Hours:D2}:{end.Minutes:D2}";
                }
            }
            return string.IsNullOrWhiteSpace(when) ? null : when.Trim();
        }

        /// <summary>
        /// Koltuk / manuel berber — alıcı rolünden bağımsız, veri varsa eklenir.
        /// </summary>
        private static string? BuildChairPart(ChairNotifyDto? chair)
        {
            if (chair is null) return null;
            if (!string.IsNullOrWhiteSpace(chair.ManuelBarberName))
                return chair.ManuelBarberName.Trim();
            if (!string.IsNullOrWhiteSpace(chair.ChairName))
                return $"Koltuk: {chair.ChairName.Trim()}";
            return null;
        }

        /// <summary>
        /// Push gövdesi için her zaman aynı sıra: tarih-saat → müşteri → dükkan → serbest berber → koltuk → (iptal ise) neden.
        /// Alıcı kendi rolündeki tarafı görmez (payload ile uyumlu bilgi yoğunluğu).
        /// </summary>
        private static List<string> BuildOrderedContextParts(
            string role,
            Entities.Concrete.Entities.Appointment appt,
            UserNotifyDto? customer,
            StoreNotifyDto? store,
            UserNotifyDto? freeBarber,
            ChairNotifyDto? chair,
            NotificationType type)
        {
            var segments = new List<string>();
            var when = BuildWhenPart(appt);
            if (!string.IsNullOrWhiteSpace(when))
                segments.Add(when);

            if (role != "customer")
            {
                var c = FormatUserWithCustomerNumber(customer);
                if (!string.IsNullOrWhiteSpace(c))
                    segments.Add(c);
            }
            if (role != "store")
            {
                var s = FormatStoreNameAndNumber(store);
                if (!string.IsNullOrWhiteSpace(s))
                    segments.Add(s);
            }
            if (role != "freebarber")
            {
                var fb = FormatUserWithCustomerNumber(freeBarber);
                if (!string.IsNullOrWhiteSpace(fb))
                    segments.Add(fb);
            }

            var chairLine = BuildChairPart(chair);
            if (!string.IsNullOrWhiteSpace(chairLine))
                segments.Add(chairLine);

            if (type == NotificationType.AppointmentCancelled && !string.IsNullOrWhiteSpace(appt.CancellationReason))
            {
                var r = appt.CancellationReason.Trim();
                if (r.Length > 120) r = r[..119] + "…";
                segments.Add($"İptal nedeni: {r}");
            }

            return segments;
        }

        /// <summary>
        /// Tüm randevu push türleri için tek tip: "Olay etiketi • bağlam..."
        /// Bağlam her zaman aynı parça sırasıyla üretilir; sadece dolu olanlar yazılır.
        /// </summary>
        private static string? GetPushEventLabel(NotificationType type)
        {
            return type switch
            {
                NotificationType.AppointmentCreated => "Yeni randevu isteği",
                NotificationType.AppointmentApproved => "Randevu onaylandı",
                NotificationType.AppointmentRejected => "Randevu reddedildi",
                NotificationType.AppointmentCancelled => "Randevu iptal edildi",
                NotificationType.AppointmentCompleted => "Randevu tamamlandı",
                NotificationType.AppointmentUnanswered => "Randevu yanıtlanamadı",
                NotificationType.AppointmentDecisionUpdated => "Randevu durumu güncellendi",
                NotificationType.FreeBarberRejectedInitial => "Serbest berber reddetti",
                NotificationType.StoreRejectedSelection => "Dükkan seçimi reddedildi",
                NotificationType.StoreApprovedSelection => "Dükkan seçimi onaylandı",
                NotificationType.StoreSelectionTimeout => "Dükkan süre aşımı",
                NotificationType.CustomerRejectedFinal => "Müşteri reddetti",
                NotificationType.CustomerApprovedFinal => "Müşteri onayladı",
                NotificationType.CustomerFinalTimeout => "Müşteri süre aşımı",
                NotificationType.AppointmentReminder => "Randevu hatırlatması",
                NotificationType.AppointmentCompletionReminder => "Tamamlama hatırlatması",
                _ => "Bildirim"
            };
        }

        /// <summary>
        /// Push bildiriminin body: tek şablon — olay etiketi + (varsa) aynı biçimde sıralanmış bağlam.
        /// </summary>
        private static string? BuildBody(
            string role,
            NotificationType type,
            Entities.Concrete.Entities.Appointment appt,
            UserNotifyDto? customer,
            StoreNotifyDto? store,
            UserNotifyDto? freeBarber,
            ChairNotifyDto? chair)
        {
            const string sep = " • ";
            var label = GetPushEventLabel(type);
            var contextParts = BuildOrderedContextParts(role, appt, customer, store, freeBarber, chair, type);
            var context = string.Join(sep, contextParts.Where(p => !string.IsNullOrWhiteSpace(p)));

            if (string.IsNullOrWhiteSpace(context))
                return TruncateNotificationText(label);

            return TruncateNotificationText($"{label}{sep}{context}");
        }
    }
}
