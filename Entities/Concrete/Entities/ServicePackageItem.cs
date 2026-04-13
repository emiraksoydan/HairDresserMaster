using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class ServicePackageItem : IEntity
    {
        public Guid Id { get; set; }
        public Guid PackageId { get; set; }
        public Guid ServiceOfferingId { get; set; }
        /// <summary>
        /// Snapshot: paket oluşturulduğu andaki hizmet adı
        /// </summary>
        public string ServiceName { get; set; }

        public ServicePackage Package { get; set; }
    }
}
