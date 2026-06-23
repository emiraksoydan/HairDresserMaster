using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Constants;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialPostDal : EfEntityRepositoryBase<SocialPost, DatabaseContext>, ISocialPostDal
    {
        private readonly DatabaseContext _context;

        public EfSocialPostDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<SocialPost>> GetFeedAsync(
            IReadOnlyList<Guid> profileIds,
            IReadOnlyCollection<Guid> blockedUserIds,
            SocialPostType? typeFilter,
            SocialPostType? excludeType,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit)
        {
            if (profileIds.Count == 0) return new List<SocialPost>();

            var query = _context.SocialPosts
                .AsNoTracking()
                .Include(p => p.Profile)
                .Where(p => profileIds.Contains(p.ProfileId) && p.Status == SocialContentStatus.Active);

            if (typeFilter.HasValue)
                query = query.Where(p => p.Type == typeFilter.Value);
            else if (excludeType.HasValue)
                query = query.Where(p => p.Type != excludeType.Value);

            if (blockedUserIds.Count > 0)
                query = query.Where(p => !blockedUserIds.Contains(p.Profile.UserId));

            query = ApplyCursor(query, beforeUtc, beforeId);

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<SocialPost>> GetByProfileAsync(
            Guid profileId,
            IReadOnlyCollection<Guid> blockedUserIds,
            SocialPostType? typeFilter,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit)
        {
            var baseQuery = _context.SocialPosts
                .AsNoTracking()
                .Include(p => p.Profile)
                .Where(p => p.ProfileId == profileId && p.Status == SocialContentStatus.Active);

            if (blockedUserIds.Count > 0)
                baseQuery = baseQuery.Where(p => !blockedUserIds.Contains(p.Profile.UserId));

            if (typeFilter.HasValue)
                baseQuery = baseQuery.Where(p => p.Type == typeFilter.Value);

            var isFirstPage = !beforeUtc.HasValue;

            if (isFirstPage)
            {
                var pinnedQuery = baseQuery.Where(p => p.IsPinned);
                var pinned = await pinnedQuery
                    .OrderByDescending(p => p.PinnedAt ?? p.CreatedAt)
                    .ThenByDescending(p => p.Id)
                    .Take(SocialMediaLimits.MaxPinnedPostsPerProfile)
                    .ToListAsync();

                var remaining = Math.Max(0, limit - pinned.Count);
                if (remaining == 0)
                    return pinned;

                var nonPinned = await baseQuery
                    .Where(p => !p.IsPinned)
                    .OrderByDescending(p => p.CreatedAt)
                    .ThenByDescending(p => p.Id)
                    .Take(remaining)
                    .ToListAsync();

                return pinned.Concat(nonPinned).ToList();
            }

            var query = baseQuery.Where(p => !p.IsPinned);
            query = ApplyCursor(query, beforeUtc, beforeId);

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<SocialPostMedia>> GetMediaByPostIdsAsync(IReadOnlyList<Guid> postIds)
        {
            if (postIds.Count == 0) return new List<SocialPostMedia>();

            return await _context.SocialPostMedia
                .AsNoTracking()
                .Where(m => postIds.Contains(m.PostId))
                .OrderBy(m => m.PostId)
                .ThenBy(m => m.SortOrder)
                .ToListAsync();
        }

        public async Task<List<SocialPost>> GetByProfileAndStatusAsync(
            Guid profileId, SocialContentStatus status, DateTime? removedAfterUtc, int limit)
        {
            var query = _context.SocialPosts
                .AsNoTracking()
                .Where(p => p.ProfileId == profileId && p.Status == status);

            if (status == SocialContentStatus.Removed && removedAfterUtc.HasValue)
            {
                query = query.Where(p =>
                    (p.RemovedAt ?? p.UpdatedAt) >= removedAfterUtc.Value);
            }

            return await query
                .OrderByDescending(p => p.RemovedAt ?? p.UpdatedAt)
                .ThenByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<int> CountPinnedAsync(Guid profileId)
        {
            return await _context.SocialPosts
                .AsNoTracking()
                .CountAsync(p =>
                    p.ProfileId == profileId &&
                    p.Status == SocialContentStatus.Active &&
                    p.IsPinned);
        }

        public async Task<List<SocialPost>> GetFeedByProfileOrderAsync(
            IReadOnlyList<Guid> orderedProfileIds,
            IReadOnlyCollection<Guid> blockedUserIds,
            SocialPostType? typeFilter,
            SocialPostType? excludeType,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit)
        {
            if (orderedProfileIds.Count == 0) return new List<SocialPost>();

            var idSet = orderedProfileIds.ToHashSet();
            var query = _context.SocialPosts
                .AsNoTracking()
                .Include(p => p.Profile)
                .Where(p => idSet.Contains(p.ProfileId) && p.Status == SocialContentStatus.Active);

            if (typeFilter.HasValue)
                query = query.Where(p => p.Type == typeFilter.Value);
            else if (excludeType.HasValue)
                query = query.Where(p => p.Type != excludeType.Value);

            if (blockedUserIds.Count > 0)
                query = query.Where(p => !blockedUserIds.Contains(p.Profile.UserId));

            var fetchSize = Math.Clamp(limit * Math.Max(orderedProfileIds.Count, 1), limit, 500);
            var candidates = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(fetchSize)
                .ToListAsync();

            if (beforeUtc.HasValue)
            {
                candidates = beforeId.HasValue
                    ? candidates.Where(p =>
                        p.CreatedAt < beforeUtc.Value ||
                        (p.CreatedAt == beforeUtc.Value && p.Id.CompareTo(beforeId.Value) < 0)).ToList()
                    : candidates.Where(p => p.CreatedAt < beforeUtc.Value).ToList();
            }

            var orderMap = orderedProfileIds
                .Select((id, index) => (id, index))
                .ToDictionary(x => x.id, x => x.index);

            return candidates
                .OrderBy(p => orderMap.GetValueOrDefault(p.ProfileId, int.MaxValue))
                .ThenByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(limit)
                .ToList();
        }

        private static IQueryable<SocialPost> ApplyCursor(
            IQueryable<SocialPost> query,
            DateTime? beforeUtc,
            Guid? beforeId)
        {
            if (!beforeUtc.HasValue) return query;

            if (beforeId.HasValue)
            {
                return query.Where(p =>
                    p.CreatedAt < beforeUtc.Value ||
                    (p.CreatedAt == beforeUtc.Value && p.Id.CompareTo(beforeId.Value) < 0));
            }

            return query.Where(p => p.CreatedAt < beforeUtc.Value);
        }
    }
}
