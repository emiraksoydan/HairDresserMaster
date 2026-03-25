CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Appointments" (
        "Id" uuid NOT NULL,
        "ChairId" uuid,
        "ChairName" text,
        "StartTime" interval,
        "EndTime" interval,
        "AppointmentDate" date,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "BarberStoreUserId" uuid,
        "StoreId" uuid,
        "CustomerUserId" uuid,
        "FreeBarberUserId" uuid,
        "ManuelBarberId" uuid,
        "RequestedBy" integer NOT NULL,
        "StoreSelectionType" integer,
        "StoreDecision" integer,
        "FreeBarberDecision" integer,
        "CustomerDecision" integer,
        "PendingExpiresAt" timestamp with time zone,
        "CancelledByUserId" uuid,
        "ApprovedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "RowVersion" bytea,
        "Note" text,
        "IsDeletedByCustomerUserId" boolean NOT NULL,
        "IsDeletedByBarberStoreUserId" boolean NOT NULL,
        "IsDeletedByFreeBarberUserId" boolean NOT NULL,
        CONSTRAINT "PK_Appointments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "BarberChairs" (
        "Id" uuid NOT NULL,
        "StoreId" uuid NOT NULL,
        "ManuelBarberId" uuid,
        "Name" text,
        "IsAvailable" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_BarberChairs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "BarberStores" (
        "Id" uuid NOT NULL,
        "BarberStoreOwnerId" uuid NOT NULL,
        "StoreName" text NOT NULL,
        "StoreNo" text NOT NULL,
        "AddressDescription" text NOT NULL,
        "Latitude" double precision NOT NULL,
        "Longitude" double precision NOT NULL,
        "Type" integer NOT NULL,
        "PricingType" integer NOT NULL,
        "PricingValue" double precision NOT NULL,
        "TaxDocumentImageId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_BarberStores" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Blockeds" (
        "Id" uuid NOT NULL,
        "BlockedFromUserId" uuid NOT NULL,
        "BlockedToUserId" uuid NOT NULL,
        "BlockReason" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_Blockeds" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Categories" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "ParentId" uuid,
        CONSTRAINT "PK_Categories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Categories_Categories_ParentId" FOREIGN KEY ("ParentId") REFERENCES "Categories" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "ChatMessages" (
        "Id" uuid NOT NULL,
        "ThreadId" uuid NOT NULL,
        "AppointmentId" uuid,
        "SenderUserId" uuid NOT NULL,
        "Text" text NOT NULL,
        "IsSystem" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ChatMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "ChatThreads" (
        "Id" uuid NOT NULL,
        "AppointmentId" uuid,
        "FavoriteFromUserId" uuid,
        "FavoriteToUserId" uuid,
        "StoreId" uuid,
        "CustomerUserId" uuid,
        "StoreOwnerUserId" uuid,
        "FreeBarberUserId" uuid,
        "CustomerUnreadCount" integer NOT NULL,
        "StoreUnreadCount" integer NOT NULL,
        "FreeBarberUnreadCount" integer NOT NULL,
        "LastMessageAt" timestamp with time zone,
        "LastMessagePreview" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "IsDeletedByCustomerUserId" boolean NOT NULL,
        "IsDeletedByStoreOwnerUserId" boolean NOT NULL,
        "IsDeletedByFreeBarberUserId" boolean NOT NULL,
        CONSTRAINT "PK_ChatThreads" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Complaints" (
        "Id" uuid NOT NULL,
        "ComplaintFromUserId" uuid NOT NULL,
        "ComplaintToUserId" uuid NOT NULL,
        "AppointmentId" uuid,
        "ComplaintReason" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_Complaints" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Favorites" (
        "Id" uuid NOT NULL,
        "FavoritedFromId" uuid NOT NULL,
        "FavoritedToId" uuid NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Favorites" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "FreeBarbers" (
        "Id" uuid NOT NULL,
        "FreeBarberUserId" uuid NOT NULL,
        "FirstName" text NOT NULL,
        "LastName" text NOT NULL,
        "Type" integer NOT NULL,
        "Latitude" double precision NOT NULL,
        "Longitude" double precision NOT NULL,
        "BarberCertificateImageId" uuid,
        "BeautySalonCertificateImageId" uuid,
        "IsAvailable" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_FreeBarbers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "HelpGuides" (
        "Id" uuid NOT NULL,
        "UserType" integer NOT NULL,
        "Title" text NOT NULL,
        "Description" text NOT NULL,
        "Order" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_HelpGuides" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Images" (
        "Id" uuid NOT NULL,
        "ImageUrl" text NOT NULL,
        "OwnerType" integer NOT NULL,
        "ImageOwnerId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Images" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "ManuelBarbers" (
        "Id" uuid NOT NULL,
        "StoreId" uuid NOT NULL,
        "FullName" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ManuelBarbers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "MessageReadReceipts" (
        "Id" uuid NOT NULL,
        "MessageId" uuid NOT NULL,
        "ThreadId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ReadAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_MessageReadReceipts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Notifications" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "AppointmentId" uuid,
        "Type" integer NOT NULL,
        "Title" text NOT NULL,
        "Body" text,
        "PayloadJson" text NOT NULL,
        "IsRead" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ReadAt" timestamp with time zone,
        CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "OperationClaims" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        CONSTRAINT "PK_OperationClaims" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "RefreshTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TokenHash" bytea NOT NULL,
        "TokenSalt" bytea NOT NULL,
        "Fingerprint" character varying(64) NOT NULL,
        "FamilyId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedByIp" text,
        "Device" character varying(128),
        "ExpiresAt" timestamp with time zone NOT NULL,
        "RevokedAt" timestamp with time zone,
        "RevokedByIp" text,
        "ReplacedByFingerprint" character varying(64),
        CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Requests" (
        "Id" uuid NOT NULL,
        "RequestFromUserId" uuid NOT NULL,
        "RequestTitle" text NOT NULL,
        "RequestMessage" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "IsProcessed" boolean NOT NULL,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_Requests" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "ServiceOfferings" (
        "Id" uuid NOT NULL,
        "OwnerId" uuid NOT NULL,
        "ServiceName" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        CONSTRAINT "PK_ServiceOfferings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "WorkingHours" (
        "Id" uuid NOT NULL,
        "OwnerId" uuid NOT NULL,
        "DayOfWeek" integer NOT NULL,
        "StartTime" interval NOT NULL,
        "EndTime" interval NOT NULL,
        "IsClosed" boolean NOT NULL,
        CONSTRAINT "PK_WorkingHours" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "AppointmentServiceOfferings" (
        "Id" uuid NOT NULL,
        "AppointmentId" uuid NOT NULL,
        "ServiceOfferingId" uuid NOT NULL,
        "ServiceName" text NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        CONSTRAINT "PK_AppointmentServiceOfferings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AppointmentServiceOfferings_Appointments_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "Appointments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "FirstName" text NOT NULL,
        "LastName" text NOT NULL,
        "PhoneNumber" character varying(20) NOT NULL,
        "IsActive" boolean NOT NULL,
        "ImageId" uuid,
        "UserType" integer NOT NULL,
        "CustomerNumber" text NOT NULL,
        "IsKvkkApproved" boolean NOT NULL,
        "KvkkApprovedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "IsBanned" boolean NOT NULL,
        "BanReason" text,
        "TrialEndDate" timestamp with time zone NOT NULL,
        "SubscriptionEndDate" timestamp with time zone,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Users_Images_ImageId" FOREIGN KEY ("ImageId") REFERENCES "Images" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Ratings" (
        "Id" uuid NOT NULL,
        "TargetId" uuid NOT NULL,
        "RatedFromId" uuid NOT NULL,
        "Score" double precision NOT NULL,
        "Comment" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "AppointmentId" uuid NOT NULL,
        CONSTRAINT "PK_Ratings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Ratings_Users_RatedFromId" FOREIGN KEY ("RatedFromId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "Settings" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ShowImageAnimation" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Settings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Settings_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "UserFcmTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "FcmToken" text NOT NULL,
        "DeviceId" text,
        "Platform" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_UserFcmTokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserFcmTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE TABLE "UserOperationClaims" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "OperationClaimId" uuid NOT NULL,
        CONSTRAINT "PK_UserOperationClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserOperationClaims_OperationClaims_OperationClaimId" FOREIGN KEY ("OperationClaimId") REFERENCES "OperationClaims" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_UserOperationClaims_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Appointments_BarberStoreUserId_Status" ON "Appointments" ("BarberStoreUserId", "Status") WHERE "Status" IN (0, 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_Appointments_ChairId_AppointmentDate_StartTime_EndTime" ON "Appointments" ("ChairId", "AppointmentDate", "StartTime", "EndTime") WHERE "Status" IN (0, 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Appointments_CustomerUserId_Status" ON "Appointments" ("CustomerUserId", "Status") WHERE "Status" IN (0, 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Appointments_FreeBarberUserId_Status" ON "Appointments" ("FreeBarberUserId", "Status") WHERE "Status" IN (0, 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Appointments_Status_PendingExpiresAt" ON "Appointments" ("Status", "PendingExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_AppointmentServiceOfferings_AppointmentId" ON "AppointmentServiceOfferings" ("AppointmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Blockeds_BlockedFromUserId" ON "Blockeds" ("BlockedFromUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_Blockeds_BlockedFromUserId_BlockedToUserId" ON "Blockeds" ("BlockedFromUserId", "BlockedToUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Blockeds_BlockedToUserId" ON "Blockeds" ("BlockedToUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Categories_ParentId" ON "Categories" ("ParentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_ChatMessages_ThreadId_CreatedAt" ON "ChatMessages" ("ThreadId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_ChatThreads_AppointmentId" ON "ChatThreads" ("AppointmentId") WHERE "AppointmentId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_ChatThreads_FavoriteFromUserId_FavoriteToUserId_StoreId" ON "ChatThreads" ("FavoriteFromUserId", "FavoriteToUserId", "StoreId") WHERE "FavoriteFromUserId" IS NOT NULL AND "FavoriteToUserId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Complaints_ComplaintFromUserId" ON "Complaints" ("ComplaintFromUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_Complaints_ComplaintFromUserId_ComplaintToUserId_Appointmen~" ON "Complaints" ("ComplaintFromUserId", "ComplaintToUserId", "AppointmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Favorites_FavoritedFromId_FavoritedToId_IsActive" ON "Favorites" ("FavoritedFromId", "FavoritedToId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Favorites_FavoritedToId_FavoritedFromId_IsActive" ON "Favorites" ("FavoritedToId", "FavoritedFromId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Favorites_FavoritedToId_IsActive" ON "Favorites" ("FavoritedToId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_FreeBarbers_FreeBarberUserId" ON "FreeBarbers" ("FreeBarberUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_FreeBarbers_IsAvailable_Latitude_Longitude" ON "FreeBarbers" ("IsAvailable", "Latitude", "Longitude");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_HelpGuides_UserType_IsActive_Order" ON "HelpGuides" ("UserType", "IsActive", "Order");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Images_ImageOwnerId" ON "Images" ("ImageOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Images_OwnerType_ImageOwnerId" ON "Images" ("OwnerType", "ImageOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_MessageReadReceipts_MessageId_UserId" ON "MessageReadReceipts" ("MessageId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_MessageReadReceipts_ThreadId_UserId" ON "MessageReadReceipts" ("ThreadId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Notifications_UserId_IsRead_CreatedAt" ON "Notifications" ("UserId", "IsRead", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Ratings_RatedFromId" ON "Ratings" ("RatedFromId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Ratings_TargetId_Score" ON "Ratings" ("TargetId", "Score");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_RefreshTokens_FamilyId" ON "RefreshTokens" ("FamilyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_RefreshTokens_Fingerprint" ON "RefreshTokens" ("Fingerprint");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_RefreshTokens_UserId_RevokedAt_ExpiresAt" ON "RefreshTokens" ("UserId", "RevokedAt", "ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Requests_RequestFromUserId" ON "Requests" ("RequestFromUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_Settings_UserId" ON "Settings" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE UNIQUE INDEX "IX_UserFcmTokens_FcmToken" ON "UserFcmTokens" ("FcmToken");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_UserFcmTokens_UserId_IsActive" ON "UserFcmTokens" ("UserId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_UserOperationClaims_OperationClaimId" ON "UserOperationClaims" ("OperationClaimId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_UserOperationClaims_UserId" ON "UserOperationClaims" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_User_PhoneNumber" ON "Users" ("PhoneNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    CREATE INDEX "IX_Users_ImageId" ON "Users" ("ImageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322152736_mig-1') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260322152736_mig-1', '9.0.7');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260324195837_AddSavedFilters') THEN
    CREATE TABLE "SavedFilters" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Name" text NOT NULL,
        "FilterCriteriaJson" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SavedFilters" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260324195837_AddSavedFilters') THEN
    CREATE INDEX "IX_SavedFilters_UserId_CreatedAt" ON "SavedFilters" ("UserId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260324195837_AddSavedFilters') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260324195837_AddSavedFilters', '9.0.7');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260325171924_AddSubscriptionRenewalFlags') THEN
    ALTER TABLE "Users" ADD "SubscriptionAutoRenew" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260325171924_AddSubscriptionRenewalFlags') THEN
    ALTER TABLE "Users" ADD "SubscriptionCancelAtPeriodEnd" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260325171924_AddSubscriptionRenewalFlags') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260325171924_AddSubscriptionRenewalFlags', '9.0.7');
    END IF;
END $EF$;
COMMIT;

