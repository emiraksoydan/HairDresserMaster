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
        public string FirstName { get; set; }      // Plain metin — legacy; yeni yazmalarda FirstNameEncrypted kullanılır
        public string? FirstNameEncrypted { get; set; } // AES şifreli ad
        public string LastName { get; set; }       // Plain metin — legacy; yeni yazmalarda LastNameEncrypted kullanılır
        public string? LastNameEncrypted { get; set; }  // AES şifreli soyad
        public string PhoneNumber { get; set; } = string.Empty; // Legacy plain E164 — sadece eski kayıtlar için fallback
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

        // Subscription
        // NOT: Trial konsepti kullanıcı isteği üzerine kaldırıldı (Madde 8 / Phase B).
        // TrialEndDate alanı production DB'de hâlâ NOT NULL kolun olduğu için entity'de
        // tutulmaya devam ediyor; ancak hiçbir iş kuralı tarafından OKUNMUYOR / YAZILMIYOR.
        // Tamamen kaldırma için ileride bir migration (DROP COLUMN trial_end_date) eklenmeli;
        // o noktaya kadar default(DateTime) değeri yazılır ve göz ardı edilir.
        [Obsolete("Trial sürümü kaldırıldı (Madde 8/Phase B). Yeni kodda kullanmayın; ileride migration ile DROP edilecek.", error: false)]
        public DateTime TrialEndDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public bool SubscriptionAutoRenew { get; set; } = false;
        public bool SubscriptionCancelAtPeriodEnd { get; set; } = false;

        /// <summary>
        /// İlk kayıt sonrası kullanım rehberi uyarısı tamamlandı mı (bir kez).
        /// </summary>
        public bool HelpGuidePromptCompleted { get; set; } = true;
    }
}
