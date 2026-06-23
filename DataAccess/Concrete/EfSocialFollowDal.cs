using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialFollowDal : EfEntityRepositoryBase<SocialFollow, DatabaseContext>, ISocialFollowDal
    {
        private readonly DatabaseContext _context;

        public EfSocialFollowDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<SocialFollow?> GetFollowAsync(Guid followerProfileId, Guid followingProfileId)
        {
            return await _context.SocialFollows
                .FirstOrDefaultAsync(f =>
                    f.FollowerProfileId == followerProfileId &&
                    f.FollowingProfileId == followingProfileId);
        }

        public async Task<List<Guid>> GetFollowingProfileIdsAsync(Guid followerProfileId)
        {
            return await _context.SocialFollows
                .AsNoTracking()
                .Where(f => f.FollowerProfileId == followerProfileId)
                .Select(f => f.FollowingProfileId)
                .ToListAsync();
        }

        public async Task<List<SocialFollow>> GetFollowersPageAsync(
            Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit)
        {
            var query = _context.SocialFollows
                .AsNoTracking()
                .Include(f => f.FollowerProfile)
                .Where(f =>
                    f.FollowingProfileId == profileId &&
                    f.FollowerProfile.Status == SocialContentStatus.Active);

            query = ApplyFollowCursor(query, beforeUtc, beforeId);

            return await query
                .OrderByDescending(f => f.CreatedAt)
                .ThenByDescending(f => f.Id)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<SocialFollow>> GetFollowingPageAsync(
            Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit)
        {
            var query = _context.SocialFollows
                .AsNoTracking()
                .Include(f => f.FollowingProfile)
                .Where(f =>
                    f.FollowerProfileId == profileId &&
                    f.FollowingProfile.Status == SocialContentStatus.Active);

            query = ApplyFollowCursor(query, beforeUtc, beforeId);

            return await query
                .OrderByDescending(f => f.CreatedAt)
                .ThenByDescending(f => f.Id)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<HashSet<Guid>> GetFollowingAmongAsync(
            IReadOnlyList<Guid> viewerProfileIds, IReadOnlyList<Guid> targetProfileIds)
        {
            if (viewerProfileIds.Count == 0 || targetProfileIds.Count == 0)
                return new HashSet<Guid>();

            var ids = await _context.SocialFollows
                .AsNoTracking()
                .Where(f =>
                    viewerProfileIds.Contains(f.FollowerProfileId) &&
                    targetProfileIds.Contains(f.FollowingProfileId))
                .Select(f => f.FollowingProfileId)
                .Distinct()
                .ToListAsync();

            return ids.ToHashSet();
        }

        public async Task<bool> CanDmUserAsync(IReadOnlyList<Guid> senderProfileIds, Guid recipientProfileId)
        {
            if (senderProfileIds.Count == 0) return false;
            return await _context.SocialFollows.AnyAsync(f =>
                senderProfileIds.Contains(f.FollowerProfileId) &&
                f.FollowingProfileId == recipientProfileId);
        }

        public async Task<int> CountMutualFollowersAsync(
            IReadOnlyList<Guid> viewerProfileIds, Guid targetProfileId)
        {
            if (viewerProfileIds.Count == 0) return 0;

            var viewerFollowingIds = _context.SocialFollows
                .AsNoTracking()
                .Where(f => viewerProfileIds.Contains(f.FollowerProfileId))
                .Select(f => f.FollowingProfileId);

            return await _context.SocialFollows
                .AsNoTracking()
                .Where(f =>
                    f.FollowingProfileId == targetProfileId &&
                    viewerFollowingIds.Contains(f.FollowerProfileId) &&
                    f.FollowerProfile.Status == SocialContentStatus.Active)
                .CountAsync();
        }

        public async Task<List<SocialFollow>> GetMutualFollowersPageAsync(
            IReadOnlyList<Guid> viewerProfileIds,
            Guid targetProfileId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit)
        {
            if (viewerProfileIds.Count == 0) return new List<SocialFollow>();

            var viewerFollowingIds = _context.SocialFollows
                .AsNoTracking()
                .Where(f => viewerProfileIds.Contains(f.FollowerProfileId))
                .Select(f => f.FollowingProfileId);

            var query = _context.SocialFollows
                .AsNoTracking()
                .Include(f => f.FollowerProfile)
                .Where(f =>
                    f.FollowingProfileId == targetProfileId &&
                    viewerFollowingIds.Contains(f.FollowerProfileId) &&
                    f.FollowerProfile.Status == SocialContentStatus.Active);

            query = ApplyFollowCursor(query, beforeUtc, beforeId);

            return await query
                .OrderByDescending(f => f.CreatedAt)
                .ThenByDescending(f => f.Id)
                .Take(limit)
                .ToListAsync();
        }

        private static IQueryable<SocialFollow> ApplyFollowCursor(
            IQueryable<SocialFollow> query, DateTime? beforeUtc, Guid? beforeId)
        {
            if (!beforeUtc.HasValue) return query;

            if (beforeId.HasValue)
            {
                return query.Where(f =>
                    f.CreatedAt < beforeUtc.Value ||
                    (f.CreatedAt == beforeUtc.Value && f.Id.CompareTo(beforeId.Value) < 0));
            }

            return query.Where(f => f.CreatedAt < beforeUtc.Value);
        }
    }
}
