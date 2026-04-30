using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfChatThreadDal : EfEntityRepositoryBase<ChatThread, DatabaseContext>, IChatThreadDal
    {
        public EfChatThreadDal(DatabaseContext context) : base(context) { }

        public async Task<List<ChatThreadListItemDto>> GetThreadsForUserAsync(Guid userId, AppointmentStatus[] allowedStatuses, DateTime? beforeUtc = null, Guid? beforeId = null, int? limit = null)
        {
            // Pagination notu:
            //  - Thread listesi iki ayrı tablodan birleşir (appointment-thread + favorite-thread) ve
            //    uygulama seviyesinde LastMessageAt DESC sıralanır. UNION ALL'u EF ile yazmak karmaşık
            //    olduğu için iki alt-sorguyu ayrı Take(limit) ile çalıştırıp sonra birleşimden ilk limit'i alıyoruz.
            //  - `beforeUtc` cursor: LastMessageAt < beforeUtc filtresi. Null LastMessageAt'li thread'ler
            //    (hiç mesaj yok) zaten sayfalamanın kenarına gider — cursor'a dahil edilmemesi tutarlı
            //    (LastMessageAt = null → DateTime.MinValue muamelesi sayesinde sırası sonda).
            //  - `limit` null ise Take uygulanmaz → eski davranış (dahili çağrılar etkilenmez).
            //
            // Keyset tie-breaker: aynı LastMessageAt'a sahip 2 thread varsa, `beforeId`
            // (ThreadId) ile `(LastMessageAt, Id)` çift-kolon sıralaması kurulur. Hem iki
            // alt-sorgunun WHERE'ine hem de birleşik sıralamaya uygulanır.

            var appointmentQuery = Context.ChatThreads.AsNoTracking()
                .Where(t => t.AppointmentId.HasValue &&
                           (t.CustomerUserId == userId || t.StoreOwnerUserId == userId || t.FreeBarberUserId == userId))
                .Join(Context.Appointments.AsNoTracking()
                      .Where(a => (a.CustomerUserId == userId && !a.IsDeletedByCustomerUserId) ||
                                  (a.BarberStoreUserId == userId && !a.IsDeletedByBarberStoreUserId) ||
                                  (a.FreeBarberUserId == userId && !a.IsDeletedByFreeBarberUserId)),
                      t => t.AppointmentId!.Value,
                      a => a.Id,
                      (t, a) => new { t, a })
                .Where(x => allowedStatuses.Contains(x.a.Status));

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    var cTs = beforeUtc.Value;
                    var cId = beforeId.Value;
                    appointmentQuery = appointmentQuery.Where(x =>
                        x.t.LastMessageAt != null &&
                        (x.t.LastMessageAt < cTs || (x.t.LastMessageAt == cTs && x.t.Id.CompareTo(cId) < 0)));
                }
                else
                {
                    appointmentQuery = appointmentQuery.Where(x => x.t.LastMessageAt != null && x.t.LastMessageAt < beforeUtc.Value);
                }
            }

            var appointmentProjected = appointmentQuery
                .OrderByDescending(x => x.t.LastMessageAt)
                .ThenByDescending(x => x.t.Id)
                .Select(x => new ChatThreadListItemDto
                {
                    ThreadId = x.t.Id,
                    AppointmentId = x.a.Id,
                    Status = x.a.Status,
                    IsFavoriteThread = false,
                    Title = string.Empty,
                    LastMessagePreview = x.t.LastMessagePreview,
                    LastMessageAt = x.t.LastMessageAt,
                    UnreadCount = x.t.CustomerUserId == userId ? x.t.CustomerUnreadCount :
                                  x.t.StoreOwnerUserId == userId ? x.t.StoreUnreadCount :
                                  x.t.FreeBarberUserId == userId ? x.t.FreeBarberUnreadCount : 0
                });

            var appointmentThreads = limit.HasValue
                ? await appointmentProjected.Take(limit.Value).ToListAsync()
                : await appointmentProjected.ToListAsync();

            var favoriteQuery = Context.ChatThreads.AsNoTracking()
                .Where(t => !t.AppointmentId.HasValue &&
                           t.FavoriteFromUserId.HasValue &&
                           t.FavoriteToUserId.HasValue &&
                           (t.FavoriteFromUserId == userId || t.FavoriteToUserId == userId));

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    var cTs = beforeUtc.Value;
                    var cId = beforeId.Value;
                    favoriteQuery = favoriteQuery.Where(t =>
                        t.LastMessageAt != null &&
                        (t.LastMessageAt < cTs || (t.LastMessageAt == cTs && t.Id.CompareTo(cId) < 0)));
                }
                else
                {
                    favoriteQuery = favoriteQuery.Where(t => t.LastMessageAt != null && t.LastMessageAt < beforeUtc.Value);
                }
            }

            var favoriteProjected = favoriteQuery
                .OrderByDescending(t => t.LastMessageAt)
                .ThenByDescending(t => t.Id)
                .Select(t => new ChatThreadListItemDto
                {
                    ThreadId = t.Id,
                    AppointmentId = null,
                    Status = null,
                    IsFavoriteThread = true,
                    Title = string.Empty,
                    LastMessagePreview = t.LastMessagePreview,
                    LastMessageAt = t.LastMessageAt,
                    FavoriteStoreId = t.FavoriteContextStoreId,
                    UnreadCount = (t.FavoriteFromUserId == userId && t.CustomerUserId == userId) ? t.CustomerUnreadCount :
                                  (t.FavoriteFromUserId == userId && t.StoreOwnerUserId == userId) ? t.StoreUnreadCount :
                                  (t.FavoriteFromUserId == userId && t.FreeBarberUserId == userId) ? t.FreeBarberUnreadCount :
                                  (t.FavoriteToUserId == userId && t.CustomerUserId == userId) ? t.CustomerUnreadCount :
                                  (t.FavoriteToUserId == userId && t.StoreOwnerUserId == userId) ? t.StoreUnreadCount :
                                  (t.FavoriteToUserId == userId && t.FreeBarberUserId == userId) ? t.FreeBarberUnreadCount : 0
                });

            var favoriteThreads = limit.HasValue
                ? await favoriteProjected.Take(limit.Value).ToListAsync()
                : await favoriteProjected.ToListAsync();

            // Birleşimde de tie-breaker tutarlı olsun: aynı LastMessageAt → daha büyük ThreadId önde.
            var merged = appointmentThreads.Concat(favoriteThreads)
                .OrderByDescending(t => t.LastMessageAt ?? DateTime.MinValue)
                .ThenByDescending(t => t.ThreadId);

            return limit.HasValue ? merged.Take(limit.Value).ToList() : merged.ToList();
        }

        public async Task<List<ChatThread>> GetFavoriteThreadsForUserAsync(Guid userId)
        {
            return await Context.ChatThreads.AsNoTracking()
                .Where(t => !t.AppointmentId.HasValue &&
                           t.FavoriteFromUserId.HasValue &&
                           t.FavoriteToUserId.HasValue &&
                           (t.FavoriteFromUserId == userId || t.FavoriteToUserId == userId))
                .ToListAsync();
        }

        public async Task<ChatThread?> GetFavoriteThreadAsync(Guid fromUserId, Guid toUserId, Guid? storeId = null)
        {
            // REVIZE: StoreId parametresi artık kullanılmıyor - User ID bazlı tek thread olmalı
            // Her iki yönü de kontrol et (from->to veya to->from)
            // StoreId null olmalı (User ID bazlı thread)
            var thread = await Context.ChatThreads
                .FirstOrDefaultAsync(t => !t.AppointmentId.HasValue &&
                                         ((t.FavoriteFromUserId == fromUserId && t.FavoriteToUserId == toUserId) ||
                                          (t.FavoriteFromUserId == toUserId && t.FavoriteToUserId == fromUserId)) &&
                                         t.StoreId == null);
            return thread;
        }

        /// <summary>
        /// Gets unread message count for a user (database-level sum for performance)
        /// Includes both appointment and favorite threads
        /// </summary>
        public async Task<int> GetUnreadMessageCountAsync(Guid userId)
        {
            // Randevu thread'leri için
            var appointmentThreadsCount = await Context.ChatThreads
                .Where(t => t.AppointmentId.HasValue &&
                           ((t.CustomerUserId == userId && !t.IsDeletedByCustomerUserId) ||
                            (t.StoreOwnerUserId == userId && !t.IsDeletedByStoreOwnerUserId) ||
                            (t.FreeBarberUserId == userId && !t.IsDeletedByFreeBarberUserId)))
                .SumAsync(t =>
                    t.CustomerUserId == userId ? t.CustomerUnreadCount :
                    t.StoreOwnerUserId == userId ? t.StoreUnreadCount :
                    t.FreeBarberUserId == userId ? t.FreeBarberUnreadCount : 0);

            // Favori thread'leri için
            var favoriteThreadsCount = await Context.ChatThreads
                .Where(t => !t.AppointmentId.HasValue &&
                           t.FavoriteFromUserId.HasValue &&
                           t.FavoriteToUserId.HasValue &&
                           (t.FavoriteFromUserId == userId || t.FavoriteToUserId == userId))
                .SumAsync(t =>
                    (t.FavoriteFromUserId == userId && t.CustomerUserId == userId) ? t.CustomerUnreadCount :
                    (t.FavoriteFromUserId == userId && t.StoreOwnerUserId == userId) ? t.StoreUnreadCount :
                    (t.FavoriteFromUserId == userId && t.FreeBarberUserId == userId) ? t.FreeBarberUnreadCount :
                    (t.FavoriteToUserId == userId && t.CustomerUserId == userId) ? t.CustomerUnreadCount :
                    (t.FavoriteToUserId == userId && t.StoreOwnerUserId == userId) ? t.StoreUnreadCount :
                    (t.FavoriteToUserId == userId && t.FreeBarberUserId == userId) ? t.FreeBarberUnreadCount : 0);

            return appointmentThreadsCount + favoriteThreadsCount;
        }
    }
}
