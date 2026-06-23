using Business.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/social/profile")]
    public class SocialProfileController : BaseApiController
    {
        private readonly ISocialProfileService _socialProfileService;

        public SocialProfileController(ISocialProfileService socialProfileService)
        {
            _socialProfileService = socialProfileService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfiles()
        {
            return await HandleUserDataOperation(userId => _socialProfileService.GetMyProfilesAsync(userId));
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] double? latitude,
            [FromQuery] double? longitude,
            [FromQuery] double radiusKm = 50,
            [FromQuery] int limit = 30,
            [FromQuery] AvailabilityFilter? availability = null,
            [FromQuery] List<Guid>? serviceIds = null)
        {
            return await HandleUserDataOperation(userId =>
                _socialProfileService.SearchProfilesAsync(
                    userId, q, latitude, longitude, radiusKm, Math.Clamp(limit, 1, 50),
                    availability, serviceIds));
        }

        [HttpGet("username/{username}")]
        public async Task<IActionResult> GetByUsername(string username)
        {
            return await HandleUserDataOperation(userId =>
                _socialProfileService.GetProfileByUsernameAsync(username, userId));
        }

        [HttpGet("{profileId:guid}")]
        public async Task<IActionResult> GetProfile(
            Guid profileId,
            [FromQuery] double? latitude = null,
            [FromQuery] double? longitude = null)
        {
            return await HandleUserDataOperation(userId =>
                _socialProfileService.GetProfileAsync(profileId, userId, latitude, longitude));
        }

        [HttpGet("owner/{ownerType}/{ownerId:guid}")]
        public async Task<IActionResult> GetByOwner(
            SocialProfileOwnerType ownerType,
            Guid ownerId,
            [FromQuery] double? latitude = null,
            [FromQuery] double? longitude = null)
        {
            return await HandleUserDataOperation(userId =>
                _socialProfileService.GetProfileByOwnerAsync(
                    ownerType, ownerId, userId, latitude, longitude));
        }

        [HttpPut("{profileId:guid}")]
        public async Task<IActionResult> Update(Guid profileId, [FromBody] SocialProfileUpdateDto dto)
        {
            return await HandleUserOperation(userId =>
                _socialProfileService.UpdateProfileAsync(userId, profileId, dto));
        }

        [HttpPost("{profileId:guid}/avatar")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar(Guid profileId, IFormFile file)
        {
            return await HandleUserDataOperation(userId =>
                _socialProfileService.UploadAvatarAsync(userId, profileId, file));
        }

        [HttpPost("{profileId:guid}/cover")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadCover(Guid profileId, IFormFile file)
        {
            return await HandleUserDataOperation(userId =>
                _socialProfileService.UploadCoverAsync(userId, profileId, file));
        }
    }
}
