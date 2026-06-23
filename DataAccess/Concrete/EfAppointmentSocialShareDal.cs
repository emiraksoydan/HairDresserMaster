using Core.DataAccess.EntityFramework;

using DataAccess.Abstract;

using Entities.Concrete.Entities;

using Microsoft.EntityFrameworkCore;



namespace DataAccess.Concrete

{

    public class EfAppointmentSocialShareDal : EfEntityRepositoryBase<AppointmentSocialShare, DatabaseContext>, IAppointmentSocialShareDal

    {

        private readonly DatabaseContext _context;



        public EfAppointmentSocialShareDal(DatabaseContext context) : base(context)

        {

            _context = context;

        }



        public async Task<bool> ExistsForUserAsync(Guid appointmentId, Guid userId)

        {

            return await _context.AppointmentSocialShares

                .AsNoTracking()

                .AnyAsync(x => x.AppointmentId == appointmentId && x.UserId == userId);

        }



        public async Task<HashSet<Guid>> GetSharedAppointmentIdsAsync(Guid userId, IReadOnlyList<Guid> appointmentIds)

        {

            if (appointmentIds.Count == 0)

                return new HashSet<Guid>();



            var ids = await _context.AppointmentSocialShares

                .AsNoTracking()

                .Where(x => x.UserId == userId && appointmentIds.Contains(x.AppointmentId))

                .Select(x => x.AppointmentId)

                .ToListAsync();



            return ids.ToHashSet();

        }

    }

}

