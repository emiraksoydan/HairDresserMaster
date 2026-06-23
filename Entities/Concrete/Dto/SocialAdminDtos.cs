using Entities.Concrete.Enums;



namespace Entities.Concrete.Dto

{

    public class SocialPostAdminDto

    {

        public Guid Id { get; set; }

        public Guid ProfileId { get; set; }

        public string ProfileUsername { get; set; } = string.Empty;

        public SocialProfileOwnerType OwnerType { get; set; }

        public string? Caption { get; set; }

        public SocialPostType Type { get; set; }

        public SocialContentStatus Status { get; set; }

        public int ViewCount { get; set; }

        public int LikeCount { get; set; }

        public int CommentCount { get; set; }

        public int MediaCount { get; set; }

        public string? ThumbnailUrl { get; set; }

        public List<SocialPostMediaAdminDto> Media { get; set; } = new();

        public DateTime CreatedAt { get; set; }

    }



    public class SocialPostMediaAdminDto

    {

        public string MediaUrl { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public int? DurationSec { get; set; }

        public bool IsVideo { get; set; }

    }



    public class SocialStoryAdminDto

    {

        public Guid Id { get; set; }

        public Guid ProfileId { get; set; }

        public string ProfileUsername { get; set; } = string.Empty;

        public SocialProfileOwnerType OwnerType { get; set; }

        public SocialContentStatus Status { get; set; }

        public string MediaUrl { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public int? DurationSec { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; }

    }



    public class SocialProfileAdminDto

    {

        public Guid Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public SocialProfileOwnerType OwnerType { get; set; }

        public Guid OwnerId { get; set; }

        public Guid UserId { get; set; }

        public string? Bio { get; set; }

        public string? AvatarUrl { get; set; }

        public SocialContentStatus Status { get; set; }

        public int PostCount { get; set; }

        public int FollowerCount { get; set; }

        public int FollowingCount { get; set; }

        public string? OwnerDisplayName { get; set; }

        public string? OwnerNumber { get; set; }

        public DateTime CreatedAt { get; set; }

    }



    public class SocialCommentAdminDto

    {

        public Guid Id { get; set; }

        public Guid PostId { get; set; }

        public string? PostCaption { get; set; }

        public Guid ProfileId { get; set; }

        public string ProfileUsername { get; set; } = string.Empty;

        public SocialProfileOwnerType OwnerType { get; set; }

        public Guid? ParentCommentId { get; set; }

        public string Text { get; set; } = string.Empty;

        public SocialContentStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

    }



    public class SocialStoryHighlightItemAdminDto

    {

        public Guid Id { get; set; }

        public string MediaUrl { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public int? DurationSec { get; set; }

        public int SortOrder { get; set; }

        public SocialContentStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

    }



    public class SocialStoryHighlightAdminDto

    {

        public Guid Id { get; set; }

        public Guid ProfileId { get; set; }

        public string ProfileUsername { get; set; } = string.Empty;

        public SocialProfileOwnerType OwnerType { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? CoverUrl { get; set; }

        public int ItemCount { get; set; }

        public int SortOrder { get; set; }

        public SocialContentStatus Status { get; set; }

        public List<SocialStoryHighlightItemAdminDto> Items { get; set; } = new();

        public DateTime CreatedAt { get; set; }

    }

}


