using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class SocialComment : IEntity
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public SocialPost Post { get; set; } = null!;
        public Guid ProfileId { get; set; }
        public SocialProfile Profile { get; set; } = null!;
        public Guid? ParentCommentId { get; set; }
        public string Text { get; set; } = string.Empty;
        public SocialContentStatus Status { get; set; } = SocialContentStatus.Active;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
