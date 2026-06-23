namespace Entities.Concrete.Dto
{
    public class SocialLimitsDto
    {
        public int StoryVideoMaxDurationSec { get; set; }
        public int StoryLifetimeHours { get; set; }
        public int PostVideoMaxDurationSec { get; set; }
        public int PostCarouselMaxImages { get; set; }
        public int HighlightMaxItemsPerHighlight { get; set; }
        public int CommentMaxLength { get; set; }
        public int FreeTierMaxStoryPublications { get; set; }
        public int FreeTierMaxHighlights { get; set; }
        public int FreeTierMaxPhotoPosts { get; set; }
        public int FreeTierMaxCarouselPosts { get; set; }
        public int FreeTierMaxVideoPosts { get; set; }
        public int FreeTierMaxReels { get; set; }
        public int MaxPinnedPostsPerProfile { get; set; }
    }
}
