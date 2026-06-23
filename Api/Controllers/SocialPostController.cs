using Business.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/social/post")]
    public class SocialPostController : BaseApiController
    {
        private readonly ISocialPostService _socialPostService;

        public SocialPostController(ISocialPostService socialPostService)
        {
            _socialPostService = socialPostService;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create(
            [FromForm] Guid profileId,
            [FromForm] string? caption,
            [FromForm] SocialPostType type,
            [FromForm] List<IFormFile>? files,
            [FromForm] int? durationSec,
            [FromForm] List<int>? durationSecs,
            [FromForm] Guid? appointmentId)
        {
            return await HandleUserDataOperation(userId =>
                _socialPostService.CreatePostAsync(
                    userId, profileId, caption, type, files ?? new List<IFormFile>(), durationSec, durationSecs, appointmentId));
        }

        [HttpGet("reels")]
        public async Task<IActionResult> GetReelsFeed(
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 20,
            [FromQuery] double? latitude = null,
            [FromQuery] double? longitude = null,
            [FromQuery] double radiusKm = 50)
        {
            return await HandleUserDataOperation(userId =>
                _socialPostService.GetReelsFeedAsync(
                    userId, before, beforeId, Math.Clamp(limit, 1, 50),
                    latitude, longitude, radiusKm));
        }

        [HttpGet("discover")]
        public async Task<IActionResult> GetDiscover(
            [FromQuery] string? q,
            [FromQuery] double? latitude,
            [FromQuery] double? longitude,
            [FromQuery] double radiusKm = 50,
            [FromQuery] Guid? profileId = null,
            [FromQuery] DateTime? before = null,
            [FromQuery] Guid? beforeId = null,
            [FromQuery] int limit = 30,
            [FromQuery] AvailabilityFilter? availability = null,
            [FromQuery] List<Guid>? serviceIds = null)
        {
            return await HandleUserDataOperation(userId =>
                _socialPostService.GetDiscoverPostsAsync(
                    userId, q, latitude, longitude, radiusKm, profileId, before, beforeId,
                    Math.Clamp(limit, 1, 50), availability, serviceIds));
        }

        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed(
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 20)
        {
            return await HandleUserDataOperation(userId =>
                _socialPostService.GetFeedAsync(userId, before, beforeId, Math.Clamp(limit, 1, 50)));
        }

        [HttpGet("profile/{profileId:guid}")]
        public async Task<IActionResult> GetByProfile(
            Guid profileId,
            [FromQuery] SocialPostType? type,
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 20)
        {
            return await HandleUserDataOperation(userId =>
                _socialPostService.GetProfilePostsAsync(
                    userId, profileId, type, before, beforeId, Math.Clamp(limit, 1, 50)));
        }

        [HttpGet("saved/{profileId:guid}")]
        public async Task<IActionResult> GetSaved(
            Guid profileId,
            [FromQuery] SocialPostType? type,
            [FromQuery] SocialPostType? excludeType,
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 30)
        {
            return await HandleUserDataOperation(userId =>
                _socialPostService.GetSavedPostsAsync(
                    userId, profileId, type, excludeType, before, beforeId, Math.Clamp(limit, 1, 50)));
        }

        [HttpGet("{postId:guid}")]
        public async Task<IActionResult> GetPost(Guid postId)
        {
            return await HandleUserDataOperation(userId =>
                _socialPostService.GetPostAsync(userId, postId));
        }

        [HttpPut("{postId:guid}")]
        public async Task<IActionResult> Update(Guid postId, [FromBody] UpdateSocialPostCaptionDto request)
        {
            return await HandleUserDataOperation(userId =>
                _socialPostService.UpdatePostCaptionAsync(userId, postId, request?.Caption));
        }

        [HttpDelete("{postId:guid}")]
        public async Task<IActionResult> Delete(Guid postId)
        {
            return await HandleUserOperation(userId =>
                _socialPostService.DeletePostAsync(userId, postId));
        }

        [HttpPost("{postId:guid}/view")]
        public async Task<IActionResult> RecordView(Guid postId, [FromBody] SocialRecordPostViewDto request)
        {
            return await HandleUserOperation(userId =>
                _socialPostService.RecordViewAsync(userId, request.ProfileId, postId));
        }

        [HttpPost("{postId:guid}/pin")]
        public async Task<IActionResult> Pin(Guid postId)
        {
            return await HandleUserOperation(userId =>
                _socialPostService.PinPostAsync(userId, postId));
        }

        [HttpDelete("{postId:guid}/pin")]
        public async Task<IActionResult> Unpin(Guid postId)
        {
            return await HandleUserOperation(userId =>
                _socialPostService.UnpinPostAsync(userId, postId));
        }
    }
}
