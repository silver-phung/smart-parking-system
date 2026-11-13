-- Read-only. Run in the database selected for recognition metadata, not the core database.
SELECT current_database() AS database_name, current_user AS database_role, current_schema() AS schema_name;

SELECT "MigrationId", "ProductVersion"
FROM public."__AiRecognitionMigrationsHistory"
ORDER BY "MigrationId";

SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'ai_recognition_records'
ORDER BY ordinal_position;

SELECT count(*) AS recognition_records,
       count(*) FILTER (WHERE "ContentSha256" IS NULL) AS legacy_records_without_content_hash
FROM public.ai_recognition_records;
