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
    }
}
