using System;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    /// <summary>
    /// Yönetim paneli (admin) kullanıcıları. Normal User akışından (telefon/OTP) tamamen bağımsızdır;
    /// email + password ile login olur, password reset email akışı SMTP üzerinden çalışır.
    /// </summary>
    public class AdminUser : IEntity
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // Yönetim paneli avatar URL'si (Azure Blob veya benzeri). Null ise initial harf gösterilir.
        public string? ProfileImageUrl { get; set; }

        // Forgot password reset token (hash + expiry)
        public string? ResetTokenHash { get; set; }
        public DateTime? ResetTokenExpiresAt { get; set; }

        // Refresh token (hash + expiry) — admin tek oturum kullandığı için tek alan yeterli.
        public string? RefreshTokenHash { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }

        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
