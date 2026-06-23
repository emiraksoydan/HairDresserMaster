START TRANSACTION;

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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611200506_AddSocialStoryHighlights') THEN
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

