-- =============================================================================

-- Yalnızca müşteri (Customer) kullanıcılar için sosyal profil oluştur

--

-- 25P02 hatası aldıysanız (transaction aborted):

--   Önce tek başına çalıştırın:  ROLLBACK;

--   Sonra bu dosyayı baştan çalıştırın.

--

-- Serbest berber / salon: API panel oluşturulunca otomatik profil açar.

-- Mevcut paneller için isteğe bağlı: 02c_social_profiles_backfill_existing_panels.sql

-- Tam sıfırlama + müşteri profilleri: 09_social_reset_and_customer_profiles.sql

--

-- OwnerType: 0 = Customer, 1 = FreeBarber, 2 = BarberStore

-- UserType:  0 = Customer, 1 = FreeBarber, 2 = BarberStore

-- Status:    0 = Active

-- =============================================================================



-- Kullanıcı adı: isim normalize + md5(owner_id) suffix (çakışma yok)

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



-- ─── Müşteri profilleri (yalnızca UserType = Customer) ───────────────────────



BEGIN;



INSERT INTO "SocialProfiles" (

    "Id", "OwnerType", "OwnerId", "UserId", "Username",

    "Bio", "AvatarImageId", "Latitude", "Longitude",

    "IsPrivate", "Status", "CreatedAt", "UpdatedAt"

)

SELECT

    gen_random_uuid(),

    0,

    u."Id",

    u."Id",

    social_make_username(

        coalesce(

            nullif(trim(concat_ws(' ', u."FirstName", u."LastName")), ''),

            nullif(u."CustomerNumber", ''),

            'kullanici'

        ),

        u."Id"

    ),

    NULL, NULL, NULL, NULL,

    false, 0,

    now() AT TIME ZONE 'utc',

    now() AT TIME ZONE 'utc'

FROM "Users" u

WHERE u."IsActive" = true

  AND u."UserType" = 0

  AND NOT EXISTS (

      SELECT 1 FROM "SocialProfiles" sp

      WHERE sp."OwnerType" = 0 AND sp."OwnerId" = u."Id"

  );



COMMIT;



-- İsteğe bağlı

-- DROP FUNCTION IF EXISTS social_make_username(text, uuid);



-- Kontrol:

-- SELECT "OwnerType", count(*) FROM "SocialProfiles" GROUP BY 1 ORDER BY 1;

-- SELECT count(*) FROM "Users" u WHERE u."UserType" != 0 AND EXISTS (

--     SELECT 1 FROM "SocialProfiles" sp WHERE sp."OwnerType" = 0 AND sp."OwnerId" = u."Id"

-- );  -- 0 olmalı (FreeBarber/Store kullanıcılarında OwnerType=0 profil kalmamalı)

