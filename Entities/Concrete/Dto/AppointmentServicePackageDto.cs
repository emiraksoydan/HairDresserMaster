using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    /// <summary>
    /// Randevu içindeki paket snapshot'ı — bildirim ve randevu kartlarında gösterim için
    /// </summary>
    public class AppointmentServicePackageDto : IDto
    {
        public Guid PackageId { get; set; }
        public string PackageName { get; set; }
        public decimal TotalPrice { get; set; }
        public string ServiceNamesSnapshot { get; set; }
    }
}
