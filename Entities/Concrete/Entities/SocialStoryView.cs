using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class SocialStoryView : IEntity
    {
        public Guid Id { get; set; }
        public Guid StoryId { get; set; }
        public SocialStory Story { get; set; } = null!;
        public Guid ProfileId { get; set; }
        public SocialProfile Profile { get; set; } = null!;
        public DateTime ViewedAt { get; set; }
    }
}
