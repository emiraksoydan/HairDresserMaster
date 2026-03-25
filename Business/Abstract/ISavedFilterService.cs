using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface ISavedFilterService
    {
        Task<IDataResult<List<SavedFilterGetDto>>> GetMyFiltersAsync(Guid userId);
        Task<IDataResult<SavedFilterGetDto>> CreateAsync(Guid userId, SavedFilterCreateDto dto);
        Task<IDataResult<SavedFilterGetDto>> UpdateAsync(Guid userId, SavedFilterUpdateDto dto);
        Task<IDataResult<bool>> DeleteAsync(Guid userId, Guid filterId);
        Task<IDataResult<List<SavedFilterGetDto>>> GetAllSavedFiltersForAdminAsync();
    }
}
