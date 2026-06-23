-- =============================================================================
-- SOSYAL MEDYA TAM SIFIRLAMA + YALNIZCA MÜŞTERİ PROFİLLERİ
--
-- Ne yapar:
--   1) Tüm sosyal içerik, etkileşim ve profilleri siler (tablo yoksa atlar)
--   2) Sosyal DM thread'lerini temizler
--   3) Sosyal bildirimleri siler
--   4) Yalnızca UserType = Customer (0) kullanıcılar için sosyal profil oluşturur
--
-- Önceki çalıştırma yarıda kaldıysa:  ROLLBACK;  sonra tekrar çalıştırın.
--
-- UYARI: Geri alınamaz. Production'da yedek alın.
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

CREATE OR REPLACE FUNCTION social_delete_if_table_exists(p_table text)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    IF to_regclass(format('public.%I', p_table)) IS NOT NULL THEN
        EXECUTE format('DELETE FROM %I', p_table);
        RAISE NOTICE 'Silindi: %', p_table;
    ELSE
        RAISE NOTICE 'Atlandı (tablo yok): %', p_table;
    END IF;
END;
$$;

BEGIN;

-- ─── 1) Sosyal DM thread'leri ────────────────────────────────────────────────

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'ChatThreads'
          AND column_name = 'IsSocialThread'
    ) THEN
        DELETE FROM "MessageReadReceipts" mrr
        USING "ChatMessages" cm, "ChatThreads" ct
        WHERE mrr."MessageId" = cm."Id"
          AND cm."ThreadId" = ct."Id"
          AND ct."IsSocialThread" = true;

        IF to_regclass('public."ChatMessageUserDeletions"') IS NOT NULL THEN
            DELETE FROM "ChatMessageUserDeletions" cmud
            USING "ChatMessages" cm, "ChatThreads" ct
            WHERE cmud."MessageId" = cm."Id"
              AND cm."ThreadId" = ct."Id"
              AND ct."IsSocialThread" = true;
        END IF;

        DELETE FROM "ChatMessages" cm
        USING "ChatThreads" ct
        WHERE cm."ThreadId" = ct."Id"
          AND ct."IsSocialThread" = true;

        DELETE FROM "ChatThreads"
        WHERE "IsSocialThread" = true;

        RAISE NOTICE 'Sosyal DM thread''leri temizlendi.';
    ELSE
        RAISE NOTICE 'Atlandı: ChatThreads.IsSocialThread kolonu yok.';
    END IF;
END $$;

-- ─── 2) Sosyal tablolar (FK sırası, yoksa atla) ─────────────────────────────

SELECT social_delete_if_table_exists('AppointmentSocialShares');
SELECT social_delete_if_table_exists('SocialStoryReplies');
SELECT social_delete_if_table_exists('SocialStoryViews');
SELECT social_delete_if_table_exists('SocialStoryHighlightItems');
SELECT social_delete_if_table_exists('SocialStoryHighlights');
SELECT social_delete_if_table_exists('SocialStories');
SELECT social_delete_if_table_exists('SocialPostViews');
SELECT social_delete_if_table_exists('SocialSavedPosts');
SELECT social_delete_if_table_exists('SocialComments');
SELECT social_delete_if_table_exists('SocialPostMedia');
SELECT social_delete_if_table_exists('SocialPosts');
SELECT social_delete_if_table_exists('SocialLikes');
SELECT social_delete_if_table_exists('SocialFollows');
SELECT social_delete_if_table_exists('SocialProfileMutes');
SELECT social_delete_if_table_exists('SocialProfiles');

-- Sosyal profil görselleri (OwnerType = 5 → SocialProfile)
DELETE FROM "Images"
WHERE "OwnerType" = 5;

-- Sosyal bildirimler (NotificationType enum: 20–26)
DELETE FROM "Notifications"
WHERE "Type" IN (20, 21, 22, 23, 24, 25, 26);

-- ─── 3) Yalnızca müşteri profilleri ─────────────────────────────────────────

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

DROP FUNCTION IF EXISTS social_delete_if_table_exists(text);

-- Kontrol:
-- SELECT "OwnerType", count(*) FROM "SocialProfiles" GROUP BY 1 ORDER BY 1;
-- SELECT count(*) FROM "SocialPosts";
