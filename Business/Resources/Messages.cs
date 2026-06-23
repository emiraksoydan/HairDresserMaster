namespace Business.Resources
{
    /// <summary>
    /// Centralized error and success messages
    /// Avoids hardcoded strings throughout the codebase
    /// </summary>
    public static class Messages
    {
        // Appointment Messages
        public const string AppointmentNotFound = "Randevu bulunamadı";
        public const string AppointmentExpired = "Randevu süresi dolmuş";
        public const string AppointmentAlreadyCompleted = "Randevu zaten tamamlanmış";
        public const string AppointmentAlreadyCancelled = "Randevu zaten iptal edilmiş";
        public const string AppointmentCannotBeCancelled = "İptal edilemez";
        public const string AppointmentAdminCancelledSuccess = "Randevu admin tarafından iptal edildi.";
        public const string AppointmentCannotBeCancelledAfterTimePassed = "Randevu saati dolduktan sonra iptal edilemez, yalnızca tamamlanabilir.";
        public const string AppointmentCancellationReasonTooLong = "İptal nedeni en fazla 500 karakter olabilir.";
        public const string AppointmentTimeNotPassed = "Randevu süresi dolmadan tamamlanamaz";
        public const string AppointmentNotApproved = "Kabul edilmemiş randevu";
        public const string AppointmentNotPending = "Beklemede değil";
        public const string AppointmentNotPendingStatus = "Bekleme yok";
        public const string AppointmentDecisionAlreadyGiven = "Karar zaten verilmiş";
        public const string AppointmentSlotTaken = "Bu randevu zamanı başka bir kullanıcı tarafından alındı. Lütfen başka bir saat seçin.";
        public const string AppointmentSlotOverlap = "Bu koltuk için seçilen saat aralığında başka bir randevu var.";
        public const string AppointmentAvailabilityRangeInvalid = "Bitiş tarihi başlangıçtan önce olamaz.";
        public const string AppointmentAvailabilityRangeTooLarge = "Müsaitlik aralığı en fazla 7 gün olabilir.";
        public const string AppointmentPastDate = "Geçmiş tarih için randevu alınamaz.";
        public const string AppointmentPastTime = "Geçmiş saat için randevu alınamaz.";
        public const string AppointmentTimeoutExpired = "Randevu süresi dolmuş (yanıtlanmadı).";
        public const string AppointmentCreatedSuccess = "Randevu başarıyla oluşturuldu.";
        public const string AppointmentApprovedSuccess = "Randevu onaylandı.";
        public const string AppointmentRejectedSuccess = "Randevu reddedildi.";
        public const string AppointmentCancelledSuccess = "Randevu iptal edildi.";
        public const string AppointmentCompletedSuccess = "Randevu tamamlandı.";

        // Authorization Messages
        public const string Unauthorized = "Yetki yok";
        public const string UnauthorizedOperation = "İşleme yetkiniz bulunmamaktadır";
        public const string NotAParticipant = "Bu randevuya katılımcı değilsiniz";

        // Store Messages
        public const string StoreNotFound = "Dükkan bulunamadı";
        public const string StoreNotFoundOrNotOwner = "Dükkan bulunamadı veya sahibi değilsiniz";
        public const string StoreNotOpen = "Dükkan bu saat aralığında açık değil";
        public const string StoreClosed = "Dükkan bu gün kapalı (tatil)";
        public const string StoreNoWorkingHours = "Dükkan bu gün için çalışma saati tanımlamamış (kapalı)";

        // Store Owner kendi dükkanına FreeBarber çağırırken kullanılır.
        // "Dükkan açık değil" ifadesi muğlak kalıyordu; bu mesajlar dükkan sahibinin
        // KENDİ dükkanının çalışma saatleri dışında olduğunu net biçimde söyler.
        public const string OwnStoreNotOpenNow = "Dükkanınız şu anda çalışma saatleri aralığında değil. Lütfen çalışma saatlerinizi güncelleyin veya açık olduğunuz bir saatte tekrar deneyin.";
        public const string OwnStoreClosedToday = "Dükkanınız bugün kapalı (tatil) olarak işaretli. Bu yüzden serbest berber çağrısı yapılamaz.";
        public const string OwnStoreNoWorkingHoursToday = "Dükkanınız bugün için çalışma saati tanımlamamış. Önce çalışma saatlerinizi ayarlayın.";
        public const string StoreCreatedSuccess = "Berber dükkanı başarıyla oluşturuldu.";
        public const string StoreUpdatedSuccess = "Berber dükkanı başarıyla güncellendi.";

        // Chair Messages
        public const string ChairNotFound = "Koltuk bulunamadı";
        public const string ChairNotInStore = "Koltuk dükkanda bulunamadı";
        public const string ChairRequired = "Koltuk seçimi gereklidir.";

        // FreeBarber Messages
        public const string FreeBarberNotFound = "Serbest berber bulunamadı";
        public const string FreeBarberNotAvailable = "Serbest berber şu an müsait değil";

        /// <summary>İstek atan kullanıcı, hedef serbest berberin kendisi olduğunda (IsAvailable=false / kilit).</summary>
        public const string FreeBarberSelfNotAvailable = "Şu anda müsait değilsiniz; randevu alabilmek için müsait olmalısınız.";
        public const string FreeBarberInvalidCoordinates = "Serbest berber koordinatları geçersiz";
        // NOT: Mesafe limiti AppointmentSettings.MaxDistanceKm <= 0 olduğunda kaldırılır
        // (sınırsız). Aşağıdaki metinler limit pozitifse kullanılır; gerçek mesafe runtime'da eklenir.
        public const string FreeBarberDistanceExceeded = "Serbest berber, izin verilen mesafenin dışında. Bu konumdan randevu oluşturulamaz.";
        public const string FreeBarberStoreDistanceExceeded = "Serbest berber ile dükkan arası, izin verilen mesafenin dışında. Bu eşleşmeyle randevu açılamaz.";
        public const string StoreFreeBarberDistanceExceeded = "Dükkan ile serbest berber arası, izin verilen mesafenin dışında. Bu eşleşmeyle randevu açılamaz.";
        public const string FreeBarberUserIdRequired = "Serbest berber seçimi gereklidir.";
        public const string FreeBarberNotAllowedForStoreAppointment = "Dükkan randevusunda serbest berber seçilemez.";
        public const string FreeBarberUpdateUnauthorized = "Bu serbest berberi güncelleme yetkiniz yok";
        public const string FreeBarberPanelAlreadyExists = "Zaten bir serbest berber paneliniz bulunmaktadır. Her kullanıcının sadece bir paneli olabilir.";
        public const string FreeBarberPanelRequired = "Randevu oluşturmak için önce serbest berber panelinizi oluşturmanız gerekmektedir.";

        // Customer Messages
        public const string CustomerHasActiveAppointment = "Müşterinin aktif (Bekleyen/Onaylanmış) randevusu var.";
        public const string CustomerAlreadyHasActiveAppointment = "Zaten aktif bir randevunuz var. Önce onu tamamlayın.";
        public const string CustomerDistanceExceeded = "Dükkan, izin verilen mesafenin dışında. Yakın değilken randevu oluşturamazsın.";

        // Store Messages (continued)
        public const string StoreHasActiveCall = "Dükkanın aktif bir serbest berber çağrısı var. Önce onu sonuçlandır.";
        public const string StoreAlreadyHasActiveAppointment = "Dükkanın zaten aktif bir randevusu var.";
        public const string StoreAlreadyHasActiveAppointmentWithThisFreeBarber =
            "Bu işletmenizde bu serbest berber için zaten bekleyen veya aktif bir çağrınız var. Önce onu sonuçlandırın.";
        public const string FreeBarberHasActiveAppointment = "Serbest berberin aktif (Bekleyen/Onaylanmış) randevusu var.";
        public const string FreeBarberAlreadyHasActiveAppointment = "Serbest berberin zaten aktif bir randevusu var.";
        public const string FreeBarberHasActiveAppointmentUpdate = "Randevu işleminiz bulunmaktadır. Lütfen işlemden sonra güncelleyiniz";

        // Validation Messages
        public const string InvalidDate = "Geçersiz tarih";
        public const string InvalidTime = "Geçersiz saat";
        public const string AppointmentDateCannotBePast = "Randevu tarihi geçmişte olamaz.";
        public const string StartTimeGreaterThanEndTime = "Başlangıç saati bitişten büyük/eşit olamaz.";
        public const string StartTimeEndTimeRequired = "Başlangıç ve bitiş saati gereklidir.";
        public const string LocationRequired = "Konum bilgisi gerekli (RequestLatitude/RequestLongitude).";
        public const string ServiceOfferingRequired = "En az bir hizmet seçilmelidir";
        public const string ServiceOfferingOwnerMismatch = "Seçilen hizmetler bu kullanıcıya ait değil.";
        public const string ServiceOfferingUsedInPackages = "Bu hizmet bir veya daha fazla pakette kullanılıyor. Önce ilgili paketlerden çıkarın veya paketleri güncelleyin.";
        public const string AppointmentEndTimeCalculationFailed = "Randevu bitiş zamanı hesaplanamadı.";
        
        // User Messages
        public const string AccountDeleteBlockedByActiveAppointments = "Bekleyen veya onaylanmış randevunuz varken hesabınızı silemezsiniz. Önce randevularınızı tamamlayın, iptal edin veya sonuçlanmasını bekleyin.";
        public const string UserNotFound = "Kullanıcı bulunamadı.";
        public const string OnlyCustomersCanCreateAppointment = "Sadece müşteriler randevu oluşturabilir.";
        public const string UserBlockedCannotCreateAppointment = "Engellenen bir kullanıcıdan randevu alamazsınız.";
        public const string UserBlockedCannotDecideAppointment = "Engelleme nedeniyle bu randevu üzerinde onay veya red veremezsiniz.";

        // Chat Messages
        public const string ChatOnlyForActiveAppointments = "Sohbet sadece Bekleyen/Onaylanmış randevular için aktiftir.";
        public const string EmptyMessage = "Boş mesaj gönderilemez";
        public const string ChatThreadNotFound = "Sohbet kaydı bulunamadı";
        public const string ChatNotFound = "Sohbet bulunamadı";
        public const string ParticipantNotFound = "Katılımcı bulunamadı";

        // ManuelBarber Messages
        public const string ManuelBarberNotFound = "Berber bulunamadı";
        public const string ManuelBarberHasActiveAppointments = "Bu berberinize ait beklemekte olan veya aktif olan randevu işlemi vardır.";
        public const string ManuelBarberAddedSuccess = "Manuel berber eklendi.";
        public const string ManuelBarberUpdatedSuccess = "Manuel berber güncellendi.";
        public const string ManuelBarberDeletedSuccess = "Manuel berber silindi.";

        // General Messages
        public const string OperationSuccess = "İşlem başarılı";
        public const string OperationFailed = "İşlem başarısız";
        public const string EntityNotFound = "Kayıt bulunamadı";
        public const string StoreHasActiveAppointments = "Bu dükkana ait aktif veya bekleyen randevu var önce müsait olmalısınız ";
        public const string BarberAssignedToMultipleChairs = "Bir berber birden fazla koltuğa atanamaz.";
        public const string BarberAssignedToChair = "Bu berberiniz bir koltuğa atanmış. Önce koltuk ayarını değiştiriniz.";
        
        
        // Additional Notification Messages
        public const string AppointmentCreatedNotification = "Randevun oluşturuldu";
        public const string AppointmentApprovedNotification = "Randevu onaylandı";
        public const string AppointmentRejectedNotification = "Randevu reddedildi";
        public const string AppointmentCancelledNotification = "Randevu iptal edildi";
        public const string AppointmentCompletedNotification = "Randevu tamamlandı";
        public const string AppointmentUnansweredNotification = "Randevu yanıtlanmadı";
        
        // UserOperationClaim Messages
        public const string UserOperationClaimsAddedSuccess = "Kullanıcı Yetkileri Eklendi";
        public const string UserOperationClaimsAdded = "Kullanıcı Yetkileri Eklendi";
        public const string UserOperationClaimsNotFound = "Kullanıcı yetkileri bulunamadı";
        public const string OperationClaimsGetFailed = "Yetkiler getirilemedi";
        
        // Additional Distance/Coordinate Messages
        public const string LocationNotSet = "Konumu ayarlı değil";
        public const string LocationInvalid = "Konumu geçersiz";
        public const string RequestLocationNotSet = "İstek konumu ayarlı değil";
        public const string TargetLocationNotSet = "Hedef konumu ayarlı değil";
        public const string FreeBarberLocationNotSet = "Serbest berber konumu ayarlı değil";
        public const string FreeBarberLocationInvalid = "Serbest berber konumu geçersiz";
        public const string DistanceExceeded = "Mesafe limiti aşıldı";
        
        // Additional Chat Thread Title Messages
        public const string ChatThreadTitleCustomer = "Müşteri";
        public const string ChatThreadTitleFreeBarber = "Serbest Berber";
        public const string ChatThreadTitleBarberStore = "Berber Dükkanı";
        
        // Additional Notification Messages
        public const string NotificationDefault = "Bildirim";
        public const string NotificationNewAppointmentRequest = "Yeni randevu isteği";
        public const string NotificationNewAppointmentRequestForStore = "Yeni randevu talebi";
        
        // Rating Messages
        public const string RatingCreatedSuccess = "Değerlendirme başarıyla kaydedildi.";
        public const string RatingUpdatedSuccess = "Değerlendirme başarıyla güncellendi.";
        public const string RatingDeletedSuccess = "Değerlendirme silindi.";
        public const string RatingNotFound = "Değerlendirme bulunamadı.";
        public const string RatingOnlyForCompleted = "Sadece tamamlanmış veya iptal edilmiş randevular için değerlendirme yapılabilir.";
        public const string CannotRateYourself = "Kendi kendinize değerlendirme yapamazsınız.";
        public const string InvalidTargetForRating = "Geçersiz hedef. Sadece Store ID, FreeBarber ID veya Customer UserId ile değerlendirme yapılabilir. ManuelBarber'a değerlendirme yapılamaz.";
        
        // Favorite Messages
        public const string FavoriteAddedSuccess = "Favorilere eklendi.";
        public const string FavoriteUpdatedSuccess = "Favori güncellendi.";
        public const string FavoriteRemovedSuccess = "Favorilerden çıkarıldı.";
        public const string FavoriteNotFound = "Favori bulunamadı.";
        public const string CannotFavoriteYourself = "Kendi kendinizi favorilere ekleyemezsiniz.";
        public const string CannotFavoriteBlockedUser = "Engellediğiniz veya sizi engelleyen kullanıcılar favorilenemez.";
        public const string TargetUserNotFound = "Hedef kullanıcı bulunamadı.";
        public const string AppointmentMustBeCompletedForFavorite = "Randevu sayfasından favorileme için randevunuzun sonuçlanması gerekir.";
        
        // Appointment Additional Messages
        public const string AppointmentCannotAddStore = "Bu randevuya dükkan eklenemez.";
        public const string FreeBarberApprovalStepNotAvailable = "Bu randevuda serbest berber onay adımı yok. Dükkan seçimi bekleniyor.";
        public const string CannotRejectAfterApproval = "Randevu onaylandı, artık red edemezsiniz.";
        public const string CannotRejectAfterCancellation = "Randevu iptal edildi, artık red edemezsiniz.";
        public const string CannotRejectAfterCompletion = "Randevu tamamlandı, artık red edemezsiniz.";
        public const string RejectionTimeoutExpired = "Reddetme süresi doldu.";
        public const string FreeBarberApprovalPending = "Serbest berber onayı bekleniyor.";
        public const string CustomerDecisionNotAllowed = "Bu randevu için müşteri kararı verilemez.";
        public const string StoreApprovalPending = "Dükkan onayı bekleniyor.";
        public const string CannotDeletePendingOrApproved = "Beklemede veya onaylanmış durumdaki randevular silinemez.";
        public const string AppointmentNotFoundForDelete = "Silinecek randevu bulunamadı.";
        public const string NoAppointmentsDeleted = "Hiçbir randevu silinemedi. {0} adet randevu beklemede veya onaylanmış durumda.";

        /// <summary>Tek bildirim sil — randevu hâlâ aksiyon bekleyen (Pending) durumda.</summary>
        public const string NotificationCannotDeleteForActiveAppointment = "Aksiyon bekleyen bir randevuya ait bildirim silinemez.";
        /// <summary>Tümünü sil — hepsi aksiyon bekleyen randevuya bağlıysa.</summary>
        public const string NotificationsDeleteAllOnlyActiveAppointments = "Silinecek bildirim bulunamadı; kalan bildirimlerin tamamı aksiyon bekleyen randevulara ait.";
        
        // Rating Additional Messages
        public const string RatingAlreadyExists = "Bu randevu için bu hedefe zaten değerlendirme yaptınız. Değerlendirme güncellenemez.";
        public const string TargetNotFound = "Hedef bulunamadı.";
        
        // Chat Additional Messages
        public const string MessageRequiresActiveAppointmentOrFavorite = "Mesaj göndermek için randevu aktif olmalı veya karşılıklı favori olmalısınız.";
        public const string MethodOnlyForFavoriteThreads = "Bu metod sadece favori thread'ler için kullanılabilir";
        public const string FavoriteNotActive = "Favori aktif değil, mesaj gönderilemez";
        public const string FavoriteNotActiveForMessages = "Favori aktif değil";
        public const string FavoriteRequiredToSend = "Mesaj gönderebilmek için bu kişiyi favorilerinize eklemelisiniz.";
        public const string FavoriteRequiredToReadMessages = "Mesajları görüntüleyebilmek için bu kişiyi favorilerinize eklemelisiniz.";
        public const string ThreadRestrictedNoFavorite = "Bu konuşmaya erişmek için karşı tarafı favorilerinize ekleyin.";
        
        // FreeBarber Additional Messages
        public const string FreeBarberPortalCreatedSuccess = "Serbest berber portalı başarıyla oluşturuldu.";
        public const string FreeBarberUpdatedSuccess = "Serbest berber güncellendi.";
        public const string FreeBarberNotAvailableCannotDeletePanel = "Müsait değilken panelinizi silemezsiniz. Önce müsait olarak işaretleyin.";
        public const string FreeBarberDeletedSuccess = "Serbest berber silindi.";
        public const string BarberNotFound = "Berber bulunamadı";
        public const string LocationUpdatedSuccess = "Konum başarıyla güncellendi";
        public const string PanelGetFailed = "Panel getirilemedi";
        public const string FilteredFreeBarbersRetrieved = "Filtrelenmiş serbest berberler getirildi";
        public const string PanelDetailGetFailed = "Panel detayı getirilemedi";
        
        // BarberStore Additional Messages
        public const string BarberStoreUpdatedSuccess = "Berber dükkanı başarıyla güncellendi.";
        public const string StoreDeletedSuccess = "Dükkan silindi.";
        public const string NearbyBarbersRetrieved = "1 Kilometreye sınırdaki berberler getirildi";
        public const string FilteredBarberStoresRetrieved = "Filtrelenmiş berber dükkanları getirildi";
        
        // Category Messages
        public const string CategoryAddedSuccess = "Kategori Eklendi";
        public const string CategoryDeletedSuccess = "Kategori Silindi";
        public const string MainCategoriesRetrieved = "Ana kategoriler getirildi";
        public const string SubCategoriesRetrieved = "Alt kategoriler getirildi";
        public const string CategoriesRetrieved = "Kategoriler getirildi";
        public const string CategoryUpdatedSuccess = "Kategori güncellendi";
        public const string CategoryNotFound = "Kategori bulunamadı";
        public const string CategoryParentNotFound = "Üst kategori bulunamadı";
        public const string CategoryNameRequired = "Kategori adı zorunludur";
        public const string CategoryCannotBeOwnParent = "Bir kategori kendi üst kategorisi olamaz";
        public const string CategoryCycleDetected = "Bu taşıma kategoride döngü oluşturur";

        // -------- HelpGuide --------
        public const string HelpGuideNotFound = "Yardım rehberi kaydı bulunamadı.";
        public const string HelpGuideTitleRequired = "Başlık zorunludur.";
        public const string HelpGuideCreated = "Yardım rehberi kaydı oluşturuldu.";
        public const string HelpGuideUpdated = "Yardım rehberi kaydı güncellendi.";
        public const string HelpGuideDeleted = "Yardım rehberi kaydı silindi.";
        
        // Image Messages
        public const string ImageOwnerIdRequired = "Resim sahibi ID'si boş olamaz";
        public const string ImageIdRequired = "Resim ID'si boş olamaz";

        // B4: Upload validation
        public const string UploadFileRequired = "Dosya boş veya gönderilmedi.";
        public const string UploadOnlyImagesAllowed = "Profil ve galeri uploadlarında yalnızca resim dosyaları kabul edilir.";
        
        // HelpGuide Messages
        public const string InvalidUserType = "Geçersiz kullanıcı tipi.";
        
        // Chat Default Names
        public const string BarberDefaultName = "Berber";
        public const string FreeBarberDefaultName = "Serbest Berber";
        public const string UserDefaultName = "Kullanıcı";
        
        // Additional Appointment Messages
        public const string CannotRejectAfterCustomerApproval = "Müşteri onay verdiği için bu randevu artık reddedilemez.";

        // Ban Messages
        public const string UserBanned = "Hesabınız yönetici tarafından askıya alınmıştır.";
        public const string UserBannedWithReason = "Hesabınız askıya alınmıştır. Sebep: {0}";
        public const string UserBannedSuccess = "Kullanıcı başarıyla engellendi.";
        public const string UserUnbannedSuccess = "Kullanıcı engeli başarıyla kaldırıldı.";

        // Subscription Messages
        // Trial konsepti kullanıcı isteği üzerine kaldırıldı (Madde 8 / Phase B).
        // SubscriptionExpired sadece Subscription:GateEnabled=true olduğunda
        // UserStatusFilter tarafından döndürülür; gate kapalıyken kullanılmaz.
        public const string SubscriptionExpired = "Bu özelliği kullanmak için lütfen abone olunuz.";
        public const string SubscriptionRequiredForAdditionalStore = "İkinci ve sonraki dükkanlar için abonelik gerekmektedir. Lütfen abone olunuz.";
        public const string BarberStorePanelAlreadyExists = "Zaten bir berber dükkanı paneliniz bulunmaktadır.";

        // ServicePackage Messages
        public const string ServicePackageNotFound = "Hizmet paketi bulunamadı.";
        public const string ServicePackageLimitReached = "En fazla 20 hizmet paketi ekleyebilirsiniz.";
        public const string ServicePackageDuplicateServices = "Aynı hizmetleri içeren bir paket zaten mevcut. En az bir farklı hizmet ekleyin.";
        public const string ServicePackageHasActiveAppointments = "Bu paketin aktif veya bekleyen randevusu olduğundan silinemez/güncellenemez.";
        public const string ServicePackageAddedSuccess = "Hizmet paketi başarıyla eklendi.";
        public const string ServicePackageUpdatedSuccess = "Hizmet paketi başarıyla güncellendi.";
        public const string ServicePackageDeletedSuccess = "Hizmet paketi başarıyla silindi.";
        public const string ServicePackageServiceNotFound = "Seçilen hizmetlerden bazıları bulunamadı.";
        public const string ServicePackageModifiedByAnotherProcess = "Hizmet paketi başka bir işlem tarafından güncellendi veya silindi. Lütfen listeyi yenileyip tekrar deneyin.";
        public const string ServicePackageOrServiceRequired = "Randevu için ya hizmet ya da paket seçilmelidir, ikisi birden seçilemez.";
        /// <summary>Tekil hizmet + paket birlikteyse paket içinde aynı hizmet ID'si olmamalı</summary>
        public const string ServicePackageOverlapsSelectedServices = "Seçilen paketlerden biri, ayrıca seçtiğiniz hizmetlerden biriyle çakışıyor. Paket yalnızca seçmediğiniz hizmetleri içerebilir.";
        public const string ServicePackageConflictingServices = "Seçilen paketlerin bazıları ortak hizmet içeriyor. Aynı hizmeti içeren birden fazla paket seçilemez.";
        public const string ServicePackageOwnerMismatch = "Seçilen paketler bu kullanıcıya ait değil.";
        /// <summary>Randevu veya yüzdelik sistemde en az hizmet ya da paket seçimi için</summary>
        public const string ServiceOfferingOrPackageRequired = "En az bir hizmet veya paket seçilmelidir.";

        // SavedFilter Messages
        public const string SavedFilterNameAlreadyExists = "Bu isimde kayıtlı bir filtre zaten var.";
        public const string SavedFilterCriteriaAlreadyExists = "Aynı filtre kriterleriyle kayıtlı bir filtre zaten var.";
        public const string SavedFilterCreatedSuccess = "Filtre kaydedildi.";
        public const string SavedFilterNotFound = "Kayıtlı filtre bulunamadı.";
        public const string SavedFilterNotOwner = "Bu filtreyi düzenleme yetkiniz yok.";
        public const string SavedFilterUpdatedSuccess = "Filtre güncellendi.";
        public const string SavedFilterDeletedSuccess = "Filtre silindi.";
        public const string SavedFilterInvalidCriteriaJson = "Kayıtlı filtre verisi geçersiz.";

        // ─────────────────────────────────────────────────────────────────────
        // Ek API mesajları (önceden sınıf içinde sabit string olarak geçenler)
        // ─────────────────────────────────────────────────────────────────────

        public const string StoreNotFoundWithDot = "Dükkan bulunamadı.";
        public const string AdminBarberStoreSuspendedSuccess = "Dükkan askıya alındı.";
        public const string AdminBarberStoreUnsuspendedSuccess = "Dükkan askıdan kaldırıldı.";
        public const string AdminFreeBarberSuspendedSuccess = "Serbest berber askıya alındı.";
        public const string AdminFreeBarberUnsuspendedSuccess = "Serbest berber askıdan kaldırıldı.";
        public const string StoreSuspendedCannotBook = "Bu dükkan şu anda hizmet vermemektedir.";
        public const string FreeBarberSuspendedCannotBook = "Bu serbest berber şu anda hizmet vermemektedir.";
        public const string StoreIdFormatInvalid = "Dükkan Id formatı hatalı.";
        public const string BarberIdFormatInvalid = "Berber Id formatı hatalı.";
        public const string ChairNotFoundWithDot = "Koltuk bulunamadı.";
        public const string ChairHasPendingOrActiveAppointment = "Bu koltuğa ait beklemekte olan veya aktif olan randevu işlemi vardır.";
        public const string BarberAlreadyAssignedToAnotherChair = "Bu berber zaten başka bir koltuğa atanmış.";

        public const string ComplaintCannotTargetSelf = "Kendinizi şikayet edemezsiniz.";
        public const string ComplaintInvalidAppointmentState = "Şikayet oluşturmak için randevu tamamlanmış, iptal edilmiş veya cevapsız olmalıdır.";
        public const string ComplaintNotAppointmentParticipant = "Bu randevunun katılımcısı değilsiniz.";
        public const string ComplaintTargetNotAppointmentParticipant = "Şikayet edilen kişi bu randevunun katılımcısı değil.";
        public const string ComplaintAlreadyReportedUser = "Bu kullanıcıyı zaten şikayet ettiniz.";
        public const string ComplaintNotFound = "Şikayet bulunamadı.";
        public const string ComplaintDeleteForbidden = "Bu şikayeti silme yetkiniz yok.";
        public const string ComplaintCreatedSuccess = "Şikayet başarıyla oluşturuldu.";
        public const string ComplaintDeletedSuccess = "Şikayet başarıyla silindi.";
        public const string ComplaintResolvedSuccess = "Şikayet çözümlendi olarak işaretlendi.";
        public const string ComplaintAlreadyResolved = "Bu şikayet zaten çözümlenmiş.";

        public const string BlockCannotTargetSelf = "Kendinizi engelleyemezsiniz.";
        public const string BlockUserAlreadyBlocked = "Bu kullanıcı zaten engellenmiş.";
        public const string BlockRemoveFailed = "Engelleme bulunamadı veya kaldırılamadı.";

        public const string UserNotFoundNoPeriod = "Kullanıcı bulunamadı";
        public const string InvalidPhoneNumber = "Geçersiz telefon numarası.";
        public const string PhoneSameAsCurrent = "Girilen numara mevcut numaranızla aynı.";
        public const string PhoneNumberAlreadyInUse = "Bu telefon numarası başka bir kullanıcı tarafından kullanılıyor.";
        public const string PhoneNumberNotFound = "Telefon numarası bulunamadı.";
        public const string PhoneNumberNotFoundNoPeriod = "Telefon numarası bulunamadı";

        public const string AuthPhoneAlreadyRegistered = "Bu telefon numarası zaten kayıtlı.";
        public const string AuthNoUserForPhone = "Bu numarayla kayıtlı kullanıcı bulunamadı.";
        public const string AuthSessionClosedSecurity = "Güvenlik nedeniyle oturum kapatıldı.";
        public const string AuthAccountNotFoundDoubleSpace = "Hesap  bulunamadı.";
        public const string AuthTokenNotFound = "Token bulunamadı.";
        public const string AuthInvalidRefreshToken = "Geçersiz refresh token.";
        public const string AuthExpiredOrRevokedToken = "Süresi dolmuş veya iptal edilmiş token.";

        public const string NotificationNotFound = "Bildirim bulunamadı";
        public const string NotificationNothingToDelete = "Silinecek bildirim bulunamadı.";

        public const string RequestNotFound = "İstek bulunamadı.";
        public const string RequestDeleteForbidden = "Bu isteği silme yetkiniz yok.";

        public const string ServiceOwnerRequired = "Hizmet sahibi belirtilmelidir.";
        public const string ServiceOwnerNotFound = "Hizmet sahibi bulunamadı.";
        public const string ServiceOfferingUpdateEmptyPayload = "Hizmet bulunamadı.";
        public const string ServiceOfferingsUpdatedSuccess = "Hizmetler güncellendi.";

        public const string ImageNotFoundWithDot = "Resim bulunamadı.";
        public const string ImageUrlNotFound = "Resim URL'i bulunamadı.";

        public const string AppointmentRecipientNotFound = "Randevu için alıcı bulunamadı.";

        public const string ChatInvalidMessageType = "Geçersiz mesaj türü.";
        public const string ChatTextMessagesWrongEndpoint = "Metin mesajları bu uç nokta ile gönderilemez.";
        public const string ChatMessageNotFound = "Mesaj bulunamadı.";
        public const string ChatInvalidMessageText = "Geçersiz mesaj metni.";
        public const string ChatEditOnlyOwnMessages = "Yalnızca kendi mesajlarınızı düzenleyebilirsiniz.";
        public const string ChatDeleteForEveryoneOnlyOwn = "Yalnızca kendi mesajınızı herkesten silebilirsiniz.";
        public const string ChatEditOnlyTextMessages = "Yalnızca metin mesajları düzenlenebilir.";

        public const string AiAssistantEmptyMessageKey = "empty_message";
        public const string AiAssistantUnavailableKey = "ai_unavailable";
        public const string AiAssistantRateLimitKey = "ai_rate_limit";
        public const string AiAssistantErrorKey = "ai_error";
        public const string AiAssistantInvalidResponseKey = "ai_invalid_response";
        public const string AiAssistantUnknownAction = "Bilinmeyen aksiyon";

        public const string WhisperUnavailableKey = "whisper_unavailable";
        public const string WhisperRateLimitKey = "whisper_rate_limit";
        public const string WhisperFailedKey = "whisper_failed";
        public const string TranscriptionEmptyKey = "transcription_empty";
        public const string WhisperTimeoutKey = "whisper_timeout";

        public const string AdminOperationRequiresAdminRole = "Bu işlem için Admin yetkisi gereklidir.";

        public const string AdminAiEmptyMessage = "Mesaj boş olamaz.";
        public const string AdminAiUnavailable = "Admin yapay zeka asistanı yapılandırılmamış (Gemini API anahtarı gerekli).";
        public const string AdminAiConfirmEmpty = "Onaylanacak işlem bulunamadı.";
        public const string AdminAiConfirmSuccess = "Onaylanan işlemler uygulandı.";
        public const string AdminAiRateLimit = "admin_ai_rate_limit";
        public const string AdminAiError = "admin_ai_error";
        public const string AdminAiToolLimit = "İşlem çok uzun sürdü (araç çağrı limiti). Lütfen isteği bölün.";
        public const string AdminAiNoReply = "Yanıt oluşturulamadı.";

        // -------- Admin Auth (email/password) --------
        public const string AdminAuthEmailRequired = "Email zorunludur.";
        public const string AdminAuthCredentialsRequired = "Email ve şifre zorunludur.";
        public const string AdminAuthUserNotFound = "Bu email ile kayıtlı admin bulunamadı.";
        public const string AdminAuthInvalidPassword = "Hatalı şifre.";
        public const string AdminAuthInactive = "Admin hesabı pasif durumda.";
        public const string AdminAuthLoginSuccess = "Giriş başarılı.";
        public const string AdminAuthForgotPasswordSent = "Şifre sıfırlama bağlantısı email adresinize gönderildi.";
        public const string AdminAuthForgotPasswordMailFailed = "Şifre sıfırlama maili gönderilemedi.";
        public const string AdminAuthResetTokenRequired = "Token ve yeni şifre zorunludur.";
        public const string AdminAuthResetPasswordTooShort = "Şifre en az 8 karakter olmalıdır.";
        public const string AdminAuthResetTokenInvalid = "Geçersiz veya süresi dolmuş bağlantı.";
        public const string AdminAuthResetPasswordSuccess = "Şifreniz başarıyla güncellendi.";
        public const string AdminAuthRefreshTokenRequired = "Refresh token zorunludur.";
        public const string AdminAuthRefreshTokenInvalid = "Refresh token geçersiz veya süresi dolmuş.";
        public const string AdminAuthLogoutSuccess = "Çıkış yapıldı.";

        // -------- Admin Management --------
        public const string AdminMgmtCreated = "Yeni admin başarıyla oluşturuldu.";
        public const string AdminMgmtDeleted = "Admin silindi.";
        public const string AdminMgmtActivated = "Admin aktive edildi.";
        public const string AdminMgmtDeactivated = "Admin pasifleştirildi.";
        public const string AdminMgmtEmailAlreadyExists = "Bu email adresi zaten kullanılıyor.";
        public const string AdminMgmtCannotModifySelf = "Kendi hesabınızın bu özelliğini değiştiremezsiniz.";
        public const string AdminMgmtCannotDeleteSelf = "Kendi hesabınızı silemezsiniz.";
        public const string AdminMgmtCannotDeleteLast = "Sistemde en az bir admin kalmalı; son admini silemezsiniz.";
        public const string AdminMgmtProfileUpdated = "Profil bilgileri güncellendi.";
        public const string AdminMgmtCurrentPasswordWrong = "Mevcut şifre hatalı.";
        public const string AdminMgmtPasswordChanged = "Şifre başarıyla değiştirildi.";
        public const string AdminMgmtAvatarFileRequired = "Lütfen bir görsel seçin.";

        // -------- Admin Chat View --------
        public const string AdminChatThreadNotFound = "Sohbet bulunamadı.";

        // -------- Audit Log --------
        public const string AuditLogPageInvalid = "Sayfa numarası ve boyutu pozitif olmalıdır.";

        public const string ModerationInappropriateText = "Mesajınız uygunsuz içerik barındırmaktadır. Lütfen küfür, hakaret veya uygunsuz ifadeler kullanmayınız.";
        public const string ModerationInappropriateImage = "Yüklediğiniz görsel uygunsuz içerik barındırmaktadır. Lütfen uygun bir görsel yükleyiniz.";

        public const string SettingUserNotFoundWithUserId = "Kullanıcı bulunamadı. UserId: {0}";

        public const string UploadFileNameEmpty = "Dosya adı boş olamaz.";
        public const string UploadFileNameInvalidChars = "Dosya adı geçersiz karakterler içeriyor.";
        public const string UploadFileExtensionMissing = "Dosya uzantısı eksik.";
        public const string UploadFileExtensionBlocked = "'{0}' uzantılı dosyalar güvenlik sebebiyle yüklenemez.";
        public const string UploadFileExtensionNotSupported = "'{0}' uzantısı desteklenmiyor.";
        public const string UploadFileCategoryNotAllowed = "Bu yükleme tipinde desteklenmeyen bir dosya formatı.";
        public const string UploadFileSizeTooLarge = "Dosya boyutu çok büyük ({0}). Bu kategori için en fazla {1} yüklenebilir.";
        public const string UploadDeclaredMimeNotAllowed = "Beyan edilen dosya tipi ('{0}') bu kategoride kabul edilmiyor.";
        public const string UploadContentMismatchExtension = "Dosya içeriği uzantı ile uyuşmuyor. Lütfen geçerli bir dosya yükleyin.";
        public const string UploadFileTooShortOrCorrupt = "Dosya çok kısa veya bozuk görünüyor.";
        public const string UploadFileReadFailed = "Dosya okunamadı.";

        public const string AppointmentDistanceSuffix = " (Mesafe: {0:0.00} km)";
        public const string EntityLocationNotSet = "{0} konumu ayarlı değil.";
        public const string EntityLocationInvalid = "{0} konumu geçersiz.";

        public const string SmsServiceNotConfigured = "SMS servisi yapılandırılmamış.";
        public const string SmsServiceUnavailable = "SMS servisi şu anda kullanılamıyor.";
        public const string SmsOtpExpiredRequestNew = "Doğrulama kodunun süresi dolmuş. Lütfen yeni kod isteyin.";
        public const string SmsTooManyWrongAttempts = "Çok fazla hatalı deneme yapıldı. Lütfen yeni kod isteyin.";
        public const string SmsInvalidCodeWithRemaining = "Geçersiz doğrulama kodu. {0} deneme hakkınız kaldı.";
        public const string SmsPhoneEmpty = "Telefon numarası boş olamaz.";
        public const string SmsMessageBodyEmpty = "Mesaj içeriği boş olamaz.";
        public const string SmsSendFailedRetry = "SMS gönderilemedi. Lütfen tekrar deneyin.";
        public const string SmsOtpHourlyLimitExceeded = "Bu saat diliminde maksimum {0} kod hakkınızı kullandınız. Yeni saat başında ({1} dakika sonra) tekrar deneyebilirsiniz.";
        public const string SmsOtpResendWaitSeconds = "Çok sık kod istediniz. Lütfen {0} saniye sonra tekrar deneyin.";
        public const string SmsOtpAlreadySentWaitValidity = "Bu numaraya zaten kod gönderildi. Lütfen {0} saniye bekleyip tekrar deneyin.";

        public const string OtpSentSuccess = "OTP gönderildi.";
        public const string SmsSentDevSuccess = "SMS gönderildi (DEV).";
        public const string SmsSentSuccess = "SMS gönderildi.";
        public const string OtpVerifiedSuccess = "Doğrulandı.";

        public const string AuthLoginNoUserForSelectedUserType =
            "Bu telefon numarasıyla seçilen hesap türü için kayıtlı kullanıcı yok. Kayıt için \"Kayıt ol\"u seçin veya hesap türünü değiştirin.";
        public const string AuthLoginSuccess = "Giriş başarılı";
        public const string AuthRefreshTokenAlreadyRevoked = "Refresh token iptal edilmiş.";
        public const string AuthRefreshTokenRevoked = "Refresh token iptal edildi.";

        public const string IapTransactionIdRequired = "transactionId gerekli";
        public const string IapProductIdAndPurchaseTokenRequired = "productId ve purchaseToken gerekli";
        public const string IapInvalidOrUnknownTransaction = "Geçersiz veya bulunamayan işlem";
        public const string IapTransactionPayloadUnreadable = "İşlem bilgisi okunamadı";
        public const string IapBundleIdMissing = "bundleId yok";
        public const string IapProductIdMissing = "productId yok";
        public const string IapPackageIdMismatch = "Paket kimliği uyuşmuyor";
        public const string IapUnknownProductPrefix = "Bilinmeyen ürün: ";
        public const string IapPlanNotCompatibleWithAccount = "Bu plan hesap türünüzle uyumlu değil";
        public const string IapGooglePlayVerificationFailed = "Google Play doğrulaması başarısız";
        public const string IapSubscriptionPaymentIncomplete = "Abonelik ödemesi tamamlanmamış";
        public const string IapServerConfigurationIncomplete = "Sunucu yapılandırması eksik";

        public const string SubscriptionInvalidMonthCount = "Geçersiz ay sayısı";
        public const string SubscriptionInvalidPlan = "Geçersiz plan";
        public const string SubscriptionCheckoutSmsRateLimit = "Çok kısa süre içinde tekrar denediniz. Lütfen {0} saniye bekleyin.";
        public const string SubscriptionSmsSendFailedFallback = "SMS gönderilemedi";
        public const string SubscriptionCheckoutLinkSmsSent = "Ödeme linki SMS olarak gönderildi.";
        public const string SubscriptionCheckoutInvalidLinkTitle = "Geçersiz Link";
        public const string SubscriptionCheckoutInvalidLinkBody = "Bu ödeme linki geçersiz veya yanlış formatlı.";
        public const string SubscriptionCheckoutLinkExpiredTitle = "Link Süresi Doldu";
        public const string SubscriptionCheckoutLinkExpiredBody = "Bu ödeme linkinin süresi dolmuş veya daha önce kullanılmış. Lütfen uygulamadan yeni bir link oluşturun.";
        public const string SubscriptionCheckoutUserNotFoundTitle = "Kullanıcı Bulunamadı";
        public const string SubscriptionCheckoutUserNotFoundBody = "Hesabınız bulunamadı. Lütfen uygulamadan tekrar deneyin.";
        public const string SettingsUpdatedSuccess = "Ayarlar başarıyla güncellendi.";
        public const string SettingsAlreadyExist = "Ayarlar zaten mevcut.";

        public const string ChatApiMessageBodyEmpty = "Mesaj boş olamaz.";
        public const string AiVoiceFileEmpty = "Ses dosyası boş.";
        public const string AiVoiceTranscriptionServiceUnavailable = "Ses çevirme servisi şu anda kullanılamıyor.";

        public const string StoreIdInvalidGuidMessage = "Geçerli mağaza kimliği bulunamadı.";

        public const string FcmTokenRequired = "FCM token is required";
        public const string FcmTokenRegisteredSuccess = "FCM token registered successfully";
        public const string FcmTokenRegistrationFailed = "Failed to register FCM token";
        public const string FcmTokenUnregisteredSuccess = "FCM token unregistered successfully";
        public const string FcmTokenUnregistrationFailed = "Failed to unregister FCM token";

        // ── API rate limiting (Api/Program.cs) ──
        public const string ApiRateLimitTooManyRequests = "Çok fazla istek gönderildi. Lütfen {0} saniye sonra tekrar deneyin.";

        // ── NetGsm exception aspect (const → attribute uyumlu) ──
        public const string NetGsmAspectOtpSendFailed = "OTP gönderilemedi. Lütfen daha sonra tekrar deneyin.";
        public const string NetGsmAspectOtpVerifyFailed = "Doğrulama başarısız. Lütfen tekrar deneyin.";
        public const string NetGsmAspectSmsSendFailed = "SMS gönderilemedi.";

        // ── Subscription reminder push (BackgroundServices) ──
        public const string SubscriptionPushTitle7DaysLeft = "Aboneliğiniz 7 Gün Sonra Sona Eriyor";
        public const string SubscriptionPushBody7DaysLeft = "Aboneliğinizin bitmesine 7 gün kaldı. Devam etmek için yenileyin.";
        public const string SubscriptionPushTitle1DayLeft = "Aboneliğiniz Yarın Sona Eriyor";
        public const string SubscriptionPushBody1DayLeft = "Aboneliğinizin bitmesine 1 gün kaldı. Hizmet kesintisini önlemek için bugün yenileyin.";
        public const string SubscriptionPushTitleExpired = "Aboneliğiniz Sona Erdi";
        public const string SubscriptionPushBodyExpired = "Aboneliğiniz sona erdi. Panel erişiminiz kısıtlandı; yenilemek için ödeme linki isteyin.";

        // ── İşlem başarı mesajları ──
        public const string ChairCreatedSuccess = "Koltuk başarıyla oluşturuldu.";
        public const string ChairUpdatedSuccess = "Koltuk güncellendi.";
        public const string ChairDeletedSuccess = "Koltuk silindi.";
        public const string ChatMessageDeletedSuccess = "Mesaj silindi.";
        public const string ChatMessageEditedSuccess = "Mesaj düzenlendi.";
        public const string ChatThreadDeletedSuccess = "Sohbet silindi.";
        public const string ChatThreadRestoredSuccess = "Sohbet geri alındı.";
        public const string WorkingHoursCreatedSuccess = "Çalışma saatleri başarıyla oluşturuldu.";
        public const string WorkingHoursUpdatedSuccess = "Saatler Güncellendi.";
        public const string ImageUploadedSuccess = "Resim başarıyla yüklendi.";
        public const string ImageMultiUploadedSuccessFormat = "{0} resim başarıyla yüklendi.";
        public const string ImageUpdatedSuccess = "Resim başarıyla güncellendi.";
        public const string UserAddedSuccess = "Kullanıcı Eklendi";
        public const string UserUpdatedSuccess = "Kullanıcı güncellendi";
        public const string UserAccountDeletedSuccess = "Hesabınız başarıyla silindi.";
        public const string UserProfileFetchedSuccess = "Kullanıcı bilgileri getirildi";
        public const string UserProfileUpdatedSuccess = "Profil başarıyla güncellendi";
        public const string UserPhoneUpdatedSuccess = "Telefon numarası başarıyla güncellendi.";
        public const string SettingsDefaultsCreatedSuccess = "Varsayılan ayarlar oluşturuldu.";
        public const string FreeBarberUpdatedShortSuccess = "Serbest berber güncellendi.";
        public const string FreeBarberAvailabilityUpdatedSuccess = "Müsaitlik durumu güncellendi.";
        public const string RequestSubmittedSuccess = "İsteğiniz başarıyla gönderildi.";
        public const string RequestDeletedSuccess = "İstek başarıyla silindi.";
        public const string RequestProcessedSuccess = "İstek işlendi olarak işaretlendi.";

        // ── FluentValidation (WithMessage) ──
        public const string ValidationPhoneRequired = "Telefon numarası zorunludur.";
        public const string ValidationPhoneTurkeyE164 = "Geçerli bir Türkiye cep numarası girin (+90 ile başlayan 10 hane, örn. +905551234567).";
        public const string ValidationLanguageCodeInvalid = "Geçersiz dil kodu.";
        public const string ValidationStartTimeRequired = "Başlangıç saati zorunludur.";
        public const string ValidationEndTimeRequired = "Bitiş saati zorunludur.";
        public const string ValidationAppointmentDateRequired = "Randevu tarihi zorunludur.";
        public const string ValidationStoreSelectionRequired = "Dükkan seçimi zorunludur.";
        public const string ValidationFreeBarberIdNotInBody = "Serbest berber ID'si request body'de gönderilmemelidir.";
        public const string ValidationStoreSelectionTypeNotAllowedHere = "Dükkan seçim tipi bu senaryoda kullanılamaz.";
        public const string ValidationChairNameOrBarberRule = "Koltuk için ya isim ya berber seçmelisiniz; ikisi birden veya ikisi de boş olamaz.";
        public const string ValidationChairBerberIfEmptyName = "İsim boş ise mutlaka bir berber seçmelisiniz.";
        public const string ValidationChairNameEmptyWhenBarber = "Berber seçili ise koltuk ismi boş olmalıdır.";
        public const string ValidationChairSelectionRequired = "Koltuk seçimi zorunludur.";
        public const string ValidationServiceSelectionRequired = "Hizmet seçimi zorunludur.";
        public const string ValidationAtLeastOneServiceSelected = "En az bir hizmet seçilmelidir.";
        public const string ValidationBlockTargetRequired = "Engellenecek kullanıcı seçilmelidir.";
        public const string ValidationBlockReasonMax500 = "Engelleme nedeni 500 karakterden uzun olamaz.";
        public const string ValidationComplaintTargetRequired = "Şikayet edilecek kullanıcı seçilmelidir.";
        public const string ValidationComplaintReasonMax1000 = "Şikayet nedeni 1000 karakterden uzun olamaz.";
        public const string ValidationLatitudeRequired = "Enlem (latitude) zorunludur.";
        public const string ValidationLatitudeRange = "Enlem değeri -90 ile 90 arasında olmalıdır.";
        public const string ValidationLongitudeRequired = "Boylam (longitude) zorunludur.";
        public const string ValidationLongitudeRange = "Boylam değeri -180 ile 180 arasında olmalıdır.";
        public const string ValidationFirstNameRequired = "Ad zorunludur";
        public const string ValidationFirstNameMin2 = "Ad en az 2 karakter olmalıdır";
        public const string ValidationFirstNameMax50 = "Ad en fazla 50 karakter olabilir";
        public const string ValidationLastNameRequired = "Soyad zorunludur";
        public const string ValidationLastNameMin2 = "Soyad en az 2 karakter olmalıdır";
        public const string ValidationLastNameMax50 = "Soyad en fazla 50 karakter olabilir";
        public const string ValidationBusinessTypeRequired = "İşletme türü zorunludur";
        public const string ValidationBusinessTypeInvalid = "Geçerli bir işletme türü seçilmelidir";
        public const string ValidationBusinessTypeInvalidWithPeriod = "Geçerli bir işletme türü seçilmelidir.";
        public const string ValidationServiceListRequired = "Hizmet listesi zorunludur";
        public const string ValidationAtLeastOneServiceOffering = "En az bir hizmet girilmelidir";
        public const string ValidationServiceNameNotEmpty = "Hizmet adı boş olamaz";
        public const string ValidationServicePriceRequired = "Hizmet fiyatı girilmelidir";
        public const string ValidationServicePriceNonNegative = "Hizmet fiyatı 0 veya daha büyük olmalıdır";
        public const string ValidationLatRangeGeneric = "Geçerli bir enlem değeri giriniz (-90..90).";
        public const string ValidationLonRangeGeneric = "Geçerli bir boylam değeri giriniz (-180..180).";
        public const string ValidationStoreNameRequired = "İşletme adı zorunludur.";
        public const string ValidationStoreNameMin2 = "İşletme adı en az 2 karakter olmalıdır.";
        public const string ValidationStoreNameMax100 = "İşletme adı en fazla 100 karakter olabilir.";
        public const string ValidationPricingServiceTypeInvalid = "Geçerli bir koltuk fiyat hizmeti seçilmelidir.";
        public const string ValidationAddressDescriptionRequired = "Adres açıklaması zorunludur.";
        public const string ValidationTaxDocumentRequired = "Vergi levhası resmi zorunludur.";
        public const string ValidationPriceRequired = "Fiyat girilmelidir.";
        public const string ValidationStorePriceNonNegativeCreate = "Fiyat 0'dan veya eşit   olmalıdır.";
        public const string ValidationPercentRequired = "Yüzdelik girilmelidir.";
        public const string ValidationPercentPositive = "Yüzdelik 0'dan büyük olmalıdır.";
        public const string ValidationPercentMax100 = "Yüzdelik 100'ü geçemez.";
        public const string ValidationAtLeastOneChair = "En az bir koltuk eklenmelidir.";
        public const string ValidationChairNameWhenNoBarber = "Berber atanmadıysa koltuk adı zorunludur.";
        public const string ValidationManuelBarberNameRequired = "Manuel berber adı zorunludur.";
        public const string ValidationManuelBarberCountMax30 = "Berber sayısı 30'u geçemez.";
        public const string ValidationChairCountMax30 = "Koltuk sayısı 30'u geçemez.";
        public const string ValidationServiceNamesUnique = "Hizmet adları benzersiz olmalıdır.";
        public const string ValidationWorkingHoursRequired = "Çalışma saatleri zorunludur.";
        public const string ValidationAtLeastOneWorkingDay = "En az bir çalışma günü girilmelidir.";
        public const string ValidationOneWorkingEntryPerDay = "Her gün için tek bir çalışma kaydı olmalıdır.";
        public const string ValidationStartTimeHHmm = "Başlangıç saati HH:mm formatında olmalı.";
        public const string ValidationEndTimeHHmm = "Bitiş saati HH:mm formatında olmalı.";
        public const string ValidationStartBeforeEndTime = "Başlangıç saati bitiş saatinden küçük olmalı.";
        public const string ValidationStorePriceNonNegativeUpdate = "Fiyat 0'dan büyük veya eşit olmalıdır.";
        public const string ValidationManuelBarberFullNameRequired = "Berber adı zorunludur.";
        public const string ValidationStoreIdRequired = "Dükkan kimliği zorunludur.";
        public const string ValidationFreeBarberSelectionRequired = "Serbest berber seçimi zorunludur.";
        public const string ValidationPackageOwnerRequired = "Paket sahibi belirtilmelidir.";
        public const string ValidationPackageIdRequired = "Paket kimliği belirtilmelidir.";
        public const string ValidationPackageNameRequired = "Paket adı zorunludur.";
        public const string ValidationPackageNameMax100 = "Paket adı en fazla 100 karakter olabilir.";
        public const string ValidationPackagePricePositive = "Paket fiyatı 0'dan büyük olmalıdır.";
        public const string ValidationAtLeastOneServiceForPackage = "En az bir hizmet seçilmelidir.";
        public const string ValidationPackageMinOneServiceCreate = "Paket oluşturmak için en az 1 hizmet seçilmelidir.";
        public const string ValidationPackageMinOneServiceUpdate = "Pakette en az 1 hizmet bulunmalıdır.";
        public const string ValidationProfileFirstNameRequired = "İsim zorunludur";
        public const string ValidationProfileFirstNameMin2 = "İsim en az 2 karakter olmalıdır";
        public const string ValidationProfileFirstNameMax20 = "İsim en fazla 20 karakter olabilir";
        public const string ValidationProfileLastNameRequired = "Soyisim zorunludur";
        public const string ValidationProfileLastNameMin2 = "Soyisim en az 2 karakter olmalıdır";
        public const string ValidationProfileLastNameMax20 = "Soyisim en fazla 20 karakter olabilir";
        public const string ValidationProfilePhoneRequired = "Telefon numarası zorunludur";
        public const string ValidationProfilePhoneNotEmpty = "Telefon numarası boş olamaz";
        public const string ValidationProfilePhoneE164Format = "Telefon numarası +90 ile başlamalı ve 13 haneli olmalıdır";
        public const string ValidationRequestTitleNotEmpty = "İstek başlığı boş olamaz.";
        public const string ValidationRequestTitleMax200 = "İstek başlığı 200 karakterden uzun olamaz.";
        public const string ValidationRequestMessageNotEmpty = "İstek mesajı boş olamaz.";
        public const string ValidationRequestMessageMax2000 = "İstek mesajı 2000 karakterden uzun olamaz.";
        public const string ValidationStoreAppointmentNoFreeBarber = "Dükkan randevusunda serbest berber seçilemez.";
        public const string ValidationLocationLatitudeRequired = "Konum bilgisi (latitude) zorunludur.";
        public const string ValidationLocationLongitudeRequired = "Konum bilgisi (longitude) zorunludur.";
        public const string ValidationOtpCodeRequired = "Kod girilmelidir";
        public const string ValidationFirstNameRequiredRegister = "İsim gerekli";
        public const string ValidationLastNameRequiredRegister = "Soyisim gerekli";
        public const string ValidationPanelIdRequired = "Panel ID zorunludur.";
        public const string ValidationStoreSelectionTypoRequired = "Dükkan seç seçilmelidir.";
        public const string ValidationInvalidStoreSelectionType = "Geçersiz dükkan seçim tipi.";
        public const string ValidationAppointmentNoteRequired = "Randevu notu zorunludur.";
        public const string ValidationStoreSelectionNoStoreId = "Dükkan seç senaryosunda storeid gönderilemez.";
        public const string ValidationStoreSelectionNoServices = "Dükkan seç senaryosunda hizmet seçilemez.";
        public const string ValidationCustomRequestNoStore = "İsteğime göre seçeneğinde dükkan seçilemez.";
    }
}

