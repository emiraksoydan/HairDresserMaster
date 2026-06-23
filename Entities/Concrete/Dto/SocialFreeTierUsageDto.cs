namespace Entities.Concrete.Dto

{

    public class SocialFreeTierUsageDto

    {

        public bool AppliesLimits { get; set; }

        public int StoryDailyLimit { get; set; }

        public int StoryUsedToday { get; set; }

        public int StoryRemainingToday { get; set; }

        public int HighlightDailyLimit { get; set; }

        public int HighlightUsedToday { get; set; }

        public int HighlightRemainingToday { get; set; }

        public int PhotoDailyLimit { get; set; }

        public int PhotoUsedToday { get; set; }

        public int PhotoRemainingToday { get; set; }

        public int CarouselDailyLimit { get; set; }

        public int CarouselUsedToday { get; set; }

        public int CarouselRemainingToday { get; set; }

        public int VideoDailyLimit { get; set; }

        public int VideoUsedToday { get; set; }

        public int VideoRemainingToday { get; set; }

        public int ReelDailyLimit { get; set; }

        public int ReelUsedToday { get; set; }

        public int ReelRemainingToday { get; set; }

        public int MaxPinnedPosts { get; set; }

    }

}

