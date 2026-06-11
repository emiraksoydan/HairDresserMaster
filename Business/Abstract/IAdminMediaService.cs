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

        /// <summary>Admin moderasyon: medyayı kaldırır (sertifika/vergi belgesi korumalı).</summary>
        Task<IResult> DeleteMediaAsync(Guid adminId, Guid id, string? category);
    }
}
