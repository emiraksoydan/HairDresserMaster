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
    public class SocialInteractionManager(
        ISocialProfileDal socialProfileDal,
        ISocialPostDal socialPostDal,
        ISocialStoryDal socialStoryDal,
        ISocialLikeDal socialLikeDal,
        ISocialSavedPostDal socialSavedPostDal,
        ISocialCommentDal socialCommentDal,
        ISocialFollowDal socialFollowDal,
        ISocialProfileMuteDal socialProfileMuteDal,
        IImageDal imageDal,
        SocialProfileOwnerEnricher socialProfileOwnerEnricher,
        BlockedHelper blockedHelper,
        SocialNotificationHelper socialNotificationHelper) : ISocialInteractionService
    {
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> ToggleLikeAsync(
            Guid userId, Guid profileId, SocialLikeTargetType targetType, Guid targetId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileInvalid);

            var existing = await socialLikeDal.GetLikeAsync(targetType, targetId, profileId);
            if (existing != null)
            {
                await socialLikeDal.Remove(existing);
                return new SuccessResult("Beğeni kaldırıldı.");
            }

            if (targetType == SocialLikeTargetType.Post)
            {
                var post = await socialPostDal.Get(p => p.Id == targetId);
                if (post == null || post.Status != SocialContentStatus.Active)
                    return new ErrorResult(SocialErrorCodes.PostNotFound);
                var postOwner = await socialProfileDal.Get(p => p.Id == post.ProfileId);
                if (postOwner != null && await blockedHelper.HasBlockBetweenAsync(userId, postOwner.UserId))
                    return new ErrorResult(SocialErrorCodes.PostNoAccess);
            }
            else if (targetType == SocialLikeTargetType.Story)
            {
                var story = await socialStoryDal.Get(s => s.Id == targetId);
                if (story == null || story.Status != SocialContentStatus.Active || story.ExpiresAt <= DateTime.UtcNow)
                    return new ErrorResult(SocialErrorCodes.StoryNotFound);
                var storyOwner = await socialProfileDal.Get(p => p.Id == story.ProfileId);
                if (storyOwner != null && await blockedHelper.HasBlockBetweenAsync(userId, storyOwner.UserId))
                    return new ErrorResult(SocialErrorCodes.StoryNoAccess);
            }

            await socialLikeDal.Add(new SocialLike
            {
                Id = Guid.NewGuid(),
                TargetType = targetType,
                TargetId = targetId,
                ProfileId = profileId,
                CreatedAt = DateTime.UtcNow,
            });

            if (targetType == SocialLikeTargetType.Post)
            {
                var post = await socialPostDal.Get(p => p.Id == targetId);
                var postOwner = post != null
                    ? await socialProfileDal.Get(p => p.Id == post.ProfileId)
                    : null;
                if (post != null && postOwner != null)
                    await socialNotificationHelper.NotifyPostLikedAsync(profile, post, postOwner);
            }
            else if (targetType == SocialLikeTargetType.Story)
            {
                var story = await socialStoryDal.Get(s => s.Id == targetId);
                var storyOwner = story != null
                    ? await socialProfileDal.Get(p => p.Id == story.ProfileId)
                    : null;
                if (story != null && storyOwner != null)
                    await socialNotificationHelper.NotifyStoryLikedAsync(profile, story, storyOwner);
            }

            return new SuccessResult("Beğenildi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> ToggleSaveAsync(Guid userId, Guid profileId, Guid postId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileInvalid);

            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.PostNotFound);

            var postOwner = await socialProfileDal.Get(p => p.Id == post.ProfileId);
            if (postOwner != null && await blockedHelper.HasBlockBetweenAsync(userId, postOwner.UserId))
                return new ErrorResult(SocialErrorCodes.PostNoAccess);

            var existing = await socialSavedPostDal.GetAsync(profileId, postId);
            if (existing != null)
            {
                await socialSavedPostDal.Remove(existing);
                return new SuccessResult("Kayıt kaldırıldı.");
            }

            await socialSavedPostDal.Add(new SocialSavedPost
            {
                Id = Guid.NewGuid(),
                ProfileId = profileId,
                PostId = postId,
                CreatedAt = DateTime.UtcNow,
            });

            return new SuccessResult("Kaydedildi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<SocialCommentDto>> CreateCommentAsync(
            Guid userId, Guid profileId, CreateSocialCommentDto dto)
        {
            var text = (dto.Text ?? "").Trim();
            if (text.Length == 0)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentEmpty);

            if (text.Length > SocialMediaLimits.CommentMaxLength)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentMaxLength);

            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.ProfileInvalid);

            var post = await socialPostDal.Get(p => p.Id == dto.PostId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentPostNotFound);

            var postOwner = await socialProfileDal.Get(p => p.Id == post.ProfileId);
            if (postOwner != null && await blockedHelper.HasBlockBetweenAsync(userId, postOwner.UserId))
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentPostBlocked);

            if (post.Type == SocialPostType.Reel && postOwner != null && postOwner.UserId == userId)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentOwnReelNotAllowed);

            SocialComment? parentComment = null;
            if (dto.ParentCommentId.HasValue)
            {
                parentComment = await socialCommentDal.Get(c => c.Id == dto.ParentCommentId.Value);
                if (parentComment == null || parentComment.PostId != dto.PostId ||
                    parentComment.Status != SocialContentStatus.Active)
                    return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentParentNotFound);
                if (parentComment.ParentCommentId.HasValue)
                    return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentReplyDepth);
            }

            var now = DateTime.UtcNow;
            var comment = new SocialComment
            {
                Id = Guid.NewGuid(),
                PostId = dto.PostId,
                ProfileId = profileId,
                ParentCommentId = dto.ParentCommentId,
                Text = text,
                Status = SocialContentStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await socialCommentDal.Add(comment);

            var commentDto = new SocialCommentDto
            {
                Id = comment.Id,
                PostId = comment.PostId,
                Profile = new SocialProfileDto
                {
                    Id = profile.Id,
                    Username = profile.Username,
                    OwnerType = profile.OwnerType,
                    OwnerId = profile.OwnerId,
                    UserId = profile.UserId,
                    IsOwnProfile = true,
                },
                ParentCommentId = comment.ParentCommentId,
                Text = comment.Text,
                LikeCount = 0,
                ReplyCount = 0,
                IsLiked = false,
                CreatedAt = comment.CreatedAt,
            };

            SocialProfile? parentAuthor = null;
            if (parentComment == null)
            {
                if (postOwner != null)
                    await socialNotificationHelper.NotifyPostCommentedAsync(profile, post, postOwner, comment);
            }
            else
            {
                parentAuthor = await socialProfileDal.Get(p => p.Id == parentComment.ProfileId);
                if (parentAuthor != null)
                    await socialNotificationHelper.NotifyCommentRepliedAsync(
                        profile, post, parentAuthor, comment, parentComment.Id);
            }

            await NotifyMentionsInCommentAsync(
                profile, post, comment, text, postOwner, parentAuthor);

            return new SuccessDataResult<SocialCommentDto>(commentDto, "Yorum eklendi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<SocialCommentDto>> UpdateCommentAsync(
            Guid userId, Guid profileId, Guid commentId, string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentEmpty);

            if (text.Length > SocialMediaLimits.CommentMaxLength)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentMaxLength);

            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.ProfileInvalid);

            var comment = await socialCommentDal.Get(c => c.Id == commentId);
            if (comment == null || comment.Status != SocialContentStatus.Active)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentNotFound);

            if (comment.ProfileId != profileId)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentNoPermission);

            var post = await socialPostDal.Get(p => p.Id == comment.PostId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorDataResult<SocialCommentDto>(SocialErrorCodes.CommentPostNotFound);

            comment.Text = text;
            comment.UpdatedAt = DateTime.UtcNow;
            await socialCommentDal.Update(comment);

            var dto = await MapCommentDtoAsync(comment, userId, profile);
            return new SuccessDataResult<SocialCommentDto>(dto, "Yorum güncellendi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeleteCommentAsync(Guid userId, Guid profileId, Guid commentId)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileInvalid);

            var comment = await socialCommentDal.Get(c => c.Id == commentId);
            if (comment == null || comment.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.CommentNotFound);

            if (comment.ProfileId != profileId)
                return new ErrorResult(SocialErrorCodes.CommentNoPermission);

            comment.Status = SocialContentStatus.Removed;
            comment.UpdatedAt = DateTime.UtcNow;
            await socialCommentDal.Update(comment);
            return new SuccessResult("Yorum silindi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialCommentDto>>> GetCommentsAsync(
            Guid userId,
            Guid postId,
            Guid? parentCommentId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 30)
        {
            limit = Math.Clamp(limit, 1, 100);
            var post = await socialPostDal.Get(p => p.Id == postId);
            if (post == null || post.Status != SocialContentStatus.Active)
                return new ErrorDataResult<List<SocialCommentDto>>(SocialErrorCodes.CommentPostNotFound);

            var comments = await socialCommentDal.GetByPostAsync(
                postId, parentCommentId, beforeUtc, beforeId, limit);
            var commentIds = comments.Select(c => c.Id).ToList();
            var likeCounts = await socialLikeDal.GetLikeCountsAsync(SocialLikeTargetType.Comment, commentIds);
            var replyCounts = !parentCommentId.HasValue && commentIds.Count > 0
                ? await socialCommentDal.GetReplyCountsAsync(commentIds)
                : new Dictionary<Guid, int>();

            var viewerProfiles = await socialProfileDal.GetByUserIdAsync(userId);
            var viewerProfileId = viewerProfiles.FirstOrDefault()?.Id;
            HashSet<Guid> likedIds = new();
            if (viewerProfileId.HasValue && commentIds.Count > 0)
            {
                likedIds = await socialLikeDal.GetLikedTargetIdsAsync(
                    SocialLikeTargetType.Comment, commentIds, viewerProfileId.Value);
            }

            var dtos = comments.Select(c => new SocialCommentDto
            {
                Id = c.Id,
                PostId = c.PostId,
                Profile = new SocialProfileDto
                {
                    Id = c.Profile.Id,
                    Username = c.Profile.Username,
                    OwnerType = c.Profile.OwnerType,
                    OwnerId = c.Profile.OwnerId,
                    UserId = c.Profile.UserId,
                    IsOwnProfile = c.Profile.UserId == userId,
                },
                ParentCommentId = c.ParentCommentId,
                Text = c.Text,
                LikeCount = likeCounts.TryGetValue(c.Id, out var lc) ? lc : 0,
                ReplyCount = replyCounts.TryGetValue(c.Id, out var rc) ? rc : 0,
                IsLiked = likedIds.Contains(c.Id),
                CreatedAt = c.CreatedAt,
            }).ToList();

            return new SuccessDataResult<List<SocialCommentDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> FollowAsync(Guid userId, Guid followerProfileId, Guid followingProfileId)
        {
            if (followerProfileId == followingProfileId)
                return new ErrorResult(SocialErrorCodes.CannotFollowSelf);

            var follower = await socialProfileDal.Get(p => p.Id == followerProfileId);
            var following = await socialProfileDal.Get(p => p.Id == followingProfileId);
            if (follower == null || following == null)
                return new ErrorResult(SocialErrorCodes.ProfileNotFound);
            if (follower.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileNoPermission);

            if (await blockedHelper.HasBlockBetweenAsync(follower.UserId, following.UserId))
                return new ErrorResult(SocialErrorCodes.CannotFollowUser);

            var existing = await socialFollowDal.GetFollowAsync(followerProfileId, followingProfileId);
            if (existing != null)
                return new SuccessResult("Zaten takip ediliyor.");

            await socialFollowDal.Add(new SocialFollow
            {
                Id = Guid.NewGuid(),
                FollowerProfileId = followerProfileId,
                FollowingProfileId = followingProfileId,
                CreatedAt = DateTime.UtcNow,
            });

            await socialNotificationHelper.NotifyNewFollowerAsync(follower, following);
            return new SuccessResult("Takip edildi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> UnfollowAsync(Guid userId, Guid followerProfileId, Guid followingProfileId)
        {
            var follower = await socialProfileDal.Get(p => p.Id == followerProfileId);
            if (follower == null || follower.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileNoPermission);

            var existing = await socialFollowDal.GetFollowAsync(followerProfileId, followingProfileId);
            if (existing == null)
                return new SuccessResult("Zaten takip edilmiyor.");

            await socialFollowDal.Remove(existing);
            return new SuccessResult("Takipten çıkıldı.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialFollowListItemDto>>> GetFollowersAsync(
            Guid userId, Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit = 30)
        {
            return await GetFollowListAsync(userId, profileId, beforeUtc, beforeId, limit, followers: true);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialFollowListItemDto>>> GetFollowingAsync(
            Guid userId, Guid profileId, DateTime? beforeUtc, Guid? beforeId, int limit = 30)
        {
            return await GetFollowListAsync(userId, profileId, beforeUtc, beforeId, limit, followers: false);
        }

        private async Task<IDataResult<List<SocialFollowListItemDto>>> GetFollowListAsync(
            Guid userId,
            Guid profileId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit,
            bool followers)
        {
            limit = Math.Clamp(limit, 1, 50);
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorDataResult<List<SocialFollowListItemDto>>(SocialErrorCodes.ProfileNotFound);

            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            var rows = followers
                ? await socialFollowDal.GetFollowersPageAsync(profileId, beforeUtc, beforeId, limit)
                : await socialFollowDal.GetFollowingPageAsync(profileId, beforeUtc, beforeId, limit);

            var viewerProfileIds = (await socialProfileDal.GetByUserIdAsync(userId))
                .Select(p => p.Id)
                .ToList();

            var targetProfiles = rows
                .Select(f => followers ? f.FollowerProfile : f.FollowingProfile)
                .Where(p => !blocked.Contains(p.UserId))
                .ToList();

            var targetIds = targetProfiles.Select(p => p.Id).ToList();
            var followingSet = await socialFollowDal.GetFollowingAmongAsync(viewerProfileIds, targetIds);

            var avatarIds = targetProfiles
                .Where(p => p.AvatarImageId.HasValue)
                .Select(p => p.AvatarImageId!.Value)
                .Distinct()
                .ToList();
            var avatarUrls = new Dictionary<Guid, string>();
            foreach (var imageId in avatarIds)
            {
                var img = await imageDal.Get(i => i.Id == imageId);
                if (img != null)
                    avatarUrls[imageId] = img.ImageUrl;
            }

            var dtos = new List<SocialFollowListItemDto>();
            foreach (var row in rows)
            {
                var p = followers ? row.FollowerProfile : row.FollowingProfile;
                if (blocked.Contains(p.UserId)) continue;

                string? avatarUrl = null;
                if (p.AvatarImageId.HasValue && avatarUrls.TryGetValue(p.AvatarImageId.Value, out var url))
                    avatarUrl = url;

                dtos.Add(new SocialFollowListItemDto
                {
                    FollowId = row.Id,
                    FollowedAt = row.CreatedAt,
                    Profile = new SocialProfileDto
                    {
                        Id = p.Id,
                        OwnerType = p.OwnerType,
                        OwnerId = p.OwnerId,
                        UserId = p.UserId,
                        Username = p.Username,
                        Bio = p.Bio,
                        AvatarUrl = avatarUrl,
                        IsPrivate = p.IsPrivate,
                        IsOwnProfile = p.UserId == userId,
                        IsFollowing = followingSet.Contains(p.Id),
                    },
                });
            }

            return new SuccessDataResult<List<SocialFollowListItemDto>>(dtos);
        }

        private async Task NotifyMentionsInCommentAsync(
            SocialProfile actor,
            SocialPost post,
            SocialComment comment,
            string text,
            SocialProfile? postOwner,
            SocialProfile? parentAuthor)
        {
            var usernames = SocialMentionParser.ExtractUsernames(text);
            if (usernames.Count == 0)
                return;

            var skipProfileIds = new HashSet<Guid> { actor.Id };
            if (parentAuthor == null && postOwner != null)
                skipProfileIds.Add(postOwner.Id);
            else if (parentAuthor != null)
                skipProfileIds.Add(parentAuthor.Id);

            foreach (var username in usernames)
            {
                var mentioned = await socialProfileDal.GetByUsernameAsync(username);
                if (mentioned == null || mentioned.Status != SocialContentStatus.Active)
                    continue;
                if (skipProfileIds.Contains(mentioned.Id))
                    continue;

                await socialNotificationHelper.NotifyMentionedAsync(actor, post, comment, mentioned);
            }
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> ToggleMuteAsync(Guid userId, Guid mutedByProfileId, Guid mutedProfileId)
        {
            var muter = await socialProfileDal.Get(p => p.Id == mutedByProfileId);
            if (muter == null || muter.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileInvalid);

            var muted = await socialProfileDal.Get(p => p.Id == mutedProfileId);
            if (muted == null || muted.Status != SocialContentStatus.Active)
                return new ErrorResult(SocialErrorCodes.ProfileNotFound);
            if (mutedByProfileId == mutedProfileId)
                return new ErrorResult(SocialErrorCodes.CannotFollowSelf);

            var existing = await socialProfileMuteDal.GetMuteAsync(mutedByProfileId, mutedProfileId);
            if (existing != null)
            {
                await socialProfileMuteDal.Remove(existing);
                return new SuccessResult("Sessize alma kaldırıldı.");
            }

            await socialProfileMuteDal.Add(new SocialProfileMute
            {
                Id = Guid.NewGuid(),
                MutedByProfileId = mutedByProfileId,
                MutedProfileId = mutedProfileId,
                CreatedAt = DateTime.UtcNow,
            });
            return new SuccessResult("Profil sessize alındı.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialFollowListItemDto>>> GetMutualFollowersAsync(
            Guid userId,
            Guid profileId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 30)
        {
            limit = Math.Clamp(limit, 1, 50);
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorDataResult<List<SocialFollowListItemDto>>(SocialErrorCodes.ProfileNotFound);

            var viewerProfileIds = (await socialProfileDal.GetByUserIdAsync(userId))
                .Select(p => p.Id)
                .ToList();

            if (viewerProfileIds.Count == 0)
                return new SuccessDataResult<List<SocialFollowListItemDto>>(new List<SocialFollowListItemDto>());

            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            var rows = await socialFollowDal.GetMutualFollowersPageAsync(
                viewerProfileIds, profileId, beforeUtc, beforeId, limit);

            var targetProfiles = rows
                .Select(f => f.FollowerProfile)
                .Where(p => !blocked.Contains(p.UserId))
                .ToList();

            var targetIds = targetProfiles.Select(p => p.Id).ToList();
            var followingSet = await socialFollowDal.GetFollowingAmongAsync(viewerProfileIds, targetIds);

            var avatarIds = targetProfiles
                .Where(p => p.AvatarImageId.HasValue)
                .Select(p => p.AvatarImageId!.Value)
                .Distinct()
                .ToList();
            var avatarUrls = new Dictionary<Guid, string>();
            foreach (var imageId in avatarIds)
            {
                var img = await imageDal.Get(i => i.Id == imageId);
                if (img != null)
                    avatarUrls[imageId] = img.ImageUrl;
            }

            var dtos = new List<SocialFollowListItemDto>();
            foreach (var row in rows)
            {
                var p = row.FollowerProfile;
                if (blocked.Contains(p.UserId)) continue;

                string? avatarUrl = null;
                if (p.AvatarImageId.HasValue && avatarUrls.TryGetValue(p.AvatarImageId.Value, out var url))
                    avatarUrl = url;

                var dto = new SocialProfileDto
                {
                    Id = p.Id,
                    OwnerType = p.OwnerType,
                    OwnerId = p.OwnerId,
                    UserId = p.UserId,
                    Username = p.Username,
                    AvatarUrl = avatarUrl,
                    IsPrivate = p.IsPrivate,
                    IsOwnProfile = p.UserId == userId,
                    IsFollowing = followingSet.Contains(p.Id),
                };
                await socialProfileOwnerEnricher.EnrichOwnerMetaAsync(dto, p);

                dtos.Add(new SocialFollowListItemDto
                {
                    FollowId = row.Id,
                    FollowedAt = row.CreatedAt,
                    Profile = dto,
                });
            }

            return new SuccessDataResult<List<SocialFollowListItemDto>>(dtos);
        }

        private async Task<SocialCommentDto> MapCommentDtoAsync(
            SocialComment comment, Guid userId, SocialProfile profile)
        {
            var likeCounts = await socialLikeDal.GetLikeCountsAsync(
                SocialLikeTargetType.Comment, new List<Guid> { comment.Id });
            var replyCounts = !comment.ParentCommentId.HasValue
                ? await socialCommentDal.GetReplyCountsAsync(new List<Guid> { comment.Id })
                : new Dictionary<Guid, int>();

            var viewerProfiles = await socialProfileDal.GetByUserIdAsync(userId);
            var viewerProfileId = viewerProfiles.FirstOrDefault()?.Id;
            var likedIds = viewerProfileId.HasValue
                ? await socialLikeDal.GetLikedTargetIdsAsync(
                    SocialLikeTargetType.Comment, new List<Guid> { comment.Id }, viewerProfileId.Value)
                : new HashSet<Guid>();

            return new SocialCommentDto
            {
                Id = comment.Id,
                PostId = comment.PostId,
                Profile = new SocialProfileDto
                {
                    Id = profile.Id,
                    Username = profile.Username,
                    OwnerType = profile.OwnerType,
                    OwnerId = profile.OwnerId,
                    UserId = profile.UserId,
                    IsOwnProfile = profile.UserId == userId,
                },
                ParentCommentId = comment.ParentCommentId,
                Text = comment.Text,
                LikeCount = likeCounts.TryGetValue(comment.Id, out var lc) ? lc : 0,
                ReplyCount = replyCounts.TryGetValue(comment.Id, out var rc) ? rc : 0,
                IsLiked = likedIds.Contains(comment.Id),
                CreatedAt = comment.CreatedAt,
            };
        }
    }
}
