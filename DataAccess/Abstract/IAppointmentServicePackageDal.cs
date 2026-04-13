using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IAppointmentServicePackageDal : IEntityRepository<AppointmentServicePackage>
    {
        Task AddRangeAsync(List<AppointmentServicePackage> records);

        /// <summary>Randevuya bağlı tüm paket snapshot kayıtlarını siler (yeniden yazım öncesi).</summary>
        Task DeleteByAppointmentIdAsync(Guid appointmentId);
    }
}
