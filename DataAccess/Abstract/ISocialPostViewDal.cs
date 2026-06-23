using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface ISocialPostViewDal : IEntityRepository<SocialPostView>
    {
        Task<bool> TryAddViewAsync(Guid postId, Guid profileId);
    }
}
