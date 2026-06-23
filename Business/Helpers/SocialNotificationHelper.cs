using Business.Abstract;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Helpers
{
    public class SocialNotificationHelper(
        INotificationService notificationService,
        IImageDal imageDal,
        ISettingDal settingDal,
        ISocialProfileDal socialProfileDal,
        ISocialProfileMuteDal socialProfileMuteDal,
        BlockedHelper blockedHelper)
    {
        public async Task NotifyPostLikedAsync(SocialProfile actor, SocialPost post, SocialProfile postOwner)
        {
            if (actor.UserId == postOwner.UserId) return;
            if (await blockedHelper.HasBlockBetweenAsync(actor.UserId, postOwner.UserId)) return;
            if (await IsActorMutedByRecipientAsync(postOwner.UserId, actor.Id)) return;
            if (!await ShouldNotifyAsync(postOwner.UserId, NotificationType.SocialPostLiked)) return;

            var avatarUrl = await ResolveAvatarUrlAsync(actor);
            var title = $"@{actor.Username} gönderini beğendi";
            var payload = new SocialNotificationPayloadDto
            {
                Kind = "post_liked",
                PostId = post.Id,
                ActorProfileId = actor.Id,
                ActorUsername = actor.Username,
                ActorAvatarUrl = avatarUrl,
                TargetProfileId = postOwner.Id,
            };

            await notificationService.CreateAndPushAsync(
                postOwner.UserId,
                NotificationType.SocialPostLiked,
                null,
                title,
                payload,
                null);
        }

        public async Task NotifyPostCommentedAsync(
            SocialProfile actor, SocialPost post, SocialProfile postOwner, SocialComment comment)
        {
            if (actor.UserId == postOwner.UserId) return;
            if (await blockedHelper.HasBlockBetweenAsync(actor.UserId, postOwner.UserId)) return;
            if (await IsActorMutedByRecipientAsync(postOwner.UserId, actor.Id)) return;
            if (!await ShouldNotifyAsync(postOwner.UserId, NotificationType.SocialPostCommented)) return;

            var avatarUrl = await ResolveAvatarUrlAsync(actor);
            var title = $"@{actor.Username} gönderine yorum yaptı";
            var body = Truncate(comment.Text, 120);
            var payload = new SocialNotificationPayloadDto
            {
                Kind = "post_commented",
                PostId = post.Id,
                CommentId = comment.Id,
                ActorProfileId = actor.Id,
                ActorUsername = actor.Username,
                ActorAvatarUrl = avatarUrl,
                TargetProfileId = postOwner.Id,
            };

            await notificationService.CreateAndPushAsync(
                postOwner.UserId,
                NotificationType.SocialPostCommented,
                null,
                title,
                payload,
                body);
        }

        public async Task NotifyCommentRepliedAsync(
            SocialProfile actor,
            SocialPost post,
            SocialProfile parentAuthor,
            SocialComment reply,
            Guid parentCommentId)
        {
            if (actor.UserId == parentAuthor.UserId) return;
            if (await blockedHelper.HasBlockBetweenAsync(actor.UserId, parentAuthor.UserId)) return;
            if (await IsActorMutedByRecipientAsync(parentAuthor.UserId, actor.Id)) return;
            if (!await ShouldNotifyAsync(parentAuthor.UserId, NotificationType.SocialCommentReplied)) return;

            var avatarUrl = await ResolveAvatarUrlAsync(actor);
            var title = $"@{actor.Username} yorumuna yanıt verdi";
            var body = Truncate(reply.Text, 120);
            var payload = new SocialNotificationPayloadDto
            {
                Kind = "comment_replied",
                PostId = post.Id,
                CommentId = reply.Id,
                ParentCommentId = parentCommentId,
                ActorProfileId = actor.Id,
                ActorUsername = actor.Username,
                ActorAvatarUrl = avatarUrl,
                TargetProfileId = parentAuthor.Id,
            };

            await notificationService.CreateAndPushAsync(
                parentAuthor.UserId,
                NotificationType.SocialCommentReplied,
                null,
                title,
                payload,
                body);
        }

        public async Task NotifyMentionedAsync(
            SocialProfile actor, SocialPost post, SocialComment comment, SocialProfile mentioned)
        {
            if (actor.UserId == mentioned.UserId) return;
            if (await blockedHelper.HasBlockBetweenAsync(actor.UserId, mentioned.UserId)) return;
            if (await IsActorMutedByRecipientAsync(mentioned.UserId, actor.Id)) return;
            if (!await ShouldNotifyAsync(mentioned.UserId, NotificationType.SocialMentioned)) return;

            var avatarUrl = await ResolveAvatarUrlAsync(actor);
            var title = $"@{actor.Username} seni bir yorumda etiketledi";
            var body = Truncate(comment.Text, 120);
            var payload = new SocialNotificationPayloadDto
            {
                Kind = "mentioned",
                PostId = post.Id,
                CommentId = comment.Id,
                ActorProfileId = actor.Id,
                ActorUsername = actor.Username,
                ActorAvatarUrl = avatarUrl,
                TargetProfileId = mentioned.Id,
            };

            await notificationService.CreateAndPushAsync(
                mentioned.UserId,
                NotificationType.SocialMentioned,
                null,
                title,
                payload,
                body);
        }

        public async Task NotifyNewFollowerAsync(SocialProfile follower, SocialProfile following)
        {
            if (follower.UserId == following.UserId) return;
            if (await blockedHelper.HasBlockBetweenAsync(follower.UserId, following.UserId)) return;
            if (await IsActorMutedByRecipientAsync(following.UserId, follower.Id)) return;
            if (!await ShouldNotifyAsync(following.UserId, NotificationType.SocialNewFollower)) return;

            var avatarUrl = await ResolveAvatarUrlAsync(follower);
            var title = $"@{follower.Username} seni takip etmeye başladı";
            var payload = new SocialNotificationPayloadDto
            {
                Kind = "new_follower",
                ActorProfileId = follower.Id,
                ActorUsername = follower.Username,
                ActorAvatarUrl = avatarUrl,
                TargetProfileId = following.Id,
            };

            await notificationService.CreateAndPushAsync(
                following.UserId,
                NotificationType.SocialNewFollower,
                null,
                title,
                payload,
                null);
        }

        public async Task NotifyStoryLikedAsync(SocialProfile actor, SocialStory story, SocialProfile storyOwner)
        {
            if (actor.UserId == storyOwner.UserId) return;
            if (await blockedHelper.HasBlockBetweenAsync(actor.UserId, storyOwner.UserId)) return;
            if (await IsActorMutedByRecipientAsync(storyOwner.UserId, actor.Id)) return;
            if (!await ShouldNotifyAsync(storyOwner.UserId, NotificationType.SocialStoryLiked)) return;

            var avatarUrl = await ResolveAvatarUrlAsync(actor);
            var title = $"@{actor.Username} hikayeni beğendi";
            var payload = new SocialNotificationPayloadDto
            {
                Kind = "story_liked",
                StoryId = story.Id,
                ActorProfileId = actor.Id,
                ActorUsername = actor.Username,
                ActorAvatarUrl = avatarUrl,
                TargetProfileId = storyOwner.Id,
            };

            await notificationService.CreateAndPushAsync(
                storyOwner.UserId,
                NotificationType.SocialStoryLiked,
                null,
                title,
                payload,
                null);
        }

        public async Task NotifyStoryRepliedAsync(
            SocialProfile actor, SocialStory story, SocialProfile storyOwner, string replyText)
        {
            if (actor.UserId == storyOwner.UserId) return;
            if (await blockedHelper.HasBlockBetweenAsync(actor.UserId, storyOwner.UserId)) return;
            if (await IsActorMutedByRecipientAsync(storyOwner.UserId, actor.Id)) return;
            if (!await ShouldNotifyAsync(storyOwner.UserId, NotificationType.SocialStoryReplied)) return;

            var avatarUrl = await ResolveAvatarUrlAsync(actor);
            var title = $"@{actor.Username} hikayene yanıt verdi";
            var body = Truncate(replyText, 120);
            var payload = new SocialNotificationPayloadDto
            {
                Kind = "story_replied",
                StoryId = story.Id,
                ActorProfileId = actor.Id,
                ActorUsername = actor.Username,
                ActorAvatarUrl = avatarUrl,
                TargetProfileId = storyOwner.Id,
            };

            await notificationService.CreateAndPushAsync(
                storyOwner.UserId,
                NotificationType.SocialStoryReplied,
                null,
                title,
                payload,
                body);
        }

        private async Task<bool> IsActorMutedByRecipientAsync(Guid recipientUserId, Guid actorProfileId)
        {
            var recipientProfiles = await socialProfileDal.GetByUserIdAsync(recipientUserId);
            if (recipientProfiles.Count == 0) return false;
            var recipientProfileIds = recipientProfiles.Select(p => p.Id).ToList();
            return await socialProfileMuteDal.IsMutedByAnyAsync(recipientProfileIds, actorProfileId);
        }

        private async Task<bool> ShouldNotifyAsync(Guid userId, NotificationType type)
        {
            var setting = await settingDal.GetByUserIdAsync(userId);
            if (setting == null) return true;

            return type switch
            {
                NotificationType.SocialPostLiked => setting.SocialNotifyPostEngagement,
                NotificationType.SocialPostCommented => setting.SocialNotifyComments,
                NotificationType.SocialCommentReplied => setting.SocialNotifyComments,
                NotificationType.SocialNewFollower => setting.SocialNotifyFollowers,
                NotificationType.SocialMentioned => setting.SocialNotifyMentions,
                NotificationType.SocialStoryLiked => setting.SocialNotifyStoryEngagement,
                NotificationType.SocialStoryReplied => setting.SocialNotifyStoryEngagement,
                _ => true,
            };
        }

        private async Task<string?> ResolveAvatarUrlAsync(SocialProfile profile)
        {
            if (!profile.AvatarImageId.HasValue) return null;
            var img = await imageDal.Get(i => i.Id == profile.AvatarImageId.Value);
            return img?.ImageUrl;
        }

        private static string? Truncate(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            return text.Length <= max ? text : text[..max] + "…";
        }
    }
}
