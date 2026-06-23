using Core.DataAccess;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace DataAccess.Abstract
{
    public interface ISocialPostDal : IEntityRepository<SocialPost>
    {
        Task<List<SocialPost>> GetFeedAsync(
            IReadOnlyList<Guid> profileIds,
            IReadOnlyCollection<Guid> blockedUserIds,
            SocialPostType? typeFilter,
            SocialPostType? excludeType,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit);

        Task<List<SocialPost>> GetByProfileAsync(
            Guid profileId,
            IReadOnlyCollection<Guid> blockedUserIds,
            SocialPostType? typeFilter,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit);

        Task<List<SocialPostMedia>> GetMediaByPostIdsAsync(IReadOnlyList<Guid> postIds);

        Task<List<SocialPost>> GetByProfileAndStatusAsync(
            Guid profileId, SocialContentStatus status, DateTime? removedAfterUtc, int limit);

        Task<int> CountPinnedAsync(Guid profileId);

        Task<List<SocialPost>> GetFeedByProfileOrderAsync(
            IReadOnlyList<Guid> orderedProfileIds,
            IReadOnlyCollection<Guid> blockedUserIds,
            SocialPostType? typeFilter,
            SocialPostType? excludeType,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit);
    }
}
