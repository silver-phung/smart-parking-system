-- Migration upgrade for AI Recognition and Staff Workflow
-- Target database: smartparking on Neon
-- Deliverable for workspace rule: neon/sql

CREATE TABLE IF NOT EXISTS "__AiRecognitionMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
    "ProductVersion" character varying(32) NOT NULL
);

ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "ConfirmationNote" character varying(256);
ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "ConfirmedAt" timestamp with time zone;
ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "ConfirmedByStaffUserId" bigint;
ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "ConfirmedPlateNumber" character varying(32);
ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "ContentSha256" text;
ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "FailureReason" character varying(512);
ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "ParkingLotId" integer;
ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "Purpose" character varying(16) DEFAULT 'ENTRY';
ALTER TABLE ai_recognition_records ADD COLUMN IF NOT EXISTS "MatchedParkingSessionId" bigint;

CREATE INDEX IF NOT EXISTS "IX_ai_recognition_records_ParkingLotId_Purpose_CreatedAt"
ON ai_recognition_records ("ParkingLotId", "Purpose", "CreatedAt");

INSERT INTO "__AiRecognitionMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261029033434_AddAiRecognitionRecords', '10.0.4')
ON CONFLICT DO NOTHING;

INSERT INTO "__AiRecognitionMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261029124117_AddStaffDetectionConfirmation', '10.0.4')
ON CONFLICT DO NOTHING;

INSERT INTO "__AiRecognitionMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261102084556_AddRecognitionContentHash', '10.0.4')
ON CONFLICT DO NOTHING;

INSERT INTO "__AiRecognitionMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261112074423_AddStaffRecognitionPurpose', '10.0.4')
ON CONFLICT DO NOTHING;

INSERT INTO "__AiRecognitionMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261112075054_AddMatchedExitSession', '10.0.4')
ON CONFLICT DO NOTHING;
