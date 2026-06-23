using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/social/story/highlight")]
    public class SocialStoryHighlightController : BaseApiController
    {
        private readonly ISocialStoryHighlightService _highlightService;

        public SocialStoryHighlightController(ISocialStoryHighlightService highlightService)
        {
            _highlightService = highlightService;
        }

        [HttpGet("profile/{profileId:guid}")]
        public async Task<IActionResult> GetByProfile(Guid profileId)
        {
            return await HandleUserDataOperation(userId =>
                _highlightService.GetProfileHighlightsAsync(userId, profileId));
        }

        [HttpGet("{highlightId:guid}")]
        public async Task<IActionResult> GetDetail(Guid highlightId)
        {
            return await HandleUserDataOperation(userId =>
                _highlightService.GetHighlightDetailAsync(userId, highlightId));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSocialStoryHighlightRequest request)
        {
            return await HandleUserDataOperation(userId =>
                _highlightService.CreateHighlightAsync(userId, request));
        }

        [HttpPut("{highlightId:guid}")]
        public async Task<IActionResult> Update(Guid highlightId, [FromBody] UpdateSocialStoryHighlightRequest request)
        {
            return await HandleUserOperation(userId =>
                _highlightService.UpdateHighlightAsync(userId, highlightId, request));
        }

        [HttpPost("{highlightId:guid}/items")]
        public async Task<IActionResult> AddItems(Guid highlightId, [FromBody] AddSocialStoryHighlightItemsRequest request)
        {
            return await HandleUserOperation(userId =>
                _highlightService.AddStoriesToHighlightAsync(userId, highlightId, request));
        }

        [HttpDelete("{highlightId:guid}/items/{itemId:guid}")]
        public async Task<IActionResult> RemoveItem(Guid highlightId, Guid itemId)
        {
            return await HandleUserOperation(userId =>
                _highlightService.RemoveHighlightItemAsync(userId, highlightId, itemId));
        }

        [HttpDelete("{highlightId:guid}")]
        public async Task<IActionResult> Delete(Guid highlightId)
        {
            return await HandleUserOperation(userId =>
                _highlightService.DeleteHighlightAsync(userId, highlightId));
        }
    }
}
