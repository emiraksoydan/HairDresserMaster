using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public enum SocialArchivedKind
    {
        Post = 0,
        Story = 1,
        Highlight = 2,
        HighlightItem = 3,
    }

    public class SocialArchivedItemDto
    {
        public SocialArchivedKind Kind { get; set; }
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string? ParentTitle { get; set; }
        public string? Title { get; set; }
        public string? ThumbUrl { get; set; }
        public SocialPostType? PostType { get; set; }
        public DateTime RemovedAt { get; set; }
    }

    public class SocialArchivedContentDto
    {
        public List<SocialArchivedItemDto> Items { get; set; } = new();
    }

    public class SocialRestoreArchivedRequest
    {
        public SocialArchivedKind Kind { get; set; }
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
    }
}
