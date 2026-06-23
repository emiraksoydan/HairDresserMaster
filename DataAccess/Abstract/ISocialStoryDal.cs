using Core.DataAccess;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace DataAccess.Abstract
{
    public interface ISocialStoryDal : IEntityRepository<SocialStory>
    {
        Task<List<SocialStory>> GetActiveByProfileIdsAsync(
            IReadOnlyList<Guid> profileIds,
            IReadOnlyCollection<Guid> blockedUserIds);

        Task<List<SocialStory>> GetActiveByProfileIdAsync(
            Guid profileId,
            IReadOnlyCollection<Guid> blockedUserIds);

        Task<List<SocialStory>> GetByProfileAndStatusAsync(
            Guid profileId, SocialContentStatus status, DateTime? removedAfterUtc, int limit);
    }
}
