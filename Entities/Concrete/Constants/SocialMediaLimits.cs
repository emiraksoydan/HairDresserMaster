using Entities.Concrete.Dto;

namespace Entities.Concrete.Constants
{
    /// <summary>
    /// Sosyal medya yükleme sınırları — backend doğrulama ve istemci config endpoint'i için tek kaynak.
    /// </summary>
    public static class SocialMediaLimits
    {
        /// <summary>Hikaye videosu üst süre (saniye).</summary>
        public const int StoryVideoMaxDurationSec = 15;

        /// <summary>Hikaye yaşam süresi (saat).</summary>
        public const int StoryLifetimeHours = 24;

        /// <summary>Gönderi / reels video üst süre (saniye).</summary>
        public const int PostVideoMaxDurationSec = 60;

        /// <summary>Carousel gönderi görsel üst sınırı.</summary>
        public const int PostCarouselMaxImages = 10;

        /// <summary>Tek bir öne çıkanda en fazla hikaye/medya öğesi.</summary>
        public const int HighlightMaxItemsPerHighlight = 100;

        /// <summary>Yorum ve gönderi açıklaması üst karakter sınırı (Instagram uyumlu).</summary>
        public const int CommentMaxLength = 2200;

        /// <summary>Abonelik yokken FreeBarber/BarberStore: tür başına günlük limit (TR günü).</summary>
        public const int FreeTierMaxStoryPublications = 1;
        public const int MaxPinnedPostsPerProfile = 3;
        public const int FreeTierMaxHighlights = 1;
        public const int FreeTierMaxPhotoPosts = 1;
        public const int FreeTierMaxCarouselPosts = 1;
        public const int FreeTierMaxVideoPosts = 1;
        public const int FreeTierMaxReels = 1;

        public static SocialLimitsDto ToDto() => new()
        {
            StoryVideoMaxDurationSec = StoryVideoMaxDurationSec,
            StoryLifetimeHours = StoryLifetimeHours,
            PostVideoMaxDurationSec = PostVideoMaxDurationSec,
            PostCarouselMaxImages = PostCarouselMaxImages,
            HighlightMaxItemsPerHighlight = HighlightMaxItemsPerHighlight,
            CommentMaxLength = CommentMaxLength,
            FreeTierMaxStoryPublications = FreeTierMaxStoryPublications,
            FreeTierMaxHighlights = FreeTierMaxHighlights,
            FreeTierMaxPhotoPosts = FreeTierMaxPhotoPosts,
            FreeTierMaxCarouselPosts = FreeTierMaxCarouselPosts,
            FreeTierMaxVideoPosts = FreeTierMaxVideoPosts,
            FreeTierMaxReels = FreeTierMaxReels,
            MaxPinnedPostsPerProfile = MaxPinnedPostsPerProfile,
        };
    }
}
