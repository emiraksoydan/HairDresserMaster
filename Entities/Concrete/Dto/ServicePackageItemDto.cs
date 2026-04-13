using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ServicePackageItemDto : IDto
    {
        public Guid ServiceOfferingId { get; set; }
        public string ServiceName { get; set; }
    }
}
