using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;

namespace Business.Abstract
{
    public interface ISocialProfileService
    {
        Task<IResult> EnsureCustomerProfileAsync(Guid userId, string displayName);
        Task<IResult> EnsureFreeBarberProfileAsync(Guid freeBarberId, Guid userId, string displayName, double latitude, double longitude);
        Task<IResult> EnsureStoreProfileAsync(Guid storeId, Guid userId, string storeName, double latitude, double longitude);
        Task<IDataResult<List<SocialProfileDto>>> GetMyProfilesAsync(Guid userId);
        Task<IDataResult<SocialProfileDto>> GetProfileAsync(
            Guid profileId, Guid? viewerUserId, double? viewerLatitude = null, double? viewerLongitude = null);
        Task<IDataResult<SocialProfileDto>> GetProfileByOwnerAsync(
            SocialProfileOwnerType ownerType, Guid ownerId, Guid? viewerUserId,
            double? viewerLatitude = null, double? viewerLongitude = null);
        Task<IDataResult<SocialProfileDto>> GetProfileByUsernameAsync(string username, Guid? viewerUserId);
        Task<IResult> UpdateProfileAsync(Guid userId, Guid profileId, SocialProfileUpdateDto dto);
        Task<IDataResult<SocialProfileDto>> UploadAvatarAsync(Guid userId, Guid profileId, IFormFile file);
        Task<IDataResult<SocialProfileDto>> UploadCoverAsync(Guid userId, Guid profileId, IFormFile file);

        Task<IDataResult<List<SocialProfileSearchResultDto>>> SearchProfilesAsync(
            Guid userId,
            string? query,
            double? latitude,
            double? longitude,
            double radiusKm,
            int limit,
            AvailabilityFilter? availability = null,
            IReadOnlyList<Guid>? serviceIds = null);
    }
}
