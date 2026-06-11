using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ManuelBarberAdminGetDto : IDto
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public string StoreName { get; set; } = null!;
        public string? StoreNo { get; set; }
        /// <summary>Bağlı salonun ilk panel fotoğrafı.</summary>
        public string? StoreImageUrl { get; set; }
        public Guid BarberStoreOwnerId { get; set; }
        /// <summary>Salon sahibinin kullanıcı id'si (frontend ownerUserId).</summary>
        public Guid OwnerUserId { get; set; }
        /// <summary>Salon sahibinin adı.</summary>
        public string? OwnerName { get; set; }
        public string FullName { get; set; } = null!;
        public string? ProfileImageUrl { get; set; }
        /// <summary>Manuel berberin fotoğrafı (frontend imageUrl).</summary>
        public string? ImageUrl { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
