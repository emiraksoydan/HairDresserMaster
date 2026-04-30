using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;


namespace Business.Abstract
{
    public interface INotificationService
    {
        Task<IDataResult<Guid>> CreateAndPushAsync(
        Guid userId,
        NotificationType type,
        Guid? appointmentId,
        string title,
        object payload,string? body);

        Task<IDataResult<int>> GetUnreadCountAsync(Guid userId);

        /// <summary>
        /// Cursor-based pagination ile bildirimleri döner.
        /// `beforeUtc` null ise en yeni sayfa; yoksa belirtilen tarihten eski ilk `limit` kadar kayıt.
        /// Controller tarafında limit clamp edilir (1..100).
        /// </summary>
        Task<IDataResult<List<NotificationDto>>> GetAllNotify(Guid userId, DateTime? beforeUtc = null, Guid? beforeId = null, int limit = 30);

        Task<IDataResult<bool>> MarkReadAsync(Guid userId, Guid notificationId);

        Task<IDataResult<bool>> MarkReadByAppointmentIdAsync(Guid userId, Guid appointmentId);

        /// <summary>
        /// Updates notification payloads for an appointment (status, decisions) and pushes via SignalR
        /// </summary>
        Task<IDataResult<bool>> UpdateNotificationPayloadByAppointmentAsync(
            Guid appointmentId,
            AppointmentStatus status,
            DecisionStatus? storeDecision = null,
            DecisionStatus? freeBarberDecision = null,
            DecisionStatus? customerDecision = null,
            DateTime? pendingExpiresAt = null,
            string? cancellationReason = null);

        /// <summary>
        /// Deletes a notification if its appointment status is not Pending or Approved. Marks as read if unread before deletion.
        /// </summary>
        Task<IDataResult<bool>> DeleteAsync(Guid userId, Guid notificationId);

        /// <summary>
        /// Deletes all notifications that can be deleted (appointment status is not Pending or Approved). Marks unread notifications as read before deletion.
        /// </summary>
        Task<IDataResult<bool>> DeleteAllAsync(Guid userId);

    }
}
