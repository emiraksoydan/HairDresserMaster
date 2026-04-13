using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ServicePackageCreateDto : IDto
    {
        public Guid OwnerId { get; set; }
        public string PackageName { get; set; }
        public decimal TotalPrice { get; set; }
        /// <summary>En az 1 hizmet seçili olmalı</summary>
        public List<Guid> ServiceOfferingIds { get; set; } = new();
    }
}
