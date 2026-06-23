using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class SocialStoryHighlight : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public SocialProfile Profile { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public int SortOrder { get; set; }
        public SocialContentStatus Status { get; set; } = SocialContentStatus.Active;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
        public ICollection<SocialStoryHighlightItem> Items { get; set; } = new List<SocialStoryHighlightItem>();
    }
}
