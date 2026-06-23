using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface ISocialAdminService
    {
        Task<IDataResult<List<SocialPostAdminDto>>> GetPostsForAdminAsync(
            SocialContentStatus? status, SocialPostType? postType, string? search, int page, int pageSize);
        Task<IDataResult<List<SocialCommentAdminDto>>> GetCommentsForAdminAsync(
            SocialContentStatus? status, string? search, int page, int pageSize);
        Task<IDataResult<List<SocialStoryAdminDto>>> GetStoriesForAdminAsync(
            SocialContentStatus? status, string? search, int page, int pageSize);
        Task<IDataResult<List<SocialProfileAdminDto>>> GetProfilesForAdminAsync(
            SocialContentStatus? status, string? search, int page, int pageSize);
        Task<IDataResult<List<SocialStoryHighlightAdminDto>>> GetHighlightsForAdminAsync(
            SocialContentStatus? status, string? search, int page, int pageSize);
        Task<IResult> AdminRemovePostAsync(Guid adminId, Guid postId);
        Task<IResult> AdminRemoveStoryAsync(Guid adminId, Guid storyId);
        Task<IResult> AdminRemoveProfileAsync(Guid adminId, Guid profileId);
        Task AdminRemoveAllProfilesForUserAsync(Guid adminId, Guid userId);
        Task AdminRestoreAllProfilesForUserAsync(Guid adminId, Guid userId);
        Task<IResult> AdminRemoveHighlightAsync(Guid adminId, Guid highlightId);
        Task<IResult> AdminRemoveCommentAsync(Guid adminId, Guid commentId);
    }
}
