using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ServicePackageGetDto : IDto
    {
        public Guid Id { get; set; }
        public string PackageName { get; set; }
        public decimal TotalPrice { get; set; }
        public List<ServicePackageItemDto> Items { get; set; } = new();
    }
}
