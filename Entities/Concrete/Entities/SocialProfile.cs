using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class SocialProfile : IEntity
    {
        public Guid Id { get; set; }
        public SocialProfileOwnerType OwnerType { get; set; }
        /// <summary>Customer → User.Id; FreeBarber → FreeBarber.Id; BarberStore → BarberStore.Id</summary>
        public Guid OwnerId { get; set; }
        /// <summary>Profili yöneten kullanıcı (engelleme/şikayet için).</summary>
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? ExternalUrl { get; set; }
        public Guid? AvatarImageId { get; set; }
        public Guid? CoverImageId { get; set; }
        public SocialDmPolicy DmPolicy { get; set; } = SocialDmPolicy.Everyone;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsPrivate { get; set; }
        public SocialContentStatus Status { get; set; } = SocialContentStatus.Active;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
