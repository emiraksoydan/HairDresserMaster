# FCM Token Security Plan

## What is done now

- Sensitive token values are masked in logs in `FirebasePushNotificationService`.
- Full token values are no longer written to warning/error/info log lines.

## Recommended next steps (safe rollout)

1. **At-rest protection**
   - Store `FcmTokenEncrypted` instead of raw `FcmToken`
   - Keep `FcmTokenHash` for exact-match queries (register/unregister/deactivate)

2. **Lookup model**
   - `GetByTokenAsync` should accept raw token and query by `FcmTokenHash`
   - Persist encrypted token only for outbound send usage

3. **Migration**
   - Add new nullable columns
   - Dual write + backfill + dual read fallback
   - Remove raw token column after full migration

4. **Data retention**
   - Inactivate stale tokens by TTL
   - Periodically purge long-inactive tokens

5. **Monitoring**
   - Metric: invalid/unregistered token rate
   - Metric: fallback-read usage during migration

