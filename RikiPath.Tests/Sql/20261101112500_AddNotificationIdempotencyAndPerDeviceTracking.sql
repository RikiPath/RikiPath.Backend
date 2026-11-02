-- Rehearsal SQL for Notification Idempotency Key & Per-Device Push Tracking
-- Applied to PostgreSQL 17 QA database rikipath_mentor_migration_rehearsal

ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "IdempotencyKey" character varying(200);
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "DeliveredTokens" text;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Notifications_IdempotencyKey"
ON "Notifications" ("IdempotencyKey")
WHERE "IdempotencyKey" IS NOT NULL;
