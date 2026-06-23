using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class SocialLike : IEntity
    {
        public Guid Id { get; set; }
        public SocialLikeTargetType TargetType { get; set; }
        public Guid TargetId { get; set; }
        public Guid ProfileId { get; set; }
        public SocialProfile Profile { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
