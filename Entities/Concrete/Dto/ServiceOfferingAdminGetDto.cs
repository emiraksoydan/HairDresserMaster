using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ServiceOfferingAdminGetDto : IDto
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public decimal Price { get; set; }
        public string ServiceName { get; set; } = null!;

        // Sahip bilgisi (admin gridinde tür + ad + 6 haneli no + görsel göstermek için)
        public string OwnerType { get; set; } = "Unknown"; // "Store" | "FreeBarber"
        public string? OwnerName { get; set; }
        public string? OwnerNumber { get; set; }
        public string? OwnerImageUrl { get; set; }
    }
}
