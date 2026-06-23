using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface ISocialStoryViewDal : IEntityRepository<SocialStoryView>
    {
        Task<bool> TryAddViewAsync(Guid storyId, Guid profileId);
        Task<int> GetViewCountAsync(Guid storyId);
        Task<List<SocialStoryView>> GetViewersAsync(
            Guid storyId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit);
    }
}
