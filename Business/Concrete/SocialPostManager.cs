using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Helpers;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using Core.Utilities.Storage;
using DataAccess.Abstract;
using Entities.Concrete.Constants;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Business.Concrete
{
    public class SocialPostManager(
        ISocialProfileDal socialProfileDal,
        ISocialPostDal socialPostDal,
        ISocialPostMediaDal socialPostMediaDal,
        ISocialLikeDal socialLikeDal,
        ISocialSavedPostDal socialSavedPostDal,
        ISocialPostViewDal socialPostViewDal,
        ISocialCommentDal socialCommentDal,
        ISocialFollowDal socialFollowDal,
        IBlobStorageService blobStorageService,
        BlockedHelper blockedHelper,
        SocialSubscriptionGuard socialSubscriptionGuard,
        AppointmentSocialShareGuard appointmentSocialShareGuard,
        SocialProfileOwnerEnricher socialProfileOwnerEnricher,
        ILogger<SocialPostManager> logger) : ISocialPostService
    {
        private const string MediaContainer = "social-media";

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<Guid>> CreatePostAsync(
            Guid userId,
            Guid profileId,
            string? caption,
            SocialPostType type,
            IReadOnlyList<IFormFile> files,
            int? durationSec,
            IReadOnlyList<int>? durationSecs = null,
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

            var subLimit = await socialSubscriptionGuard.EnsureCanCreatePostAsync(userId, type);
            if (subLimit != null)
                return new ErrorDataResult<Guid>(subLimit.Message);

            caption = caption?.Trim();
            if (!string.IsNullOrEmpty(caption) && caption.Length > SocialMediaLimits.CommentMaxLength)
                return new ErrorDataResult<Guid>(SocialErrorCodes.CaptionMaxLength);

            var fileList = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
            var validation = ValidateFilesForType(type, fileList, durationSec, durationSecs);
            if (!validation.Success)
                return new ErrorDataResult<Guid>(validation.Message);

            var now = DateTime.UtcNow;
            var post = new SocialPost
            {
                Id = Guid.NewGuid(),
                ProfileId = profileId,
                Caption = caption,
                Type = type,
                ViewCount = 0,
                Status = SocialContentStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await socialPostDal.Add(post);

            for (var i = 0; i < fileList.Count; i++)
            {
                var file = fileList[i];
                var isVideo = IsVideoPostMedia(type, file);
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

                await socialPostMediaDal.Add(new SocialPostMedia
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    SortOrder = i,
                    MediaUrl = uploadResult.Data!,
                    DurationSec = isVideo ? ResolveMediaDurationSec(type, i, durationSec, durationSecs) : null,
                    CreatedAt = now,
                });
            }

            if (appointmentId.HasValue && appointmentId.Value != Guid.Empty)
            {
                await appointmentSocialShareGuard.RecordShareAsync(
                    userId,
                    appointmentId.Value,
                    AppointmentSocialShareContentType.Post,
                    post.Id);
            }

            return new SuccessDataResult<Guid>(post.Id, "Gönderi paylaşıldı.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialPostDto>>> GetFeedAsync(
            Guid userId, DateTime? beforeUtc, Guid? beforeId, int limit = 20)
        {
            limit = Math.Clamp(limit, 1, 50);
            var feedProfileIds = await BuildFeedProfileIdsAsync(userId);
            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);

            var posts = await socialPostDal.GetFeedAsync(
                feedProfileIds, blocked, null, SocialPostType.Reel, beforeUtc, beforeId, limit);

            var dtos = await MapPostsAsync(posts, userId);
            return new SuccessDataResult<List<SocialPostDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialPostDto>>> GetProfilePostsAsync(
            Guid userId,
            Guid profileId,
            SocialPostType? typeFilter,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 20)
        {
            limit = Math.Clamp(limit, 1, 50);
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorDataResult<List<SocialPostDto>>(SocialErrorCodes.ProfileNotFound);

            if (await blockedHelper.HasBlockBetweenAsync(userId, profile.UserId))
                return new ErrorDataResult<List<SocialPostDto>>(SocialErrorCodes.ProfileNoAccess);

            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            var posts = await socialPostDal.GetByProfileAsync(
                profileId, blocked, typeFilter, beforeUtc, beforeId, limit);

            var dtos = await MapPostsAsync(posts, userId);
            return new SuccessDataResult<List<SocialPostDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SocialPostDto>> GetPostAsync(Guid userId, Guid postId)
        {
            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorDataResult<SocialPostDto>(SocialErrorCodes.PostNotFound);

            var profile = await socialProfileDal.Get(p => p.Id == post.ProfileId);
            if (profile != null && await blockedHelper.HasBlockBetweenAsync(userId, profile.UserId))
                return new ErrorDataResult<SocialPostDto>(SocialErrorCodes.PostNoAccess);

            var dtos = await MapPostsAsync(new List<SocialPost> { post }, userId);
            return new SuccessDataResult<SocialPostDto>(dtos[0]);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<SocialPostDto>> UpdatePostCaptionAsync(
            Guid userId, Guid postId, string? caption)
        {
            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorDataResult<SocialPostDto>(SocialErrorCodes.PostNotFound);

            var profile = await socialProfileDal.Get(p => p.Id == post.ProfileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorDataResult<SocialPostDto>(SocialErrorCodes.PostNoPermission);

            caption = caption?.Trim();
            if (string.IsNullOrEmpty(caption))
            {
                post.Caption = null;
            }
            else
            {
                if (caption.Length > SocialMediaLimits.CommentMaxLength)
                    return new ErrorDataResult<SocialPostDto>(SocialErrorCodes.CaptionMaxLength);

                post.Caption = caption;
            }

            post.UpdatedAt = DateTime.UtcNow;
            await socialPostDal.Update(post);

            var dtos = await MapPostsAsync(new List<SocialPost> { post }, userId);
            return new SuccessDataResult<SocialPostDto>(dtos[0], "Gönderi güncellendi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeletePostAsync(Guid userId, Guid postId)
        {
            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null)
                return new ErrorResult(SocialErrorCodes.PostNotFound);

            var profile = await socialProfileDal.Get(p => p.Id == post.ProfileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.PostNoPermission);

            var now = DateTime.UtcNow;
            post.Status = SocialContentStatus.Removed;
            post.RemovedAt = now;
            post.UpdatedAt = now;
            await socialPostDal.Update(post);
            return new SuccessResult("Gönderi silindi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> RecordViewAsync(Guid userId, Guid profileId, Guid postId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileInvalid);

            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.PostNotFound);

            if (post.ProfileId == profileId)
                return new SuccessResult();

            var isNew = await socialPostViewDal.TryAddViewAsync(postId, profileId);
            if (!isNew)
                return new SuccessResult();

            post.ViewCount++;
            post.UpdatedAt = DateTime.UtcNow;
            await socialPostDal.Update(post);
            return new SuccessResult();
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialPostDto>>> GetReelsFeedAsync(
            Guid userId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 20,
            double? latitude = null,
            double? longitude = null,
            double radiusKm = 50)
        {
            limit = Math.Clamp(limit, 1, 50);
            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            List<SocialPost> posts;

            if (latitude.HasValue && longitude.HasValue)
            {
                radiusKm = radiusKm > 0 ? Math.Clamp(radiusKm, 1, 500) : 50;
                var rows = await socialProfileDal.SearchProfilesAsync(
                    null, latitude, longitude, radiusKm, blocked, userId, 100);
                var orderedIds = rows.Select(r => r.Profile.Id).Distinct().ToList();
                if (orderedIds.Count == 0)
                    return new SuccessDataResult<List<SocialPostDto>>(new List<SocialPostDto>());

                posts = await socialPostDal.GetFeedByProfileOrderAsync(
                    orderedIds, blocked, SocialPostType.Reel, null, beforeUtc, beforeId, limit);
            }
            else
            {
                var feedProfileIds = await BuildFeedProfileIdsAsync(userId);
                posts = await socialPostDal.GetFeedAsync(
                    feedProfileIds, blocked, SocialPostType.Reel, null, beforeUtc, beforeId, limit);
            }

            var dtos = await MapPostsAsync(posts, userId);
            return new SuccessDataResult<List<SocialPostDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialPostDto>>> GetDiscoverPostsAsync(
            Guid userId,
            string? query,
            double? latitude,
            double? longitude,
            double radiusKm,
            Guid? profileId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 30,
            AvailabilityFilter? availability = null,
            IReadOnlyList<Guid>? serviceIds = null)
        {
            limit = Math.Clamp(limit, 1, 50);
            radiusKm = SocialDiscoverFilterHelper.NormalizeRadiusKm(radiusKm);

            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            List<Guid> profileIds;

            if (profileId.HasValue)
            {
                var profile = await socialProfileDal.Get(p => p.Id == profileId.Value);
                if (profile == null || profile.Status != SocialContentStatus.Active)
                    return new SuccessDataResult<List<SocialPostDto>>(new List<SocialPostDto>());
                if (await blockedHelper.HasBlockBetweenAsync(userId, profile.UserId))
                    return new SuccessDataResult<List<SocialPostDto>>(new List<SocialPostDto>());
                profileIds = new List<Guid> { profileId.Value };
            }
            else
            {
                var hasQuery = !string.IsNullOrWhiteSpace(query);
                var hasGeo = latitude.HasValue && longitude.HasValue;
                var hasAvailabilityFilter = availability.HasValue && availability.Value != AvailabilityFilter.Any;
                var hasServiceFilter = serviceIds != null && serviceIds.Count > 0;
                if (!hasQuery && !hasGeo && !hasAvailabilityFilter && !hasServiceFilter)
                    return new ErrorDataResult<List<SocialPostDto>>(SocialErrorCodes.SearchNeedInput);

                var rows = await socialProfileDal.SearchProfilesAsync(
                    query, latitude, longitude, radiusKm, blocked, userId, 50, availability, serviceIds);
                profileIds = rows.Select(r => r.Profile.Id).Distinct().ToList();
            }

            if (profileIds.Count == 0)
                return new SuccessDataResult<List<SocialPostDto>>(new List<SocialPostDto>());

            List<SocialPost> posts;
            if (profileId.HasValue)
            {
                posts = await socialPostDal.GetByProfileAsync(
                    profileId.Value, blocked, null, beforeUtc, beforeId, limit);
            }
            else
            {
                posts = await socialPostDal.GetFeedByProfileOrderAsync(
                    profileIds, blocked, null, null, beforeUtc, beforeId, limit);
            }
            var dtos = await MapPostsAsync(posts, userId);
            return new SuccessDataResult<List<SocialPostDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialPostDto>>> GetSavedPostsAsync(
            Guid userId,
            Guid profileId,
            SocialPostType? typeFilter,
            SocialPostType? excludeType,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 30)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorDataResult<List<SocialPostDto>>(SocialErrorCodes.ProfileInvalid);

            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            var rows = await socialSavedPostDal.GetSavedPostsAsync(
                profileId, blocked, typeFilter, excludeType, beforeUtc, beforeId, limit);
            var posts = rows.Select(r => r.Post).ToList();
            var dtos = await MapPostsAsync(posts, userId);
            for (var i = 0; i < dtos.Count && i < rows.Count; i++)
            {
                dtos[i].SavedAt = rows[i].SavedAt;
                dtos[i].SavedEntryId = rows[i].SaveId;
            }
            return new SuccessDataResult<List<SocialPostDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IResult> PinPostAsync(Guid userId, Guid postId)
        {
            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.PostNotFound);

            var profile = await socialProfileDal.Get(p => p.Id == post.ProfileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.PostNoPermission);

            if (post.IsPinned)
                return new SuccessResult();

            var pinnedCount = await socialPostDal.CountPinnedAsync(post.ProfileId);
            if (pinnedCount >= SocialMediaLimits.MaxPinnedPostsPerProfile)
                return new ErrorResult(SocialErrorCodes.PostPinLimit);

            post.IsPinned = true;
            post.PinnedAt = DateTime.UtcNow;
            post.UpdatedAt = DateTime.UtcNow;
            await socialPostDal.Update(post);
            return new SuccessResult();
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IResult> UnpinPostAsync(Guid userId, Guid postId)
        {
            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.PostNotFound);

            var profile = await socialProfileDal.Get(p => p.Id == post.ProfileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.PostNoPermission);

            if (!post.IsPinned)
                return new SuccessResult();

            post.IsPinned = false;
            post.PinnedAt = null;
            post.UpdatedAt = DateTime.UtcNow;
            await socialPostDal.Update(post);
            return new SuccessResult();
        }

        private async Task<List<Guid>> BuildFeedProfileIdsAsync(Guid userId)
        {
            var myProfiles = await socialProfileDal.GetByUserIdAsync(userId);
            var ids = myProfiles.Select(p => p.Id).ToHashSet();

            foreach (var mp in myProfiles)
            {
                var following = await socialFollowDal.GetFollowingProfileIdsAsync(mp.Id);
                foreach (var f in following)
                    ids.Add(f);
            }

            return ids.ToList();
        }

        private async Task<List<SocialPostDto>> MapPostsAsync(List<SocialPost> posts, Guid viewerUserId)
        {
            if (posts.Count == 0) return new List<SocialPostDto>();

            var postIds = posts.Select(p => p.Id).ToList();
            var media = await socialPostDal.GetMediaByPostIdsAsync(postIds);
            var mediaByPost = media.GroupBy(m => m.PostId).ToDictionary(g => g.Key, g => g.ToList());

            var likeCounts = await socialLikeDal.GetLikeCountsAsync(SocialLikeTargetType.Post, postIds);
            var commentCounts = await socialCommentDal.GetCommentCountsAsync(postIds);

            var viewerProfiles = await socialProfileDal.GetByUserIdAsync(viewerUserId);
            var viewerProfileIds = viewerProfiles.Select(p => p.Id).ToList();
            HashSet<Guid> likedPostIds = new();
            HashSet<Guid> savedPostIds = new();
            if (viewerProfileIds.Count > 0)
            {
                likedPostIds = await socialLikeDal.GetLikedTargetIdsAsync(
                    SocialLikeTargetType.Post, postIds, viewerProfileIds[0]);
                savedPostIds = await socialSavedPostDal.GetSavedPostIdsAsync(viewerProfileIds[0], postIds);
            }

            var profileCache = new Dictionary<Guid, SocialProfileDto>();
            var result = new List<SocialPostDto>();

            foreach (var post in posts)
            {
                if (!profileCache.TryGetValue(post.ProfileId, out var profileDto))
                {
                    var prof = post.Profile ?? await socialProfileDal.Get(p => p.Id == post.ProfileId);
                    if (prof == null) continue;

                    var stats = await socialProfileDal.GetStatsAsync(
                        prof.Id,
                        viewerProfileIds.Count > 0 ? viewerProfileIds : null);

                    profileDto = new SocialProfileDto
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
                    await socialProfileOwnerEnricher.EnrichOwnerMetaAsync(profileDto, prof);
                    profileCache[post.ProfileId] = profileDto;
                }

                var postMedia = mediaByPost.TryGetValue(post.Id, out var ml)
                    ? ml.Select(m => new SocialPostMediaDto
                    {
                        Id = m.Id,
                        SortOrder = m.SortOrder,
                        MediaUrl = m.MediaUrl,
                        ThumbnailUrl = m.ThumbnailUrl,
                        DurationSec = m.DurationSec,
                    }).ToList()
                    : new List<SocialPostMediaDto>();

                result.Add(new SocialPostDto
                {
                    Id = post.Id,
                    ProfileId = post.ProfileId,
                    Profile = profileDto,
                    Caption = post.Caption,
                    Type = post.Type,
                    Media = postMedia,
                    ViewCount = post.ViewCount,
                    LikeCount = likeCounts.TryGetValue(post.Id, out var lc) ? lc : 0,
                    CommentCount = commentCounts.TryGetValue(post.Id, out var cc) ? cc : 0,
                    IsLiked = likedPostIds.Contains(post.Id),
                    IsSaved = savedPostIds.Contains(post.Id),
                    IsOwnPost = profileDto.UserId == viewerUserId,
                    IsPinned = post.IsPinned,
                    PinnedAt = post.PinnedAt,
                    CreatedAt = post.CreatedAt,
                });
            }

            return result;
        }

        private static IResult ValidateFilesForType(
            SocialPostType type,
            List<IFormFile> files,
            int? durationSec,
            IReadOnlyList<int>? durationSecs)
        {
            if (files.Count == 0)
                return new ErrorResult(SocialErrorCodes.PostMediaRequired);

            IResult baseResult = type switch
            {
                SocialPostType.Photo when files.Count != 1 =>
                    new ErrorResult(SocialErrorCodes.PostPhotoSingle),
                SocialPostType.Carousel when files.Count < 2 || files.Count > SocialMediaLimits.PostCarouselMaxImages =>
                    new ErrorResult(SocialErrorCodes.PostCarouselRange),
                SocialPostType.Video or SocialPostType.Reel when files.Count != 1 =>
                    new ErrorResult(SocialErrorCodes.PostVideoSingle),
                SocialPostType.Video or SocialPostType.Reel when durationSec is > SocialMediaLimits.PostVideoMaxDurationSec =>
                    new ErrorResult(SocialErrorCodes.PostVideoMaxDuration),
                _ => new SuccessResult(),
            };

            if (!baseResult.Success)
                return baseResult;

            if (type == SocialPostType.Carousel)
            {
                for (var i = 0; i < files.Count; i++)
                {
                    if (!IsVideoFile(files[i])) continue;
                    var sec = ResolveMediaDurationSec(type, i, durationSec, durationSecs);
                    if (sec is > SocialMediaLimits.PostVideoMaxDurationSec)
                        return new ErrorResult(SocialErrorCodes.PostVideoMaxDuration);
                }
            }

            return new SuccessResult();
        }

        private static bool IsVideoFile(IFormFile file) =>
            !string.IsNullOrWhiteSpace(file.ContentType) &&
            file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

        private static bool IsVideoPostMedia(SocialPostType type, IFormFile file) =>
            type is SocialPostType.Video or SocialPostType.Reel ||
            (type == SocialPostType.Carousel && IsVideoFile(file));

        private static int? ResolveMediaDurationSec(
            SocialPostType type,
            int index,
            int? durationSec,
            IReadOnlyList<int>? durationSecs)
        {
            if (type == SocialPostType.Carousel && durationSecs != null && index < durationSecs.Count)
                return durationSecs[index] > 0 ? durationSecs[index] : 1;
            return durationSec;
        }

        private Task<IDataResult<string>> UploadMediaAsync(IFormFile file, bool isVideo) =>
            SocialMediaUploadHelper.UploadAsync(
                file, isVideo, MediaContainer, blobStorageService, logger);
    }
}
