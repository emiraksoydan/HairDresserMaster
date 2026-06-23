namespace Entities.Concrete.Dto
{
    public class SocialStoryDto
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int? DurationSec { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOwnStory { get; set; }
        public int? ViewCount { get; set; }
        public int LikeCount { get; set; }
        public bool IsLiked { get; set; }
    }

    public class SocialStoryGroupDto
    {
        public SocialProfileDto Profile { get; set; } = null!;
        public List<SocialStoryDto> Stories { get; set; } = new();
        public bool HasUnviewed { get; set; }
    }
}
