BEGIN;
SET LOCAL lock_timeout = '10s';

ALTER TABLE public."Lessons" ADD COLUMN IF NOT EXISTS "ImageUrl" text;
ALTER TABLE public."Vocabularies" ADD COLUMN IF NOT EXISTS "ImageUrl" text;
ALTER TABLE public."Kanjis" ADD COLUMN IF NOT EXISTS "ImageUrl" text;
ALTER TABLE public."GrammarPatterns" ADD COLUMN IF NOT EXISTS "ImageUrl" text;
ALTER TABLE public."PracticeExercises" ADD COLUMN IF NOT EXISTS "ImageUrl" text;
ALTER TABLE public."PracticeQuestions" ADD COLUMN IF NOT EXISTS "ImageUrl" text;
ALTER TABLE public."PracticeQuestionOptions" ADD COLUMN IF NOT EXISTS "ImageUrl" text;

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261118100842_AddImportedContentImages', '8.0.8')
ON CONFLICT ("MigrationId") DO NOTHING;
COMMIT;
