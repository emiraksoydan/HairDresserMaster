-- RemovedAt columns + backfill for 30-day archive purge
ALTER TABLE "SocialPosts" ADD COLUMN IF NOT EXISTS "RemovedAt" timestamptz NULL;
ALTER TABLE "SocialStories" ADD COLUMN IF NOT EXISTS "RemovedAt" timestamptz NULL;
ALTER TABLE "SocialStoryHighlights" ADD COLUMN IF NOT EXISTS "RemovedAt" timestamptz NULL;
ALTER TABLE "SocialStoryHighlightItems" ADD COLUMN IF NOT EXISTS "RemovedAt" timestamptz NULL;

UPDATE "SocialPosts" SET "RemovedAt" = "UpdatedAt" WHERE "Status" = 2 AND "RemovedAt" IS NULL;
UPDATE "SocialStories" SET "RemovedAt" = "CreatedAt" WHERE "Status" = 2 AND "RemovedAt" IS NULL;
UPDATE "SocialStoryHighlights" SET "RemovedAt" = "UpdatedAt" WHERE "Status" = 2 AND "RemovedAt" IS NULL;
UPDATE "SocialStoryHighlightItems" SET "RemovedAt" = "CreatedAt" WHERE "Status" = 2 AND "RemovedAt" IS NULL;
