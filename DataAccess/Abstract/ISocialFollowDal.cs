using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface ISocialFollowDal : IEntityRepository<SocialFollow>
    {
        Task<SocialFollow?> GetFollowAsync(Guid followerProfileId, Guid followingProfileId);
        Task<List<Guid>> GetFollowingProfileIdsAsync(Guid followerProfileId);
        Task<List<SocialFollow>> GetFollowersPageAsync(
            Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit);
        Task<List<SocialFollow>> GetFollowingPageAsync(
            Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit);
        Task<HashSet<Guid>> GetFollowingAmongAsync(
            IReadOnlyList<Guid> viewerProfileIds, IReadOnlyList<Guid> targetProfileIds);
        Task<bool> CanDmUserAsync(IReadOnlyList<Guid> senderProfileIds, Guid recipientProfileId);
        Task<int> CountMutualFollowersAsync(IReadOnlyList<Guid> viewerProfileIds, Guid targetProfileId);
        Task<List<SocialFollow>> GetMutualFollowersPageAsync(
            IReadOnlyList<Guid> viewerProfileIds,
            Guid targetProfileId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit);
    }
}
