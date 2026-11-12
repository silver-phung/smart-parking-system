-- Generated from EF Core migrations. No credentials or CREATE DATABASE statements.
-- Run the entire file in the intended database on Neon, schema public.
-- Full files: empty database or the same EF-managed migration chain.
-- Upgrade files: require the documented Sprint 3 baseline.
DO $neon_preflight$
BEGIN
    IF current_schema() IS DISTINCT FROM 'public' THEN
        RAISE EXCEPTION 'Expected current schema public. Check the selected database and role search_path.';
    END IF;
    IF to_regclass('public.parking_lots') IS NOT NULL
       AND to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'Application tables exist without __EFMigrationsHistory. Do not guess applied migration IDs; compare the existing schema first.';
    END IF;
END $neon_preflight$;

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE TABLE roles (
        id integer GENERATED ALWAYS AS IDENTITY,
        code character varying(50) NOT NULL,
        name character varying(100) NOT NULL,
        description character varying(255),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_roles PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE TABLE users (
        id bigint GENERATED ALWAYS AS IDENTITY,
        username character varying(50) NOT NULL,
        email character varying(255) NOT NULL,
        password_hash character varying(255) NOT NULL,
        role_id integer NOT NULL,
        full_name character varying(100) NOT NULL,
        phone_number character varying(20),
        avatar_url character varying(500),
        is_active boolean NOT NULL DEFAULT TRUE,
        is_email_verified boolean NOT NULL DEFAULT FALSE,
        approval_status character varying(20) NOT NULL DEFAULT 'PENDING',
        approved_by bigint,
        approved_at timestamp with time zone,
        failed_login_attempts integer NOT NULL DEFAULT 0,
        lockout_end_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_users PRIMARY KEY (id),
        CONSTRAINT chk_users_approval_status CHECK (approval_status IN ('PENDING', 'APPROVED', 'REJECTED')),
        CONSTRAINT chk_users_failed_login_attempts CHECK (failed_login_attempts >= 0),
        CONSTRAINT fk_users_approved_by FOREIGN KEY (approved_by) REFERENCES users (id) ON DELETE SET NULL,
        CONSTRAINT fk_users_role FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE TABLE refresh_tokens (
        id bigint GENERATED ALWAYS AS IDENTITY,
        user_id bigint NOT NULL,
        token_hash character varying(64) NOT NULL,
        user_agent character varying(255),
        ip_address character varying(45),
        expires_at timestamp with time zone NOT NULL,
        is_revoked boolean NOT NULL DEFAULT FALSE,
        revoked_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
        CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE TABLE user_tokens (
        id bigint GENERATED ALWAYS AS IDENTITY,
        user_id bigint NOT NULL,
        token_type character varying(50) NOT NULL,
        token_hash character varying(64) NOT NULL,
        attempts_count integer NOT NULL DEFAULT 0,
        expires_at timestamp with time zone NOT NULL,
        is_used boolean NOT NULL DEFAULT FALSE,
        used_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_user_tokens PRIMARY KEY (id),
        CONSTRAINT chk_user_tokens_attempts CHECK (attempts_count >= 0),
        CONSTRAINT chk_user_tokens_type CHECK (token_type IN ('EMAIL_VERIFICATION', 'PASSWORD_RESET')),
        CONSTRAINT fk_user_tokens_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    INSERT INTO roles (id, code, created_at, description, name)
    OVERRIDING SYSTEM VALUE
    VALUES (1, 'ADMIN', TIMESTAMPTZ '2026-01-01T00:00:00Z', 'Toàn quyền quản trị hệ thống Smart Parking', 'Quản trị viên');
    INSERT INTO roles (id, code, created_at, description, name)
    OVERRIDING SYSTEM VALUE
    VALUES (2, 'MANAGER', TIMESTAMPTZ '2026-01-01T00:00:00Z', 'Quản lý vận hành bãi xe, cấu hình giá, nhân sự và báo cáo doanh thu', 'Quản lý bãi đỗ');
    INSERT INTO roles (id, code, created_at, description, name)
    OVERRIDING SYSTEM VALUE
    VALUES (3, 'STAFF', TIMESTAMPTZ '2026-01-01T00:00:00Z', 'Nhân viên trực cổng, kiểm soát vào ra và hỗ trợ xử lý sự cố', 'Nhân viên vận hành');
    INSERT INTO roles (id, code, created_at, description, name)
    OVERRIDING SYSTEM VALUE
    VALUES (4, 'USER', TIMESTAMPTZ '2026-01-01T00:00:00Z', 'Người dùng tự đăng ký tài khoản, gửi xe và thanh toán cá nhân', 'Người dùng');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE UNIQUE INDEX uq_refresh_tokens_hash ON refresh_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE UNIQUE INDEX uq_roles_code ON roles (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE INDEX idx_user_tokens_user_type ON user_tokens (user_id, token_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE INDEX "IX_users_approved_by" ON users (approved_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE INDEX "IX_users_role_id" ON users (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE UNIQUE INDEX uq_users_email ON users (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    CREATE UNIQUE INDEX uq_users_username ON users (username);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    PERFORM setval(
        pg_get_serial_sequence('roles', 'id'),
        GREATEST(
            (SELECT MAX(id) FROM roles) + 1,
            nextval(pg_get_serial_sequence('roles', 'id'))),
        false);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023013452_InitialAccounts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023013452_InitialAccounts', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE TABLE audit_logs (
        id bigint GENERATED ALWAYS AS IDENTITY,
        actor_id bigint,
        action character varying(50) NOT NULL,
        target_entity character varying(50) NOT NULL,
        target_id character varying(50),
        old_values text,
        new_values text,
        ip_address character varying(45),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_audit_logs PRIMARY KEY (id),
        CONSTRAINT fk_audit_logs_actor FOREIGN KEY (actor_id) REFERENCES users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE TABLE parking_lots (
        id integer GENERATED ALWAYS AS IDENTITY,
        name character varying(100) NOT NULL,
        address character varying(255),
        total_capacity integer NOT NULL DEFAULT 0,
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_parking_lots PRIMARY KEY (id),
        CONSTRAINT chk_parking_lots_capacity CHECK (total_capacity >= 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE TABLE vehicles (
        id bigint GENERATED ALWAYS AS IDENTITY,
        user_id bigint,
        plate_number character varying(20) NOT NULL,
        brand character varying(50),
        model character varying(50),
        color character varying(30),
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_vehicles PRIMARY KEY (id),
        CONSTRAINT chk_vehicles_plate_normalized CHECK (plate_number = UPPER(TRIM(plate_number))),
        CONSTRAINT fk_vehicles_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE TABLE parking_floors (
        id integer GENERATED ALWAYS AS IDENTITY,
        lot_id integer NOT NULL,
        floor_number integer NOT NULL,
        name character varying(50) NOT NULL,
        total_slots integer NOT NULL DEFAULT 0,
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_parking_floors PRIMARY KEY (id),
        CONSTRAINT chk_parking_floors_slots CHECK (total_slots >= 0),
        CONSTRAINT fk_parking_floors_lot FOREIGN KEY (lot_id) REFERENCES parking_lots (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE TABLE parking_slots (
        id bigint GENERATED ALWAYS AS IDENTITY,
        floor_id integer NOT NULL,
        slot_code character varying(20) NOT NULL,
        physical_status character varying(20) NOT NULL DEFAULT 'EMPTY',
        position_x numeric(10,2),
        position_y numeric(10,2),
        position_z numeric(10,2),
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_parking_slots PRIMARY KEY (id),
        CONSTRAINT chk_parking_slots_physical_status CHECK (physical_status IN ('EMPTY', 'OCCUPIED', 'MAINTENANCE', 'UNAVAILABLE')),
        CONSTRAINT fk_parking_slots_floor FOREIGN KEY (floor_id) REFERENCES parking_floors (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE TABLE parking_sessions (
        id bigint GENERATED ALWAYS AS IDENTITY,
        lot_id integer NOT NULL,
        vehicle_id bigint,
        plate_number character varying(20) NOT NULL,
        check_in_time timestamp with time zone NOT NULL DEFAULT (now()),
        check_out_time timestamp with time zone,
        current_slot_id bigint,
        status character varying(20) NOT NULL DEFAULT 'ACTIVE',
        total_fee numeric(12,2) NOT NULL DEFAULT 0.0,
        paid_at timestamp with time zone,
        check_in_image_url character varying(500),
        check_out_image_url character varying(500),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_parking_sessions PRIMARY KEY (id),
        CONSTRAINT chk_parking_sessions_checkout_time CHECK (check_out_time IS NULL OR check_out_time >= check_in_time),
        CONSTRAINT chk_parking_sessions_fee CHECK (total_fee >= 0),
        CONSTRAINT chk_parking_sessions_plate_normalized CHECK (plate_number = UPPER(TRIM(plate_number))),
        CONSTRAINT chk_parking_sessions_status CHECK (status IN ('ACTIVE', 'PAID', 'COMPLETED', 'CANCELLED')),
        CONSTRAINT fk_parking_sessions_lot FOREIGN KEY (lot_id) REFERENCES parking_lots (id) ON DELETE RESTRICT,
        CONSTRAINT fk_parking_sessions_slot FOREIGN KEY (current_slot_id) REFERENCES parking_slots (id) ON DELETE RESTRICT,
        CONSTRAINT fk_parking_sessions_vehicle FOREIGN KEY (vehicle_id) REFERENCES vehicles (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE TABLE parking_dispatches (
        id bigint GENERATED ALWAYS AS IDENTITY,
        session_id bigint NOT NULL,
        assigned_slot_id bigint NOT NULL,
        actual_slot_id bigint,
        status character varying(20) NOT NULL DEFAULT 'PENDING',
        dispatch_mode character varying(20) NOT NULL DEFAULT 'AUTO',
        dispatched_at timestamp with time zone NOT NULL DEFAULT (now()),
        confirmed_at timestamp with time zone,
        notes character varying(255),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_parking_dispatches PRIMARY KEY (id),
        CONSTRAINT chk_parking_dispatches_mode CHECK (dispatch_mode IN ('AUTO', 'MANUAL')),
        CONSTRAINT chk_parking_dispatches_status CHECK (status IN ('PENDING', 'ASSIGNED', 'OCCUPIED', 'WRONG_SLOT', 'CANCELLED')),
        CONSTRAINT fk_parking_dispatches_actual_slot FOREIGN KEY (actual_slot_id) REFERENCES parking_slots (id) ON DELETE RESTRICT,
        CONSTRAINT fk_parking_dispatches_assigned_slot FOREIGN KEY (assigned_slot_id) REFERENCES parking_slots (id) ON DELETE RESTRICT,
        CONSTRAINT fk_parking_dispatches_session FOREIGN KEY (session_id) REFERENCES parking_sessions (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_audit_logs_actor_id ON audit_logs (actor_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_audit_logs_created_at ON audit_logs (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_audit_logs_target ON audit_logs (target_entity, target_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_parking_dispatches_actual_slot_id ON parking_dispatches (actual_slot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE UNIQUE INDEX uq_parking_dispatches_active_session ON parking_dispatches (session_id) WHERE status IN ('PENDING', 'ASSIGNED');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE UNIQUE INDEX uq_parking_dispatches_assigned_slot ON parking_dispatches (assigned_slot_id) WHERE status = 'ASSIGNED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE UNIQUE INDEX uq_parking_floors_lot_floor_number ON parking_floors (lot_id, floor_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE UNIQUE INDEX uq_parking_floors_lot_name ON parking_floors (lot_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE UNIQUE INDEX uq_parking_lots_name ON parking_lots (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_parking_sessions_check_in_time ON parking_sessions (check_in_time);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_parking_sessions_current_slot_id ON parking_sessions (current_slot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_parking_sessions_lot_status ON parking_sessions (lot_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_parking_sessions_vehicle_id ON parking_sessions (vehicle_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE UNIQUE INDEX uq_parking_sessions_active_plate ON parking_sessions (plate_number) WHERE status IN ('ACTIVE', 'PAID');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_parking_slots_status_active ON parking_slots (physical_status, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE UNIQUE INDEX uq_parking_slots_floor_slot_code ON parking_slots (floor_id, slot_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE INDEX ix_vehicles_user_id ON vehicles (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    CREATE UNIQUE INDEX uq_vehicles_plate_number ON vehicles (plate_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023074918_AddParkingCoreTables') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023074918_AddParkingCoreTables', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023135618_AddStaffLotAssignments') THEN
    CREATE TABLE staff_lot_assignments (
        id bigint GENERATED ALWAYS AS IDENTITY,
        user_id bigint NOT NULL,
        lot_id integer NOT NULL,
        assigned_by bigint NOT NULL,
        assigned_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        is_active boolean NOT NULL DEFAULT TRUE,
        revoked_by bigint,
        revoked_at timestamp with time zone,
        revocation_reason character varying(255),
        notes text,
        created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT pk_staff_lot_assignments PRIMARY KEY (id),
        CONSTRAINT fk_staff_lot_assignments_assigned_by FOREIGN KEY (assigned_by) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_staff_lot_assignments_lot FOREIGN KEY (lot_id) REFERENCES parking_lots (id) ON DELETE RESTRICT,
        CONSTRAINT fk_staff_lot_assignments_revoked_by FOREIGN KEY (revoked_by) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_staff_lot_assignments_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023135618_AddStaffLotAssignments') THEN
    CREATE INDEX "IX_staff_lot_assignments_assigned_by" ON staff_lot_assignments (assigned_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023135618_AddStaffLotAssignments') THEN
    CREATE INDEX ix_staff_lot_assignments_lot_active ON staff_lot_assignments (lot_id) WHERE is_active = TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023135618_AddStaffLotAssignments') THEN
    CREATE INDEX "IX_staff_lot_assignments_revoked_by" ON staff_lot_assignments (revoked_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023135618_AddStaffLotAssignments') THEN
    CREATE INDEX ix_staff_lot_assignments_user_active ON staff_lot_assignments (user_id) WHERE is_active = TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023135618_AddStaffLotAssignments') THEN
    CREATE UNIQUE INDEX uq_staff_lot_assignments_active ON staff_lot_assignments (user_id, lot_id) WHERE is_active = TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023135618_AddStaffLotAssignments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023135618_AddStaffLotAssignments', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    ALTER TABLE parking_sessions ADD booking_id bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    CREATE TABLE bookings (
        id bigint GENERATED ALWAYS AS IDENTITY,
        lot_id integer NOT NULL,
        user_id bigint NOT NULL,
        vehicle_id bigint NOT NULL,
        plate_number character varying(20) NOT NULL,
        start_time timestamp with time zone NOT NULL,
        end_time timestamp with time zone,
        status character varying(20) NOT NULL DEFAULT 'PENDING',
        checked_in_at timestamp with time zone,
        cancelled_at timestamp with time zone,
        cancellation_reason character varying(500),
        notes character varying(500),
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_bookings PRIMARY KEY (id),
        CONSTRAINT chk_bookings_plate_normalized CHECK (plate_number = UPPER(TRIM(plate_number))),
        CONSTRAINT chk_bookings_status CHECK (status IN ('PENDING', 'CHECKED_IN', 'CANCELLED', 'EXPIRED')),
        CONSTRAINT chk_bookings_time_order CHECK (end_time IS NULL OR end_time >= start_time),
        CONSTRAINT fk_bookings_lot FOREIGN KEY (lot_id) REFERENCES parking_lots (id) ON DELETE RESTRICT,
        CONSTRAINT fk_bookings_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_bookings_vehicle FOREIGN KEY (vehicle_id) REFERENCES vehicles (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    CREATE UNIQUE INDEX "IX_parking_sessions_booking_id" ON parking_sessions (booking_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    CREATE INDEX ix_bookings_lot_status ON bookings (lot_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    CREATE INDEX ix_bookings_plate_status ON bookings (plate_number, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    CREATE INDEX ix_bookings_user_id ON bookings (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    CREATE UNIQUE INDEX uq_bookings_pending_vehicle ON bookings (vehicle_id) WHERE status = 'PENDING';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    ALTER TABLE parking_sessions ADD CONSTRAINT fk_parking_sessions_booking FOREIGN KEY (booking_id) REFERENCES bookings (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027135014_AddBookingsAndCapacity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027135014_AddBookingsAndCapacity', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028013753_AddStoredImages') THEN
    CREATE TABLE stored_images (
        id bigint GENERATED ALWAYS AS IDENTITY,
        user_id bigint NOT NULL,
        public_id character varying(255) NOT NULL,
        url character varying(2048) NOT NULL,
        format character varying(10) NOT NULL,
        bytes bigint NOT NULL,
        width integer NOT NULL,
        height integer NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_stored_images PRIMARY KEY (id),
        CONSTRAINT fk_stored_images_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028013753_AddStoredImages') THEN
    CREATE INDEX ix_stored_images_user_id ON stored_images (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028013753_AddStoredImages') THEN
    CREATE UNIQUE INDEX uq_stored_images_public_id ON stored_images (public_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028013753_AddStoredImages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261028013753_AddStoredImages', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029114209_AddDutyToStaffLotAssignment') THEN
    ALTER TABLE staff_lot_assignments ADD duty character varying(30) NOT NULL DEFAULT 'GATE';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029114209_AddDutyToStaffLotAssignment') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261029114209_AddDutyToStaffLotAssignment', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

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
COMMIT;

START TRANSACTION;

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
COMMIT;
