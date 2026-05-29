using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ServicePackageAdminGetDto : IDto
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string PackageName { get; set; } = null!;
        public decimal TotalPrice { get; set; }
        public int ItemCount { get; set; }
        public List<ServicePackageItemDto> Items { get; set; } = new();

        // Sahip bilgisi (admin gridinde tür + ad + 6 haneli no + görsel göstermek için)
        public string OwnerType { get; set; } = "Unknown"; // "Store" | "FreeBarber"
        public string? OwnerName { get; set; }
        public string? OwnerNumber { get; set; }
        public string? OwnerImageUrl { get; set; }
    }
}
