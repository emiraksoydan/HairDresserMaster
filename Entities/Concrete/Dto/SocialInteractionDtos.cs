using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class SocialLikeRequest
    {
        public Guid ProfileId { get; set; }
        public SocialLikeTargetType TargetType { get; set; }
        public Guid TargetId { get; set; }
    }

    public class SocialSaveRequest
    {
        public Guid ProfileId { get; set; }
        public Guid PostId { get; set; }
    }

    public class SocialRecordPostViewDto
    {
        public Guid ProfileId { get; set; }
    }

    public class SocialFollowRequest
    {
        public Guid FollowerProfileId { get; set; }
        public Guid FollowingProfileId { get; set; }
    }

    public class CreateSocialCommentWithProfileDto : CreateSocialCommentDto
    {
        public Guid ProfileId { get; set; }
    }

    public class UpdateSocialCommentWithProfileDto
    {
        public Guid ProfileId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class DeleteSocialCommentWithProfileDto
    {
        public Guid ProfileId { get; set; }
    }
}
