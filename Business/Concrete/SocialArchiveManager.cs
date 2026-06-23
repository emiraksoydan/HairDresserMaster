using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Core.Aspect.Autofac.Logging;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Constants;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class SocialArchiveManager(
        ISocialProfileDal socialProfileDal,
        ISocialPostDal socialPostDal,
        ISocialStoryDal socialStoryDal,
        ISocialStoryHighlightDal highlightDal,
        ISocialStoryHighlightItemDal highlightItemDal) : ISocialArchiveService
    {
        private const int MaxArchiveItems = 200;

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SocialArchivedContentDto>> GetProfileArchiveAsync(
            Guid userId, Guid profileId, int limit = 100)
        {
            var owner = await EnsureProfileOwnerAsync(userId, profileId);
            if (!owner.Success) return new ErrorDataResult<SocialArchivedContentDto>(owner.Message);

            limit = Math.Clamp(limit, 1, MaxArchiveItems);
            var items = new List<SocialArchivedItemDto>();

            var posts = await socialPostDal.GetByProfileAndStatusAsync(
                profileId, SocialContentStatus.Removed, null, limit);
            if (posts.Count > 0)
            {
                var media = await socialPostDal.GetMediaByPostIdsAsync(posts.Select(p => p.Id).ToList());
                var mediaByPost = media.GroupBy(m => m.PostId).ToDictionary(g => g.Key, g => g.ToList());
                foreach (var post in posts)
                {
                    var thumb = mediaByPost.TryGetValue(post.Id, out var ml)
                        ? ml[0].ThumbnailUrl ?? ml[0].MediaUrl
                        : null;
                    items.Add(new SocialArchivedItemDto
                    {
                        Kind = SocialArchivedKind.Post,
                        Id = post.Id,
                        Title = post.Caption,
                        ThumbUrl = thumb,
                        PostType = post.Type,
                        RemovedAt = post.RemovedAt ?? post.UpdatedAt,
                    });
                }
            }

            var stories = await socialStoryDal.GetByProfileAndStatusAsync(
                profileId, SocialContentStatus.Removed, null, limit);
            foreach (var story in stories)
            {
                items.Add(new SocialArchivedItemDto
                {
                    Kind = SocialArchivedKind.Story,
                    Id = story.Id,
                    ThumbUrl = story.ThumbnailUrl ?? story.MediaUrl,
                    RemovedAt = story.RemovedAt ?? story.CreatedAt,
                });
            }

            var highlights = await highlightDal.GetByProfileAndStatusAsync(
                profileId, SocialContentStatus.Removed, null, limit);
            foreach (var h in highlights)
            {
                items.Add(new SocialArchivedItemDto
                {
                    Kind = SocialArchivedKind.Highlight,
                    Id = h.Id,
                    Title = h.Title,
                    ThumbUrl = h.CoverUrl,
                    RemovedAt = h.RemovedAt ?? h.UpdatedAt,
                });
            }

            var highlightItems = await highlightItemDal.GetRemovedByProfileIdAsync(profileId, null, limit);
            foreach (var item in highlightItems)
            {
                items.Add(new SocialArchivedItemDto
                {
                    Kind = SocialArchivedKind.HighlightItem,
                    Id = item.Id,
                    ParentId = item.HighlightId,
                    ParentTitle = item.Highlight?.Title,
                    ThumbUrl = item.ThumbnailUrl ?? item.MediaUrl,
                    RemovedAt = item.RemovedAt ?? item.CreatedAt,
                });
            }

            items = items
                .OrderByDescending(i => i.RemovedAt)
                .Take(limit)
                .ToList();

            return new SuccessDataResult<SocialArchivedContentDto>(
                new SocialArchivedContentDto { Items = items });
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IResult> RestoreAsync(Guid userId, SocialRestoreArchivedRequest request)
        {
            return request.Kind switch
            {
                SocialArchivedKind.Post => await RestorePostAsync(userId, request.Id),
                SocialArchivedKind.Story => await RestoreStoryAsync(userId, request.Id),
                SocialArchivedKind.Highlight => await RestoreHighlightAsync(userId, request.Id),
                SocialArchivedKind.HighlightItem => await RestoreHighlightItemAsync(
                    userId, request.ParentId, request.Id),
                _ => new ErrorResult(SocialErrorCodes.InvalidArchivedKind),
            };
        }

        private async Task<IResult> RestorePostAsync(Guid userId, Guid postId)
        {
            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null || post.Status != SocialContentStatus.Removed)
                return new ErrorResult(SocialErrorCodes.ArchivedItemNotFound);

            var owner = await EnsureProfileOwnerAsync(userId, post.ProfileId);
            if (!owner.Success) return owner;

            post.Status = SocialContentStatus.Active;
            post.RemovedAt = null;
            post.UpdatedAt = DateTime.UtcNow;
            await socialPostDal.Update(post);
            return new SuccessResult("Gönderi geri yüklendi.");
        }

        private async Task<IResult> RestoreStoryAsync(Guid userId, Guid storyId)
        {
            var story = await socialStoryDal.Get(s => s.Id == storyId);
            if (story == null || story.Status != SocialContentStatus.Removed)
                return new ErrorResult(SocialErrorCodes.ArchivedItemNotFound);

            var owner = await EnsureProfileOwnerAsync(userId, story.ProfileId);
            if (!owner.Success) return owner;

            var now = DateTime.UtcNow;
            story.Status = SocialContentStatus.Active;
            story.RemovedAt = null;
            if (story.ExpiresAt <= now)
                story.ExpiresAt = now.AddHours(SocialMediaLimits.StoryLifetimeHours);
            await socialStoryDal.Update(story);
            return new SuccessResult("Hikaye geri yüklendi.");
        }

        private async Task<IResult> RestoreHighlightAsync(Guid userId, Guid highlightId)
        {
            var highlight = await highlightDal.Get(h => h.Id == highlightId);
            if (highlight == null || highlight.Status != SocialContentStatus.Removed)
                return new ErrorResult(SocialErrorCodes.ArchivedItemNotFound);

            var owner = await EnsureProfileOwnerAsync(userId, highlight.ProfileId);
            if (!owner.Success) return owner;

            highlight.Status = SocialContentStatus.Active;
            highlight.RemovedAt = null;
            highlight.UpdatedAt = DateTime.UtcNow;
            await highlightDal.Update(highlight);
            return new SuccessResult("Öne çıkan geri yüklendi.");
        }

        private async Task<IResult> RestoreHighlightItemAsync(
            Guid userId, Guid? highlightId, Guid itemId)
        {
            if (!highlightId.HasValue)
                return new ErrorResult(SocialErrorCodes.HighlightParentIdRequired);

            var highlight = await highlightDal.Get(h => h.Id == highlightId.Value);
            if (highlight == null || highlight.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.HighlightNotFound);

            var owner = await EnsureProfileOwnerAsync(userId, highlight.ProfileId);
            if (!owner.Success) return owner;

            var item = await highlightItemDal.Get(i => i.Id == itemId && i.HighlightId == highlightId.Value);
            if (item == null || item.Status != SocialContentStatus.Removed)
                return new ErrorResult(SocialErrorCodes.ArchivedItemNotFound);

            var counts = await highlightDal.GetItemCountsAsync(new[] { highlight.Id });
            var activeCount = counts.GetValueOrDefault(highlight.Id, 0);
            if (activeCount >= SocialMediaLimits.HighlightMaxItemsPerHighlight)
                return new ErrorResult(SocialErrorCodes.HighlightMaxItems);

            item.Status = SocialContentStatus.Active;
            item.RemovedAt = null;
            await highlightItemDal.Update(item);
            highlight.UpdatedAt = DateTime.UtcNow;
            await highlightDal.Update(highlight);
            return new SuccessResult("Öğe öne çıkana geri eklendi.");
        }

        private async Task<IResult> EnsureProfileOwnerAsync(Guid userId, Guid profileId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.ProfileNotFound);
            if (profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileNoPermission);
            return new SuccessResult();
        }
    }
}
