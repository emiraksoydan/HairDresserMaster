using Core.DataAccess;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace DataAccess.Abstract
{
    public interface ISocialSavedPostDal : IEntityRepository<SocialSavedPost>
    {
        Task<SocialSavedPost?> GetAsync(Guid profileId, Guid postId);
        Task<HashSet<Guid>> GetSavedPostIdsAsync(Guid profileId, IReadOnlyList<Guid> postIds);
        Task<List<(SocialPost Post, Guid SaveId, DateTime SavedAt)>> GetSavedPostsAsync(
            Guid profileId,
            IReadOnlyCollection<Guid> blockedUserIds,
            SocialPostType? typeFilter,
            SocialPostType? excludeType,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit);
    }
}
