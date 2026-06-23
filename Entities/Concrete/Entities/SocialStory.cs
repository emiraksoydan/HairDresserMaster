using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class SocialStory : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public SocialProfile Profile { get; set; } = null!;
        public string MediaUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int? DurationSec { get; set; }
        public DateTime ExpiresAt { get; set; }
        public SocialContentStatus Status { get; set; } = SocialContentStatus.Active;
        public DateTime CreatedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
    }
}
