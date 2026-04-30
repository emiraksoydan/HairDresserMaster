using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Core.Aspect.Autofac.Logging;
using DataAccess.Abstract;
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
                foreach (var thread in favoriteEntities)
                {
                    if (!thread.FavoriteFromUserId.HasValue || !thread.FavoriteToUserId.HasValue)
                        continue;
                    var fromUserId = thread.FavoriteFromUserId.Value;
                    var toUserId = thread.FavoriteToUserId.Value;
                    var isActive =
                        await HasActiveFavoriteFromUserAsync(fromUserId, toUserId) ||
                        await HasActiveFavoriteFromUserAsync(toUserId, fromUserId);
                    if (isActive)
                        visibleFavoriteThreadIds.Add(thread.Id);
                }
            }

            // Calculate total chat unread count and per-thread counts
            var chatUnreadCount = 0;
            var threadUnreadCounts = new Dictionary<Guid, int>();

            foreach (var thread in threads)
            {
                if (thread.IsFavoriteThread && !visibleFavoriteThreadIds.Contains(thread.ThreadId))
                    continue;
                var unreadCount = thread.UnreadCount;
                if (unreadCount > 0)
                {
                    chatUnreadCount += unreadCount;
                    threadUnreadCounts[thread.ThreadId] = unreadCount;
                }
            }

            return new BadgeCountDto
            {
                NotificationUnreadCount = notificationUnreadCount,
                ChatUnreadCount = chatUnreadCount,
                ThreadUnreadCounts = threadUnreadCounts
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
            await realtime.PushBadgeUpdateAsync(userId, counts.NotificationUnreadCount, counts.ChatUnreadCount);
        }

        /// <summary>
        /// Batch notify multiple users about badge changes
        /// </summary>
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task NotifyBadgeChangeBatchAsync(List<Guid> userIds, BadgeChangeReason reason)
        {
            foreach (var userId in userIds.Distinct())
            {
                await NotifyBadgeChangeAsync(userId, reason);
            }
        }

        /// <summary>
        /// Helper method to get unread count for a specific user in a thread
        /// </summary>
        private async Task<bool> HasActiveFavoriteFromUserAsync(Guid fromUserId, Guid toUserId)
        {
            var directFav = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (directFav?.IsActive == true) return true;

            var toUserStores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
            if (!toUserStores.Any()) return false;

            var storeIds = toUserStores.Select(s => s.Id).ToList();
            var storeFav = await favoriteDal.Get(x =>
                x.FavoritedFromId == fromUserId &&
                storeIds.Contains(x.FavoritedToId) &&
                x.IsActive);
            return storeFav != null;
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
