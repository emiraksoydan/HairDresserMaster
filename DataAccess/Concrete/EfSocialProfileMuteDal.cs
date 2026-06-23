using Core.DataAccess.EntityFramework;

using DataAccess.Abstract;

using Entities.Concrete.Entities;

using Microsoft.EntityFrameworkCore;



namespace DataAccess.Concrete

{

    public class EfSocialProfileMuteDal : EfEntityRepositoryBase<SocialProfileMute, DatabaseContext>, ISocialProfileMuteDal

    {

        private readonly DatabaseContext _context;



        public EfSocialProfileMuteDal(DatabaseContext context) : base(context)

        {

            _context = context;

        }



        public async Task<SocialProfileMute?> GetMuteAsync(Guid mutedByProfileId, Guid mutedProfileId)

        {

            return await _context.SocialProfileMutes

                .AsNoTracking()

                .FirstOrDefaultAsync(m =>

                    m.MutedByProfileId == mutedByProfileId &&

                    m.MutedProfileId == mutedProfileId);

        }



        public async Task<bool> IsMutedAsync(Guid mutedByProfileId, Guid mutedProfileId)

        {

            return await _context.SocialProfileMutes.AnyAsync(m =>

                m.MutedByProfileId == mutedByProfileId &&

                m.MutedProfileId == mutedProfileId);

        }



        public async Task<bool> IsMutedByAnyAsync(IReadOnlyList<Guid> mutedByProfileIds, Guid mutedProfileId)

        {

            if (mutedByProfileIds.Count == 0) return false;

            return await _context.SocialProfileMutes.AnyAsync(m =>

                mutedByProfileIds.Contains(m.MutedByProfileId) &&

                m.MutedProfileId == mutedProfileId);

        }

    }

}

