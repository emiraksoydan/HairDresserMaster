using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class ServicePackage : IEntity
    {
        public Guid Id { get; set; }
        /// <summary>
        /// BarberStore.Id veya FreeBarber.Id olabilir
        /// </summary>
        public Guid OwnerId { get; set; }
        public string PackageName { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<ServicePackageItem> Items { get; set; } = new List<ServicePackageItem>();
    }
}
