using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;

namespace Business.Abstract
{
    public interface ISocialStoryService
    {
        Task<IDataResult<Guid>> CreateStoryAsync(
            Guid userId,
            Guid profileId,
            IFormFile file,
            int? durationSec,
            Guid? appointmentId = null);

        Task<IDataResult<List<SocialStoryGroupDto>>> GetStoryFeedAsync(Guid userId);

        Task<IDataResult<List<SocialStoryDto>>> GetProfileStoriesAsync(Guid userId, Guid profileId);

        Task<IResult> DeleteStoryAsync(Guid userId, Guid storyId);
        Task<IResult> RecordViewAsync(Guid userId, Guid profileId, Guid storyId);
        Task<IDataResult<List<SocialStoryViewerDto>>> GetViewersAsync(
            Guid userId,
            Guid storyId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 50);
        Task<IResult> ReplyAsync(Guid userId, Guid storyId, CreateSocialStoryReplyDto dto);
    }
}
