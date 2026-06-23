-- =============================================================================
-- Sosyal medya şema (migration / __EFMigrationsHistory YOK)
-- PostgreSQL — tek seferlik çalıştırın.
-- Zaten tablolar varsa: CREATE IF NOT EXISTS / ADD COLUMN IF NOT EXISTS kullanıldı.
-- =============================================================================

-- ─── 1) Ana sosyal tablolar ───────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "SocialProfiles" (
    "Id" uuid NOT NULL,
    "OwnerType" integer NOT NULL,
    "OwnerId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Username" character varying(32) NOT NULL,
    "Bio" character varying(500),
    "AvatarImageId" uuid,
    "Latitude" double precision,
    "Longitude" double precision,
    "IsPrivate" boolean NOT NULL DEFAULT false,
    "Status" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialProfiles" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "SocialFollows" (
    "Id" uuid NOT NULL,
    "FollowerProfileId" uuid NOT NULL,
    "FollowingProfileId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialFollows" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialFollows_SocialProfiles_FollowerProfileId"
        FOREIGN KEY ("FollowerProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_SocialFollows_SocialProfiles_FollowingProfileId"
        FOREIGN KEY ("FollowingProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "SocialLikes" (
    "Id" uuid NOT NULL,
    "TargetType" integer NOT NULL,
    "TargetId" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialLikes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialLikes_SocialProfiles_ProfileId"
        FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "SocialPosts" (
    "Id" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "Caption" character varying(2200),
    "Type" integer NOT NULL,
    "ViewCount" integer NOT NULL DEFAULT 0,
    "Status" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialPosts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialPosts_SocialProfiles_ProfileId"
        FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "SocialStories" (
    "Id" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "MediaUrl" character varying(2048) NOT NULL,
    "ThumbnailUrl" character varying(2048),
    "DurationSec" integer,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialStories" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialStories_SocialProfiles_ProfileId"
        FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "SocialComments" (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "ParentCommentId" uuid,
    "Text" character varying(1000) NOT NULL,
    "Status" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialComments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialComments_SocialPosts_PostId"
        FOREIGN KEY ("PostId") REFERENCES "SocialPosts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_SocialComments_SocialProfiles_ProfileId"
        FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "SocialPostMedia" (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "SortOrder" integer NOT NULL,
    "MediaUrl" character varying(2048) NOT NULL,
    "ThumbnailUrl" character varying(2048),
    "DurationSec" integer,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialPostMedia" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialPostMedia_SocialPosts_PostId"
        FOREIGN KEY ("PostId") REFERENCES "SocialPosts" ("Id") ON DELETE CASCADE
);

-- ─── 2) İndeksler ─────────────────────────────────────────────────────────────

CREATE INDEX IF NOT EXISTS "IX_SocialComments_PostId_CreatedAt"
    ON "SocialComments" ("PostId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_SocialComments_ProfileId"
    ON "SocialComments" ("ProfileId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialFollows_FollowerProfileId_FollowingProfileId"
    ON "SocialFollows" ("FollowerProfileId", "FollowingProfileId");
CREATE INDEX IF NOT EXISTS "IX_SocialFollows_FollowingProfileId"
    ON "SocialFollows" ("FollowingProfileId");
CREATE INDEX IF NOT EXISTS "IX_SocialLikes_ProfileId"
    ON "SocialLikes" ("ProfileId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialLikes_TargetType_TargetId_ProfileId"
    ON "SocialLikes" ("TargetType", "TargetId", "ProfileId");
CREATE INDEX IF NOT EXISTS "IX_SocialPostMedia_PostId_SortOrder"
    ON "SocialPostMedia" ("PostId", "SortOrder");
CREATE INDEX IF NOT EXISTS "IX_SocialPosts_ProfileId_CreatedAt"
    ON "SocialPosts" ("ProfileId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_SocialProfiles_Latitude_Longitude"
    ON "SocialProfiles" ("Latitude", "Longitude");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialProfiles_OwnerType_OwnerId"
    ON "SocialProfiles" ("OwnerType", "OwnerId");
CREATE INDEX IF NOT EXISTS "IX_SocialProfiles_UserId"
    ON "SocialProfiles" ("UserId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialProfiles_Username"
    ON "SocialProfiles" ("Username");
CREATE INDEX IF NOT EXISTS "IX_SocialStories_ProfileId_ExpiresAt"
    ON "SocialStories" ("ProfileId", "ExpiresAt");

-- ─── 3) Sosyal DM (ChatThreads) ─────────────────────────────────────────────

ALTER TABLE "ChatThreads"
    ADD COLUMN IF NOT EXISTS "IsSocialThread" boolean NOT NULL DEFAULT false;

-- ─── 4) Hikaye öne çıkanlar ─────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "SocialStoryHighlights" (
    "Id" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "Title" character varying(64) NOT NULL,
    "CoverUrl" character varying(2048),
    "SortOrder" integer NOT NULL DEFAULT 0,
    "Status" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialStoryHighlights" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialStoryHighlights_SocialProfiles_ProfileId"
        FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "SocialStoryHighlightItems" (
    "Id" uuid NOT NULL,
    "HighlightId" uuid NOT NULL,
    "SourceStoryId" uuid,
    "MediaUrl" character varying(2048) NOT NULL,
    "ThumbnailUrl" character varying(2048),
    "DurationSec" integer,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_SocialStoryHighlightItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialStoryHighlightItems_SocialStoryHighlights_HighlightId"
        FOREIGN KEY ("HighlightId") REFERENCES "SocialStoryHighlights" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_SocialStoryHighlightItems_HighlightId_SortOrder"
    ON "SocialStoryHighlightItems" ("HighlightId", "SortOrder");
CREATE INDEX IF NOT EXISTS "IX_SocialStoryHighlights_ProfileId_SortOrder"
    ON "SocialStoryHighlights" ("ProfileId", "SortOrder");
