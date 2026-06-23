using Core.Utilities.Results;



namespace Business.Abstract

{

    public interface ISocialAppointmentShareService

    {

        Task<IDataResult<List<Guid>>> GetSharedAppointmentIdsAsync(

            Guid userId,

            IReadOnlyList<Guid> appointmentIds);

    }

}

