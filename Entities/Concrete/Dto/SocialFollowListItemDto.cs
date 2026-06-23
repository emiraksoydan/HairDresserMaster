namespace Entities.Concrete.Dto
{
    public class SocialFollowListItemDto
    {
        public Guid FollowId { get; set; }
        public DateTime FollowedAt { get; set; }
        public SocialProfileDto Profile { get; set; } = null!;
    }
}
