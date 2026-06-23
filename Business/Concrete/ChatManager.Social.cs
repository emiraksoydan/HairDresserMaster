using Business.BusinessAspect.Autofac;

using Business.Resources;

using Core.Aspect.Autofac.Logging;

using Core.Aspect.Autofac.Transaction;

using Core.Utilities.Results;

using Entities.Concrete.Constants;

using Entities.Concrete.Dto;

using Entities.Concrete.Entities;

using Entities.Concrete.Enums;



namespace Business.Concrete

{

    public partial class ChatManager

    {

        [SecuredOperation("Customer,FreeBarber,BarberStore")]

        [LogAspect]

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]

        public async Task<IDataResult<Guid>> EnsureSocialThreadAsync(Guid fromUserId, Guid fromProfileId, Guid toProfileId)

        {

            if (fromProfileId == toProfileId)

                return new ErrorDataResult<Guid>("Kendinizle sohbet başlatılamaz.");



            var fromProfile = await socialProfileDal.Get(p => p.Id == fromProfileId);

            if (fromProfile == null || fromProfile.UserId != fromUserId || fromProfile.Status != SocialContentStatus.Active)

                return new ErrorDataResult<Guid>(SocialErrorCodes.ProfileInvalid);



            var toProfile = await socialProfileDal.Get(p => p.Id == toProfileId);

            if (toProfile == null || toProfile.Status != SocialContentStatus.Active)

                return new ErrorDataResult<Guid>(SocialErrorCodes.ProfileInvalid);



            var toUserId = toProfile.UserId;

            if (fromUserId == toUserId)

                return new ErrorDataResult<Guid>("Kendinizle sohbet başlatılamaz.");



            if (await blockedHelper.HasBlockBetweenAsync(fromUserId, toUserId))

                return new ErrorDataResult<Guid>("Bu kullanıcıyla mesajlaşma kullanılamıyor.");



            var (profileLow, profileHigh) = CanonicalSocialProfilePair(fromProfileId, toProfileId);



            var existing = await threadDal.GetSocialThreadByProfilePairAsync(profileLow, profileHigh);

            if (existing != null)

            {

                if (IsThreadHiddenForUser(existing, fromUserId))

                {

                    SetThreadHiddenForUser(existing, fromUserId, false);

                    await threadDal.Update(existing);

                }

                return new SuccessDataResult<Guid>(existing.Id);

            }



            var legacy = await threadDal.GetSocialThreadAsync(fromUserId, toUserId);

            if (legacy != null &&

                legacy.SocialProfileLowId == null &&

                legacy.SocialProfileHighId == null)

            {

                legacy.SocialProfileLowId = profileLow;

                legacy.SocialProfileHighId = profileHigh;

                legacy.UpdatedAt = DateTime.UtcNow;

                await threadDal.Update(legacy);

                if (IsThreadHiddenForUser(legacy, fromUserId))

                {

                    SetThreadHiddenForUser(legacy, fromUserId, false);

                    await threadDal.Update(legacy);

                }

                return new SuccessDataResult<Guid>(legacy.Id);

            }



            if (toProfile.DmPolicy == SocialDmPolicy.FollowersOnly)

            {

                if (!await socialFollowDal.CanDmUserAsync(new List<Guid> { fromProfileId }, toProfile.Id))

                    return new ErrorDataResult<Guid>(SocialErrorCodes.DmFollowersOnly);

            }



            var toUser = await userDal.Get(u => u.Id == toUserId);

            var fromUser = await userDal.Get(u => u.Id == fromUserId);

            if (fromUser == null || toUser == null)

                return new ErrorDataResult<Guid>(Messages.UserNotFound);



            var now = DateTime.UtcNow;

            var thread = new ChatThread

            {

                Id = Guid.NewGuid(),

                AppointmentId = null,

                FavoriteFromUserId = fromUserId,

                FavoriteToUserId = toUserId,

                IsSocialThread = true,

                SocialProfileLowId = profileLow,

                SocialProfileHighId = profileHigh,

                CreatedAt = now,

                UpdatedAt = now,

            };



            MapUsersToThreadSlots(thread, fromUser);

            MapUsersToThreadSlots(thread, toUser);



            await threadDal.Add(thread);

            return new SuccessDataResult<Guid>(thread.Id);

        }



        [SecuredOperation("Customer,FreeBarber,BarberStore")]

        [LogAspect]

        public async Task<IDataResult<List<ChatThreadListItemDto>>> GetSocialThreadsAsync(

            Guid userId,

            Guid? viewerProfileId = null,

            DateTime? beforeUtc = null,

            Guid? beforeId = null,

            int? limit = null)

        {

            return await GetSocialThreadsInternalAsync(userId, viewerProfileId, beforeUtc, beforeId, limit, hiddenOnly: false);

        }



        [SecuredOperation("Customer,FreeBarber,BarberStore")]

        [LogAspect]

        public async Task<IDataResult<List<ChatThreadListItemDto>>> GetDeletedSocialThreadsAsync(

            Guid userId,

            Guid? viewerProfileId = null,

            DateTime? beforeUtc = null,

            Guid? beforeId = null,

            int? limit = null)

        {

            return await GetSocialThreadsInternalAsync(userId, viewerProfileId, beforeUtc, beforeId, limit, hiddenOnly: true);

        }



        [SecuredOperation("Customer,FreeBarber,BarberStore")]

        [LogAspect]

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]

        public async Task<IResult> RestoreSocialThreadAsync(Guid userId, Guid threadId)

        {

            var thread = await threadDal.Get(t => t.Id == threadId);

            if (thread is null || !thread.IsSocialThread)

                return new ErrorResult(Messages.ChatNotFound);



            var isParticipant =

                thread.FavoriteFromUserId == userId ||

                thread.FavoriteToUserId == userId ||

                thread.CustomerUserId == userId ||

                thread.StoreOwnerUserId == userId ||

                thread.FreeBarberUserId == userId;



            if (!isParticipant)

                return new ErrorResult(Messages.NotAParticipant);



            if (!IsThreadHiddenForUser(thread, userId) &&

                !(thread.LastMessageAt.HasValue && !await HasVisibleMessagesAsync(userId, threadId)))

                return new ErrorResult("Bu sohbet silinmiş görünmüyor.");



            SetThreadHiddenForUser(thread, userId, false);

            thread.UpdatedAt = DateTime.UtcNow;

            await threadDal.Update(thread);

            await messageDal.RemoveUserDeletionsForThreadAsync(threadId, userId);



            var viewerProfileId = await ResolveViewerProfileIdForThreadAsync(thread, userId);

            var restored = await GetSocialThreadsAsync(userId, viewerProfileId, limit: 100);

            var dto = restored.Data?.FirstOrDefault(t => t.ThreadId == threadId);

            if (dto != null)

            {

                try { await realtime.PushChatThreadUpdatedAsync(userId, dto); } catch { }

            }



            await auditService.RecordAsync(AuditAction.ChatThreadHiddenForUser, userId, threadId, null, false);



            return new SuccessResult(Messages.ChatThreadRestoredSuccess);

        }



        private async Task<IDataResult<List<ChatThreadListItemDto>>> GetSocialThreadsInternalAsync(

            Guid userId,

            Guid? viewerProfileId,

            DateTime? beforeUtc,

            Guid? beforeId,

            int? limit,

            bool hiddenOnly)

        {

            if (viewerProfileId.HasValue)

            {

                var owned = await socialProfileDal.GetByUserIdAsync(userId);

                if (!owned.Any(p => p.Id == viewerProfileId.Value))

                    return new ErrorDataResult<List<ChatThreadListItemDto>>(SocialErrorCodes.ProfileInvalid);

            }



            var threads = await threadDal.GetSocialThreadsForUserAsync(

                userId, viewerProfileId, beforeUtc, beforeId, limit, hiddenOnly: hiddenOnly);



            if (viewerProfileId.HasValue)

                threads = await FilterLegacySocialThreadsForProfileAsync(userId, viewerProfileId.Value, threads);



            if (threads.Count == 0)

                return new SuccessDataResult<List<ChatThreadListItemDto>>(threads);



            return await EnrichSocialThreadsAsync(userId, viewerProfileId, threads, hiddenOnly);

        }



        private async Task<List<ChatThreadListItemDto>> FilterLegacySocialThreadsForProfileAsync(

            Guid userId,

            Guid viewerProfileId,

            List<ChatThreadListItemDto> threads)

        {

            var legacyThreadIds = threads

                .Select(t => t.ThreadId)

                .ToList();



            if (legacyThreadIds.Count == 0)

                return threads;



            var entities = await threadDal.GetAll(t => legacyThreadIds.Contains(t.Id));

            var legacyIds = entities

                .Where(t => t.SocialProfileLowId == null && t.SocialProfileHighId == null)

                .Select(t => t.Id)

                .ToHashSet();



            if (legacyIds.Count == 0)

                return threads;



            var legacyProfileId = await GetLegacySocialInboxProfileIdAsync(userId);

            return threads

                .Where(t => !legacyIds.Contains(t.ThreadId) || viewerProfileId == legacyProfileId)

                .ToList();

        }



        private async Task<Guid?> GetLegacySocialInboxProfileIdAsync(Guid userId)

        {

            var profiles = await socialProfileDal.GetByUserIdAsync(userId);

            return profiles

                .OrderBy(p => p.CreatedAt)

                .Select(p => p.Id)

                .FirstOrDefault();

        }



        private async Task<IDataResult<List<ChatThreadListItemDto>>> EnrichSocialThreadsAsync(

            Guid userId,

            Guid? viewerProfileId,

            List<ChatThreadListItemDto> socialThreads,

            bool hiddenOnly)

        {

            if (socialThreads.Count == 0)

                return new SuccessDataResult<List<ChatThreadListItemDto>>(socialThreads);



            var result = new List<ChatThreadListItemDto>();

            var socialEntities = await threadDal.GetAll(t => socialThreads.Select(s => s.ThreadId).Contains(t.Id));

            var socialDict = socialEntities.ToDictionary(t => t.Id);



            var currentUser = await userDal.Get(u => u.Id == userId);

            if (currentUser == null)

                return new ErrorDataResult<List<ChatThreadListItemDto>>(Messages.UserNotFound);



            foreach (var threadDto in socialThreads)

            {

                if (!socialDict.TryGetValue(threadDto.ThreadId, out var threadEntity))

                    continue;



                var viewerSideProfileId = viewerProfileId ?? await ResolveViewerProfileIdForThreadAsync(threadEntity, userId);

                threadDto.ViewerSocialProfileId = viewerSideProfileId;



                string? currentUserImageUrl = null;

                if (viewerSideProfileId.HasValue)

                    currentUserImageUrl = await ResolveSocialAvatarForProfileIdAsync(viewerSideProfileId.Value);

                if (currentUserImageUrl == null)

                    currentUserImageUrl = await ResolveSocialAvatarForUserAsync(currentUser);



                var otherProfileId = ResolvePeerProfileId(threadEntity, viewerSideProfileId);

                var otherUserId = threadEntity.FavoriteFromUserId == userId

                    ? threadEntity.FavoriteToUserId!.Value

                    : threadEntity.FavoriteFromUserId!.Value;



                var otherUser = await userDal.Get(u => u.Id == otherUserId);

                if (otherUser == null) continue;



                var (displayName, imageUrl, barberType, socialProfileId) = otherProfileId.HasValue

                    ? await ResolveSocialParticipantInfoByProfileIdAsync(otherProfileId.Value, otherUser)

                    : await ResolveSocialParticipantInfoAsync(otherUser);



                threadDto.Title = displayName;

                threadDto.Participants = new List<ChatThreadParticipantDto>

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

                };

                threadDto.CurrentUserImageUrl = currentUserImageUrl;

                threadDto.IsRestrictedForCurrentUser = false;

                result.Add(threadDto);

            }



            var threadIdsForPreview = result.Select(r => r.ThreadId).Distinct().ToList();

            var latestVisibleByThread = await messageDal.GetLatestVisibleMessagePerThreadAsync(userId, threadIdsForPreview);

            foreach (var t in result)

            {

                if (latestVisibleByThread.TryGetValue(t.ThreadId, out var lastMsg))

                {

                    t.LastMessageAt = lastMsg.CreatedAt;

                    t.LastMessagePreview = BuildThreadListLastPreviewPlain(lastMsg);

                }

                else if (!hiddenOnly)

                {

                    t.LastMessageAt = null;

                    t.LastMessagePreview = null;

                }

            }



            var filtered = hiddenOnly

                ? result.Where(t =>

                {

                    if (!socialDict.TryGetValue(t.ThreadId, out var entity)) return false;

                    return IsThreadHiddenForUser(entity, userId) || IsSocialThreadLegacyHidden(entity, t);

                }).ToList()

                : result.Where(t =>

                {

                    if (!socialDict.TryGetValue(t.ThreadId, out var entity)) return false;

                    if (IsThreadHiddenForUser(entity, userId)) return false;

                    if (IsSocialThreadLegacyHidden(entity, t)) return false;

                    return true;

                }).ToList();



            filtered = filtered.OrderByDescending(t => t.LastMessageAt ?? DateTime.MinValue).ToList();

            return new SuccessDataResult<List<ChatThreadListItemDto>>(filtered);

        }



        private static (Guid low, Guid high) CanonicalSocialProfilePair(Guid profileIdA, Guid profileIdB)

        {

            return profileIdA.CompareTo(profileIdB) < 0

                ? (profileIdA, profileIdB)

                : (profileIdB, profileIdA);

        }



        private static Guid? ResolvePeerProfileId(ChatThread thread, Guid? viewerProfileId)

        {

            if (!thread.SocialProfileLowId.HasValue || !thread.SocialProfileHighId.HasValue)

                return null;



            if (viewerProfileId.HasValue)

            {

                if (thread.SocialProfileLowId == viewerProfileId)

                    return thread.SocialProfileHighId;

                if (thread.SocialProfileHighId == viewerProfileId)

                    return thread.SocialProfileLowId;

            }



            return thread.SocialProfileHighId;

        }



        private async Task<Guid?> ResolveViewerProfileIdForThreadAsync(ChatThread thread, Guid userId)
        {
            if (!thread.SocialProfileLowId.HasValue || !thread.SocialProfileHighId.HasValue)
                return null;

            var profiles = await socialProfileDal.GetByUserIdAsync(userId);
            var owned = profiles.Select(p => p.Id).ToHashSet();
            if (owned.Contains(thread.SocialProfileLowId.Value))
                return thread.SocialProfileLowId;
            if (owned.Contains(thread.SocialProfileHighId.Value))
                return thread.SocialProfileHighId;
            return null;
        }



        private async Task<bool> HasVisibleMessagesAsync(Guid userId, Guid threadId)

        {

            var visible = await messageDal.GetLatestVisibleMessagePerThreadAsync(userId, new[] { threadId });

            return visible.ContainsKey(threadId);

        }



        private static bool IsSocialThreadLegacyHidden(ChatThread entity, ChatThreadListItemDto thread)

        {

            return entity.LastMessageAt.HasValue && thread.LastMessageAt == null;

        }



        private async Task<(string displayName, string? imageUrl, BarberType? barberType, Guid? socialProfileId)> ResolveSocialParticipantInfoByProfileIdAsync(

            Guid profileId,

            User fallbackUser)

        {

            var profile = await socialProfileDal.Get(p => p.Id == profileId);

            if (profile == null)

                return await ResolveSocialParticipantInfoAsync(fallbackUser);



            string? avatar = null;

            if (profile.AvatarImageId.HasValue)

            {

                var img = await imageDal.Get(i => i.Id == profile.AvatarImageId.Value);

                avatar = img?.ImageUrl;

            }



            var displayName = !string.IsNullOrWhiteSpace(profile.Username)

                ? $"@{profile.Username.Trim()}"

                : $"{fallbackUser.FirstName} {fallbackUser.LastName}".Trim();



            BarberType? barberType = null;

            if (profile.OwnerType == SocialProfileOwnerType.BarberStore)

            {

                var store = await barberStoreDal.Get(x => x.Id == profile.OwnerId);

                barberType = store?.Type;

            }

            else if (profile.OwnerType == SocialProfileOwnerType.FreeBarber)

            {

                var freeBarber = await freeBarberDal.Get(x => x.Id == profile.OwnerId);

                barberType = freeBarber?.Type;

            }



            return (displayName, avatar, barberType, profile.Id);

        }



        private async Task<(string displayName, string? imageUrl, BarberType? barberType, Guid? socialProfileId)> ResolveSocialParticipantInfoAsync(User otherUser)

        {

            var profile = await ResolveSocialProfileForUserAsync(otherUser);

            if (profile != null)

            {

                string? avatar = null;

                if (profile.AvatarImageId.HasValue)

                {

                    var img = await imageDal.Get(i => i.Id == profile.AvatarImageId.Value);

                    avatar = img?.ImageUrl;

                }



                var displayName = !string.IsNullOrWhiteSpace(profile.Username)

                    ? $"@{profile.Username.Trim()}"

                    : $"{otherUser.FirstName} {otherUser.LastName}".Trim();



                BarberType? barberType = null;

                if (otherUser.UserType == UserType.BarberStore)

                {

                    var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUser.Id);

                    barberType = store?.Type;

                }

                else if (otherUser.UserType == UserType.FreeBarber)

                {

                    var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUser.Id);

                    barberType = freeBarber?.Type;

                }



                return (displayName, avatar, barberType, profile.Id);

            }



            return ($"{otherUser.FirstName} {otherUser.LastName}".Trim(), null, null, null);

        }



        private async Task<SocialProfile?> ResolveSocialProfileForUserAsync(User user)

        {

            if (user.UserType == UserType.Customer)

                return await socialProfileDal.GetByOwnerAsync(SocialProfileOwnerType.Customer, user.Id);



            if (user.UserType == UserType.BarberStore)

            {

                var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == user.Id);

                return store != null

                    ? await socialProfileDal.GetByOwnerAsync(SocialProfileOwnerType.BarberStore, store.Id)

                    : null;

            }



            if (user.UserType == UserType.FreeBarber)

            {

                var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == user.Id);

                return freeBarber != null

                    ? await socialProfileDal.GetByOwnerAsync(SocialProfileOwnerType.FreeBarber, freeBarber.Id)

                    : null;

            }



            return null;

        }



        private async Task<string?> ResolveSocialAvatarForProfileIdAsync(Guid profileId)

        {

            var profile = await socialProfileDal.Get(p => p.Id == profileId);

            if (profile?.AvatarImageId == null) return null;

            var img = await imageDal.Get(i => i.Id == profile.AvatarImageId.Value);

            return img?.ImageUrl;

        }



        private async Task<string?> ResolveSocialAvatarForUserAsync(User user)

        {

            var profile = await ResolveSocialProfileForUserAsync(user);

            if (profile?.AvatarImageId == null) return null;

            var img = await imageDal.Get(i => i.Id == profile.AvatarImageId.Value);

            return img?.ImageUrl;

        }



        private static void SetThreadHiddenForUser(ChatThread thread, Guid userId, bool hidden)

        {

            if (thread.CustomerUserId == userId)

                thread.IsDeletedByCustomerUserId = hidden;

            else if (thread.StoreOwnerUserId == userId)

                thread.IsDeletedByStoreOwnerUserId = hidden;

            else if (thread.FreeBarberUserId == userId)

                thread.IsDeletedByFreeBarberUserId = hidden;

        }



        private static bool IsThreadHiddenForUser(ChatThread thread, Guid userId)

        {

            if (thread.CustomerUserId == userId) return thread.IsDeletedByCustomerUserId;

            if (thread.StoreOwnerUserId == userId) return thread.IsDeletedByStoreOwnerUserId;

            if (thread.FreeBarberUserId == userId) return thread.IsDeletedByFreeBarberUserId;

            return false;

        }



        private static void MapUsersToThreadSlots(ChatThread thread, User user)

        {

            if (user.UserType == UserType.Customer && thread.CustomerUserId != user.Id)

                thread.CustomerUserId = user.Id;

            else if (user.UserType == UserType.BarberStore && thread.StoreOwnerUserId != user.Id)

                thread.StoreOwnerUserId = user.Id;

            else if (user.UserType == UserType.FreeBarber && thread.FreeBarberUserId != user.Id)

                thread.FreeBarberUserId = user.Id;

        }

    }

}


