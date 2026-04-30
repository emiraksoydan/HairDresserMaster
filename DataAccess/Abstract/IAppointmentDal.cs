using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IAppointmentDal : IEntityRepository<Appointment>
    {
        Task<List<ChairSlotDto>> GetAvailibilitySlot(Guid storeId,DateOnly dateOnly,CancellationToken ct = default);

        Task<List<StoreDayAvailabilityDto>> GetAvailabilitySlotRange(Guid storeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

        /// <summary>
        /// Filtreye göre randevuları döner. Opsiyonel cursor pagination:
        /// `beforeUtc` dolu ise CreatedAt &lt; beforeUtc olanlar; `limit` dolu ise Take(limit).
        /// Dahili (worker/AI) çağrıcılar parametresiz kullandığında eski davranış korunur.
        /// </summary>
        Task<List<AppointmentGetDto>> GetAllAppointmentByFilter(Guid currentUserId, AppointmentFilter appointmentFilter, bool forAdmin = false, DateTime? beforeUtc = null, Guid? beforeId = null, int? limit = null, Guid? singleAppointmentId = null);

    }
}
