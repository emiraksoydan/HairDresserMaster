using Core.DataAccess;

using Entities.Concrete.Entities;



namespace DataAccess.Abstract

{

    public interface ISocialProfileMuteDal : IEntityRepository<SocialProfileMute>

    {

        Task<SocialProfileMute?> GetMuteAsync(Guid mutedByProfileId, Guid mutedProfileId);

        Task<bool> IsMutedByAnyAsync(IReadOnlyList<Guid> mutedByProfileIds, Guid mutedProfileId);

        Task<bool> IsMutedAsync(Guid mutedByProfileId, Guid mutedProfileId);

    }

}

