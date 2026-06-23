using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/social/archive")]
    public class SocialArchiveController : BaseApiController
    {
        private readonly ISocialArchiveService _archiveService;

        public SocialArchiveController(ISocialArchiveService archiveService)
        {
            _archiveService = archiveService;
        }

        [HttpGet("profile/{profileId:guid}")]
        public async Task<IActionResult> GetProfileArchive(Guid profileId, [FromQuery] int limit = 100)
        {
            return await HandleUserDataOperation(userId =>
                _archiveService.GetProfileArchiveAsync(userId, profileId, Math.Clamp(limit, 1, 200)));
        }

        [HttpPost("restore")]
        public async Task<IActionResult> Restore([FromBody] SocialRestoreArchivedRequest request)
        {
            return await HandleUserOperation(userId => _archiveService.RestoreAsync(userId, request));
        }
    }
}
