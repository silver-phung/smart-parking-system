-- Apply the Parking Operations migrations to the existing Core DB.
-- Run the whole file together. Test on a branch with existing data before deployment.
BEGIN;

DO $neon_preflight$
BEGIN
    IF current_database() <> 'smartparking' THEN
        RAISE EXCEPTION 'Select the existing smartparking database.';
    END IF;
    IF current_schema() IS DISTINCT FROM 'public' THEN
        RAISE EXCEPTION 'Expected current schema public. Check the selected database and role search_path.';
    END IF;
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'Missing __EFMigrationsHistory. Use the full script on a new database, or reconcile the existing schema first.';
    END IF;
    LOCK TABLE public."__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261029114209_AddDutyToStaffLotAssignment') THEN
        RAISE EXCEPTION 'Required baseline 20261029114209_AddDutyToStaffLotAssignment is missing. Use the full migration-chain script for an EF-managed database.';
    END IF;
END $neon_preflight$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    DROP INDEX ix_parking_sessions_current_slot_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    DROP INDEX uq_parking_sessions_active_plate;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_slots ADD "AllowedFuelType" character varying(20) NOT NULL DEFAULT 'ANY';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "EntryEventId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "EntryImageId" bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "EntryRequestHash" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "FuelType" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "PolicyId" bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "RecognitionId" bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "Revision" bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "TemporaryPlateNumber" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "VerificationStatus" character varying(30) NOT NULL DEFAULT 'LEGACY';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "VerifiedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "VerifiedBy" bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD "ZoneId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_floors ADD "ZoneId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE bookings ADD "PolicyId" bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD CONSTRAINT "AK_parking_sessions_id_lot_id" UNIQUE (id, lot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE TABLE parking_tickets (
        "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
        "LotId" integer NOT NULL,
        "Code" character varying(40) NOT NULL,
        "SessionId" bigint NOT NULL,
        "Status" text NOT NULL,
        "IssuedBy" bigint NOT NULL,
        "IssuedAt" timestamp with time zone NOT NULL,
        "ClosedBy" bigint,
        "ClosedAt" timestamp with time zone,
        "Reason" character varying(500),
        CONSTRAINT "PK_parking_tickets" PRIMARY KEY ("Id"),
        CONSTRAINT ck_ticket_status CHECK ("Status" IN ('ISSUED','LOST','REPLACED','REVOKED')),
        CONSTRAINT "FK_parking_tickets_parking_sessions_SessionId_LotId" FOREIGN KEY ("SessionId", "LotId") REFERENCES parking_sessions (id, lot_id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE TABLE parking_zones (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "LotId" integer NOT NULL,
        "Code" character varying(30) NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Type" character varying(20) NOT NULL,
        "Layout" character varying(20) NOT NULL,
        "IsActive" boolean NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_parking_zones" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_parking_zones_Id_LotId" UNIQUE ("Id", "LotId"),
        CONSTRAINT ck_zone_layout CHECK ("Layout" IN ('INSIDE','EXTERNAL')),
        CONSTRAINT ck_zone_type CHECK ("Type" IN ('TRANSIENT','FIXED')),
        CONSTRAINT "FK_parking_zones_parking_lots_LotId" FOREIGN KEY ("LotId") REFERENCES parking_lots (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE TABLE parking_policies (
        "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
        "LotId" integer NOT NULL,
        "ZoneId" integer,
        "EffectiveFrom" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" bigint NOT NULL,
        "ApprovedBy" bigint,
        "ApprovedAt" timestamp with time zone,
        "DecisionReference" character varying(500) NOT NULL,
        "HoldMinutes" integer,
        "EarlyArrivalMinutes" integer,
        "LateArrivalMinutes" integer,
        "CancellationNoticeMinutes" integer,
        "DepositAmount" numeric(12,2),
        "NearFullThreshold" integer,
        "FeeUnitMinutes" integer,
        "FeeRounding" text,
        "NoShowAction" text,
        "CancellationAction" text,
        "RateSelection" text,
        "SlotReleaseRule" text NOT NULL,
        CONSTRAINT "PK_parking_policies" PRIMARY KEY ("Id"),
        CONSTRAINT ck_policy_values CHECK (("HoldMinutes" IS NULL OR "HoldMinutes" > 0) AND ("FeeUnitMinutes" IS NULL OR "FeeUnitMinutes" > 0) AND ("EarlyArrivalMinutes" IS NULL OR "EarlyArrivalMinutes" >= 0) AND ("LateArrivalMinutes" IS NULL OR "LateArrivalMinutes" >= 0) AND ("CancellationNoticeMinutes" IS NULL OR "CancellationNoticeMinutes" >= 0) AND ("DepositAmount" IS NULL OR "DepositAmount" >= 0) AND ("NearFullThreshold" IS NULL OR "NearFullThreshold" >= 0)),
        CONSTRAINT "FK_parking_policies_parking_lots_LotId" FOREIGN KEY ("LotId") REFERENCES parking_lots (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_parking_policies_parking_zones_ZoneId_LotId" FOREIGN KEY ("ZoneId", "LotId") REFERENCES parking_zones ("Id", "LotId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE UNIQUE INDEX ix_parking_sessions_current_slot_id ON parking_sessions (current_slot_id) WHERE status IN ('ACTIVE','PAID') AND "VerificationStatus" = 'VERIFIED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE UNIQUE INDEX "IX_parking_sessions_EntryEventId" ON parking_sessions ("EntryEventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE INDEX "IX_parking_sessions_PolicyId" ON parking_sessions ("PolicyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE UNIQUE INDEX "IX_parking_sessions_RecognitionId" ON parking_sessions ("RecognitionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE INDEX "IX_parking_sessions_ZoneId_lot_id" ON parking_sessions ("ZoneId", lot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE UNIQUE INDEX uq_parking_sessions_active_plate ON parking_sessions (plate_number) WHERE status IN ('ACTIVE', 'PAID') AND "VerificationStatus" <> 'WAITING_VERIFICATION';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE INDEX "IX_parking_floors_ZoneId_lot_id" ON parking_floors ("ZoneId", lot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE INDEX "IX_bookings_PolicyId" ON bookings ("PolicyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE INDEX "IX_parking_policies_LotId_ZoneId_EffectiveFrom" ON parking_policies ("LotId", "ZoneId", "EffectiveFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE INDEX "IX_parking_policies_ZoneId_LotId" ON parking_policies ("ZoneId", "LotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE INDEX "IX_parking_tickets_SessionId_LotId" ON parking_tickets ("SessionId", "LotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE UNIQUE INDEX uq_ticket_code ON parking_tickets ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE UNIQUE INDEX uq_ticket_open_session ON parking_tickets ("SessionId") WHERE "Status" = 'ISSUED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    CREATE UNIQUE INDEX "IX_parking_zones_LotId_Code" ON parking_zones ("LotId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE bookings ADD CONSTRAINT "FK_bookings_parking_policies_PolicyId" FOREIGN KEY ("PolicyId") REFERENCES parking_policies ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_floors ADD CONSTRAINT "FK_parking_floors_parking_zones_ZoneId_lot_id" FOREIGN KEY ("ZoneId", lot_id) REFERENCES parking_zones ("Id", "LotId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD CONSTRAINT "FK_parking_sessions_parking_policies_PolicyId" FOREIGN KEY ("PolicyId") REFERENCES parking_policies ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    ALTER TABLE parking_sessions ADD CONSTRAINT "FK_parking_sessions_parking_zones_ZoneId_lot_id" FOREIGN KEY ("ZoneId", lot_id) REFERENCES parking_zones ("Id", "LotId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    INSERT INTO parking_zones ("LotId", "Code", "Name", "Type", "Layout", "IsActive", "UpdatedAt")
    SELECT id, 'TRANSIENT', 'Khu vãng lai hiện có', 'TRANSIENT', 'INSIDE', true, now()
    FROM parking_lots
    ON CONFLICT ("LotId", "Code") DO NOTHING;
    UPDATE parking_floors f SET "ZoneId" = z."Id"
    FROM parking_zones z
    WHERE f.lot_id = z."LotId" AND z."Code" = 'TRANSIENT' AND f."ZoneId" IS NULL;
    UPDATE parking_sessions s SET "ZoneId" = z."Id"
    FROM parking_zones z
    WHERE s.lot_id = z."LotId" AND z."Code" = 'TRANSIENT' AND s."ZoneId" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083650_AddZonesPoliciesTicketsAndVerification') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102083650_AddZonesPoliciesTicketsAndVerification', '10.0.4');
    END IF;
END $EF$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102084552_LinkEntryEvidence') THEN
    ALTER TABLE stored_images ADD "ContentSha256" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102084552_LinkEntryEvidence') THEN
    CREATE INDEX "IX_parking_sessions_EntryImageId" ON parking_sessions ("EntryImageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102084552_LinkEntryEvidence') THEN
    ALTER TABLE parking_sessions ADD CONSTRAINT "FK_parking_sessions_stored_images_EntryImageId" FOREIGN KEY ("EntryImageId") REFERENCES stored_images (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102084552_LinkEntryEvidence') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102084552_LinkEntryEvidence', '10.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = '20261104032258_PreserveParkingOperationDispatchNotes') THEN
        ALTER TABLE public.parking_dispatches ALTER COLUMN notes TYPE text;
        INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20261104032258_PreserveParkingOperationDispatchNotes', '10.0.4');
    END IF;
END $EF$;

COMMIT;
