-- =============================================================================
-- İSTEĞE BAĞLI: Mevcut serbest berber / salon kayıtları için sosyal profil
--
-- 09_social_reset_and_customer_profiles.sql sonrasında veya ilk kurulumda
-- zaten paneli olan kullanıcılar için bir kez çalıştırın.
-- Yeni panel/salon oluşturulduğunda API zaten Ensure*ProfileAsync çağırır.
--
-- OwnerType: 0 = Customer, 1 = FreeBarber, 2 = BarberStore
-- =============================================================================

CREATE OR REPLACE FUNCTION social_make_username(display_name text, owner_id uuid)
RETURNS varchar(32)
LANGUAGE sql
IMMUTABLE
AS $$
    WITH normalized AS (
        SELECT trim(both '_' FROM regexp_replace(
            regexp_replace(
                lower(translate(
                    coalesce(nullif(trim(display_name), ''), 'kullanici'),
                    'çğıöşüÇĞİÖŞÜ',
                    'cgiosucgiosu'
                )),
                '[^a-z0-9_]', '_', 'g'
            ),
            '_+', '_', 'g'
        )) AS base
    ),
    sized AS (
        SELECT CASE
            WHEN length(base) < 3 THEN 'kullanici'
            ELSE left(base, 23)
        END AS prefix
        FROM normalized
    )
    SELECT left(prefix || '_' || substr(md5(owner_id::text), 1, 8), 32)
    FROM sized;
$$;

-- ─── Serbest berber profilleri ───────────────────────────────────────────────

BEGIN;

INSERT INTO "SocialProfiles" (
    "Id", "OwnerType", "OwnerId", "UserId", "Username",
    "Bio", "AvatarImageId", "Latitude", "Longitude",
    "IsPrivate", "Status", "CreatedAt", "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    1,
    fb."Id",
    fb."FreeBarberUserId",
    social_make_username(
        coalesce(
            nullif(trim(concat_ws(' ', fb."FirstName", fb."LastName")), ''),
            'berber'
        ),
        fb."Id"
    ),
    NULL, NULL,
    fb."Latitude", fb."Longitude",
    false, 0,
    now() AT TIME ZONE 'utc',
    now() AT TIME ZONE 'utc'
FROM "FreeBarbers" fb
WHERE fb."IsSuspended" = false
  AND NOT EXISTS (
      SELECT 1 FROM "SocialProfiles" sp
      WHERE sp."OwnerType" = 1 AND sp."OwnerId" = fb."Id"
  );

COMMIT;

-- ─── Salon profilleri ───────────────────────────────────────────────────────

BEGIN;

INSERT INTO "SocialProfiles" (
    "Id", "OwnerType", "OwnerId", "UserId", "Username",
    "Bio", "AvatarImageId", "Latitude", "Longitude",
    "IsPrivate", "Status", "CreatedAt", "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    2,
    s."Id",
    s."BarberStoreOwnerId",
    social_make_username(
        coalesce(nullif(trim(s."StoreName"), ''), 'salon'),
        s."Id"
    ),
    NULL, NULL,
    s."Latitude", s."Longitude",
    false, 0,
    now() AT TIME ZONE 'utc',
    now() AT TIME ZONE 'utc'
FROM "BarberStores" s
WHERE s."IsSuspended" = false
  AND NOT EXISTS (
      SELECT 1 FROM "SocialProfiles" sp
      WHERE sp."OwnerType" = 2 AND sp."OwnerId" = s."Id"
  );

COMMIT;
