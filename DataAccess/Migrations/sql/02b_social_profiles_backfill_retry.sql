-- Kısmi backfill sonrası tekrar denemek için (transaction içinde hata aldıysanız genelde rollback olur;
-- yine de önce kaç kayıt eksik kontrol edin).
--
--   SELECT "OwnerType", count(*) FROM "SocialProfiles" GROUP BY 1;
--
-- Ardından güncellenmiş 02_social_profiles_backfill.sql dosyasını tekrar çalıştırın.
-- Tam sıfırlama için: 09_social_reset_and_customer_profiles.sql

-- Aynı isimden üretilmiş olabilecek username çakışması (teorik — md5 suffix ile olmamalı)
SELECT "Username", count(*) AS cnt
FROM "SocialProfiles"
GROUP BY "Username"
HAVING count(*) > 1;

-- Henüz sosyal profili olmayan aktif müşteriler (UserType = Customer)
SELECT count(*) AS missing_customer_profiles
FROM "Users" u
WHERE u."IsActive" = true
  AND u."UserType" = 0
  AND NOT EXISTS (
      SELECT 1 FROM "SocialProfiles" sp
      WHERE sp."OwnerType" = 0 AND sp."OwnerId" = u."Id"
  );

-- Hatalı: FreeBarber/Store kullanıcılarında OwnerType=0 müşteri profili (eski backfill artığı)
SELECT count(*) AS wrong_customer_profiles_on_non_customers
FROM "Users" u
INNER JOIN "SocialProfiles" sp ON sp."OwnerType" = 0 AND sp."OwnerId" = u."Id"
WHERE u."UserType" != 0;
