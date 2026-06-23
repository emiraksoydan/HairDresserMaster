using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class SocialPostMedia : IEntity
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public SocialPost Post { get; set; } = null!;
        public int SortOrder { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int? DurationSec { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
