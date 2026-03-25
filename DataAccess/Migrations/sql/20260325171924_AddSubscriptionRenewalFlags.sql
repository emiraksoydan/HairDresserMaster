START TRANSACTION;
ALTER TABLE "Users" ADD "SubscriptionAutoRenew" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Users" ADD "SubscriptionCancelAtPeriodEnd" boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260325171924_AddSubscriptionRenewalFlags', '9.0.7');

COMMIT;

