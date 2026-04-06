-- Sunucuda manuel çalıştırma: Appointments.CancellationReason (iptal notu, isteğe bağlı, max 500)
-- EF migration: 20260406232208_AddAppointmentCancellationReason
-- Önce yedek alın. Tablo "Appointments" (tırnaklı, PostgreSQL).
--
-- Not: DO $$ ... $$ blokları bazı panel / istemcilerde bozulabiliyor; bu yüzden sadece ALTER kullanıldı.

ALTER TABLE "Appointments"
  ADD COLUMN IF NOT EXISTS "CancellationReason" character varying(500) NULL;

-- DDL'i elle uyguladıysanız EF geçmişine kayıt (dotnet ef database update kullanıyorsanız gerekmez)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260406232208_AddAppointmentCancellationReason', '9.0.7'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260406232208_AddAppointmentCancellationReason'
);
