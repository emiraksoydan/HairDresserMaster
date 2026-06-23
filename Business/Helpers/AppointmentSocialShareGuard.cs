using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Constants;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Helpers
{
    public class AppointmentSocialShareGuard(
        IAppointmentDal appointmentDal,
        IAppointmentSocialShareDal appointmentSocialShareDal)
    {
        public async Task<IResult?> EnsureCanLinkShareAsync(Guid userId, Guid appointmentId)
        {
            var appointment = await appointmentDal.Get(a => a.Id == appointmentId);
            if (appointment == null)
                return new ErrorResult(SocialErrorCodes.AppointmentShareNotFound);

            if (appointment.Status != AppointmentStatus.Completed)
                return new ErrorResult(SocialErrorCodes.AppointmentShareNotCompleted);

            if (!IsParticipant(appointment, userId))
                return new ErrorResult(SocialErrorCodes.AppointmentShareNotParticipant);

            if (await appointmentSocialShareDal.ExistsForUserAsync(appointmentId, userId))
                return new ErrorResult(SocialErrorCodes.AppointmentShareAlreadyShared);

            return null;
        }

        public async Task RecordShareAsync(
            Guid userId,
            Guid appointmentId,
            AppointmentSocialShareContentType contentType,
            Guid contentId)
        {
            if (await appointmentSocialShareDal.ExistsForUserAsync(appointmentId, userId))
                return;

            await appointmentSocialShareDal.Add(new AppointmentSocialShare
            {
                Id = Guid.NewGuid(),
                AppointmentId = appointmentId,
                UserId = userId,
                ContentType = contentType,
                ContentId = contentId,
                CreatedAt = DateTime.UtcNow,
            });
        }

        private static bool IsParticipant(Appointment appointment, Guid userId) =>
            appointment.CustomerUserId == userId
            || appointment.BarberStoreUserId == userId
            || appointment.FreeBarberUserId == userId;
    }
}
