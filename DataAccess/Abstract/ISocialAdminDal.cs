using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace DataAccess.Abstract
{
    public interface ISocialAdminDal
    {
        Task<List<SocialPost>> GetPostsForAdminAsync(
            SocialContentStatus? status, SocialPostType? postType, string? search, int skip, int take);
        Task<List<SocialComment>> GetCommentsForAdminAsync(
            SocialContentStatus? status, string? search, int skip, int take);
        Task<List<SocialStory>> GetStoriesForAdminAsync(
            SocialContentStatus? status, string? search, int skip, int take);
        Task<List<SocialProfile>> GetProfilesForAdminAsync(
            SocialContentStatus? status, string? search, int skip, int take);
        Task<List<SocialStoryHighlight>> GetHighlightsForAdminAsync(
            SocialContentStatus? status, string? search, int skip, int take);
        Task SetProfileContentRemovedAsync(Guid profileId);
        Task RestoreProfileContentAsync(Guid profileId);
    }
}
