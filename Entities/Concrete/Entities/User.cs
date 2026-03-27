using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class User : IEntity
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty; // Legacy plain E164 (migration fallback)
        public string? PhoneNumberHash { get; set; } // HMAC-SHA256 for indexed lookup
        public string? PhoneNumberEncrypted { get; set; } // AES encrypted E164
        public bool IsActive { get; set; }
        public Guid? ImageId { get; set; }
        public Image Image { get; set; }
        public UserType UserType { get; set; }
        public string CustomerNumber { get; set; } // Müşteri numarası - aynı telefon numarasına sahip kullanıcılar aynı numarayı paylaşır
        public ICollection<UserOperationClaim> UserOperationClaims { get; set; }
        public bool IsKvkkApproved { get; set; }
        public DateTime? KvkkApprovedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Ban
        public bool IsBanned { get; set; } = false;
        public string? BanReason { get; set; }

        // Subscription / Trial
        public DateTime TrialEndDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public bool SubscriptionAutoRenew { get; set; } = false;
        public bool SubscriptionCancelAtPeriodEnd { get; set; } = false;
    }
}
