using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Core.Aspect.Autofac.Logging;
using Core.Utilities.Results;
using Core.Utilities.Security.PhoneSetting;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class SocialAdminManager(
        ISocialAdminDal socialAdminDal,
        ISocialPostDal socialPostDal,
        ISocialStoryDal socialStoryDal,
        ISocialProfileDal socialProfileDal,
        ISocialStoryHighlightDal socialStoryHighlightDal,
        ISocialLikeDal socialLikeDal,
        ISocialCommentDal socialCommentDal,
        IImageDal imageDal,
        IUserDal userDal,
        IFreeBarberDal freeBarberDal,
        IBarberStoreDal barberStoreDal,
        IPhoneService phoneService,
        IAuditService auditService) : ISocialAdminService
    {
        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<SocialPostAdminDto>>> GetPostsForAdminAsync(
            SocialContentStatus? status, SocialPostType? postType, string? search, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var skip = (page - 1) * pageSize;

            var posts = await socialAdminDal.GetPostsForAdminAsync(status, postType, search, skip, pageSize);
            if (posts.Count == 0)
                return new SuccessDataResult<List<SocialPostAdminDto>>(new List<SocialPostAdminDto>());

            var postIds = posts.Select(p => p.Id).ToList();
            var media = await socialPostDal.GetMediaByPostIdsAsync(postIds);
            var mediaByPost = media.GroupBy(m => m.PostId).ToDictionary(g => g.Key, g => g.ToList());
            var likeCounts = await socialLikeDal.GetLikeCountsAsync(SocialLikeTargetType.Post, postIds);
            var commentCounts = await socialCommentDal.GetCommentCountsAsync(postIds);

            var dtos = posts.Select(p =>
            {
                var postMedia = mediaByPost.GetValueOrDefault(p.Id) ?? new List<SocialPostMedia>();
                var orderedMedia = postMedia.OrderBy(m => m.SortOrder).ToList();
                var thumb = orderedMedia.FirstOrDefault();
                var isVideoPost = p.Type is SocialPostType.Video or SocialPostType.Reel;
                return new SocialPostAdminDto
                {
                    Id = p.Id,
                    ProfileId = p.ProfileId,
                    ProfileUsername = p.Profile.Username,
                    OwnerType = p.Profile.OwnerType,
                    Caption = p.Caption,
                    Type = p.Type,
                    Status = p.Status,
                    ViewCount = p.ViewCount,
                    LikeCount = likeCounts.TryGetValue(p.Id, out var lc) ? lc : 0,
                    CommentCount = commentCounts.TryGetValue(p.Id, out var cc) ? cc : 0,
                    MediaCount = postMedia.Count,
                    ThumbnailUrl = thumb?.ThumbnailUrl ?? thumb?.MediaUrl,
                    Media = orderedMedia.Select(m => new SocialPostMediaAdminDto
                    {
                        MediaUrl = m.MediaUrl,
                        ThumbnailUrl = m.ThumbnailUrl,
                        DurationSec = m.DurationSec,
                        IsVideo = isVideoPost || m.DurationSec.HasValue || IsVideoMediaUrl(m.MediaUrl),
                    }).ToList(),
                    CreatedAt = p.CreatedAt,
                };
            }).ToList();

            return new SuccessDataResult<List<SocialPostAdminDto>>(dtos);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<SocialCommentAdminDto>>> GetCommentsForAdminAsync(
            SocialContentStatus? status, string? search, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var skip = (page - 1) * pageSize;

            var comments = await socialAdminDal.GetCommentsForAdminAsync(status, search, skip, pageSize);
            var dtos = comments.Select(c => new SocialCommentAdminDto
            {
                Id = c.Id,
                PostId = c.PostId,
                PostCaption = c.Post.Caption,
                ProfileId = c.ProfileId,
                ProfileUsername = c.Profile.Username,
                OwnerType = c.Profile.OwnerType,
                ParentCommentId = c.ParentCommentId,
                Text = c.Text,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
            }).ToList();

            return new SuccessDataResult<List<SocialCommentAdminDto>>(dtos);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<SocialStoryAdminDto>>> GetStoriesForAdminAsync(
            SocialContentStatus? status, string? search, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var skip = (page - 1) * pageSize;

            var stories = await socialAdminDal.GetStoriesForAdminAsync(status, search, skip, pageSize);
            var dtos = stories.Select(s => new SocialStoryAdminDto
            {
                Id = s.Id,
                ProfileId = s.ProfileId,
                ProfileUsername = s.Profile.Username,
                OwnerType = s.Profile.OwnerType,
                Status = s.Status,
                MediaUrl = s.MediaUrl,
                ThumbnailUrl = s.ThumbnailUrl,
                DurationSec = s.DurationSec,
                ExpiresAt = s.ExpiresAt,
                CreatedAt = s.CreatedAt,
            }).ToList();

            return new SuccessDataResult<List<SocialStoryAdminDto>>(dtos);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<SocialProfileAdminDto>>> GetProfilesForAdminAsync(
            SocialContentStatus? status, string? search, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var skip = (page - 1) * pageSize;

            var profiles = await socialAdminDal.GetProfilesForAdminAsync(status, search, skip, pageSize);
            var dtos = new List<SocialProfileAdminDto>();

            foreach (var p in profiles)
            {
                string? avatarUrl = null;
                if (p.AvatarImageId.HasValue)
                {
                    var img = await imageDal.Get(i => i.Id == p.AvatarImageId.Value);
                    avatarUrl = img?.ImageUrl;
                }

                var stats = await socialProfileDal.GetStatsAsync(p.Id, null);
                var dto = new SocialProfileAdminDto
                {
                    Id = p.Id,
                    Username = p.Username,
                    OwnerType = p.OwnerType,
                    OwnerId = p.OwnerId,
                    UserId = p.UserId,
                    Bio = p.Bio,
                    AvatarUrl = avatarUrl,
                    Status = p.Status,
                    PostCount = stats.PostCount,
                    FollowerCount = stats.FollowerCount,
                    FollowingCount = stats.FollowingCount,
                    CreatedAt = p.CreatedAt,
                };
                await EnrichOwnerInfoAsync(dto, p);
                dtos.Add(dto);
            }

            return new SuccessDataResult<List<SocialProfileAdminDto>>(dtos);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<SocialStoryHighlightAdminDto>>> GetHighlightsForAdminAsync(
            SocialContentStatus? status, string? search, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var skip = (page - 1) * pageSize;

            var highlights = await socialAdminDal.GetHighlightsForAdminAsync(status, search, skip, pageSize);
            var dtos = highlights.Select(h => new SocialStoryHighlightAdminDto
            {
                Id = h.Id,
                ProfileId = h.ProfileId,
                ProfileUsername = h.Profile.Username,
                OwnerType = h.Profile.OwnerType,
                Title = h.Title,
                CoverUrl = h.CoverUrl,
                ItemCount = h.Items.Count,
                SortOrder = h.SortOrder,
                Status = h.Status,
                Items = h.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new SocialStoryHighlightItemAdminDto
                    {
                        Id = i.Id,
                        MediaUrl = i.MediaUrl,
                        ThumbnailUrl = i.ThumbnailUrl,
                        DurationSec = i.DurationSec,
                        SortOrder = i.SortOrder,
                        Status = i.Status,
                        CreatedAt = i.CreatedAt,
                    }).ToList(),
                CreatedAt = h.CreatedAt,
            }).ToList();

            return new SuccessDataResult<List<SocialStoryHighlightAdminDto>>(dtos);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> AdminRemovePostAsync(Guid adminId, Guid postId)
        {
            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null) return new ErrorResult("Gönderi bulunamadı.");
            if (post.Status == SocialContentStatus.Removed)
                return new SuccessResult("Gönderi zaten kaldırılmış.");

            var now = DateTime.UtcNow;
            post.Status = SocialContentStatus.Removed;
            post.RemovedAt = now;
            post.UpdatedAt = now;
            await socialPostDal.Update(post);
            await auditService.RecordAsync(AuditAction.AdminSocialPostRemoved, adminId, postId, post.ProfileId, true);
            return new SuccessResult("Gönderi kaldırıldı.");
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> AdminRemoveStoryAsync(Guid adminId, Guid storyId)
        {
            var story = await socialStoryDal.Get(s => s.Id == storyId);
            if (story == null) return new ErrorResult("Hikaye bulunamadı.");
            if (story.Status == SocialContentStatus.Removed)
                return new SuccessResult("Hikaye zaten kaldırılmış.");

            story.Status = SocialContentStatus.Removed;
            story.RemovedAt = DateTime.UtcNow;
            await socialStoryDal.Update(story);
            await auditService.RecordAsync(AuditAction.AdminSocialStoryRemoved, adminId, storyId, story.ProfileId, true);
            return new SuccessResult("Hikaye kaldırıldı.");
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> AdminRemoveProfileAsync(Guid adminId, Guid profileId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null) return new ErrorResult("Sosyal profil bulunamadı.");
            if (profile.Status == SocialContentStatus.Removed)
                return new SuccessResult("Profil zaten kaldırılmış.");

            profile.Status = SocialContentStatus.Removed;
            profile.UpdatedAt = DateTime.UtcNow;
            await socialProfileDal.Update(profile);
            await socialAdminDal.SetProfileContentRemovedAsync(profileId);
            await auditService.RecordAsync(AuditAction.AdminSocialProfileRemoved, adminId, profileId, profile.UserId, true);
            return new SuccessResult("Sosyal profil ve içerikleri kaldırıldı.");
        }

        [SecuredOperation("Admin")]
        public async Task AdminRemoveAllProfilesForUserAsync(Guid adminId, Guid userId)
        {
            var profiles = await socialProfileDal.GetAll(p => p.UserId == userId);
            foreach (var profile in profiles)
            {
                if (profile.Status == SocialContentStatus.Removed) continue;
                await AdminRemoveProfileAsync(adminId, profile.Id);
            }
        }

        [SecuredOperation("Admin")]
        public async Task AdminRestoreAllProfilesForUserAsync(Guid adminId, Guid userId)
        {
            var profiles = await socialProfileDal.GetAll(p =>
                p.UserId == userId && p.Status == SocialContentStatus.Removed);
            foreach (var profile in profiles)
                await AdminRestoreProfileInternalAsync(adminId, profile.Id);
        }

        private async Task AdminRestoreProfileInternalAsync(Guid adminId, Guid profileId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.Status != SocialContentStatus.Removed)
                return;

            profile.Status = SocialContentStatus.Active;
            profile.UpdatedAt = DateTime.UtcNow;
            await socialProfileDal.Update(profile);
            await socialAdminDal.RestoreProfileContentAsync(profileId);
            await auditService.RecordAsync(
                AuditAction.AdminSocialProfileRestored, adminId, profileId, profile.UserId, true);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> AdminRemoveHighlightAsync(Guid adminId, Guid highlightId)
        {
            var highlight = await socialStoryHighlightDal.Get(h => h.Id == highlightId);
            if (highlight == null) return new ErrorResult("Öne çıkan bulunamadı.");
            if (highlight.Status == SocialContentStatus.Removed)
                return new SuccessResult("Öne çıkan zaten kaldırılmış.");

            var now = DateTime.UtcNow;
            highlight.Status = SocialContentStatus.Removed;
            highlight.RemovedAt = now;
            highlight.UpdatedAt = now;
            await socialStoryHighlightDal.Update(highlight);
            await auditService.RecordAsync(
                AuditAction.AdminSocialHighlightRemoved, adminId, highlightId, highlight.ProfileId, true);
            return new SuccessResult("Öne çıkan kaldırıldı.");
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> AdminRemoveCommentAsync(Guid adminId, Guid commentId)
        {
            var comment = await socialCommentDal.Get(c => c.Id == commentId);
            if (comment == null) return new ErrorResult("Yorum bulunamadı.");
            if (comment.Status == SocialContentStatus.Removed)
                return new SuccessResult("Yorum zaten kaldırılmış.");

            comment.Status = SocialContentStatus.Removed;
            comment.UpdatedAt = DateTime.UtcNow;
            await socialCommentDal.Update(comment);
            await auditService.RecordAsync(
                AuditAction.AdminSocialCommentRemoved, adminId, commentId, comment.ProfileId, true);
            return new SuccessResult("Yorum kaldırıldı.");
        }

        private static bool IsVideoMediaUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            var path = url.Split('?', '#')[0];
            return path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase);
        }

        private async Task EnrichOwnerInfoAsync(SocialProfileAdminDto dto, SocialProfile profile)
        {
            switch (profile.OwnerType)
            {
                case SocialProfileOwnerType.Customer:
                {
                    var user = await userDal.Get(u => u.Id == profile.OwnerId);
                    if (user == null) return;
                    var first = phoneService.DecryptForRead(user.FirstNameEncrypted) ?? user.FirstName;
                    var last = phoneService.DecryptForRead(user.LastNameEncrypted) ?? user.LastName;
                    dto.OwnerDisplayName = $"{first} {last}".Trim();
                    dto.OwnerNumber = user.CustomerNumber;
                    break;
                }
                case SocialProfileOwnerType.FreeBarber:
                {
                    var fb = await freeBarberDal.Get(f => f.Id == profile.OwnerId);
                    if (fb == null) return;
                    dto.OwnerDisplayName = $"{fb.FirstName} {fb.LastName}".Trim();
                    var fbUser = await userDal.Get(u => u.Id == fb.FreeBarberUserId);
                    dto.OwnerNumber = fbUser?.CustomerNumber;
                    break;
                }
                case SocialProfileOwnerType.BarberStore:
                {
                    var store = await barberStoreDal.Get(s => s.Id == profile.OwnerId);
                    if (store == null) return;
                    dto.OwnerDisplayName = store.StoreName;
                    dto.OwnerNumber = store.StoreNo;
                    break;
                }
            }
        }
    }
}
