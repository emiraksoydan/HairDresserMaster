using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface ISocialArchiveService
    {
        Task<IDataResult<SocialArchivedContentDto>> GetProfileArchiveAsync(Guid userId, Guid profileId, int limit = 100);
        Task<IResult> RestoreAsync(Guid userId, SocialRestoreArchivedRequest request);
    }
}
