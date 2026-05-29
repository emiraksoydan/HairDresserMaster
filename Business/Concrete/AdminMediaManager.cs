using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;

namespace Business.Concrete
{
    public class AdminMediaManager(IAdminMediaDal adminMediaDal) : IAdminMediaService
    {
        public async Task<IDataResult<PagedResultDto<AdminMediaFileDto>>> GetMediaFilesAsync(
            string? category,
            string? search,
            int page,
            int pageSize)
        {
            var (items, total) = await adminMediaDal.GetMediaFilesAsync(category, search, page, pageSize);
            return new SuccessDataResult<PagedResultDto<AdminMediaFileDto>>(new PagedResultDto<AdminMediaFileDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
            });
        }

        public async Task<IDataResult<AdminMediaStatsDto>> GetMediaStatsAsync()
        {
            var stats = await adminMediaDal.GetMediaStatsAsync();
            return new SuccessDataResult<AdminMediaStatsDto>(stats);
        }
    }
}
