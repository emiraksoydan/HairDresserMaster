namespace Entities.Concrete.Dto
{
    public class SocialNotificationPayloadDto
    {
        public string Kind { get; set; } = string.Empty;
        public Guid? PostId { get; set; }
        public Guid? StoryId { get; set; }
        public Guid? CommentId { get; set; }
        public Guid? ParentCommentId { get; set; }
        public Guid ActorProfileId { get; set; }
        public string ActorUsername { get; set; } = string.Empty;
        public string? ActorAvatarUrl { get; set; }
        public Guid? TargetProfileId { get; set; }
    }
}
