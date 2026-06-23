using Core.DataAccess;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace DataAccess.Abstract
{
    public interface ISocialStoryHighlightItemDal : IEntityRepository<SocialStoryHighlightItem>
    {
        Task<List<SocialStoryHighlightItem>> GetRemovedByProfileIdAsync(
            Guid profileId, DateTime? removedAfterUtc, int limit);
    }
}
