using Entities.Concrete.Dto;

namespace DataAccess.Abstract
{
    public interface IAdminMediaDal
    {
        Task<(List<AdminMediaFileDto> items, int total)> GetMediaFilesAsync(
            string? category,
            string? search,
            int page,
            int pageSize);

        Task<AdminMediaStatsDto> GetMediaStatsAsync();
    }
}
