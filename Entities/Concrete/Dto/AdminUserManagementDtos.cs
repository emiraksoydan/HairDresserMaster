using System;
using System.Collections.Generic;

namespace Entities.Concrete.Dto
{
    /// <summary>Admin listesinde dönen özet bilgi (şifre hash'i hariç).</summary>
    public class AdminUserListItemDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? ProfileImageUrl { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Yeni admin oluşturma payload'ı (sadece mevcut admin çağırabilir).</summary>
    public class AdminUserCreateDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public class AdminUserSetActiveDto
    {
        public bool IsActive { get; set; }
    }

    /// <summary>Admin'in kendi profil bilgilerini güncellemesi.</summary>
    public class AdminUserUpdateProfileDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>Admin'in kendi şifresini değiştirmesi.</summary>
    public class AdminUserChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>Genel sayfalı liste cevabı.</summary>
    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
