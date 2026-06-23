using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class SocialFollow : IEntity
    {
        public Guid Id { get; set; }
        public Guid FollowerProfileId { get; set; }
        public SocialProfile FollowerProfile { get; set; } = null!;
        public Guid FollowingProfileId { get; set; }
        public SocialProfile FollowingProfile { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
