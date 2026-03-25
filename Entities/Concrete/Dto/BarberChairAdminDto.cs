using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class BarberChairAdminDto : IDto
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public Guid? ManuelBarberId { get; set; }
        public string? Name { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
