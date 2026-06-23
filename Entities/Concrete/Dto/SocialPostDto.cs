using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class SocialPostDto
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public SocialProfileDto Profile { get; set; } = null!;
        public string? Caption { get; set; }
        public SocialPostType Type { get; set; }
        public List<SocialPostMediaDto> Media { get; set; } = new();
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLiked { get; set; }
        public bool IsSaved { get; set; }
        public bool IsOwnPost { get; set; }
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SavedAt { get; set; }
        public Guid? SavedEntryId { get; set; }
    }
}
