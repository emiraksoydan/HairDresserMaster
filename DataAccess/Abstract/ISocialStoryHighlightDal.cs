using Core.DataAccess;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace DataAccess.Abstract
{
    public interface ISocialStoryHighlightDal : IEntityRepository<SocialStoryHighlight>
    {
        Task<List<SocialStoryHighlight>> GetByProfileIdAsync(Guid profileId);
        Task<SocialStoryHighlight?> GetWithItemsAsync(Guid highlightId);
        Task<int> GetNextSortOrderAsync(Guid profileId);
        Task<Dictionary<Guid, int>> GetItemCountsAsync(IReadOnlyList<Guid> highlightIds);
        Task<List<SocialStoryHighlight>> GetByProfileAndStatusAsync(
            Guid profileId, SocialContentStatus status, DateTime? removedAfterUtc, int limit);
    }
}
