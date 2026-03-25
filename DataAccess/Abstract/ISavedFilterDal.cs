using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface ISavedFilterDal : IEntityRepository<SavedFilter>
    {
        Task<List<SavedFilter>> GetByUserIdAsync(Guid userId);
        Task<int> CountByUserIdAsync(Guid userId);
    }
}
