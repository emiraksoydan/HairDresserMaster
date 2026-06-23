CREATE TABLE IF NOT EXISTS "SocialPostViews" (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "ViewedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SocialPostViews" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialPostViews_SocialPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "SocialPosts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_SocialPostViews_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialPostViews_PostId_ProfileId" ON "SocialPostViews" ("PostId", "ProfileId");
