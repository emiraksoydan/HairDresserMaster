using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class SocialStoryHighlightItem : IEntity
    {
        public Guid Id { get; set; }
        public Guid HighlightId { get; set; }
        public SocialStoryHighlight Highlight { get; set; } = null!;
        public Guid? SourceStoryId { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int? DurationSec { get; set; }
        public int SortOrder { get; set; }
        public SocialContentStatus Status { get; set; } = SocialContentStatus.Active;
        public DateTime CreatedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
    }
}
