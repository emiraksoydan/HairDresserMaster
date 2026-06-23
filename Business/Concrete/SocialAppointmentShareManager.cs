using Business.Abstract;

using Business.BusinessAspect.Autofac;

using Core.Aspect.Autofac.Logging;

using Core.Utilities.Results;

using DataAccess.Abstract;



namespace Business.Concrete

{

    public class SocialAppointmentShareManager(IAppointmentSocialShareDal appointmentSocialShareDal)

        : ISocialAppointmentShareService

    {

        [SecuredOperation("Customer,FreeBarber,BarberStore")]

        [LogAspect]

        public async Task<IDataResult<List<Guid>>> GetSharedAppointmentIdsAsync(

            Guid userId,

            IReadOnlyList<Guid> appointmentIds)

        {

            var ids = appointmentIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();

            if (ids.Count == 0)

                return new SuccessDataResult<List<Guid>>(new List<Guid>());



            var shared = await appointmentSocialShareDal.GetSharedAppointmentIdsAsync(userId, ids);

            return new SuccessDataResult<List<Guid>>(shared.ToList());

        }

    }

}

