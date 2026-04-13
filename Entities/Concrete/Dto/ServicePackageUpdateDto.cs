using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ServicePackageUpdateDto : IDto
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string PackageName { get; set; }
        public decimal TotalPrice { get; set; }
        public List<Guid> ServiceOfferingIds { get; set; } = new();
    }
}
