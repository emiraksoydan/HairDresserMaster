using Core.DataAccess;

using Entities.Concrete.Entities;



namespace DataAccess.Abstract

{

    public interface IAppointmentSocialShareDal : IEntityRepository<AppointmentSocialShare>

    {

        Task<bool> ExistsForUserAsync(Guid appointmentId, Guid userId);



        Task<HashSet<Guid>> GetSharedAppointmentIdsAsync(Guid userId, IReadOnlyList<Guid> appointmentIds);

    }

}

