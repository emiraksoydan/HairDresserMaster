using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialSavedPostDal : EfEntityRepositoryBase<SocialSavedPost, DatabaseContext>, ISocialSavedPostDal
    {
        private readonly DatabaseContext _context;

        public EfSocialSavedPostDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<SocialSavedPost?> GetAsync(Guid profileId, Guid postId)
        {
            return await _context.SocialSavedPosts
                .FirstOrDefaultAsync(s => s.ProfileId == profileId && s.PostId == postId);
        }

        public async Task<HashSet<Guid>> GetSavedPostIdsAsync(Guid profileId, IReadOnlyList<Guid> postIds)
        {
            if (postIds.Count == 0) return new HashSet<Guid>();

            var ids = await _context.SocialSavedPosts
                .AsNoTracking()
                .Where(s => s.ProfileId == profileId && postIds.Contains(s.PostId))
                .Select(s => s.PostId)
                .ToListAsync();

            return ids.ToHashSet();
        }

        public async Task<List<(SocialPost Post, Guid SaveId, DateTime SavedAt)>> GetSavedPostsAsync(
            Guid profileId,
            IReadOnlyCollection<Guid> blockedUserIds,
            SocialPostType? typeFilter,
            SocialPostType? excludeType,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit)
        {
            var query = _context.SocialSavedPosts
                .AsNoTracking()
                .Where(s => s.ProfileId == profileId)
                .Join(
                    _context.SocialPosts.AsNoTracking().Include(p => p.Profile),
                    s => s.PostId,
                    p => p.Id,
                    (save, post) => new { Save = save, Post = post })
                .Where(x => x.Post.Status == SocialContentStatus.Active);

            if (typeFilter.HasValue)
                query = query.Where(x => x.Post.Type == typeFilter.Value);
            else if (excludeType.HasValue)
                query = query.Where(x => x.Post.Type != excludeType.Value);

            if (blockedUserIds.Count > 0)
                query = query.Where(x => !blockedUserIds.Contains(x.Post.Profile.UserId));

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    query = query.Where(x =>
                        x.Save.CreatedAt < beforeUtc.Value ||
                        (x.Save.CreatedAt == beforeUtc.Value && x.Save.Id.CompareTo(beforeId.Value) < 0));
                }
                else
                {
                    query = query.Where(x => x.Save.CreatedAt < beforeUtc.Value);
                }
            }

            var rows = await query
                .OrderByDescending(x => x.Save.CreatedAt)
                .ThenByDescending(x => x.Save.Id)
                .Take(limit)
                .Select(x => new { x.Post, SaveId = x.Save.Id, SavedAt = x.Save.CreatedAt })
                .ToListAsync();

            return rows.Select(x => (x.Post, x.SaveId, x.SavedAt)).ToList();
        }
    }
}
