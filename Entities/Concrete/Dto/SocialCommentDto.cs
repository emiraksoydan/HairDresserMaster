namespace Entities.Concrete.Dto
{
    public class SocialCommentDto
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public SocialProfileDto Profile { get; set; } = null!;
        public Guid? ParentCommentId { get; set; }
        public string Text { get; set; } = string.Empty;
        public int LikeCount { get; set; }
        public int ReplyCount { get; set; }
        public bool IsLiked { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateSocialCommentDto
    {
        public Guid PostId { get; set; }
        public string Text { get; set; } = string.Empty;
        public Guid? ParentCommentId { get; set; }
    }
}
