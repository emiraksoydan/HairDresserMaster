# Phone Number Hybrid Protection Plan

## Goal

Protect phone number PII at rest while preserving exact-match lookup performance.

## Hybrid Model

- `PhoneNumberEncrypted` (string, nullable initially): AES-256 encrypted E164 value
- `PhoneNumberHash` (string, nullable initially): deterministic HMAC-SHA256(E164, pepper)
- Keep existing `PhoneNumber` temporarily only during migration window

Why both:

- Encrypted value is for secure storage and controlled read operations.
- Hash is for indexed equality search (`GetByPhone`, login/register checks).

## Crypto Details

- Normalize first: `E164` (already implemented).
- Hash: `Base64(HMACSHA256(pepper, e164))`
- Encryption: AES-256 with random IV per value, store as `Base64(IV + CIPHERTEXT)`.
- Config:
  - `SecurityOptions:PhonePepperBase64`
  - `SecurityOptions:PhoneEncKeyBase64`

## Migration Strategy (Safe Rollout)

1. **Schema expand**
   - Add new nullable columns: `PhoneNumberEncrypted`, `PhoneNumberHash`
   - Add non-unique index on `PhoneNumberHash` first

2. **Dual write**
   - On create/update phone, write:
     - `PhoneNumberEncrypted`
     - `PhoneNumberHash`
     - keep old `PhoneNumber` for compatibility

3. **Backfill**
   - Batch migrate existing users:
     - read `PhoneNumber` (plain)
     - compute hash/encrypted
     - write new columns
   - Log progress and retry failed rows

4. **Dual read**
   - Queries use `PhoneNumberHash` first
   - Fallback to plain `PhoneNumber` only while backfill is incomplete

5. **Cutover**
   - When backfill reaches 100% and fallback metrics are zero:
     - remove fallback reads
     - optionally drop/blank plain `PhoneNumber`
     - make `PhoneNumberEncrypted` + `PhoneNumberHash` required
     - switch index to unique if business rule requires

## Consistency Rules

- Always normalize to E164 before hash/encrypt.
- Never compare raw phone input against DB.
- Treat `PhoneNumberHash` as lookup key, `PhoneNumberEncrypted` as source of truth for display.

## Operational Notes

- Rotate keys with versioning strategy (e.g. `PhoneKeyVersion`).
- Keep keys outside repo in environment/secret store.
- Mask phone in logs (`+90 5** *** ** 12` style).

