using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialStoryDal : EfEntityRepositoryBase<SocialStory, DatabaseContext>, ISocialStoryDal
    {
        private readonly DatabaseContext _context;

        public EfSocialStoryDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<SocialStory>> GetActiveByProfileIdsAsync(
            IReadOnlyList<Guid> profileIds,
            IReadOnlyCollection<Guid> blockedUserIds)
        {
            if (profileIds.Count == 0)
                return new List<SocialStory>();

            var now = DateTime.UtcNow;
            return await _context.SocialStories
                .AsNoTracking()
                .Include(s => s.Profile)
                .Where(s =>
                    profileIds.Contains(s.ProfileId) &&
                    s.Status == SocialContentStatus.Active &&
                    s.ExpiresAt > now &&
                    !blockedUserIds.Contains(s.Profile.UserId))
                .OrderBy(s => s.ProfileId)
                .ThenBy(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<SocialStory>> GetActiveByProfileIdAsync(
            Guid profileId,
            IReadOnlyCollection<Guid> blockedUserIds)
        {
            var now = DateTime.UtcNow;
            return await _context.SocialStories
                .AsNoTracking()
                .Include(s => s.Profile)
                .Where(s =>
                    s.ProfileId == profileId &&
                    s.Status == SocialContentStatus.Active &&
                    s.ExpiresAt > now &&
                    !blockedUserIds.Contains(s.Profile.UserId))
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<SocialStory>> GetByProfileAndStatusAsync(
            Guid profileId, SocialContentStatus status, DateTime? removedAfterUtc, int limit)
        {
            var query = _context.SocialStories
                .AsNoTracking()
                .Where(s => s.ProfileId == profileId && s.Status == status);

            if (status == SocialContentStatus.Removed && removedAfterUtc.HasValue)
            {
                query = query.Where(s =>
                    (s.RemovedAt ?? s.CreatedAt) >= removedAfterUtc.Value);
            }

            return await query
                .OrderByDescending(s => s.RemovedAt ?? s.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
    }
}
