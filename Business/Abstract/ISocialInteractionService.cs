using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface ISocialInteractionService
    {
        Task<IResult> ToggleLikeAsync(Guid userId, Guid profileId, SocialLikeTargetType targetType, Guid targetId);
        Task<IResult> ToggleSaveAsync(Guid userId, Guid profileId, Guid postId);
        Task<IDataResult<SocialCommentDto>> CreateCommentAsync(Guid userId, Guid profileId, CreateSocialCommentDto dto);
        Task<IDataResult<SocialCommentDto>> UpdateCommentAsync(
            Guid userId, Guid profileId, Guid commentId, string text);
        Task<IResult> DeleteCommentAsync(Guid userId, Guid profileId, Guid commentId);
        Task<IDataResult<List<SocialCommentDto>>> GetCommentsAsync(
            Guid userId, Guid postId, Guid? parentCommentId, DateTime? beforeUtc, Guid? beforeId, int limit = 30);
        Task<IResult> FollowAsync(Guid userId, Guid followerProfileId, Guid followingProfileId);
        Task<IResult> UnfollowAsync(Guid userId, Guid followerProfileId, Guid followingProfileId);
        Task<IDataResult<List<SocialFollowListItemDto>>> GetFollowersAsync(
            Guid userId, Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit = 30);
        Task<IDataResult<List<SocialFollowListItemDto>>> GetFollowingAsync(
            Guid userId, Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit = 30);
        Task<IResult> ToggleMuteAsync(Guid userId, Guid mutedByProfileId, Guid mutedProfileId);
        Task<IDataResult<List<SocialFollowListItemDto>>> GetMutualFollowersAsync(
            Guid userId, Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit = 30);
    }
}
