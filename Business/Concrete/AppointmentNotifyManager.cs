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
        IFreeBarberDal freeBarberDal
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

            // Eğer hiç recipient yoksa hata döndür
            if (recipients.Count == 0)
            {
                return new ErrorResult("Randevu için alıcı bulunamadı.");
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

            // FreeBarber entity'sini al (image ve diğer bilgiler için)
            FreeBarber? freeBarberEntity = null;
            string? freeBarberImageUrl = null;
            if (appt.FreeBarberUserId.HasValue)
            {
                // FreeBarber entity'sini FreeBarberUserId ile bul (FreeBarberUserId = FreeBarber.FreeBarberUserId)
                freeBarberEntity = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                if (freeBarberEntity != null)
                {
                    // FreeBarber image'ını Image tablosundan al (FreeBarber ID ile)
                    var freeBarberImage = await imageDal.GetLatestImageAsync(freeBarberEntity.Id, ImageOwnerType.FreeBarber);
                    freeBarberImageUrl = freeBarberImage?.ImageUrl;

                    // Eğer freeBarberInfo varsa, image'ı güncelle
                    if (freeBarberInfo != null)
                    {
                        freeBarberInfo.AvatarUrl = freeBarberImageUrl;
                        freeBarberInfo.BarberType = freeBarberEntity.Type;
                        // FreeBarber canlı konumu — frontend bu snapshot'ı kullanır,
                        // ayrıca "Haritada Göster" ekranı 10sn'de bir live polling yapar.
                        freeBarberInfo.Latitude = freeBarberEntity.Latitude;
                        freeBarberInfo.Longitude = freeBarberEntity.Longitude;
                    }
                }
            }

            // Store (ownerId ile bulunuyor)
            // ÖNEMLİ: BarberStoreUserId null ise store bulunamaz
            BarberStore? store = null;
            if (appt.BarberStoreUserId.HasValue)
            {
                store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
            }

            // store image null-safe - PERFORMANCE: Use GetLatestImageAsync
            string? storeImageUrl = null;
            if (store is not null)
            {
                var storeImage = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                storeImageUrl = storeImage?.ImageUrl;
            }

            StoreNotifyDto? storeInfo = null;
            if (store is not null)
            {
                // Store owner'ın customerNumber'ını userMap'ten al
                string? storeOwnerNumber = null;
                if (userMap.TryGetValue(store.BarberStoreOwnerId, out var storeOwner))
                {
                    storeOwnerNumber = storeOwner.CustomerNumber;
                }

                storeInfo = new StoreNotifyDto
                {
                    StoreId = store.Id,
                    StoreOwnerUserId = store.BarberStoreOwnerId,
                    StoreName = store.StoreName,
                    ImageUrl = storeImageUrl,
                    Type = store.Type,
                    AddressDescription = store.AddressDescription,
                    StoreOwnerNumber = storeOwnerNumber,
                    StoreNo = store.StoreNo,
                    Latitude = store.Latitude,
                    Longitude = store.Longitude
                };
            }

            // Chair + ManuelBarber (opsiyonel)
            ChairNotifyDto? chairInfo = null;
            if (appt.ChairId.HasValue)
            {
                var chair = await chairDal.Get(c => c.Id == appt.ChairId.Value);
                if (chair is not null)
                {
                    chairInfo = new ChairNotifyDto
                    {
                        ChairId = chair.Id,
                        ChairName = chair.Name,              // sadece isimli de olabilir
                        ManuelBarberId = chair.ManuelBarberId // null olabilir
                    };

                    // ManuelBarber sadece varsa
                    if (chair.ManuelBarberId.HasValue)
                    {
                        var mb = await manuelBarberDal.Get(x => x.Id == chair.ManuelBarberId.Value);
                        if (mb is not null)
                        {
                            chairInfo.ManuelBarberName = mb.FullName;
                            
                            // DÜZELTME: Manuel barber fotoğrafını ekle - PERFORMANCE: Use GetLatestImageAsync
                            var manuelBarberImage = await imageDal.GetLatestImageAsync(mb.Id, ImageOwnerType.ManuelBarber);
                            
                            if (manuelBarberImage != null)
                            {
                                chairInfo.ManuelBarberImageUrl = manuelBarberImage.ImageUrl;
                                chairInfo.ManuelBarberType = store?.Type;
                            }
                        }
                    }
                }
            }

            // ChairId, slot serbest bırakılırken null olabilir (ör. AppointmentTimeoutWorker); kart/bildirim için
            // koltuk adı ve manuel berber id'si appointment satırında ChairName / ManuelBarberId olarak snapshot kalır.
            if (chairInfo is null &&
                (!string.IsNullOrWhiteSpace(appt.ChairName) || appt.ManuelBarberId.HasValue))
            {
                chairInfo = new ChairNotifyDto
                {
                    ChairId = appt.ChairId ?? Guid.Empty,
                    ChairName = appt.ChairName,
                    ManuelBarberId = appt.ManuelBarberId
                };
                if (chairInfo.ManuelBarberId.HasValue)
                {
                    var mb = await manuelBarberDal.Get(x => x.Id == chairInfo.ManuelBarberId.Value);
                    if (mb is not null)
                    {
                        chairInfo.ManuelBarberName = mb.FullName;
                        chairInfo.ManuelBarberType = store?.Type;
                        var manuelBarberImage = await imageDal.GetLatestImageAsync(mb.Id, ImageOwnerType.ManuelBarber);
                        if (manuelBarberImage is not null)
                        {
                            chairInfo.ManuelBarberImageUrl = manuelBarberImage.ImageUrl;
                        }
                    }
                }
            }

            // Not: FreeBarber varsa "manuel barber olmayacak" demiştin.
            // Yine de defensive kalalım:
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

                var title = BuildTitle(type, role, appt.Status, userId, appt);

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

                // Push body: status başlığı yerine randevuya dair somut detay göster
                // (kim/hangi dükkan, tarih-saat, varsa koltuk). İptal durumunda sebep de eklenir.
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
        /// Push bildiriminin body kısmı: status başlığı yerine somut randevu özeti.
        /// Role'e göre karşı tarafın adı + tarih/saat (+ varsa iptal sebebi/koltuk).
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
            // Tarih + saat parçası: "02.05 14:30-15:00"
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

            // Karşı tarafın etiketi: kullanıcı kim, kime gidiyor?
            string? counterparty = role switch
            {
                // Customer'a giden: dükkan veya free barber adı
                "customer" => !string.IsNullOrWhiteSpace(store?.StoreName)
                    ? store!.StoreName
                    : freeBarber?.DisplayName,
                // Store'a giden: müşteri adı
                "store" => customer?.DisplayName,
                // FreeBarber'a giden: müşteri adı
                "freebarber" => customer?.DisplayName,
                _ => null
            };

            // Koltuk/manuel berber bilgisi (store rolü için anlamlı)
            string? chairPart = null;
            if (role == "store" && chair is not null)
            {
                if (!string.IsNullOrWhiteSpace(chair.ManuelBarberName))
                    chairPart = chair.ManuelBarberName;
                else if (!string.IsNullOrWhiteSpace(chair.ChairName))
                    chairPart = chair.ChairName;
            }

            // Parçaları birleştir: "Berber Ahmet • 02.05 14:30-15:00 • Koltuk 3"
            var parts = new List<string?> { counterparty, when, chairPart }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            var summary = string.Join(" • ", parts!);

            // İptal nedenini başa ekle (varsa)
            if (type == NotificationType.AppointmentCancelled && !string.IsNullOrWhiteSpace(appt.CancellationReason))
            {
                var r = appt.CancellationReason.Trim();
                if (r.Length > 160) r = r[..160] + "…";
                return string.IsNullOrEmpty(summary) ? $"Neden: {r}" : $"Neden: {r} — {summary}";
            }

            // Hatırlatma için "Yaklaşan randevu" prefix'i ekle
            if (type == NotificationType.AppointmentReminder && !string.IsNullOrEmpty(summary))
            {
                return $"Yaklaşan randevu: {summary}";
            }

            // Hiç bilgi yoksa boş yerine başlığı tekrar etmeye gerek yok; null dönünce push servisi
            // body için title fallback'ini kullanır (eski davranış).
            return string.IsNullOrEmpty(summary) ? null : summary;
        }
    }
}
