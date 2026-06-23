-- Gümüş Makas — Sosyal medya PostgreSQL migration (idempotent)
-- Migration'lar:
--   20260611181154_AddSocialMediaTables
--   20260611190019_AddSocialChatThreadFlag
--   20260611200506_AddSocialStoryHighlights
--
-- Sunucuda çalıştırmadan önce:
--   SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
-- ile hangilerinin zaten uygulandığını kontrol edin.

START TRANSACTION;

-- ─── 1) Sosyal medya tabloları ───────────────────────────────────────────────

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    CREATE TABLE "SocialProfiles" (
        "Id" uuid NOT NULL,
        "OwnerType" integer NOT NULL,
        "OwnerId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Username" character varying(32) NOT NULL,
        "Bio" character varying(500),
        "AvatarImageId" uuid,
        "Latitude" double precision,
        "Longitude" double precision,
        "IsPrivate" boolean NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialProfiles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    CREATE TABLE "SocialFollows" (
        "Id" uuid NOT NULL,
        "FollowerProfileId" uuid NOT NULL,
        "FollowingProfileId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialFollows" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialFollows_SocialProfiles_FollowerProfileId" FOREIGN KEY ("FollowerProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SocialFollows_SocialProfiles_FollowingProfileId" FOREIGN KEY ("FollowingProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    CREATE TABLE "SocialLikes" (
        "Id" uuid NOT NULL,
        "TargetType" integer NOT NULL,
        "TargetId" uuid NOT NULL,
        "ProfileId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialLikes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialLikes_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    CREATE TABLE "SocialPosts" (
        "Id" uuid NOT NULL,
        "ProfileId" uuid NOT NULL,
        "Caption" character varying(2200),
        "Type" integer NOT NULL,
        "ViewCount" integer NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialPosts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialPosts_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    CREATE TABLE "SocialStories" (
        "Id" uuid NOT NULL,
        "ProfileId" uuid NOT NULL,
        "MediaUrl" character varying(2048) NOT NULL,
        "ThumbnailUrl" character varying(2048),
        "DurationSec" integer,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialStories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialStories_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    CREATE TABLE "SocialComments" (
        "Id" uuid NOT NULL,
        "PostId" uuid NOT NULL,
        "ProfileId" uuid NOT NULL,
        "ParentCommentId" uuid,
        "Text" character varying(1000) NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialComments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialComments_SocialPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "SocialPosts" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SocialComments_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    CREATE TABLE "SocialPostMedia" (
        "Id" uuid NOT NULL,
        "PostId" uuid NOT NULL,
        "SortOrder" integer NOT NULL,
        "MediaUrl" character varying(2048) NOT NULL,
        "ThumbnailUrl" character varying(2048),
        "DurationSec" integer,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialPostMedia" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialPostMedia_SocialPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "SocialPosts" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    CREATE INDEX "IX_SocialComments_PostId_CreatedAt" ON "SocialComments" ("PostId", "CreatedAt");
    CREATE INDEX "IX_SocialComments_ProfileId" ON "SocialComments" ("ProfileId");
    CREATE UNIQUE INDEX "IX_SocialFollows_FollowerProfileId_FollowingProfileId" ON "SocialFollows" ("FollowerProfileId", "FollowingProfileId");
    CREATE INDEX "IX_SocialFollows_FollowingProfileId" ON "SocialFollows" ("FollowingProfileId");
    CREATE INDEX "IX_SocialLikes_ProfileId" ON "SocialLikes" ("ProfileId");
    CREATE UNIQUE INDEX "IX_SocialLikes_TargetType_TargetId_ProfileId" ON "SocialLikes" ("TargetType", "TargetId", "ProfileId");
    CREATE INDEX "IX_SocialPostMedia_PostId_SortOrder" ON "SocialPostMedia" ("PostId", "SortOrder");
    CREATE INDEX "IX_SocialPosts_ProfileId_CreatedAt" ON "SocialPosts" ("ProfileId", "CreatedAt");
    CREATE INDEX "IX_SocialProfiles_Latitude_Longitude" ON "SocialProfiles" ("Latitude", "Longitude");
    CREATE UNIQUE INDEX "IX_SocialProfiles_OwnerType_OwnerId" ON "SocialProfiles" ("OwnerType", "OwnerId");
    CREATE INDEX "IX_SocialProfiles_UserId" ON "SocialProfiles" ("UserId");
    CREATE UNIQUE INDEX "IX_SocialProfiles_Username" ON "SocialProfiles" ("Username");
    CREATE INDEX "IX_SocialStories_ProfileId_ExpiresAt" ON "SocialStories" ("ProfileId", "ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611181154_AddSocialMediaTables') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260611181154_AddSocialMediaTables', '9.0.7');
    END IF;
END $EF$;

-- ─── 2) Sosyal DM thread bayrağı ───────────────────────────────────────────

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611190019_AddSocialChatThreadFlag') THEN
    ALTER TABLE "ChatThreads" ADD "IsSocialThread" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611190019_AddSocialChatThreadFlag') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260611190019_AddSocialChatThreadFlag', '9.0.7');
    END IF;
END $EF$;

-- ─── 3) Hikaye öne çıkanlar ──────────────────────────────────────────────────

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611200506_AddSocialStoryHighlights') THEN
    CREATE TABLE "SocialStoryHighlights" (
        "Id" uuid NOT NULL,
        "ProfileId" uuid NOT NULL,
        "Title" character varying(64) NOT NULL,
        "CoverUrl" character varying(2048),
        "SortOrder" integer NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialStoryHighlights" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialStoryHighlights_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611200506_AddSocialStoryHighlights') THEN
    CREATE TABLE "SocialStoryHighlightItems" (
        "Id" uuid NOT NULL,
        "HighlightId" uuid NOT NULL,
        "SourceStoryId" uuid,
        "MediaUrl" character varying(2048) NOT NULL,
        "ThumbnailUrl" character varying(2048),
        "DurationSec" integer,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SocialStoryHighlightItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialStoryHighlightItems_SocialStoryHighlights_HighlightId" FOREIGN KEY ("HighlightId") REFERENCES "SocialStoryHighlights" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611200506_AddSocialStoryHighlights') THEN
    CREATE INDEX "IX_SocialStoryHighlightItems_HighlightId_SortOrder" ON "SocialStoryHighlightItems" ("HighlightId", "SortOrder");
    CREATE INDEX "IX_SocialStoryHighlights_ProfileId_SortOrder" ON "SocialStoryHighlights" ("ProfileId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611200506_AddSocialStoryHighlights') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260611200506_AddSocialStoryHighlights', '9.0.7');
    END IF;
END $EF$;

COMMIT;
