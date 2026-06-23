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
        private async Task PushAppointmentThreadAsync(Guid appointmentId, PushThreadAction action)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt == null) return;

            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread == null) return;

            if (appt.Status != AppointmentStatus.Pending && appt.Status != AppointmentStatus.Approved)
                return;

            var participants = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var users = await userDal.GetAll(u => participants.Contains(u.Id));
            var userDict = users.ToDictionary(u => u.Id);

            BarberStore? store = null;
            if (appt.BarberStoreUserId.HasValue)
            {
                store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
            }

            var storeDict = new Dictionary<Guid, BarberStore>();
            if (store != null)
            {
                storeDict[store.BarberStoreOwnerId] = store;
            }

            var freeBarberDict = new Dictionary<Guid, FreeBarber>();
            if (appt.FreeBarberUserId.HasValue)
            {
                var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                if (freeBarber != null)
                {
                    freeBarberDict[freeBarber.FreeBarberUserId] = freeBarber;
                }
            }

            var userImageDict = await GetImagesForOwnersAsync(users.Select(u => u.Id).ToList(), ImageOwnerType.User);
            var storeImageDict = await GetImagesForOwnersAsync(storeDict.Values.Select(s => s.Id).ToList(), ImageOwnerType.Store);
            var freeBarberImageDict = await GetImagesForOwnersAsync(freeBarberDict.Values.Select(f => f.Id).ToList(), ImageOwnerType.FreeBarber);

            foreach (var userId in participants)
            {
                // ExceptionHandlingAspect method seviyesinde exception'ları handle eder
                // Burada try-catch'e gerek yok, aspect otomatik handle edecek
                var title = BuildThreadTitleForUser(userId, appt, store?.StoreName);

                    // Participants listesini oluştur - ÖNEMLİ: Sadece kendi userId'miz hariç diğer participant'ları göster
                    var participantsList = new List<ChatThreadParticipantDto>();

                    // Customer participant - sadece kendimiz değilsek ekle
                    if (appt.CustomerUserId.HasValue && appt.CustomerUserId.Value != userId)
                    {
                        if (userDict.TryGetValue(appt.CustomerUserId.Value, out var customerUser))
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (thread.CustomerUserId == appt.CustomerUserId.Value)
                            {
                                userImageDict.TryGetValue(customerUser.Id, out var customerImageUrl);
                                participantsList.Add(new ChatThreadParticipantDto
                                {
                                    UserId = customerUser.Id,
                                    DisplayName = $"{customerUser.FirstName} {customerUser.LastName}",
                                    ImageUrl = customerImageUrl,
                                    UserType = customerUser.UserType,
                                    BarberType = null
                                });
                            }
                        }
                    }

                    // Store participant - sadece kendimiz değilsek ekle
                    if (appt.BarberStoreUserId.HasValue && store != null)
                    {
                        // Store sahibi userId'si ile karşılaştır - ÖNEMLİ: Kendi store bilgilerimizi değil, karşı tarafın bilgilerini göster
                        if (store.BarberStoreOwnerId != userId)
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (thread.StoreOwnerUserId == store.BarberStoreOwnerId ||
                                (thread.StoreOwnerUserId.HasValue && thread.StoreOwnerUserId.Value == store.BarberStoreOwnerId))
                            {
                                storeImageDict.TryGetValue(store.Id, out var storeImageUrl);
                                participantsList.Add(new ChatThreadParticipantDto
                                {
                                    UserId = store.BarberStoreOwnerId,
                                    DisplayName = store.StoreName,
                                    ImageUrl = storeImageUrl,
                                    UserType = UserType.BarberStore,
                                    BarberType = store.Type
                                });
                            }
                        }
                    }

                    // FreeBarber participant - sadece kendimiz değilsek ekle
                    if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId.Value != userId)
                    {
                        if (freeBarberDict.TryGetValue(appt.FreeBarberUserId.Value, out var freeBarberEntity))
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (thread.FreeBarberUserId == freeBarberEntity.FreeBarberUserId ||
                                (thread.FreeBarberUserId.HasValue && thread.FreeBarberUserId.Value == freeBarberEntity.FreeBarberUserId))
                            {
                                freeBarberImageDict.TryGetValue(freeBarberEntity.Id, out var freeBarberImageUrl);
                                participantsList.Add(new ChatThreadParticipantDto
                                {
                                    UserId = appt.FreeBarberUserId.Value,
                                    DisplayName = $"{freeBarberEntity.FirstName} {freeBarberEntity.LastName}",
                                    ImageUrl = freeBarberImageUrl,
                                    UserType = UserType.FreeBarber,
                                    BarberType = freeBarberEntity.Type
                                });
                            }
                        }
                    }

                    // UnreadCount'u thread entity'den al
                    int unreadCount = 0;
                    if (thread.CustomerUserId == userId)
                        unreadCount = thread.CustomerUnreadCount;
                    else if (thread.StoreOwnerUserId == userId)
                        unreadCount = thread.StoreUnreadCount;
                    else if (thread.FreeBarberUserId == userId)
                        unreadCount = thread.FreeBarberUnreadCount;

                    // Mevcut kullanıcının (userId) profil resmini al
                    string? currentUserImageUrlForThisUser = null;
                    if (userDict.TryGetValue(userId, out var currentUserEntity))
                    {
                        if (currentUserEntity.UserType == UserType.Customer)
                        {
                            userImageDict.TryGetValue(currentUserEntity.Id, out currentUserImageUrlForThisUser);
                        }
                        else if (currentUserEntity.UserType == UserType.BarberStore)
                        {
                            if (storeDict.TryGetValue(userId, out var userStore))
                            {
                                storeImageDict.TryGetValue(userStore.Id, out currentUserImageUrlForThisUser);
                            }
                        }
                        else if (currentUserEntity.UserType == UserType.FreeBarber)
                        {
                            if (freeBarberDict.TryGetValue(userId, out var userFreeBarber))
                            {
                                freeBarberImageDict.TryGetValue(userFreeBarber.Id, out currentUserImageUrlForThisUser);
                            }
                        }
                    }

                    var threadDto = new ChatThreadListItemDto
                    {
                        ThreadId = thread.Id,
                        AppointmentId = appt.Id,
                        Status = appt.Status,
                        IsFavoriteThread = false,
                        Title = title,
                        LastMessagePreview = messageEncryption.Decrypt(thread.LastMessagePreview),
                        LastMessageAt = thread.LastMessageAt,
                        UnreadCount = unreadCount,
                        CurrentUserImageUrl = currentUserImageUrlForThisUser,
                        Participants = participantsList
                    };

                    if (action == PushThreadAction.Created)
                        await realtime.PushChatThreadCreatedAsync(userId, threadDto);
                    else
                        await realtime.PushChatThreadUpdatedAsync(userId, threadDto);
            }
        }

        public async Task PushAppointmentThreadCreatedAsync(Guid appointmentId)
        {
            await PushAppointmentThreadAsync(appointmentId, PushThreadAction.Created);
        }

        public async Task PushAppointmentThreadUpdatedAsync(Guid appointmentId)
        {
            await PushAppointmentThreadAsync(appointmentId, PushThreadAction.Updated);
        }

        public async Task PushSocialThreadUpdatedAsync(Guid fromUserId, Guid toUserId, Guid threadId)
        {
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null || !thread.IsSocialThread) return;

            var recipients = new[] { fromUserId, toUserId }.Distinct().ToList();
            foreach (var recipientUserId in recipients)
            {
                var otherUserId = thread.FavoriteFromUserId == recipientUserId
                    ? thread.FavoriteToUserId!.Value
                    : thread.FavoriteFromUserId!.Value;

                var otherUser = await userDal.Get(u => u.Id == otherUserId);
                if (otherUser is null) continue;

                var viewerSideProfileId = await ResolveViewerProfileIdForThreadAsync(thread, recipientUserId);
                var otherProfileId = ResolvePeerProfileId(thread, viewerSideProfileId);

                var (displayName, imageUrl, barberType, socialProfileId) = otherProfileId.HasValue
                    ? await ResolveSocialParticipantInfoByProfileIdAsync(otherProfileId.Value, otherUser)
                    : await ResolveSocialParticipantInfoAsync(otherUser);

                var recipientUser = await userDal.Get(u => u.Id == recipientUserId);
                var currentUserImageUrlForRecipient = viewerSideProfileId.HasValue
                    ? await ResolveSocialAvatarForProfileIdAsync(viewerSideProfileId.Value)
                    : recipientUser != null
                        ? await ResolveSocialAvatarForUserAsync(recipientUser)
                        : null;

                var unreadCount = 0;
                if (thread.CustomerUserId == recipientUserId)
                    unreadCount = thread.CustomerUnreadCount;
                else if (thread.StoreOwnerUserId == recipientUserId)
                    unreadCount = thread.StoreUnreadCount;
                else if (thread.FreeBarberUserId == recipientUserId)
                    unreadCount = thread.FreeBarberUnreadCount;

                var latestVisible = await messageDal.GetLatestVisibleMessagePerThreadAsync(recipientUserId, new[] { threadId });
                string? preview = null;
                DateTime? lastAt = thread.LastMessageAt;
                if (latestVisible.TryGetValue(threadId, out var lastMsg))
                {
                    lastAt = lastMsg.CreatedAt;
                    preview = BuildThreadListLastPreviewPlain(lastMsg);
                }

                var threadDto = new ChatThreadListItemDto
                {
                    ThreadId = thread.Id,
                    AppointmentId = null,
                    Status = null,
                    IsFavoriteThread = false,
                    IsSocialThread = true,
                    Title = displayName,
                    LastMessagePreview = preview,
                    LastMessageAt = lastAt,
                    UnreadCount = unreadCount,
                    CurrentUserImageUrl = currentUserImageUrlForRecipient,
                    ViewerSocialProfileId = viewerSideProfileId,
                    IsRestrictedForCurrentUser = false,
                    Participants = new List<ChatThreadParticipantDto>
                    {
                        new()
                        {
                            UserId = otherUser.Id,
                            DisplayName = displayName,
                            ImageUrl = imageUrl,
                            UserType = otherUser.UserType,
                            BarberType = barberType,
                            SocialProfileId = socialProfileId,
                        },
                    },
                };

                await realtime.PushChatThreadUpdatedAsync(recipientUserId, threadDto);
            }
        }

        public async Task PushFavoriteThreadUpdatedAsync(Guid fromUserId, Guid toUserId, Guid threadId)
        {
            // Favori thread güncellendiğinde (mesaj gönderildiğinde) her iki kullanıcıya da thread güncellemesini push et
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread == null) return;
            if (thread.IsSocialThread) return;

            // Favori aktif mi kontrol et
            bool isFavoriteActive = false;

            // 1. fromUserId -> toUserId yönünde
            var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (favorite1 != null && favorite1.IsActive)
            {
                isFavoriteActive = true;
            }
            else
            {
                // Store ID kontrolü: toUserId bir Store Owner ID olabilir
                var store1 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == toUserId);
                if (store1 != null)
                {
                    var favorite1Store = await favoriteDal.Get(x => x.FavoritedFromId == fromUserId && x.FavoritedToId == store1.Id && x.IsActive);
                    if (favorite1Store != null)
                        isFavoriteActive = true;
                }
            }

            // 2. toUserId -> fromUserId yönünde
            if (!isFavoriteActive)
            {
                var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                if (favorite2 != null && favorite2.IsActive)
                {
                    isFavoriteActive = true;
                }
                else
                {
                    // Store ID kontrolü: fromUserId bir Store Owner ID olabilir
                    var store2 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == fromUserId);
                    if (store2 != null)
                    {
                        var favorite2Store = await favoriteDal.Get(x => x.FavoritedFromId == toUserId && x.FavoritedToId == store2.Id && x.IsActive);
                        if (favorite2Store != null)
                            isFavoriteActive = true;
                    }
                }
            }

            // Eğer hiçbir tarafın favori aktif değilse thread gönderme
            if (!isFavoriteActive)
            {
                return;
            }

            // Performance: Her iki kullanıcı için de thread detaylarını al ve SignalR ile gönder
            var recipients = new[] { fromUserId, toUserId }.Distinct().ToList();

            // Performance: Parallel processing için Task.WhenAll kullanılabilir ama şimdilik sequential
            foreach (var recipientUserId in recipients)
            {
                // ExceptionHandlingAspect method seviyesinde exception'ları handle eder
                // Burada try-catch'e gerek yok, aspect otomatik handle edecek
                string displayName = "";
                string? imageUrl = null;
                BarberType? barberType = null;
                Guid participantUserId = Guid.Empty;
                UserType participantUserType;

                // REVIZE: Thread bilgisinde dükkan/serbest berber panel bilgileri gösterilsin
                // Store bazlı favoriler için: En az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                // FreeBarber için: Panel bilgileri gösterilsin
                var otherUserId = thread.FavoriteFromUserId == recipientUserId
                    ? thread.FavoriteToUserId!.Value
                    : thread.FavoriteFromUserId!.Value;

                var otherUser = await userDal.Get(u => u.Id == otherUserId);
                if (otherUser == null) continue;

                participantUserId = otherUser.Id;
                participantUserType = otherUser.UserType; // ÖNEMLİ: participantUserType'ı burada set et
                Guid? favoriteStoreId = null;

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
                    // recipientUserId'nin otherUserId'nin store'larından birini favoriye eklemiş olabilir
                    BarberStore? store = null;
                    if (thread.FavoriteContextStoreId.HasValue)
                        store = await barberStoreDal.Get(x => x.Id == thread.FavoriteContextStoreId.Value && x.BarberStoreOwnerId == otherUserId);
                    if (store == null)
                    {
                        var favoriteStores = await favoriteDal.GetAll(x => x.FavoritedFromId == recipientUserId && x.IsActive);
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
                        favoriteStoreId = store.Id;
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
                            favoriteStoreId = firstStore.Id;
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

                // Participant bilgilerini kontrol et - geçerli değilse atla
                if (participantUserId == recipientUserId || participantUserId == Guid.Empty || string.IsNullOrEmpty(displayName))
                {
                    continue; // Geçersiz participant bilgisi
                }

                // UnreadCount'u thread entity'den al
                int unreadCount = 0;
                if (thread.CustomerUserId == recipientUserId)
                    unreadCount = thread.CustomerUnreadCount;
                else if (thread.StoreOwnerUserId == recipientUserId)
                    unreadCount = thread.StoreUnreadCount;
                else if (thread.FreeBarberUserId == recipientUserId)
                    unreadCount = thread.FreeBarberUnreadCount;

                // Mevcut kullanıcının (recipientUserId) profil resmini al
                string? currentUserImageUrlForRecipient = null;
                var recipientUser = await userDal.Get(u => u.Id == recipientUserId);
                if (recipientUser != null)
                {
                    if (recipientUser.UserType == UserType.Customer)
                    {
                        var userImg = await imageDal.GetLatestImageAsync(recipientUser.Id, ImageOwnerType.User);
                        currentUserImageUrlForRecipient = userImg?.ImageUrl;
                    }
                    else if (recipientUser.UserType == UserType.BarberStore)
                    {
                        var userStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == recipientUserId);
                        if (userStore != null)
                        {
                            var storeImg = await imageDal.GetLatestImageAsync(userStore.Id, ImageOwnerType.Store);
                            currentUserImageUrlForRecipient = storeImg?.ImageUrl;
                        }
                    }
                    else if (recipientUser.UserType == UserType.FreeBarber)
                    {
                        var userFreeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == recipientUserId);
                        if (userFreeBarber != null)
                        {
                            var freeBarberImg = await imageDal.GetLatestImageAsync(userFreeBarber.Id, ImageOwnerType.FreeBarber);
                            currentUserImageUrlForRecipient = freeBarberImg?.ImageUrl;
                        }
                    }
                }

                // Alıcının karşı tarafa aktif favorisi var mı? (kısıtlı mı?)
                bool isRestrictedForRecipient = !await HasActiveFavoriteFromUserAsync(recipientUserId, otherUserId);

                var threadDto = new ChatThreadListItemDto
                {
                    ThreadId = thread.Id,
                    AppointmentId = null,
                    Status = null,
                    IsFavoriteThread = true,
                    Title = displayName,
                    LastMessagePreview = isRestrictedForRecipient ? null : DecryptOptionalPreview(thread.LastMessagePreview),
                    LastMessageAt = thread.LastMessageAt,
                    UnreadCount = unreadCount,
                    CurrentUserImageUrl = currentUserImageUrlForRecipient,
                    IsRestrictedForCurrentUser = isRestrictedForRecipient,
                    FavoriteStoreId = favoriteStoreId,
                    Participants = new List<ChatThreadParticipantDto>
                    {
                        new ChatThreadParticipantDto
                        {
                            UserId = participantUserId,
                            DisplayName = displayName,
                            ImageUrl = imageUrl,
                            UserType = participantUserType,
                            BarberType = barberType
                        }
                    }
                };

                // ThreadUpdated gönder
                await realtime.PushChatThreadUpdatedAsync(recipientUserId, threadDto);
            }
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<bool>> NotifyTypingAsync(Guid userId, Guid threadId, bool isTyping)
        {
            // Thread'i kontrol et
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread == null) return new ErrorDataResult<bool>(Messages.ChatNotFound);

            // Katılımcı kontrolü
            bool isParticipant = false;
            string? userName = null;

            if (thread.AppointmentId.HasValue)
            {
                // Randevu thread'i
                var appt = await appointmentDal.Get(x => x.Id == thread.AppointmentId.Value);
                if (appt == null) return new ErrorDataResult<bool>(Messages.AppointmentNotFound);

                isParticipant = thread.CustomerUserId == userId ||
                               thread.StoreOwnerUserId == userId ||
                               thread.FreeBarberUserId == userId;
            }
            else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                // Favori veya sosyal DM thread'i
                isParticipant = thread.FavoriteFromUserId == userId || thread.FavoriteToUserId == userId;
            }

            if (!isParticipant) return new ErrorDataResult<bool>(Messages.NotAParticipant);

            // Kullanıcı adını al — sosyal thread'de sosyal profil adı
            var user = await userDal.Get(u => u.Id == userId);
            if (user != null)
            {
                if (thread.IsSocialThread)
                {
                    var (socialName, _, _, _) = await ResolveSocialParticipantInfoAsync(user);
                    userName = socialName;
                }
                else if (user.UserType == UserType.Customer)
                {
                    userName = $"{user.FirstName} {user.LastName}";
                }
                else if (user.UserType == UserType.BarberStore)
                {
                    var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == userId);
                    userName = store?.StoreName ?? Messages.BarberDefaultName;
                }
                else if (user.UserType == UserType.FreeBarber)
                {
                    var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
                    userName = freeBarber != null ? $"{freeBarber.FirstName} {freeBarber.LastName}" : Messages.FreeBarberDefaultName;
                }
            }

            // Thread'deki diğer katılımcılara typing event'i gönder
            var participants = new List<Guid>();

            if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                if (thread.FavoriteFromUserId != userId) participants.Add(thread.FavoriteFromUserId.Value);
                if (thread.FavoriteToUserId != userId) participants.Add(thread.FavoriteToUserId.Value);
            }

            if (thread.CustomerUserId.HasValue && thread.CustomerUserId != userId)
                participants.Add(thread.CustomerUserId.Value);
            if (thread.StoreOwnerUserId.HasValue && thread.StoreOwnerUserId != userId)
                participants.Add(thread.StoreOwnerUserId.Value);
            if (thread.FreeBarberUserId.HasValue && thread.FreeBarberUserId != userId)
                participants.Add(thread.FreeBarberUserId.Value);

            foreach (var participantId in participants.Distinct())
            {
                await realtime.PushChatTypingAsync(participantId, threadId, userId, userName ?? Messages.UserDefaultName, isTyping);
            }

            return new SuccessDataResult<bool>(true);
        }
    }
}
