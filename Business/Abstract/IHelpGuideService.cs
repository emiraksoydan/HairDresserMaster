using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IHelpGuideService
    {
        Task<IDataResult<List<HelpGuideGetDto>>> GetByUserTypeAsync(int userType);
        Task<IDataResult<List<HelpGuideGetDto>>> GetActiveByUserTypeAsync(int userType);

        // ---- Admin CRUD ----
        Task<IDataResult<List<HelpGuideGetDto>>> GetAllForAdminAsync(int? userType);
        Task<IDataResult<HelpGuideGetDto>> CreateAsync(HelpGuideCreateDto dto);
        Task<IResult> UpdateAsync(Guid id, HelpGuideUpdateDto dto);
        Task<IResult> DeleteAsync(Guid id);
        Task<IResult> SetActiveAsync(Guid id, bool isActive);
    }
}
