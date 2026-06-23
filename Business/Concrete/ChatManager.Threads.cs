using Business.Abstract;
using Business.BusinessAspect.Autofac;

using Business.Helpers;
using Business.Resources;
using Core.Aspect.Autofac.ExceptionHandling;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Business.Concrete
{
    public partial class ChatManager
    {


        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        // Read-only query - no transaction needed
        public async Task<IDataResult<List<ChatThreadListItemDto>>> GetThreadsAsync(Guid userId, DateTime? beforeUtc = null, Guid? beforeId = null, int? limit = null)
        {
            // sadece Pending + Approved randevular için
            var allowed = new[] { AppointmentStatus.Pending, AppointmentStatus.Approved };

            var threads = await threadDal.GetThreadsForUserAsync(userId, allowed, beforeUtc, beforeId, limit);

            if (threads.Count == 0)
                return new SuccessDataResult<List<ChatThreadListItemDto>>(threads);

            // Randevu ve favori thread'leri ayır (sosyal DM'ler ayrı endpoint'te)
            var appointmentThreads = threads.Where(t => !t.IsFavoriteThread && t.AppointmentId.HasValue).ToList();
            var favoriteThreads = threads.Where(t => t.IsFavoriteThread).ToList();

            var result = new List<ChatThreadListItemDto>();

            // Mevcut kullanıcının UserType'ını al
            var currentUser = await userDal.Get(u => u.Id == userId);
            if (currentUser == null)
                return new ErrorDataResult<List<ChatThreadListItemDto>>(Messages.UserNotFound);
            var currentUserType = currentUser.UserType;

            // Mevcut kullanıcının profil resmini al
            string? currentUserImageUrl = null;
            if (currentUserType == UserType.Customer)
            {
                // Customer için User image
                if (currentUser.ImageId.HasValue)
                {
                    var userImg = await imageDal.GetLatestImageAsync(currentUser.Id, ImageOwnerType.User);
                    currentUserImageUrl = userImg?.ImageUrl;
                }
            }
            else if (currentUserType == UserType.BarberStore)
            {
                // BarberStore için Store image
                var userStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == userId);
                if (userStore != null)
                {
                    var storeImg = await imageDal.GetLatestImageAsync(userStore.Id, ImageOwnerType.Store);
                    currentUserImageUrl = storeImg?.ImageUrl;
                }
            }
            else if (currentUserType == UserType.FreeBarber)
            {
                // FreeBarber için FreeBarber image
                var userFreeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
                if (userFreeBarber != null)
                {
                    var freeBarberImg = await imageDal.GetLatestImageAsync(userFreeBarber.Id, ImageOwnerType.FreeBarber);
                    currentUserImageUrl = freeBarberImg?.ImageUrl;
                }
            }

            // Randevu thread'leri için işlem
            if (appointmentThreads.Any())
            {
                // Performance: HashSet kullanarak daha hızlı Contains kontrolü
                var appointmentIds = new HashSet<Guid>(appointmentThreads.Select(t => t.AppointmentId!.Value));
                var appointments = await appointmentDal.GetAll(x => appointmentIds.Contains(x.Id));
                var apptDict = appointments.ToDictionary(a => a.Id);

                // Thread entity'lerini getir (participant bilgileri için)
                var threadEntities = await threadDal.GetAll(t => t.AppointmentId.HasValue && appointmentIds.Contains(t.AppointmentId.Value));
                // GroupBy kullanarak duplicate AppointmentId'leri handle et (her AppointmentId için en son thread'i al)
                var threadDict = threadEntities
                    .GroupBy(t => t.AppointmentId!.Value)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.CreatedAt).First());

                // Tüm katılımcı ID'leri topla
                var participantIds = new HashSet<Guid>();
                foreach (var appt in appointments)
                {
                    if (appt.CustomerUserId.HasValue) participantIds.Add(appt.CustomerUserId.Value);
                    if (appt.BarberStoreUserId.HasValue) participantIds.Add(appt.BarberStoreUserId.Value);
                    if (appt.FreeBarberUserId.HasValue) participantIds.Add(appt.FreeBarberUserId.Value);
                }

                // Kullanıcı bilgilerini batch olarak çek
                var users = await userDal.GetAll(u => participantIds.Contains(u.Id));
                var userDict = users.ToDictionary(u => u.Id);

                // Performance: HashSet kullanarak daha hızlı lookup
                var storeOwnerIds = appointments
                    .Where(a => a.BarberStoreUserId.HasValue)
                    .Select(a => a.BarberStoreUserId!.Value)
                    .Distinct()
                    .ToHashSet();
                var stores = storeOwnerIds.Count > 0
                    ? await barberStoreDal.GetAll(x => storeOwnerIds.Contains(x.BarberStoreOwnerId))
                    : new List<BarberStore>();
                // GroupBy kullanarak duplicate BarberStoreOwnerId'leri handle et (her owner için en son store'u al)
                var storeDict = stores
                    .GroupBy(s => s.BarberStoreOwnerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

                // Performance: HashSet kullanarak daha hızlı lookup
                var freeBarberIds = appointments
                    .Where(a => a.FreeBarberUserId.HasValue)
                    .Select(a => a.FreeBarberUserId!.Value)
                    .Distinct()
                    .ToHashSet();
                var freeBarbers = freeBarberIds.Count > 0
                    ? await freeBarberDal.GetAll(x => freeBarberIds.Contains(x.FreeBarberUserId))
                    : new List<FreeBarber>();
                // GroupBy kullanarak duplicate FreeBarberUserId'leri handle et (her user için en son freeBarber'ı al)
                var freeBarberDict = freeBarbers
                    .GroupBy(fb => fb.FreeBarberUserId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(fb => fb.CreatedAt).First());

                // Performance: HashSet kullanarak daha hızlı Contains kontrolü
                var userImageIds = users
                    .Where(u => u.ImageId.HasValue)
                    .Select(u => u.ImageId!.Value)
                    .Distinct()
                    .ToHashSet();
                var storeImageOwnerIds = stores.Select(s => s.Id).ToHashSet();
                var freeBarberImageOwnerIds = freeBarbers.Select(fb => fb.Id).ToHashSet();

                var userImages = userImageIds.Count > 0
                    ? await imageDal.GetAll(i => userImageIds.Contains(i.Id) && i.OwnerType == ImageOwnerType.User)
                    : new List<Image>();
                var storeImages = storeImageOwnerIds.Count > 0
                    ? await imageDal.GetAll(i => storeImageOwnerIds.Contains(i.ImageOwnerId) && i.OwnerType == ImageOwnerType.Store)
                    : new List<Image>();
                var freeBarberImages = freeBarberImageOwnerIds.Count > 0
                    ? await imageDal.GetAll(i => freeBarberImageOwnerIds.Contains(i.ImageOwnerId) && i.OwnerType == ImageOwnerType.FreeBarber)
                    : new List<Image>();

                // GroupBy kullanarak duplicate ImageId'leri handle et (her image için en son olanı al)
                var userImageDict = userImages
                    .GroupBy(i => i.Id)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First());
                var storeImageDict = storeImages
                    .GroupBy(i => i.ImageOwnerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);
                var freeBarberImageDict = freeBarberImages
                    .GroupBy(i => i.ImageOwnerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);

                // Her randevu thread'i için işlem
                foreach (var threadDto in appointmentThreads)
                {
                    if (!apptDict.TryGetValue(threadDto.AppointmentId!.Value, out var appt)) continue;
                    if (!threadDict.TryGetValue(threadDto.AppointmentId.Value, out var threadEntity)) continue;

                    // ÖNEMLİ: Appointment thread görünürlüğü kontrolü
                    // Thread görünür olması için iki koşuldan biri sağlanmalı:
                    // 1. Status Pending veya Approved ise görünür
                    // 2. Status Pending/Approved değilse, en az bir aktif favori varsa görünür (favori thread mantığı gibi)
                    bool isStatusVisible = appt.Status == AppointmentStatus.Pending || appt.Status == AppointmentStatus.Approved;
                    bool isFavoriteVisible = false;

                    // Store bilgisini al (favori kontrolü için)
                    storeDict.TryGetValue(appt.BarberStoreUserId ?? Guid.Empty, out var store);

                    // Status uygun değilse favori kontrolü yap
                    if (!isStatusVisible)
                    {
                        // Katılımcıları belirle (kendimiz hariç)
                        var threadParticipantIds = new List<Guid>();
                        if (appt.CustomerUserId.HasValue && appt.CustomerUserId.Value != userId)
                            threadParticipantIds.Add(appt.CustomerUserId.Value);
                        if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId.Value != userId)
                            threadParticipantIds.Add(appt.FreeBarberUserId.Value);
                        if (appt.BarberStoreUserId.HasValue && appt.BarberStoreUserId.Value != userId)
                            threadParticipantIds.Add(appt.BarberStoreUserId.Value);

                        // Store bazlı favori kontrolü (eğer store varsa)
                        if (store != null && store.BarberStoreOwnerId != userId)
                        {
                            var favoriteStore = await favoriteDal.Get(x =>
                                x.FavoritedFromId == userId &&
                                x.FavoritedToId == store.Id &&
                                x.IsActive);
                            if (favoriteStore != null)
                            {
                                isFavoriteVisible = true;
                            }
                        }

                        // Performance: User bazlı favori kontrolü - batch olarak çek
                        if (!isFavoriteVisible && threadParticipantIds.Any())
                        {
                            // Tüm participant ID'leri için favorileri batch olarak çek
                            var participantIdsList = threadParticipantIds.ToList();
                            var favorites1 = await favoriteDal.GetAll(x =>
                                x.FavoritedFromId == userId &&
                                participantIdsList.Contains(x.FavoritedToId) &&
                                x.IsActive);
                            var favorites2 = await favoriteDal.GetAll(x =>
                                participantIdsList.Contains(x.FavoritedFromId) &&
                                x.FavoritedToId == userId &&
                                x.IsActive);

                            if (favorites1.Any() || favorites2.Any())
                            {
                                isFavoriteVisible = true;
                            }
                        }
                    }

                    // Thread görünür değilse atla
                    if (!isStatusVisible && !isFavoriteVisible)
                    {
                        continue;
                    }
                    threadDto.Title = BuildThreadTitleForUser(userId, appt, store?.StoreName);

                    // Status'u appointment'tan güncelle (threadDto.Status zaten appointment'tan geliyor ama güncel olması için)
                    threadDto.Status = appt.Status;

                    // Participants listesini doldur - ÖNEMLİ: Sadece kendi userId'miz hariç diğer participant'ları göster
                    threadDto.Participants = new List<ChatThreadParticipantDto>();

                    // Customer - sadece kendimiz değilsek ekle
                    if (appt.CustomerUserId.HasValue && appt.CustomerUserId.Value != userId)
                    {
                        if (userDict.TryGetValue(appt.CustomerUserId.Value, out var customer))
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (threadEntity.CustomerUserId == appt.CustomerUserId.Value)
                            {
                                var imageUrl = customer.ImageId.HasValue && userImageDict.TryGetValue(customer.ImageId.Value, out var img) ? img.ImageUrl : null;
                                threadDto.Participants.Add(new ChatThreadParticipantDto
                                {
                                    UserId = customer.Id,
                                    DisplayName = $"{customer.FirstName} {customer.LastName}",
                                    ImageUrl = imageUrl,
                                    UserType = customer.UserType,
                                    BarberType = null
                                });
                            }
                        }
                    }

                    // Store - sadece kendimiz değilsek ekle
                    if (appt.BarberStoreUserId.HasValue && store != null)
                    {
                        // Store sahibi userId'si ile karşılaştır
                        if (store.BarberStoreOwnerId != userId)
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (threadEntity.StoreOwnerUserId == store.BarberStoreOwnerId ||
                                (threadEntity.StoreOwnerUserId.HasValue && threadEntity.StoreOwnerUserId.Value == store.BarberStoreOwnerId))
                            {
                                var imageUrl = storeImageDict.TryGetValue(store.Id, out var imgUrl) ? imgUrl : null;
                                if (userDict.TryGetValue(store.BarberStoreOwnerId, out var storeOwner))
                                {
                                    threadDto.Participants.Add(new ChatThreadParticipantDto
                                    {
                                        UserId = storeOwner.Id,
                                        DisplayName = store.StoreName,
                                        ImageUrl = imageUrl,
                                        UserType = UserType.BarberStore,
                                        BarberType = store.Type
                                    });
                                }
                            }
                        }
                    }

                    // FreeBarber - sadece kendimiz değilsek ekle
                    if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId.Value != userId)
                    {
                        var freeBarber = freeBarbers.FirstOrDefault(fb => fb.FreeBarberUserId == appt.FreeBarberUserId.Value);
                        if (freeBarber != null)
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (threadEntity.FreeBarberUserId == freeBarber.FreeBarberUserId ||
                                (threadEntity.FreeBarberUserId.HasValue && threadEntity.FreeBarberUserId.Value == freeBarber.FreeBarberUserId))
                            {
                                var imageUrl = freeBarberImageDict.TryGetValue(freeBarber.Id, out var imgUrl) ? imgUrl : null;
                                if (userDict.TryGetValue(freeBarber.FreeBarberUserId, out var fbUser))
                                {
                                    threadDto.Participants.Add(new ChatThreadParticipantDto
                                    {
                                        UserId = fbUser.Id,
                                        DisplayName = $"{freeBarber.FirstName} {freeBarber.LastName}",
                                        ImageUrl = imageUrl,
                                        UserType = UserType.FreeBarber,
                                        BarberType = freeBarber.Type
                                    });
                                }
                            }
                        }
                    }

                    // Mevcut kullanıcının profil resmini ekle
                    threadDto.CurrentUserImageUrl = currentUserImageUrl;

                    result.Add(threadDto);
                }
            }

            // Favori thread'leri için işlem
            if (favoriteThreads.Any())
            {
                var favoriteThreadEntities = await threadDal.GetFavoriteThreadsForUserAsync(userId);
                var favoriteDict = favoriteThreadEntities.ToDictionary(t => t.Id);

                // Aktif favorileri kontrol et
                var activeFavoriteThreads = new List<ChatThreadListItemDto>();
                foreach (var threadDto in favoriteThreads)
                {
                    // Thread entity'sini bul (ThreadId ile)
                    if (!favoriteDict.TryGetValue(threadDto.ThreadId, out var threadEntity))
                        continue;

                    // REVIZE: Favori aktif mi kontrol et - en az bir tarafın favori olması yeterli
                    // Artık User ID bazlı kontrol yapılır (Store bazlı thread'ler için de)
                    bool isFavoriteActive = false;
                    var fromUserId = threadEntity.FavoriteFromUserId!.Value;
                    var toUserId = threadEntity.FavoriteToUserId!.Value;

                    // 1. User ID bazlı favori kontrolü (her iki yönde)
                    var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
                    if (favorite1 != null && favorite1.IsActive)
                    {
                        isFavoriteActive = true;
                    }

                    if (!isFavoriteActive)
                    {
                        var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                        if (favorite2 != null && favorite2.IsActive)
                        {
                            isFavoriteActive = true;
                        }
                    }

                    // Performance: Store bazlı favoriler için kontrol - batch olarak çek
                    if (!isFavoriteActive)
                    {
                        var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
                        if (stores.Any())
                        {
                            var storeIds = stores.Select(s => s.Id).ToList();
                            var favoriteStores = await favoriteDal.GetAll(x =>
                                x.FavoritedFromId == fromUserId &&
                                storeIds.Contains(x.FavoritedToId) &&
                                x.IsActive);
                            if (favoriteStores.Any())
                            {
                                isFavoriteActive = true;
                            }
                        }
                    }

                    // Performance: toUserId'nin fromUserId'nin store'larından birini favoriye eklemiş olabilir - batch olarak çek
                    if (!isFavoriteActive)
                    {
                        var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == fromUserId);
                        if (stores.Any())
                        {
                            var storeIds = stores.Select(s => s.Id).ToList();
                            var favoriteStores = await favoriteDal.GetAll(x =>
                                x.FavoritedFromId == toUserId &&
                                storeIds.Contains(x.FavoritedToId) &&
                                x.IsActive);
                            if (favoriteStores.Any())
                            {
                                isFavoriteActive = true;
                            }
                        }
                    }

                    // En az bir tarafın favori olması yeterli (aktif olmalı)
                    if (!isFavoriteActive) continue; // Hiçbiri aktif değilse thread'i atla

                    string displayName = "";
                    string? imageUrl = null;
                    BarberType? barberType = null;
                    Guid participantUserId = Guid.Empty;
                    UserType participantUserType;

                    // REVIZE: Thread bilgisinde dükkan/serbest berber panel bilgileri gösterilsin
                    // Store bazlı favoriler için: En az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                    // FreeBarber için: Panel bilgileri gösterilsin
                    var otherUserId = threadEntity.FavoriteFromUserId == userId
                        ? threadEntity.FavoriteToUserId!.Value
                        : threadEntity.FavoriteFromUserId!.Value;

                    // Diğer kullanıcının bilgilerini çek
                    var otherUser = await userDal.Get(u => u.Id == otherUserId);
                    if (otherUser == null) continue;

                    participantUserId = otherUser.Id;
                    participantUserType = otherUser.UserType;

                    if (otherUser.UserType == UserType.Customer)
                    {
                        displayName = $"{otherUser.FirstName} {otherUser.LastName}";
                        if (otherUser.ImageId.HasValue)
                        {
                            var img = await imageDal.GetLatestImageAsync(otherUser.Id, ImageOwnerType.User);
                            imageUrl = img?.ImageUrl;
                        }
                        barberType = null;
                    }
                    else if (otherUser.UserType == UserType.BarberStore)
                    {
                        // REVIZE: Store sahibi için - en az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                        // userId'nin otherUserId'nin store'larından birini favoriye eklemiş olabilir
                        BarberStore? store = null;
                        if (threadEntity.FavoriteContextStoreId.HasValue)
                            store = await barberStoreDal.Get(x => x.Id == threadEntity.FavoriteContextStoreId.Value && x.BarberStoreOwnerId == otherUserId);
                        if (store == null)
                        {
                            var favoriteStores = await favoriteDal.GetAll(x => x.FavoritedFromId == userId && x.IsActive);
                            var storeIds = favoriteStores.Select(f => f.FavoritedToId).ToList();
                            var stores = await barberStoreDal.GetAll(x => storeIds.Contains(x.Id) && x.BarberStoreOwnerId == otherUserId);
                            store = stores.FirstOrDefault();
                        }
                        if (store != null)
                        {
                            displayName = store.StoreName;
                            barberType = store.Type;
                            var img = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                            imageUrl = img?.ImageUrl;
                            threadDto.FavoriteStoreId = store.Id;
                        }
                        else
                        {
                            // Favori store yoksa, ilk store'u göster (fallback)
                            var firstStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUserId);
                            if (firstStore != null)
                            {
                                displayName = firstStore.StoreName;
                                barberType = firstStore.Type;
                                var img = await imageDal.GetLatestImageAsync(firstStore.Id, ImageOwnerType.Store);
                                imageUrl = img?.ImageUrl;
                                threadDto.FavoriteStoreId = firstStore.Id;
                            }
                        }
                    }
                    else if (otherUser.UserType == UserType.FreeBarber)
                    {
                        // REVIZE: FreeBarber için panel bilgileri gösterilsin
                        var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUserId);
                        if (freeBarber != null)
                        {
                            displayName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                            barberType = freeBarber.Type;
                            var img = await imageDal.GetLatestImageAsync(freeBarber.Id, ImageOwnerType.FreeBarber);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    else
                    {
                        continue; // Beklenmeyen durum
                    }

                    threadDto.Title = displayName;

                    // Participant bilgilerini ayarla
                    // ÖNEMLİ: participantUserId kendi userId'miz değil, diğer kullanıcının userId'si olmalı
                    // participantUserType set edilmemişse veya participantUserId geçersizse atla
                    if (participantUserId == userId || participantUserId == Guid.Empty || string.IsNullOrEmpty(displayName))
                    {
                        // Kendi bilgilerimiz participant olarak eklenmiş veya geçersiz - bu yanlış, atla
                        continue;
                    }

                    threadDto.Participants = new List<ChatThreadParticipantDto>
                    {
                        new ChatThreadParticipantDto
                        {
                            UserId = participantUserId,
                            DisplayName = displayName,
                            ImageUrl = imageUrl,
                            UserType = participantUserType,
                            BarberType = barberType
                        }
                    };

                    // Mevcut kullanıcının profil resmini ekle
                    threadDto.CurrentUserImageUrl = currentUserImageUrl;

                    // Kısıtlı kullanıcı kontrolü: mevcut kullanıcının karşı tarafa aktif favorisi var mı?
                    threadDto.IsRestrictedForCurrentUser = !await HasActiveFavoriteFromUserAsync(userId, otherUserId);

                    activeFavoriteThreads.Add(threadDto);
                }

                result.AddRange(activeFavoriteThreads);
            }

            // Thread satırı: ChatThread.LastMessage* tüm kullanıcılar için tek kayıt; kullanıcı bazlı silinen son mesaj için
            // bu kullanıcının gördüğü son mesaja göre önizleme ve tarih (refresh sonrası eski metin dönmesin).
            var threadIdsForPreview = result.Select(r => r.ThreadId).Distinct().ToList();
            var latestVisibleByThread = await messageDal.GetLatestVisibleMessagePerThreadAsync(userId, threadIdsForPreview);
            foreach (var t in result)
            {
                if (latestVisibleByThread.TryGetValue(t.ThreadId, out var lastMsg))
                {
                    t.LastMessageAt = lastMsg.CreatedAt;
                    t.LastMessagePreview = BuildThreadListLastPreviewPlain(lastMsg);
                }
                else
                {
                    t.LastMessageAt = null;
                    t.LastMessagePreview = null;
                }
            }

            // Son mesaj zamanına göre sırala
            result = result.OrderByDescending(t => t.LastMessageAt ?? DateTime.MinValue).ToList();

            // Favori thread'de karşıyı favorilemeyen kullanıcıya içerik yok
            foreach (var t in result)
            {
                if (t.IsFavoriteThread && t.IsRestrictedForCurrentUser)
                    t.LastMessagePreview = null;
            }

            return new SuccessDataResult<List<ChatThreadListItemDto>>(result);
        }

        /// <summary>Thread listesi için düz metin önizleme (şifreli metni çözer; medya tipleri için sabit etiket).</summary>
        private string? BuildThreadListLastPreviewPlain(ChatMessageItemDto msg)
        {
            var mt = (ChatMessageType)msg.MessageType;
            if (mt == ChatMessageType.Text)
            {
                var plain = messageEncryption.Decrypt(msg.Text);
                if (string.IsNullOrEmpty(plain)) return "";
                return plain.Length > 60 ? plain[..60] : plain;
            }

            var caption = messageEncryption.Decrypt(msg.Text);
            if (!string.IsNullOrWhiteSpace(caption))
                return caption.Length > 60 ? caption[..60] : caption;

            return mt switch
            {
                ChatMessageType.Image => "Fotoğraf",
                ChatMessageType.Location => "Konum paylaşıldı",
                ChatMessageType.File => string.IsNullOrWhiteSpace(msg.FileName) ? "Dosya" : msg.FileName!,
                ChatMessageType.Audio => "Ses mesajı",
                _ => ""
            };
        }

        private static string BuildThreadTitleForUser(Guid userId, Appointment appt, string? storeName)
        {
            if (appt.BarberStoreUserId == userId)
            {
                // store owner kendi listesinde karşı taraf
                return appt.CustomerUserId.HasValue ? Messages.ChatThreadTitleCustomer : Messages.ChatThreadTitleFreeBarber;
            }

            // customer/freebarber tarafı store'u görsün
            return string.IsNullOrWhiteSpace(storeName) ? Messages.ChatThreadTitleBarberStore : storeName!;
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<ChatThreadListItemDto>>> GetAllThreadsForAdminAsync()
        {
            var threads = await threadDal.GetAll();
            if (threads.Count == 0)
                return new SuccessDataResult<List<ChatThreadListItemDto>>(new List<ChatThreadListItemDto>());

            var appointmentIds = threads
                .Where(t => t.AppointmentId.HasValue)
                .Select(t => t.AppointmentId!.Value)
                .Distinct()
                .ToList();

            var appointments = appointmentIds.Count == 0
                ? new List<Appointment>()
                : await appointmentDal.GetAll(a => appointmentIds.Contains(a.Id));

            var apptDict = appointments.ToDictionary(a => a.Id, a => a);

            // ── Katılımcıların foto + isim + tip bilgisini batch olarak çöz ──
            var participantUserIds = new HashSet<Guid>();
            foreach (var t in threads)
            {
                if (t.AppointmentId.HasValue)
                {
                    if (t.CustomerUserId.HasValue) participantUserIds.Add(t.CustomerUserId.Value);
                    if (t.StoreOwnerUserId.HasValue) participantUserIds.Add(t.StoreOwnerUserId.Value);
                    if (t.FreeBarberUserId.HasValue) participantUserIds.Add(t.FreeBarberUserId.Value);
                }
                else
                {
                    if (t.FavoriteFromUserId.HasValue) participantUserIds.Add(t.FavoriteFromUserId.Value);
                    if (t.FavoriteToUserId.HasValue) participantUserIds.Add(t.FavoriteToUserId.Value);
                }
            }

            var pUsers = participantUserIds.Count > 0
                ? await userDal.GetAll(u => participantUserIds.Contains(u.Id))
                : new List<User>();
            var pUserDict = pUsers.ToDictionary(u => u.Id, u => u);

            var storeOwnerIds = pUsers.Where(u => u.UserType == UserType.BarberStore).Select(u => u.Id).ToHashSet();
            var pStores = storeOwnerIds.Count > 0
                ? await barberStoreDal.GetAll(s => storeOwnerIds.Contains(s.BarberStoreOwnerId))
                : new List<BarberStore>();
            var storeById = pStores.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            var storeByOwner = pStores.GroupBy(s => s.BarberStoreOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.CreatedAt).First());

            var freeBarberOwnerIds = pUsers.Where(u => u.UserType == UserType.FreeBarber).Select(u => u.Id).ToHashSet();
            var pFreeBarbers = freeBarberOwnerIds.Count > 0
                ? await freeBarberDal.GetAll(fb => freeBarberOwnerIds.Contains(fb.FreeBarberUserId))
                : new List<FreeBarber>();
            var freeBarberByOwner = pFreeBarbers.GroupBy(fb => fb.FreeBarberUserId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(fb => fb.CreatedAt).First());

            var pUserImageIds = pUsers.Where(u => u.ImageId.HasValue).Select(u => u.ImageId!.Value).ToHashSet();
            var pUserImages = pUserImageIds.Count > 0
                ? await imageDal.GetAll(i => pUserImageIds.Contains(i.Id) && i.OwnerType == ImageOwnerType.User)
                : new List<Image>();
            var pUserImageDict = pUserImages.GroupBy(i => i.Id)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);

            var pStoreIds = pStores.Select(s => s.Id).ToHashSet();
            var pStoreImages = pStoreIds.Count > 0
                ? await imageDal.GetAll(i => pStoreIds.Contains(i.ImageOwnerId) && i.OwnerType == ImageOwnerType.Store)
                : new List<Image>();
            var pStoreImageDict = pStoreImages.GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.CreatedAt).First().ImageUrl);

            var pFbIds = pFreeBarbers.Select(fb => fb.Id).ToHashSet();
            var pFbImages = pFbIds.Count > 0
                ? await imageDal.GetAll(i => pFbIds.Contains(i.ImageOwnerId) && i.OwnerType == ImageOwnerType.FreeBarber)
                : new List<Image>();
            var pFbImageDict = pFbImages.GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.CreatedAt).First().ImageUrl);

            string? ProfileImg(User u) =>
                u.ImageId.HasValue && pUserImageDict.TryGetValue(u.ImageId.Value, out var pi) ? pi : null;

            ChatThreadParticipantDto? BuildParticipant(Guid userId, Guid? contextStoreId)
            {
                if (!pUserDict.TryGetValue(userId, out var u)) return null;
                var dto = new ChatThreadParticipantDto { UserId = userId, UserType = u.UserType };
                switch (u.UserType)
                {
                    case UserType.BarberStore:
                        BarberStore? store = null;
                        if (contextStoreId.HasValue) storeById.TryGetValue(contextStoreId.Value, out store);
                        if (store == null) storeByOwner.TryGetValue(userId, out store);
                        dto.DisplayName = store?.StoreName ?? $"{u.FirstName} {u.LastName}".Trim();
                        dto.ImageUrl = store != null && pStoreImageDict.TryGetValue(store.Id, out var simg)
                            ? simg : ProfileImg(u);
                        dto.BarberType = store?.Type;
                        break;
                    case UserType.FreeBarber:
                        freeBarberByOwner.TryGetValue(userId, out var fb);
                        dto.DisplayName = fb != null ? $"{fb.FirstName} {fb.LastName}".Trim() : $"{u.FirstName} {u.LastName}".Trim();
                        dto.ImageUrl = fb != null && pFbImageDict.TryGetValue(fb.Id, out var fimg)
                            ? fimg : ProfileImg(u);
                        dto.BarberType = fb?.Type;
                        break;
                    default:
                        dto.DisplayName = $"{u.FirstName} {u.LastName}".Trim();
                        dto.ImageUrl = ProfileImg(u);
                        break;
                }
                if (string.IsNullOrWhiteSpace(dto.DisplayName)) dto.DisplayName = "Kullanıcı";
                return dto;
            }

            var result = threads
                .OrderByDescending(t => t.LastMessageAt ?? DateTime.MinValue)
                .Select(t =>
                {
                    var isFavorite = !t.AppointmentId.HasValue;
                    AppointmentStatus? status = null;
                    if (!isFavorite && apptDict.TryGetValue(t.AppointmentId!.Value, out var appt))
                        status = appt.Status;

                    var ctxStoreId = t.StoreId ?? t.FavoriteContextStoreId;
                    var ids = new List<Guid>();
                    if (isFavorite)
                    {
                        if (t.FavoriteFromUserId.HasValue) ids.Add(t.FavoriteFromUserId.Value);
                        if (t.FavoriteToUserId.HasValue) ids.Add(t.FavoriteToUserId.Value);
                    }
                    else
                    {
                        if (t.CustomerUserId.HasValue) ids.Add(t.CustomerUserId.Value);
                        if (t.StoreOwnerUserId.HasValue) ids.Add(t.StoreOwnerUserId.Value);
                        if (t.FreeBarberUserId.HasValue) ids.Add(t.FreeBarberUserId.Value);
                    }

                    var participants = ids
                        .Select(id => BuildParticipant(id, ctxStoreId))
                        .Where(p => p != null)
                        .Select(p => p!)
                        .ToList();

                    var title = participants.Count > 0
                        ? string.Join(" ↔ ", participants.Select(p => p.DisplayName))
                        : (isFavorite ? "Favori Thread" : "Randevu Thread");

                    return new ChatThreadListItemDto
                    {
                        ThreadId = t.Id,
                        AppointmentId = t.AppointmentId,
                        Status = status,
                        IsFavoriteThread = isFavorite,
                        Title = title,
                        LastMessagePreview = t.LastMessagePreview,
                        LastMessageAt = t.LastMessageAt,
                        UnreadCount = t.CustomerUnreadCount + t.StoreUnreadCount + t.FreeBarberUnreadCount,
                        FavoriteStoreId = t.StoreId,
                        Participants = participants,
                    };
                })
                .ToList();

            // UI ekranı için decrypt gerekli olabilir; mevcut alanı null/boş ise atlarız.
            foreach (var dto in result)
            {
                if (!string.IsNullOrEmpty(dto.LastMessagePreview))
                    dto.LastMessagePreview = messageEncryption.Decrypt(dto.LastMessagePreview);
            }

            return new SuccessDataResult<List<ChatThreadListItemDto>>(result);
        }

    }
}
