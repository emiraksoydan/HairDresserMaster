using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Helpers;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using Core.Utilities.Storage;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Constants;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Business.Concrete
{
    public class SocialStoryManager(
        ISocialProfileDal socialProfileDal,
        ISocialStoryDal socialStoryDal,
        ISocialStoryViewDal socialStoryViewDal,
        ISocialStoryReplyDal socialStoryReplyDal,
        ISocialLikeDal socialLikeDal,
        ISocialFollowDal socialFollowDal,
        IChatService chatService,
        IImageDal imageDal,
        IBlobStorageService blobStorageService,
        BlockedHelper blockedHelper,
        SocialSubscriptionGuard socialSubscriptionGuard,
        AppointmentSocialShareGuard appointmentSocialShareGuard,
        SocialProfileOwnerEnricher socialProfileOwnerEnricher,
        SocialNotificationHelper socialNotificationHelper,
        ILogger<SocialStoryManager> logger) : ISocialStoryService
    {
        private const string MediaContainer = "social-media";

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<Guid>> CreateStoryAsync(
            Guid userId,
            Guid profileId,
            IFormFile file,
            int? durationSec,
            Guid? appointmentId = null)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null)
                return new ErrorDataResult<Guid>(SocialErrorCodes.ProfileNotFound);
            if (profile.UserId != userId)
                return new ErrorDataResult<Guid>(SocialErrorCodes.ProfileNoPermission);

            if (appointmentId.HasValue && appointmentId.Value != Guid.Empty)
            {
                var appointmentGuard = await appointmentSocialShareGuard.EnsureCanLinkShareAsync(
                    userId, appointmentId.Value);
                if (appointmentGuard != null)
                    return new ErrorDataResult<Guid>(appointmentGuard.Message);
            }

            var subLimit = await socialSubscriptionGuard.EnsureCanCreateStoryAsync(userId);
            if (subLimit != null)
                return new ErrorDataResult<Guid>(subLimit.Message);

            if (file == null || file.Length == 0)
                return new ErrorDataResult<Guid>(SocialErrorCodes.StoryMediaRequired);

            var isVideo = file.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true;
            if (isVideo && durationSec is > SocialMediaLimits.StoryVideoMaxDurationSec)
                return new ErrorDataResult<Guid>(SocialErrorCodes.StoryVideoMaxDuration);

            var uploadResult = await UploadMediaAsync(file, isVideo);
            if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.Data))
            {
                var err = uploadResult.Message;
                if (string.IsNullOrWhiteSpace(err) ||
                    err.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    err.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    err = SocialErrorCodes.MediaUploadFailed;
                return new ErrorDataResult<Guid>(err);
            }

            var now = DateTime.UtcNow;
            var story = new SocialStory
            {
                Id = Guid.NewGuid(),
                ProfileId = profileId,
                MediaUrl = uploadResult.Data!,
                DurationSec = isVideo ? durationSec : null,
                ExpiresAt = now.AddHours(SocialMediaLimits.StoryLifetimeHours),
                Status = SocialContentStatus.Active,
                CreatedAt = now,
            };

            await socialStoryDal.Add(story);

            if (appointmentId.HasValue && appointmentId.Value != Guid.Empty)
            {
                await appointmentSocialShareGuard.RecordShareAsync(
                    userId,
                    appointmentId.Value,
                    AppointmentSocialShareContentType.Story,
                    story.Id);
            }

            return new SuccessDataResult<Guid>(story.Id, "Hikaye paylaşıldı.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialStoryGroupDto>>> GetStoryFeedAsync(Guid userId)
        {
            var feedProfileIds = await BuildFeedProfileIdsAsync(userId);
            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            var stories = await socialStoryDal.GetActiveByProfileIdsAsync(feedProfileIds, blocked);

            var viewerProfiles = await socialProfileDal.GetByUserIdAsync(userId);
            var viewerProfileIds = viewerProfiles.Select(p => p.Id).ToList();

            var grouped = new List<SocialStoryGroupDto>();
            foreach (var g in stories.GroupBy(s => s.ProfileId))
            {
                var prof = g.First().Profile;
                var stats = await socialProfileDal.GetStatsAsync(
                    prof.Id,
                    viewerProfileIds.Count > 0 ? viewerProfileIds : null);

                var profileDto = await MapProfileForViewerListAsync(prof, stats, userId);

                grouped.Add(new SocialStoryGroupDto
                {
                    Profile = profileDto,
                    Stories = g.Select(s => MapStory(s, userId)).ToList(),
                    HasUnviewed = true,
                });
            }

            foreach (var group in grouped)
            {
                await EnrichOwnStoryViewCountsAsync(group.Stories);
                await EnrichStoryEngagementAsync(group.Stories, viewerProfileIds);
            }

            grouped = grouped
                .OrderByDescending(g => g.Profile.IsOwnProfile)
                .ThenByDescending(g => g.Stories.Max(s => s.CreatedAt))
                .ToList();

            return new SuccessDataResult<List<SocialStoryGroupDto>>(grouped);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialStoryDto>>> GetProfileStoriesAsync(Guid userId, Guid profileId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorDataResult<List<SocialStoryDto>>(SocialErrorCodes.ProfileNotFound);

            if (await blockedHelper.HasBlockBetweenAsync(userId, profile.UserId))
                return new ErrorDataResult<List<SocialStoryDto>>(SocialErrorCodes.ProfileNoAccess);

            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            var stories = await socialStoryDal.GetActiveByProfileIdAsync(profileId, blocked);
            var viewerProfiles = await socialProfileDal.GetByUserIdAsync(userId);
            var viewerProfileIds = viewerProfiles.Select(p => p.Id).ToList();
            var dtos = stories.Select(s => MapStory(s, userId)).ToList();
            await EnrichOwnStoryViewCountsAsync(dtos);
            await EnrichStoryEngagementAsync(dtos, viewerProfileIds);
            return new SuccessDataResult<List<SocialStoryDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeleteStoryAsync(Guid userId, Guid storyId)
        {
            var story = await socialStoryDal.Get(s => s.Id == storyId);
            if (story == null)
                return new ErrorResult(SocialErrorCodes.StoryNotFound);

            var profile = await socialProfileDal.Get(p => p.Id == story.ProfileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.StoryNoPermission);

            story.Status = SocialContentStatus.Removed;
            story.RemovedAt = DateTime.UtcNow;
            await socialStoryDal.Update(story);
            return new SuccessResult("Hikaye silindi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> RecordViewAsync(Guid userId, Guid profileId, Guid storyId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileInvalid);

            var story = await socialStoryDal.Get(s => s.Id == storyId);
            if (story == null || story.Status != SocialContentStatus.Active || story.ExpiresAt <= DateTime.UtcNow)
                return new ErrorResult(SocialErrorCodes.StoryNotFound);

            var storyOwner = await socialProfileDal.Get(p => p.Id == story.ProfileId);
            if (storyOwner != null && await blockedHelper.HasBlockBetweenAsync(userId, storyOwner.UserId))
                return new ErrorResult(SocialErrorCodes.StoryNoAccess);

            if (story.ProfileId == profileId)
                return new SuccessResult();

            await socialStoryViewDal.TryAddViewAsync(storyId, profileId);
            return new SuccessResult();
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialStoryViewerDto>>> GetViewersAsync(
            Guid userId, Guid storyId, DateTime? beforeUtc, Guid? beforeId, int limit = 50)
        {
            var story = await socialStoryDal.Get(s => s.Id == storyId);
            if (story == null)
                return new ErrorDataResult<List<SocialStoryViewerDto>>(SocialErrorCodes.StoryNotFound);

            var ownerProfile = await socialProfileDal.Get(p => p.Id == story.ProfileId);
            if (ownerProfile == null || ownerProfile.UserId != userId)
                return new ErrorDataResult<List<SocialStoryViewerDto>>(SocialErrorCodes.StoryNoPermission);

            limit = Math.Clamp(limit, 1, 100);
            var views = await socialStoryViewDal.GetViewersAsync(storyId, beforeUtc, beforeId, limit);
            var likes = await socialLikeDal.GetAll(l =>
                l.TargetType == SocialLikeTargetType.Story && l.TargetId == storyId);
            var likedProfileIds = likes.Select(l => l.ProfileId).ToHashSet();

            var result = new List<SocialStoryViewerDto>();
            foreach (var view in views)
            {
                var prof = view.Profile ?? await socialProfileDal.Get(p => p.Id == view.ProfileId);
                if (prof == null) continue;

                var stats = await socialProfileDal.GetStatsAsync(prof.Id, null);
                var profileDto = await MapProfileForViewerListAsync(prof, stats, userId);
                result.Add(new SocialStoryViewerDto
                {
                    ViewId = view.Id,
                    Profile = profileDto,
                    ViewedAt = view.ViewedAt,
                    IsLiked = likedProfileIds.Contains(view.ProfileId),
                });
            }

            return new SuccessDataResult<List<SocialStoryViewerDto>>(result);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> ReplyAsync(Guid userId, Guid storyId, CreateSocialStoryReplyDto dto)
        {
            var text = (dto.Text ?? string.Empty).Trim();
            if (text.Length == 0)
                return new ErrorResult(SocialErrorCodes.CommentEmpty);
            if (text.Length > SocialMediaLimits.CommentMaxLength)
                return new ErrorResult(SocialErrorCodes.CommentMaxLength);

            var profile = await socialProfileDal.Get(p => p.Id == dto.ProfileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileInvalid);

            var story = await socialStoryDal.Get(s => s.Id == storyId);
            if (story == null || story.Status != SocialContentStatus.Active || story.ExpiresAt <= DateTime.UtcNow)
                return new ErrorResult(SocialErrorCodes.StoryNotFound);

            var storyOwner = await socialProfileDal.Get(p => p.Id == story.ProfileId);
            if (storyOwner == null)
                return new ErrorResult(SocialErrorCodes.StoryNotFound);

            if (storyOwner.UserId == userId)
                return new ErrorResult(SocialErrorCodes.StoryNoAccess);

            if (await blockedHelper.HasBlockBetweenAsync(userId, storyOwner.UserId))
                return new ErrorResult(SocialErrorCodes.StoryNoAccess);

            var threadResult = await chatService.EnsureSocialThreadAsync(userId, dto.ProfileId, storyOwner.Id);
            if (!threadResult.Success || threadResult.Data == Guid.Empty)
                return new ErrorResult(threadResult.Message ?? SocialErrorCodes.StoryNoAccess);

            var messageResult = await chatService.SendFavoriteMessageAsync(userId, threadResult.Data, text);
            if (!messageResult.Success)
                return new ErrorResult(messageResult.Message ?? SocialErrorCodes.StoryNoAccess);

            await socialStoryReplyDal.Add(new SocialStoryReply
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                ProfileId = dto.ProfileId,
                Text = text,
                CreatedAt = DateTime.UtcNow,
            });

            await socialStoryViewDal.TryAddViewAsync(storyId, dto.ProfileId);
            await socialNotificationHelper.NotifyStoryRepliedAsync(profile, story, storyOwner, text);

            return new SuccessResult("Yanıt gönderildi.");
        }

        private async Task EnrichStoryEngagementAsync(
            IReadOnlyList<SocialStoryDto> stories,
            IReadOnlyList<Guid> viewerProfileIds)
        {
            if (stories.Count == 0) return;

            var storyIds = stories.Select(s => s.Id).ToList();
            var likeCounts = await socialLikeDal.GetLikeCountsAsync(SocialLikeTargetType.Story, storyIds);
            HashSet<Guid> likedStoryIds = new();
            if (viewerProfileIds.Count > 0)
            {
                likedStoryIds = await socialLikeDal.GetLikedTargetIdsAsync(
                    SocialLikeTargetType.Story, storyIds, viewerProfileIds[0]);
            }

            foreach (var dto in stories)
            {
                dto.LikeCount = likeCounts.GetValueOrDefault(dto.Id);
                dto.IsLiked = likedStoryIds.Contains(dto.Id);
            }
        }

        private async Task EnrichOwnStoryViewCountsAsync(IReadOnlyList<SocialStoryDto> stories)
        {
            foreach (var dto in stories.Where(s => s.IsOwnStory))
                dto.ViewCount = await socialStoryViewDal.GetViewCountAsync(dto.Id);
        }

        private async Task<List<Guid>> BuildFeedProfileIdsAsync(Guid userId)
        {
            var myProfiles = await socialProfileDal.GetByUserIdAsync(userId);
            var ids = myProfiles.Select(p => p.Id).ToHashSet();

            foreach (var mp in myProfiles)
            {
                var following = await socialFollowDal.GetFollowingProfileIdsAsync(mp.Id);
                foreach (var fid in following)
                    ids.Add(fid);
            }

            return ids.ToList();
        }

        private static SocialProfileDto MapProfile(
            SocialProfile prof,
            SocialProfileStatsDto stats,
            Guid viewerUserId)
        {
            return new SocialProfileDto
            {
                Id = prof.Id,
                OwnerType = prof.OwnerType,
                OwnerId = prof.OwnerId,
                UserId = prof.UserId,
                Username = prof.Username,
                Bio = prof.Bio,
                Latitude = prof.Latitude,
                Longitude = prof.Longitude,
                IsPrivate = prof.IsPrivate,
                PostCount = stats.PostCount,
                FollowerCount = stats.FollowerCount,
                FollowingCount = stats.FollowingCount,
                IsFollowing = stats.IsFollowing,
                IsOwnProfile = prof.UserId == viewerUserId,
            };
        }

        private async Task<SocialProfileDto> MapProfileForViewerListAsync(
            SocialProfile prof,
            SocialProfileStatsDto stats,
            Guid viewerUserId)
        {
            string? avatarUrl = null;
            if (prof.AvatarImageId.HasValue)
            {
                var img = await imageDal.Get(i => i.Id == prof.AvatarImageId.Value);
                avatarUrl = img?.ImageUrl;
            }

            var dto = MapProfile(prof, stats, viewerUserId);
            dto.AvatarUrl = avatarUrl;
            await socialProfileOwnerEnricher.EnrichOwnerMetaAsync(dto, prof);
            return dto;
        }

        private static SocialStoryDto MapStory(SocialStory story, Guid viewerUserId)
        {
            return new SocialStoryDto
            {
                Id = story.Id,
                ProfileId = story.ProfileId,
                MediaUrl = story.MediaUrl,
                ThumbnailUrl = story.ThumbnailUrl,
                DurationSec = story.DurationSec,
                ExpiresAt = story.ExpiresAt,
                CreatedAt = story.CreatedAt,
                IsOwnStory = story.Profile?.UserId == viewerUserId,
            };
        }

        private Task<IDataResult<string>> UploadMediaAsync(IFormFile file, bool isVideo) =>
            SocialMediaUploadHelper.UploadAsync(
                file, isVideo, MediaContainer, blobStorageService, logger);
    }
}
