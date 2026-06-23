using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/social/story")]
    public class SocialStoryController : BaseApiController
    {
        private readonly ISocialStoryService _socialStoryService;

        public SocialStoryController(ISocialStoryService socialStoryService)
        {
            _socialStoryService = socialStoryService;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create(
            [FromForm] Guid profileId,
            [FromForm] IFormFile file,
            [FromForm] int? durationSec,
            [FromForm] Guid? appointmentId)
        {
            return await HandleUserDataOperation(userId =>
                _socialStoryService.CreateStoryAsync(userId, profileId, file, durationSec, appointmentId));
        }

        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed()
        {
            return await HandleUserDataOperation(_socialStoryService.GetStoryFeedAsync);
        }

        [HttpGet("profile/{profileId:guid}")]
        public async Task<IActionResult> GetByProfile(Guid profileId)
        {
            return await HandleUserDataOperation(userId =>
                _socialStoryService.GetProfileStoriesAsync(userId, profileId));
        }

        [HttpDelete("{storyId:guid}")]
        public async Task<IActionResult> Delete(Guid storyId)
        {
            return await HandleUserOperation(userId =>
                _socialStoryService.DeleteStoryAsync(userId, storyId));
        }

        [HttpPost("{storyId:guid}/view")]
        public async Task<IActionResult> RecordView(Guid storyId, [FromBody] SocialRecordStoryViewDto request)
        {
            return await HandleUserOperation(userId =>
                _socialStoryService.RecordViewAsync(userId, request.ProfileId, storyId));
        }

        [HttpGet("{storyId:guid}/viewers")]
        public async Task<IActionResult> GetViewers(
            Guid storyId,
            [FromQuery] DateTime? before,
            [FromQuery] Guid? beforeId,
            [FromQuery] int limit = 50)
        {
            return await HandleUserDataOperation(userId =>
                _socialStoryService.GetViewersAsync(
                    userId, storyId, before, beforeId, Math.Clamp(limit, 1, 100)));
        }

        [HttpPost("{storyId:guid}/reply")]
        public async Task<IActionResult> Reply(Guid storyId, [FromBody] CreateSocialStoryReplyDto request)
        {
            return await HandleUserOperation(userId =>
                _socialStoryService.ReplyAsync(userId, storyId, request));
        }
    }
}
