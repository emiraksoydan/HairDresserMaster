CREATE TABLE IF NOT EXISTS "SocialStoryViews" (
    "Id" uuid NOT NULL,
    "StoryId" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "ViewedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SocialStoryViews" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialStoryViews_SocialStories_StoryId" FOREIGN KEY ("StoryId") REFERENCES "SocialStories" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_SocialStoryViews_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialStoryViews_StoryId_ProfileId" ON "SocialStoryViews" ("StoryId", "ProfileId");
