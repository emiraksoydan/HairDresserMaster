-- Social pin, profile external URL, notification prefs
ALTER TABLE "SocialPosts" ADD COLUMN IF NOT EXISTS "IsPinned" boolean NOT NULL DEFAULT false;
ALTER TABLE "SocialPosts" ADD COLUMN IF NOT EXISTS "PinnedAt" timestamp with time zone NULL;

ALTER TABLE "SocialProfiles" ADD COLUMN IF NOT EXISTS "ExternalUrl" text NULL;

ALTER TABLE "Settings" ADD COLUMN IF NOT EXISTS "SocialNotifyPostEngagement" boolean NOT NULL DEFAULT true;
ALTER TABLE "Settings" ADD COLUMN IF NOT EXISTS "SocialNotifyComments" boolean NOT NULL DEFAULT true;
ALTER TABLE "Settings" ADD COLUMN IF NOT EXISTS "SocialNotifyFollowers" boolean NOT NULL DEFAULT true;
ALTER TABLE "Settings" ADD COLUMN IF NOT EXISTS "SocialNotifyMentions" boolean NOT NULL DEFAULT true;
ALTER TABLE "Settings" ADD COLUMN IF NOT EXISTS "SocialNotifyStoryEngagement" boolean NOT NULL DEFAULT true;
