using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IServicePackageDal : IEntityRepository<ServicePackage>
    {
        /// <summary>Sahibine ait tüm paketleri hizmet detaylarıyla getirir</summary>
        Task<List<ServicePackageGetDto>> GetPackagesByOwnerIdAsync(Guid ownerId);

        /// <summary>Randevuya ait paket snapshot'larını getirir</summary>
        Task<List<AppointmentServicePackageDto>> GetPackagesByAppointmentIdAsync(Guid appointmentId);

        /// <summary>Bu paket için bekleyen veya onaylı randevu var mı?</summary>
        Task<bool> HasActiveAppointmentWithPackageAsync(Guid packageId);

        /// <summary>Hizmet adı güncellenince ilgili paket satırlarındaki snapshot adlarını senkronize eder</summary>
        Task SyncItemServiceNamesForOfferingsAsync(Guid ownerId, List<Guid> serviceOfferingIds);

        /// <summary>Paketi item'larıyla birlikte getirir</summary>
        Task<ServicePackage?> GetWithItemsAsync(Guid packageId);

        /// <summary>Birden fazla paketi ID listesiyle, item'lar ve ServiceOffering detaylarıyla birlikte getirir</summary>
        Task<List<ServicePackage>> GetPackagesByIdsWithItemsAsync(List<Guid> packageIds);

        /// <summary>Silinmek istenen hizmetler bu sahibin paketlerinde kullanılıyor mu?</summary>
        Task<bool> AnyPackageItemsReferenceOfferingsAsync(Guid ownerId, List<Guid> serviceOfferingIds);
    }
}
