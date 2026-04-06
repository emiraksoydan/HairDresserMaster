-- Sunucuda manuel çalıştırma: HelpGuide onboarding + çeviri anahtarı migration'ları
-- EF: AddHelpGuidePromptCompleted (20260406215715), AddHelpGuideTranslationKey (20260406220709)
-- Önce yedek alın. Tablolar "Users" / "HelpGuides" (tırnaklı).
--
-- Not: DO $$ ... $$ blokları bazı panel / istemcilerde bozulabiliyor; bu yüzden sadece ALTER kullanıldı.

-- ========== 1) Users: HelpGuidePromptCompleted ==========
ALTER TABLE "Users"
  ADD COLUMN IF NOT EXISTS "HelpGuidePromptCompleted" boolean NOT NULL DEFAULT TRUE;

-- ========== 2) HelpGuides: TranslationKey ==========
ALTER TABLE "HelpGuides"
  ADD COLUMN IF NOT EXISTS "TranslationKey" text NOT NULL DEFAULT '';

-- ========== 3) EF migration geçmişi (DDL'i elle uyguladıysanız) ==========
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260406215715_AddHelpGuidePromptCompleted', '9.0.7'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260406215715_AddHelpGuidePromptCompleted'
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260406220709_AddHelpGuideTranslationKey', '9.0.7'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260406220709_AddHelpGuideTranslationKey'
);
