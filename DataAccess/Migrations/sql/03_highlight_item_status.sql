-- Öne çıkan öğeleri için soft-delete durumu (mevcut kayıtlar Active=0)
ALTER TABLE "SocialStoryHighlightItems"
    ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 0;

UPDATE "SocialStoryHighlightItems" SET "Status" = 0 WHERE "Status" IS NULL;
