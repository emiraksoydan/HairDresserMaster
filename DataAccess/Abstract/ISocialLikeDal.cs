using Core.DataAccess;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace DataAccess.Abstract
{
    public interface ISocialLikeDal : IEntityRepository<SocialLike>
    {
        Task<SocialLike?> GetLikeAsync(SocialLikeTargetType targetType, Guid targetId, Guid profileId);
        Task<Dictionary<Guid, int>> GetLikeCountsAsync(SocialLikeTargetType targetType, IReadOnlyList<Guid> targetIds);
        Task<HashSet<Guid>> GetLikedTargetIdsAsync(SocialLikeTargetType targetType, IReadOnlyList<Guid> targetIds, Guid profileId);
    }
}
