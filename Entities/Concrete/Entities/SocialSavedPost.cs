using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class SocialSavedPost : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public SocialProfile Profile { get; set; } = null!;
        public Guid PostId { get; set; }
        public SocialPost Post { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
