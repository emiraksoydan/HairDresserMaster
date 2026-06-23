CREATE TABLE IF NOT EXISTS "SocialStoryReplies" (
    "Id" uuid NOT NULL,
    "StoryId" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "Text" character varying(2200) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SocialStoryReplies" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialStoryReplies_SocialStories_StoryId" FOREIGN KEY ("StoryId") REFERENCES "SocialStories" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_SocialStoryReplies_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_SocialStoryReplies_ProfileId" ON "SocialStoryReplies" ("ProfileId");
CREATE INDEX IF NOT EXISTS "IX_SocialStoryReplies_StoryId_ProfileId_CreatedAt" ON "SocialStoryReplies" ("StoryId", "ProfileId", "CreatedAt");
