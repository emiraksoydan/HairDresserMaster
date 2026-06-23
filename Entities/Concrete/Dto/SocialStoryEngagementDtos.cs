namespace Entities.Concrete.Dto
{
    public class SocialRecordStoryViewDto
    {
        public Guid ProfileId { get; set; }
    }

    public class CreateSocialStoryReplyDto
    {
        public Guid ProfileId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class SocialStoryViewerDto
    {
        public Guid ViewId { get; set; }
        public SocialProfileDto Profile { get; set; } = null!;
        public DateTime ViewedAt { get; set; }
        public bool IsLiked { get; set; }
    }
}
