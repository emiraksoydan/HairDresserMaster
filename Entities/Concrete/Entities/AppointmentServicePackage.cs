using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    /// <summary>
    /// Randevuya bağlı paket snapshot'ı — randevu geçmişinde paketin o anki bilgilerini saklar.
    /// </summary>
    public class AppointmentServicePackage : IEntity
    {
        public Guid Id { get; set; }
        public Guid AppointmentId { get; set; }
        public Guid PackageId { get; set; }
        /// <summary>Snapshot: randevu anındaki paket adı</summary>
        public string PackageName { get; set; }
        /// <summary>Snapshot: randevu anındaki toplam fiyat</summary>
        public decimal TotalPrice { get; set; }
        /// <summary>Snapshot: randevu anındaki hizmet listesi (virgülle ayrılmış)</summary>
        public string ServiceNamesSnapshot { get; set; }

        public Appointment Appointment { get; set; }
    }
}
