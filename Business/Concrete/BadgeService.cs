using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Core.Aspect.Autofac.Logging;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Concrete
{
    /// <summary>
    /// Centralized Badge Count Management Service
    ///
    /// Responsibilities:
    /// 1. Calculate total notification unread count
    /// 2. Calculate total chat unread count
    /// 3. Calculate per-thread unread counts
    /// 4. Push badge updates via SignalR when counts change
    ///
    /// Performance Optimizations:
    /// - Single query for all counts
    /// - Cached results (short TTL)
    /// - Batch updates for multiple users
    /// </summary>
    public class BadgeService(
        INotificationDal notificationDal,
        IChatThreadDal threadDal,
        IFavoriteDal favoriteDal,
        IBarberStoreDal barberStoreDal,
        ISocialProfileDal socialProfileDal,
        IRealTimePublisher realtime)
    {
        /// <summary>
        /// Gets all badge counts for a user in a single query
        /// </summary>
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<BadgeCountDto> GetBadgeCountsAsync(Guid userId)
        {
            // Notification unread count
            var notificationUnreadCount = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);

            // Sadece kullanıcının görebildiği thread'leri badge'e dahil et
            var allowed = new[] { AppointmentStatus.Pending, AppointmentStatus.Approved };
            var threads = await threadDal.GetThreadsForUserAsync(userId, allowed);

            var visibleFavoriteThreadIds = new HashSet<Guid>();
            var favoriteThreadIds = threads
                .Where(t => t.IsFavoriteThread)
                .Select(t => t.ThreadId)
                .Distinct()
                .ToList();
            if (favoriteThreadIds.Count > 0)
            {
                var favoriteEntities = await threadDal.GetAll(t => favoriteThreadIds.Contains(t.Id));

                // Tüm ilgili user ID'lerini topla — tek batch sorguda yükle.
                var allUserIds = favoriteEntities
                    .Where(t => t.FavoriteFromUserId.HasValue && t.FavoriteToUserId.HasValue)
                    .SelectMany(t => new[] { t.FavoriteFromUserId!.Value, t.FavoriteToUserId!.Value })
                    .Distinct()
                    .ToList();

                if (allUserIds.Count > 0)
                {
                    // Batch 1: user↔user direkt favorileri
                    var directFavorites = await favoriteDal.GetAll(f =>
                        allUserIds.Contains(f.FavoritedFromId) &&
                        allUserIds.Contains(f.FavoritedToId) &&
                        f.IsActive);
                    var directFavSet = new HashSet<(Guid, Guid)>(
                        directFavorites.Select(f => (f.FavoritedFromId, f.FavoritedToId)));

                    // Batch 2: ilgili kullanıcıların tüm dükkanları
                    var stores = await barberStoreDal.GetAll(s => allUserIds.Contains(s.BarberStoreOwnerId));
                    var ownerToStoreIds = stores
                        .GroupBy(s => s.BarberStoreOwnerId)
                        .ToDictionary(g => g.Key, g => g.Select(s => s.Id).ToHashSet());

                    // Batch 3: dükkan favorileri
                    var allStoreIds = stores.Select(s => s.Id).ToList();
                    HashSet<(Guid, Guid)> storeFavSet;
                    if (allStoreIds.Count > 0)
                    {
                        var storeFavorites = await favoriteDal.GetAll(f =>
                            allUserIds.Contains(f.FavoritedFromId) &&
                            allStoreIds.Contains(f.FavoritedToId) &&
                            f.IsActive);
                        storeFavSet = new HashSet<(Guid, Guid)>(
                            storeFavorites.Select(f => (f.FavoritedFromId, f.FavoritedToId)));
                    }
                    else
                    {
                        storeFavSet = new HashSet<(Guid, Guid)>();
                    }

                    foreach (var thread in favoriteEntities)
                    {
                        if (!thread.FavoriteFromUserId.HasValue || !thread.FavoriteToUserId.HasValue)
                            continue;
                        var from = thread.FavoriteFromUserId.Value;
                        var to = thread.FavoriteToUserId.Value;
                        var isActive =
                            CheckActiveFavoriteInMemory(from, to, directFavSet, ownerToStoreIds, storeFavSet) ||
                            CheckActiveFavoriteInMemory(to, from, directFavSet, ownerToStoreIds, storeFavSet);
                        if (isActive)
                            visibleFavoriteThreadIds.Add(thread.Id);
                    }
                }
            }

            // Normal mod chat (randevu + favori) — sosyal DM'ler ayrı sayılır
            var chatUnreadCount = 0;
            var threadUnreadCounts = new Dictionary<Guid, int>();

            foreach (var thread in threads)
            {
                if (thread.IsSocialThread)
                    continue;
                if (thread.IsFavoriteThread && !visibleFavoriteThreadIds.Contains(thread.ThreadId))
                    continue;
                var unreadCount = thread.UnreadCount;
                if (unreadCount > 0)
                {
                    chatUnreadCount += unreadCount;
                    threadUnreadCounts[thread.ThreadId] = unreadCount;
                }
            }

            var socialChatUnreadCount = 0;
            var socialThreadUnreadCounts = new Dictionary<Guid, int>();
            var socialProfileUnreadCounts = new Dictionary<Guid, int>();
            var socialThreads = await threadDal.GetSocialThreadsForUserAsync(userId, hiddenOnly: false);
            var userProfiles = await socialProfileDal.GetByUserIdAsync(userId);
            var userProfileIds = userProfiles.Select(p => p.Id).ToHashSet();
            Guid? legacySocialProfileId = null;

            var unreadSocialItems = socialThreads.Where(t => t.UnreadCount > 0).ToList();
            Dictionary<Guid, ChatThread> socialThreadEntities = new();
            if (unreadSocialItems.Count > 0)
            {
                var unreadThreadIds = unreadSocialItems.Select(t => t.ThreadId).ToList();
                var entities = await threadDal.GetAll(t => unreadThreadIds.Contains(t.Id));
                socialThreadEntities = entities.ToDictionary(t => t.Id);
            }

            foreach (var thread in socialThreads)
            {
                var unreadCount = thread.UnreadCount;
                if (unreadCount > 0)
                {
                    socialChatUnreadCount += unreadCount;
                    socialThreadUnreadCounts[thread.ThreadId] = unreadCount;

                    if (socialThreadEntities.TryGetValue(thread.ThreadId, out var entity))
                    {
                        var ownerProfileId = ResolveSocialInboxProfileId(
                            entity, userProfileIds, userProfiles, ref legacySocialProfileId);
                        if (ownerProfileId.HasValue)
                        {
                            socialProfileUnreadCounts.TryGetValue(ownerProfileId.Value, out var profileUnread);
                            socialProfileUnreadCounts[ownerProfileId.Value] = profileUnread + unreadCount;
                        }
                    }
                }
            }

            return new BadgeCountDto
            {
                NotificationUnreadCount = notificationUnreadCount,
                ChatUnreadCount = chatUnreadCount,
                ThreadUnreadCounts = threadUnreadCounts,
                SocialChatUnreadCount = socialChatUnreadCount,
                SocialThreadUnreadCounts = socialThreadUnreadCounts,
                SocialProfileUnreadCounts = socialProfileUnreadCounts
            };
        }

        /// <summary>
        /// Notifies user that badge count has changed (triggers SignalR push with counts)
        /// </summary>
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task NotifyBadgeChangeAsync(Guid userId, BadgeChangeReason reason)
        {
            // Hesapla ve gönder - ANLIK güncelleme için
            var counts = await GetBadgeCountsAsync(userId);
            await realtime.PushBadgeUpdateAsync(
                userId,
                counts.NotificationUnreadCount,
                counts.ChatUnreadCount,
                counts.SocialChatUnreadCount);
        }

        /// <summary>
        /// Batch notify multiple users about badge changes
        /// </summary>
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task NotifyBadgeChangeBatchAsync(List<Guid> userIds, BadgeChangeReason reason)
        {
            foreach (var uid in userIds.Distinct())
                await NotifyBadgeChangeAsync(uid, reason);
        }

        /// <summary>
        /// Batch yüklenen verilerle favori aktifliğini memory'de kontrol eder (DB sorgusu yok).
        /// </summary>
        private static bool CheckActiveFavoriteInMemory(
            Guid from,
            Guid to,
            HashSet<(Guid, Guid)> directFavSet,
            Dictionary<Guid, HashSet<Guid>> ownerToStoreIds,
            HashSet<(Guid, Guid)> storeFavSet)
        {
            if (directFavSet.Contains((from, to))) return true;
            if (ownerToStoreIds.TryGetValue(to, out var storeIds))
            {
                foreach (var storeId in storeIds)
                    if (storeFavSet.Contains((from, storeId))) return true;
            }
            return false;
        }

        /// <summary>
        /// Sosyal DM thread'inde okunmamış sayısını hangi sosyal profile yazacağımızı bulur.
        /// </summary>
        private static Guid? ResolveSocialInboxProfileId(
            ChatThread thread,
            HashSet<Guid> userProfileIds,
            List<SocialProfile> userProfiles,
            ref Guid? legacySocialProfileId)
        {
            if (thread.SocialProfileLowId.HasValue && userProfileIds.Contains(thread.SocialProfileLowId.Value))
                return thread.SocialProfileLowId.Value;
            if (thread.SocialProfileHighId.HasValue && userProfileIds.Contains(thread.SocialProfileHighId.Value))
                return thread.SocialProfileHighId.Value;

            if (thread.SocialProfileLowId.HasValue || thread.SocialProfileHighId.HasValue)
                return null;

            if (!legacySocialProfileId.HasValue)
            {
                legacySocialProfileId = userProfiles
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => p.Id)
                    .FirstOrDefault();
                if (legacySocialProfileId == Guid.Empty)
                    legacySocialProfileId = null;
            }

            return legacySocialProfileId;
        }
    }

    /// <summary>
    /// DTO for badge counts
    /// </summary>
    public class BadgeCountDto
    {
        public int NotificationUnreadCount { get; set; }
        public int ChatUnreadCount { get; set; }
        public Dictionary<Guid, int> ThreadUnreadCounts { get; set; } = new();
        public int SocialChatUnreadCount { get; set; }
        public Dictionary<Guid, int> SocialThreadUnreadCounts { get; set; } = new();
        /// <summary>Sosyal DM okunmamışları, kullanıcının sosyal profili başına.</summary>
        public Dictionary<Guid, int> SocialProfileUnreadCounts { get; set; } = new();
    }

    /// <summary>
    /// Enum for badge change reasons (for logging/debugging)
    /// </summary>
    public enum BadgeChangeReason
    {
        NotificationReceived,
        NotificationRead,
        NotificationDeleted,
        MessageReceived,
        MessageRead,
        ThreadRemoved,
        AppointmentStatusChanged
    }
}
