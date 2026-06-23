using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace DataAccess.Abstract
{
    public interface ISocialProfileDal : IEntityRepository<SocialProfile>
    {
        Task<SocialProfile?> GetByOwnerAsync(SocialProfileOwnerType ownerType, Guid ownerId);
        Task<SocialProfile?> GetByUsernameAsync(string username);
        Task<bool> UsernameExistsAsync(string username);
        Task<List<SocialProfile>> GetByUserIdAsync(Guid userId);
        Task<SocialProfileStatsDto> GetStatsAsync(Guid profileId, IReadOnlyList<Guid>? viewerProfileIds);
        Task<bool> HasActiveStoryAsync(Guid profileId);
        Task<int> GetTotalPostViewsAsync(Guid profileId);
        Task<int> GetHighlightCountAsync(Guid profileId);
        Task<int> GetReelCountAsync(Guid profileId);

        Task<List<(SocialProfile Profile, double? DistanceKm)>> SearchProfilesAsync(
            string? usernameQuery,
            double? latitude,
            double? longitude,
            double radiusKm,
            IReadOnlyCollection<Guid> blockedUserIds,
            Guid? excludeUserId,
            int limit,
            AvailabilityFilter? availability = null,
            IReadOnlyList<Guid>? serviceIds = null);
    }
}
