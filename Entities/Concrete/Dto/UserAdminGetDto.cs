using Entities.Concrete.Enums;
using System;

namespace Entities.Concrete.Dto
{
    public class UserAdminGetDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public UserType UserType { get; set; }

        public bool IsActive { get; set; }
        public bool IsBanned { get; set; }
        public string? BanReason { get; set; }

        public string CustomerNumber { get; set; } = string.Empty;
        public Guid? ImageId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

