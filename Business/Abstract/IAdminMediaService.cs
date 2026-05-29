using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IAdminMediaService
    {
        Task<IDataResult<PagedResultDto<AdminMediaFileDto>>> GetMediaFilesAsync(
            string? category,
            string? search,
            int page,
            int pageSize);

        Task<IDataResult<AdminMediaStatsDto>> GetMediaStatsAsync();
    }
}
