using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Helpers;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Constants;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class SocialStoryHighlightManager(
        ISocialProfileDal socialProfileDal,
        ISocialStoryDal socialStoryDal,
        ISocialStoryHighlightDal highlightDal,
        ISocialStoryHighlightItemDal highlightItemDal,
        BlockedHelper blockedHelper,
        SocialSubscriptionGuard socialSubscriptionGuard) : ISocialStoryHighlightService
    {
        private const int MaxTitleLength = 64;

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialStoryHighlightDto>>> GetProfileHighlightsAsync(
            Guid userId, Guid profileId)
        {
            var access = await EnsureCanViewProfileAsync(userId, profileId);
            if (!access.Success) return new ErrorDataResult<List<SocialStoryHighlightDto>>(access.Message);

            var highlights = await highlightDal.GetByProfileIdAsync(profileId);
            var ids = highlights.Select(h => h.Id).ToList();
            var counts = await highlightDal.GetItemCountsAsync(ids);

            var dtos = highlights.Select(h => MapSummary(h, counts)).ToList();
            return new SuccessDataResult<List<SocialStoryHighlightDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SocialStoryHighlightDetailDto>> GetHighlightDetailAsync(
            Guid userId, Guid highlightId)
        {
            var highlight = await highlightDal.GetWithItemsAsync(highlightId);
            if (highlight == null)
                return new ErrorDataResult<SocialStoryHighlightDetailDto>(SocialErrorCodes.HighlightNotFound);

            var access = await EnsureCanViewProfileAsync(userId, highlight.ProfileId);
            if (!access.Success)
                return new ErrorDataResult<SocialStoryHighlightDetailDto>(access.Message);

            var counts = await highlightDal.GetItemCountsAsync(new[] { highlight.Id });
            var dto = new SocialStoryHighlightDetailDto
            {
                Id = highlight.Id,
                ProfileId = highlight.ProfileId,
                Title = highlight.Title,
                CoverUrl = ResolveCoverUrl(highlight),
                ItemCount = counts.GetValueOrDefault(highlight.Id, highlight.Items.Count),
                SortOrder = highlight.SortOrder,
                CreatedAt = highlight.CreatedAt,
                Items = highlight.Items.Select(MapItem).ToList(),
            };

            return new SuccessDataResult<SocialStoryHighlightDetailDto>(dto);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<Guid>> CreateHighlightAsync(
            Guid userId, CreateSocialStoryHighlightRequest request)
        {
            var profile = await socialProfileDal.Get(p => p.Id == request.ProfileId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorDataResult<Guid>(SocialErrorCodes.ProfileNotFound);
            if (profile.UserId != userId)
                return new ErrorDataResult<Guid>(SocialErrorCodes.ProfileNoPermission);

            var subLimit = await socialSubscriptionGuard.EnsureCanCreateHighlightAsync(userId);
            if (subLimit != null)
                return new ErrorDataResult<Guid>(subLimit.Message);

            var title = (request.Title ?? string.Empty).Trim();
            if (title.Length < 1 || title.Length > MaxTitleLength)
                return new ErrorDataResult<Guid>(SocialErrorCodes.HighlightTitleLength);

            if (request.StoryIds == null || request.StoryIds.Count == 0)
                return new ErrorDataResult<Guid>(SocialErrorCodes.HighlightStoriesRequired);

            if (request.StoryIds.Count > SocialMediaLimits.HighlightMaxItemsPerHighlight)
                return new ErrorDataResult<Guid>(SocialErrorCodes.HighlightMaxItems);

            var now = DateTime.UtcNow;
            var highlight = new SocialStoryHighlight
            {
                Id = Guid.NewGuid(),
                ProfileId = request.ProfileId,
                Title = title,
                SortOrder = await highlightDal.GetNextSortOrderAsync(request.ProfileId),
                Status = SocialContentStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await highlightDal.Add(highlight);

            var addResult = await AddStorySnapshotsAsync(highlight, request.ProfileId, request.StoryIds);
            if (!addResult.Success)
            {
                highlight.Status = SocialContentStatus.Removed;
                highlight.RemovedAt = now;
                highlight.UpdatedAt = now;
                await highlightDal.Update(highlight);
                return new ErrorDataResult<Guid>(addResult.Message);
            }

            await RefreshCoverAsync(highlight.Id);
            return new SuccessDataResult<Guid>(highlight.Id, "Öne çıkan oluşturuldu.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> UpdateHighlightAsync(
            Guid userId, Guid highlightId, UpdateSocialStoryHighlightRequest request)
        {
            var highlight = await highlightDal.Get(h => h.Id == highlightId);
            if (highlight == null || highlight.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.HighlightNotFound);

            var owner = await EnsureProfileOwnerAsync(userId, highlight.ProfileId);
            if (!owner.Success) return owner;

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                var title = request.Title.Trim();
                if (title.Length < 1 || title.Length > MaxTitleLength)
                    return new ErrorResult(SocialErrorCodes.HighlightTitleLength);
                highlight.Title = title;
            }

            if (request.SortOrder.HasValue)
                highlight.SortOrder = request.SortOrder.Value;

            highlight.UpdatedAt = DateTime.UtcNow;
            await highlightDal.Update(highlight);
            return new SuccessResult("Öne çıkan güncellendi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> AddStoriesToHighlightAsync(
            Guid userId, Guid highlightId, AddSocialStoryHighlightItemsRequest request)
        {
            var highlight = await highlightDal.Get(h => h.Id == highlightId);
            if (highlight == null || highlight.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.HighlightNotFound);

            var owner = await EnsureProfileOwnerAsync(userId, highlight.ProfileId);
            if (!owner.Success) return owner;

            if (request.StoryIds == null || request.StoryIds.Count == 0)
                return new ErrorResult(SocialErrorCodes.HighlightStoriesRequiredAdd);

            var addResult = await AddStorySnapshotsAsync(highlight, highlight.ProfileId, request.StoryIds);
            if (!addResult.Success) return addResult;

            await RefreshCoverAsync(highlight.Id);
            return new SuccessResult("Hikayeler öne çıkana eklendi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> RemoveHighlightItemAsync(Guid userId, Guid highlightId, Guid itemId)
        {
            var highlight = await highlightDal.Get(h => h.Id == highlightId);
            if (highlight == null || highlight.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.HighlightNotFound);

            var owner = await EnsureProfileOwnerAsync(userId, highlight.ProfileId);
            if (!owner.Success) return owner;

            var item = await highlightItemDal.Get(i => i.Id == itemId && i.HighlightId == highlightId);
            if (item == null || item.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.HighlightItemNotFound);

            var removedAt = DateTime.UtcNow;
            item.Status = SocialContentStatus.Removed;
            item.RemovedAt = removedAt;
            await highlightItemDal.Update(item);
            highlight.UpdatedAt = DateTime.UtcNow;
            await highlightDal.Update(highlight);
            await RefreshCoverAsync(highlight.Id);
            return new SuccessResult("Hikaye öne çıkandan kaldırıldı.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeleteHighlightAsync(Guid userId, Guid highlightId)
        {
            var highlight = await highlightDal.Get(h => h.Id == highlightId);
            if (highlight == null || highlight.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.HighlightNotFound);

            var owner = await EnsureProfileOwnerAsync(userId, highlight.ProfileId);
            if (!owner.Success) return owner;

            var removedAt = DateTime.UtcNow;
            highlight.Status = SocialContentStatus.Removed;
            highlight.RemovedAt = removedAt;
            highlight.UpdatedAt = removedAt;
            await highlightDal.Update(highlight);
            return new SuccessResult("Öne çıkan silindi.");
        }

        private async Task<IResult> EnsureCanViewProfileAsync(Guid userId, Guid profileId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.ProfileNotFound);

            if (await blockedHelper.HasBlockBetweenAsync(userId, profile.UserId))
                return new ErrorResult(SocialErrorCodes.ProfileNoAccess);

            return new SuccessResult();
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

        private async Task<IResult> AddStorySnapshotsAsync(
            SocialStoryHighlight highlight, Guid profileId, IReadOnlyList<Guid> storyIds)
        {
            var distinctIds = storyIds.Distinct().ToList();
            var existingCount = (await highlightDal.GetItemCountsAsync(new[] { highlight.Id }))
                .GetValueOrDefault(highlight.Id, 0);
            if (existingCount + distinctIds.Count > SocialMediaLimits.HighlightMaxItemsPerHighlight)
                return new ErrorResult(SocialErrorCodes.HighlightMaxItems);

            var stories = new List<SocialStory>();
            foreach (var storyId in distinctIds)
            {
                var story = await socialStoryDal.Get(s => s.Id == storyId);
                if (story == null || story.ProfileId != profileId || story.Status != SocialContentStatus.Active)
                    return new ErrorResult(SocialErrorCodes.HighlightStoryInvalid);
                stories.Add(story);
            }

            var sortBase = existingCount;
            var now = DateTime.UtcNow;
            for (var i = 0; i < stories.Count; i++)
            {
                var story = stories[i];
                var already = await highlightItemDal.Get(
                    x => x.HighlightId == highlight.Id && x.SourceStoryId == story.Id);
                if (already != null)
                {
                    if (already.Status == SocialContentStatus.Removed)
                    {
                        already.Status = SocialContentStatus.Active;
                        already.RemovedAt = null;
                        already.SortOrder = sortBase + i;
                        await highlightItemDal.Update(already);
                    }
                    continue;
                }

                await highlightItemDal.Add(new SocialStoryHighlightItem
                {
                    Id = Guid.NewGuid(),
                    HighlightId = highlight.Id,
                    SourceStoryId = story.Id,
                    MediaUrl = story.MediaUrl,
                    ThumbnailUrl = story.ThumbnailUrl,
                    DurationSec = story.DurationSec,
                    SortOrder = sortBase + i,
                    Status = SocialContentStatus.Active,
                    CreatedAt = now,
                });
            }

            highlight.UpdatedAt = now;
            await highlightDal.Update(highlight);
            return new SuccessResult();
        }

        private async Task RefreshCoverAsync(Guid highlightId)
        {
            var highlight = await highlightDal.GetWithItemsAsync(highlightId);
            if (highlight == null) return;

            var cover = ResolveCoverUrl(highlight);
            var entity = await highlightDal.Get(h => h.Id == highlightId);
            if (entity == null) return;

            entity.CoverUrl = cover;
            entity.UpdatedAt = DateTime.UtcNow;
            await highlightDal.Update(entity);
        }

        private static string? ResolveCoverUrl(SocialStoryHighlight highlight)
        {
            if (!string.IsNullOrWhiteSpace(highlight.CoverUrl))
                return highlight.CoverUrl;

            var first = highlight.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.CreatedAt).FirstOrDefault();
            return first?.ThumbnailUrl ?? first?.MediaUrl;
        }

        private static SocialStoryHighlightDto MapSummary(
            SocialStoryHighlight highlight, Dictionary<Guid, int> counts)
        {
            return new SocialStoryHighlightDto
            {
                Id = highlight.Id,
                ProfileId = highlight.ProfileId,
                Title = highlight.Title,
                CoverUrl = highlight.CoverUrl,
                ItemCount = counts.GetValueOrDefault(highlight.Id, 0),
                SortOrder = highlight.SortOrder,
                CreatedAt = highlight.CreatedAt,
            };
        }

        private static SocialStoryHighlightItemDto MapItem(SocialStoryHighlightItem item)
        {
            return new SocialStoryHighlightItemDto
            {
                Id = item.Id,
                SourceStoryId = item.SourceStoryId,
                MediaUrl = item.MediaUrl,
                ThumbnailUrl = item.ThumbnailUrl,
                DurationSec = item.DurationSec,
                SortOrder = item.SortOrder,
                CreatedAt = item.CreatedAt,
            };
        }
    }
}
