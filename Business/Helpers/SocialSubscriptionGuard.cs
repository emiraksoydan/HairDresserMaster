using Core.Utilities.Results;
using Core.Utilities.Helpers;
using DataAccess.Abstract;
using Entities.Concrete.Constants;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;

namespace Business.Helpers
{
    /// <summary>
    /// FreeBarber / BarberStore kullanıcıları için abonelik yokken sosyal içerik üst sınırları.
    /// Günlük sayaçlar Türkiye (Europe/Istanbul) gün başlangıcına göre sıfırlanır.
    /// Customer sosyal içerik limitine tabi değildir.
    /// </summary>
    public class SocialSubscriptionGuard(
        IUserDal userDal,
        ISocialProfileDal socialProfileDal,
        ISocialPostDal socialPostDal,
        ISocialStoryDal socialStoryDal,
        ISocialStoryHighlightDal highlightDal)
    {
        public async Task<IResult?> EnsureCanCreateStoryAsync(Guid userId)
        {
            if (!await RequiresFreeTierLimitAsync(userId)) return null;

            var profileIds = await GetProfileIdsAsync(userId);
            if (profileIds.Count == 0) return null;

            var dayStartUtc = TimeZoneHelper.GetTurkeyDayStartUtc();
            var count = (await socialStoryDal.GetAll(s =>
                    profileIds.Contains(s.ProfileId) &&
                    s.Status != SocialContentStatus.Hidden &&
                    s.CreatedAt >= dayStartUtc))
                .Count;

            if (count >= SocialMediaLimits.FreeTierMaxStoryPublications)
                return new ErrorResult(SocialErrorCodes.SubscriptionStoryLimit);

            return null;
        }

        public async Task<SocialFreeTierUsageDto> GetFreeTierUsageAsync(Guid userId)
        {
            var applies = await RequiresFreeTierLimitAsync(userId);
            var dto = new SocialFreeTierUsageDto
            {
                AppliesLimits = applies,
                StoryDailyLimit = SocialMediaLimits.FreeTierMaxStoryPublications,
                HighlightDailyLimit = SocialMediaLimits.FreeTierMaxHighlights,
                PhotoDailyLimit = SocialMediaLimits.FreeTierMaxPhotoPosts,
                CarouselDailyLimit = SocialMediaLimits.FreeTierMaxCarouselPosts,
                VideoDailyLimit = SocialMediaLimits.FreeTierMaxVideoPosts,
                ReelDailyLimit = SocialMediaLimits.FreeTierMaxReels,
                MaxPinnedPosts = SocialMediaLimits.MaxPinnedPostsPerProfile,
            };

            if (!applies)
            {
                dto.StoryRemainingToday = dto.StoryDailyLimit;
                dto.HighlightRemainingToday = dto.HighlightDailyLimit;
                dto.PhotoRemainingToday = dto.PhotoDailyLimit;
                dto.CarouselRemainingToday = dto.CarouselDailyLimit;
                dto.VideoRemainingToday = dto.VideoDailyLimit;
                dto.ReelRemainingToday = dto.ReelDailyLimit;
                return dto;
            }

            var profileIds = await GetProfileIdsAsync(userId);
            if (profileIds.Count == 0)
            {
                dto.StoryRemainingToday = dto.StoryDailyLimit;
                dto.HighlightRemainingToday = dto.HighlightDailyLimit;
                dto.PhotoRemainingToday = dto.PhotoDailyLimit;
                dto.CarouselRemainingToday = dto.CarouselDailyLimit;
                dto.VideoRemainingToday = dto.VideoDailyLimit;
                dto.ReelRemainingToday = dto.ReelDailyLimit;
                return dto;
            }

            var dayStartUtc = TimeZoneHelper.GetTurkeyDayStartUtc();

            dto.StoryUsedToday = (await socialStoryDal.GetAll(s =>
                    profileIds.Contains(s.ProfileId) &&
                    s.Status != SocialContentStatus.Hidden &&
                    s.CreatedAt >= dayStartUtc))
                .Count;
            dto.StoryRemainingToday = Math.Max(0, dto.StoryDailyLimit - dto.StoryUsedToday);

            dto.HighlightUsedToday = (await highlightDal.GetAll(h =>
                    profileIds.Contains(h.ProfileId) &&
                    h.Status != SocialContentStatus.Hidden &&
                    h.CreatedAt >= dayStartUtc))
                .Count;
            dto.HighlightRemainingToday = Math.Max(0, dto.HighlightDailyLimit - dto.HighlightUsedToday);

            var postsToday = await socialPostDal.GetAll(p =>
                profileIds.Contains(p.ProfileId) &&
                p.Status != SocialContentStatus.Hidden &&
                p.CreatedAt >= dayStartUtc);

            dto.PhotoUsedToday = postsToday.Count(p => p.Type == SocialPostType.Photo);
            dto.PhotoRemainingToday = Math.Max(0, dto.PhotoDailyLimit - dto.PhotoUsedToday);

            dto.CarouselUsedToday = postsToday.Count(p => p.Type == SocialPostType.Carousel);
            dto.CarouselRemainingToday = Math.Max(0, dto.CarouselDailyLimit - dto.CarouselUsedToday);

            dto.VideoUsedToday = postsToday.Count(p => p.Type == SocialPostType.Video);
            dto.VideoRemainingToday = Math.Max(0, dto.VideoDailyLimit - dto.VideoUsedToday);

            dto.ReelUsedToday = postsToday.Count(p => p.Type == SocialPostType.Reel);
            dto.ReelRemainingToday = Math.Max(0, dto.ReelDailyLimit - dto.ReelUsedToday);

            return dto;
        }

        public async Task<IResult?> EnsureCanCreateHighlightAsync(Guid userId)
        {
            if (!await RequiresFreeTierLimitAsync(userId)) return null;

            var profileIds = await GetProfileIdsAsync(userId);
            if (profileIds.Count == 0) return null;

            var dayStartUtc = TimeZoneHelper.GetTurkeyDayStartUtc();
            var count = (await highlightDal.GetAll(h =>
                    profileIds.Contains(h.ProfileId) &&
                    h.Status != SocialContentStatus.Hidden &&
                    h.CreatedAt >= dayStartUtc))
                .Count;

            if (count >= SocialMediaLimits.FreeTierMaxHighlights)
                return new ErrorResult(SocialErrorCodes.SubscriptionHighlightLimit);

            return null;
        }

        public async Task<IResult?> EnsureCanCreatePostAsync(Guid userId, SocialPostType type)
        {
            if (!await RequiresFreeTierLimitAsync(userId)) return null;

            var profileIds = await GetProfileIdsAsync(userId);
            if (profileIds.Count == 0) return null;

            var dayStartUtc = TimeZoneHelper.GetTurkeyDayStartUtc();
            var posts = await socialPostDal.GetAll(p =>
                profileIds.Contains(p.ProfileId) &&
                p.Status != SocialContentStatus.Hidden &&
                p.CreatedAt >= dayStartUtc);

            var limit = type switch
            {
                SocialPostType.Photo => SocialMediaLimits.FreeTierMaxPhotoPosts,
                SocialPostType.Carousel => SocialMediaLimits.FreeTierMaxCarouselPosts,
                SocialPostType.Video => SocialMediaLimits.FreeTierMaxVideoPosts,
                SocialPostType.Reel => SocialMediaLimits.FreeTierMaxReels,
                _ => 1,
            };

            var count = posts.Count(p => p.Type == type);
            if (count >= limit)
            {
                return type switch
                {
                    SocialPostType.Reel => new ErrorResult(SocialErrorCodes.SubscriptionReelLimit),
                    SocialPostType.Carousel => new ErrorResult(SocialErrorCodes.SubscriptionCarouselLimit),
                    SocialPostType.Video => new ErrorResult(SocialErrorCodes.SubscriptionVideoLimit),
                    _ => new ErrorResult(SocialErrorCodes.SubscriptionPostLimit),
                };
            }

            return null;
        }

        private async Task<bool> RequiresFreeTierLimitAsync(Guid userId)
        {
            var user = await userDal.Get(u => u.Id == userId);
            if (user == null) return false;

            if (user.UserType is UserType.Customer) return false;

            if (user.UserType is UserType.FreeBarber or UserType.BarberStore)
            {
                var active = user.SubscriptionEndDate.HasValue &&
                             user.SubscriptionEndDate.Value > DateTime.UtcNow;
                return !active;
            }

            return false;
        }

        private async Task<List<Guid>> GetProfileIdsAsync(Guid userId)
        {
            var profiles = await socialProfileDal.GetByUserIdAsync(userId);
            return profiles
                .Where(p => p.Status == SocialContentStatus.Active)
                .Select(p => p.Id)
                .ToList();
        }
    }
}
