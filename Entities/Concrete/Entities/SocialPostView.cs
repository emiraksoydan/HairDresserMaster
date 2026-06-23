using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class SocialPostView : IEntity
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public SocialPost Post { get; set; } = null!;
        public Guid ProfileId { get; set; }
        public SocialProfile Profile { get; set; } = null!;
        public DateTime ViewedAt { get; set; }
    }
}
