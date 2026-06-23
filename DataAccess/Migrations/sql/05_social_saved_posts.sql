CREATE TABLE IF NOT EXISTS "SocialSavedPosts" (
    "Id" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SocialSavedPosts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SocialSavedPosts_SocialPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "SocialPosts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_SocialSavedPosts_SocialProfiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "SocialProfiles" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialSavedPosts_ProfileId_PostId" ON "SocialSavedPosts" ("ProfileId", "PostId");
