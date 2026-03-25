using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ManuelBarberAdminGetDto : IDto
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public string StoreName { get; set; } = null!;
        public Guid BarberStoreOwnerId { get; set; }
        public string FullName { get; set; } = null!;
        public string? ProfileImageUrl { get; set; }
        public double Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
