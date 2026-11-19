BEGIN;
SET LOCAL lock_timeout = '10s';
ALTER TABLE public."MockQuestions" ADD COLUMN IF NOT EXISTS "AudioTranscript" text;
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261118091131_AddMockQuestionAudioTranscript', '8.0.8')
ON CONFLICT ("MigrationId") DO NOTHING;
COMMIT;
