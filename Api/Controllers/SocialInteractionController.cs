using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/social/interaction")]
    public class SocialInteractionController : BaseApiController
    {
        private readonly ISocialInteractionService _socialInteractionService;

        public SocialInteractionController(ISocialInteractionService socialInteractionService)
        {
            _socialInteractionService = socialInteractionService;
        }

        [HttpPost("like")]
        public async Task<IActionResult> ToggleLike(
            [FromBody] SocialLikeRequest request)
        {
            return await HandleUserOperation(userId =>
                _socialInteractionService.ToggleLikeAsync(
                    userId, request.ProfileId, request.TargetType, request.TargetId));
        }

        [HttpPost("save")]
        public async Task<IActionResult> ToggleSave([FromBody] SocialSaveRequest request)
        {
            return await HandleUserOperation(userId =>
                _socialInteractionService.ToggleSaveAsync(userId, request.ProfileId, request.PostId));
        }

        [HttpPost("comment")]
        public async Task<IActionResult> CreateComment(
            [FromBody] CreateSocialCommentWithProfileDto request)
        {
            return await HandleUserDataOperation(userId =>
                _socialInteractionService.CreateCommentAsync(userId, request.ProfileId, request));
        }

        [HttpGet("comment/{postId:guid}")]
        public async Task<IActionResult> GetComments(
            Guid postId,
            [FromQuery] Guid? parentCommentId,
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 30)
        {
            return await HandleUserDataOperation(userId =>
                _socialInteractionService.GetCommentsAsync(
                    userId, postId, parentCommentId, before, beforeId, Math.Clamp(limit, 1, 100)));
        }

        [HttpPut("comment/{commentId:guid}")]
        public async Task<IActionResult> UpdateComment(
            Guid commentId,
            [FromBody] UpdateSocialCommentWithProfileDto request)
        {
            return await HandleUserDataOperation(userId =>
                _socialInteractionService.UpdateCommentAsync(
                    userId, request.ProfileId, commentId, request.Text));
        }

        [HttpDelete("comment/{commentId:guid}")]
        public async Task<IActionResult> DeleteComment(
            Guid commentId,
            [FromBody] DeleteSocialCommentWithProfileDto request)
        {
            return await HandleUserOperation(userId =>
                _socialInteractionService.DeleteCommentAsync(userId, request.ProfileId, commentId));
        }

        [HttpPost("follow")]
        public async Task<IActionResult> Follow([FromBody] SocialFollowRequest request)
        {
            return await HandleUserOperation(userId =>
                _socialInteractionService.FollowAsync(
                    userId, request.FollowerProfileId, request.FollowingProfileId));
        }

        [HttpPost("unfollow")]
        public async Task<IActionResult> Unfollow([FromBody] SocialFollowRequest request)
        {
            return await HandleUserOperation(userId =>
                _socialInteractionService.UnfollowAsync(
                    userId, request.FollowerProfileId, request.FollowingProfileId));
        }

        [HttpGet("followers/{profileId:guid}")]
        public async Task<IActionResult> GetFollowers(
            Guid profileId,
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 30)
        {
            return await HandleUserDataOperation(userId =>
                _socialInteractionService.GetFollowersAsync(
                    userId, profileId, before, beforeId, Math.Clamp(limit, 1, 50)));
        }

        [HttpGet("following/{profileId:guid}")]
        public async Task<IActionResult> GetFollowing(
            Guid profileId,
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 30)
        {
            return await HandleUserDataOperation(userId =>
                _socialInteractionService.GetFollowingAsync(
                    userId, profileId, before, beforeId, Math.Clamp(limit, 1, 50)));
        }

        [HttpPost("mute")]
        public async Task<IActionResult> ToggleMute([FromBody] SocialMuteRequest request)
        {
            return await HandleUserOperation(userId =>
                _socialInteractionService.ToggleMuteAsync(
                    userId, request.MutedByProfileId, request.MutedProfileId));
        }

        [HttpGet("mutual/{profileId:guid}")]
        public async Task<IActionResult> GetMutualFollowers(
            Guid profileId,
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 30)
        {
            return await HandleUserDataOperation(userId =>
                _socialInteractionService.GetMutualFollowersAsync(
                    userId, profileId, before, beforeId, Math.Clamp(limit, 1, 50)));
        }
    }

    public class SocialMuteRequest
    {
        public Guid MutedByProfileId { get; set; }
        public Guid MutedProfileId { get; set; }
    }
}
