-- Social profile extras: DM policy, cover photo, profile mutes
ALTER TABLE "SocialProfiles" ADD COLUMN IF NOT EXISTS "CoverImageId" uuid NULL;
ALTER TABLE "SocialProfiles" ADD COLUMN IF NOT EXISTS "DmPolicy" integer NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS "SocialProfileMutes" (
    "Id" uuid NOT NULL,
    "MutedByProfileId" uuid NOT NULL,
    "MutedProfileId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SocialProfileMutes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialProfileMutes_SocialProfiles_MutedByProfileId"
        FOREIGN KEY ("MutedByProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_SocialProfileMutes_SocialProfiles_MutedProfileId"
        FOREIGN KEY ("MutedProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialProfileMutes_MutedByProfileId_MutedProfileId"
    ON "SocialProfileMutes" ("MutedByProfileId", "MutedProfileId");
