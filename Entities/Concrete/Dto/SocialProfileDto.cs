using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class SocialProfileDto
    {
        public Guid Id { get; set; }
        public SocialProfileOwnerType OwnerType { get; set; }
        public Guid OwnerId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? ExternalUrl { get; set; }
        public string? AvatarUrl { get; set; }
        public string? CoverUrl { get; set; }
        public SocialDmPolicy DmPolicy { get; set; }
        public bool HasActiveStory { get; set; }
        public bool IsMuted { get; set; }
        public int MutualFollowerCount { get; set; }
        public bool? IsAvailable { get; set; }
        public int? TotalPostViews { get; set; }
        public int? HighlightCount { get; set; }
        public int? ReelCount { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsPrivate { get; set; }
        public int PostCount { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsOwnProfile { get; set; }
        /// <summary>Kendi profilinde: müşteri adı, panel adı veya dükkan adı.</summary>
        public string? OwnerDisplayName { get; set; }
        /// <summary>Kendi profilinde: müşteri/berber no veya dükkan no.</summary>
        public string? OwnerNumber { get; set; }
        public BarberType? OwnerBarberType { get; set; }
        public double? AverageRating { get; set; }
        public int? RatingCount { get; set; }
        public double? DistanceKm { get; set; }
    }
}
