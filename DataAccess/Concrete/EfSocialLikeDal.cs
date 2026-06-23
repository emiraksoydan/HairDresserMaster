using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialLikeDal : EfEntityRepositoryBase<SocialLike, DatabaseContext>, ISocialLikeDal
    {
        private readonly DatabaseContext _context;

        public EfSocialLikeDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<SocialLike?> GetLikeAsync(SocialLikeTargetType targetType, Guid targetId, Guid profileId)
        {
            return await _context.SocialLikes
                .FirstOrDefaultAsync(l =>
                    l.TargetType == targetType &&
                    l.TargetId == targetId &&
                    l.ProfileId == profileId);
        }

        public async Task<Dictionary<Guid, int>> GetLikeCountsAsync(SocialLikeTargetType targetType, IReadOnlyList<Guid> targetIds)
        {
            if (targetIds.Count == 0) return new Dictionary<Guid, int>();

            return await _context.SocialLikes
                .AsNoTracking()
                .Where(l => l.TargetType == targetType && targetIds.Contains(l.TargetId))
                .GroupBy(l => l.TargetId)
                .Select(g => new { TargetId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TargetId, x => x.Count);
        }

        public async Task<HashSet<Guid>> GetLikedTargetIdsAsync(
            SocialLikeTargetType targetType,
            IReadOnlyList<Guid> targetIds,
            Guid profileId)
        {
            if (targetIds.Count == 0) return new HashSet<Guid>();

            var ids = await _context.SocialLikes
                .AsNoTracking()
                .Where(l =>
                    l.TargetType == targetType &&
                    l.ProfileId == profileId &&
                    targetIds.Contains(l.TargetId))
                .Select(l => l.TargetId)
                .ToListAsync();

            return ids.ToHashSet();
        }
    }
}
