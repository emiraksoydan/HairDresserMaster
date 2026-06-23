namespace Entities.Concrete.Dto
{
    public class SocialStoryHighlightDto
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public int ItemCount { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SocialStoryHighlightDetailDto : SocialStoryHighlightDto
    {
        public List<SocialStoryHighlightItemDto> Items { get; set; } = new();
    }

    public class SocialStoryHighlightItemDto
    {
        public Guid Id { get; set; }
        public Guid? SourceStoryId { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int? DurationSec { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateSocialStoryHighlightRequest
    {
        public Guid ProfileId { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<Guid>? StoryIds { get; set; }
    }

    public class UpdateSocialStoryHighlightRequest
    {
        public string? Title { get; set; }
        public int? SortOrder { get; set; }
    }

    public class AddSocialStoryHighlightItemsRequest
    {
        public List<Guid> StoryIds { get; set; } = new();
    }
}
