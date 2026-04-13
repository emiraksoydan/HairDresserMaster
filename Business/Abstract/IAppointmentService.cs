using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IAppointmentService
    {
        Task<IDataResult<bool>> AnyControl(Guid id);
        /// <summary>Kullanıcı herhangi bir rolde (müşteri, serbest berber, dükkan sahibi) Pending/Approved randevuda katılımcı mı — hesap silme vb.</summary>
        Task<IDataResult<bool>> AnyBlockingAppointmentForUserAsync(Guid userId);
        Task<IDataResult<bool>> AnyChairControl(Guid id);
        Task<IDataResult<bool>> AnyStoreControl(Guid id);

        /// <summary>Bu dükkan kaydına (StoreId veya koltuk) bağlı Pending/Approved randevu var mı — silme/güncelleme için.</summary>
        Task<IDataResult<bool>> AnyBlockingAppointmentForStoreAsync(Guid storeId);

        /// <summary>Bu freebarber kaydına (FreeBarberUserId) bağlı Pending/Approved randevu var mı — silme/güncelleme için.</summary>
        Task<IDataResult<bool>> AnyBlockingAppointmentForFreeBarberAsync(Guid freeBarberUserId);

        Task<IDataResult<bool>> AnyManuelBarberControl(Guid id);
        Task<IDataResult<List<ChairSlotDto>>> GetAvailibity(Guid storeId, DateOnly dateOnly, CancellationToken ct = default);

        /// <summary>Dükkan müsaitliği: fromDate–toDate (dahil) aralığında günlük koltuk/slot listesi; tek istekte haftalık grid için.</summary>
        Task<IDataResult<List<StoreDayAvailabilityDto>>> GetAvailabilityRangeAsync(Guid storeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
        Task<IDataResult<Guid>> CreateCustomerToFreeBarberAsync(Guid customerUserId, CreateAppointmentRequestDto req);
        Task<IDataResult<Guid>> CreateCustomerToStoreControlAsync(Guid customerUserId, CreateAppointmentRequestDto req);
        Task<IDataResult<Guid>> CreateFreeBarberToStoreAsync(Guid freeBarberUserId, CreateAppointmentRequestDto req);
        Task<IDataResult<Guid>> CreateStoreToFreeBarberAsync(Guid storeOwnerUserId, CreateStoreToFreeBarberRequestDto req);
        Task<IDataResult<bool>> AddStoreToExistingAppointmentAsync(Guid freeBarberUserId, Guid appointmentId, Guid storeId, Guid chairId, DateOnly appointmentDate, TimeSpan startTime, TimeSpan endTime, List<Guid> serviceOfferingIds, List<Guid>? packageIds = null);
        Task<IDataResult<List<AppointmentGetDto>>> GetAllAppointmentByFilter(Guid currentUserId, AppointmentFilter appointmentFilter);

        /// <summary>Tüm randevular (yalnızca Admin rolü).</summary>
        Task<IDataResult<List<AppointmentGetDto>>> GetAllAppointmentsForAdminAsync(AppointmentFilter appointmentFilter);
        Task<IDataResult<bool>> StoreDecisionAsync(Guid storeOwnerUserId, Guid appointmentId, bool approve);
        Task<IDataResult<bool>> FreeBarberDecisionAsync(Guid freeBarberUserId, Guid appointmentId, bool approve);
        Task<IDataResult<bool>> CustomerDecisionAsync(Guid customerUserId, Guid appointmentId, bool approve);
        Task<IDataResult<bool>> CancelAsync(Guid userId, Guid appointmentId, CancelAppointmentRequestDto? request = null);
        Task<IDataResult<bool>> CompleteAsync(Guid userId, Guid appointmentId);
        
        /// <summary>
        /// Soft deletes an appointment for a specific user. Can only delete if appointment is not Pending or Approved.
        /// </summary>
        Task<IDataResult<bool>> DeleteAsync(Guid userId, Guid appointmentId);
        
        /// <summary>
        /// Soft deletes all deletable appointments for a user. Can only delete if appointment is not Pending or Approved.
        /// </summary>
        Task<IDataResult<bool>> DeleteAllAsync(Guid userId);
    }
}
