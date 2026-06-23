using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class SocialPost : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public SocialProfile Profile { get; set; } = null!;
        public string? Caption { get; set; }
        public SocialPostType Type { get; set; }
        public int ViewCount { get; set; }
        public SocialContentStatus Status { get; set; } = SocialContentStatus.Active;
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
    }
}
