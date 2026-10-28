using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartParking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingCoreTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    actor_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_entity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    old_values = table.Column<string>(type: "text", nullable: true),
                    new_values = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_actor",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "parking_lots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    total_capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parking_lots", x => x.id);
                    table.CheckConstraint("chk_parking_lots_capacity", "total_capacity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    plate_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    brand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.CheckConstraint("chk_vehicles_plate_normalized", "plate_number = UPPER(TRIM(plate_number))");
                    table.ForeignKey(
                        name: "fk_vehicles_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "parking_floors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lot_id = table.Column<int>(type: "integer", nullable: false),
                    floor_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_slots = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parking_floors", x => x.id);
                    table.CheckConstraint("chk_parking_floors_slots", "total_slots >= 0");
                    table.ForeignKey(
                        name: "fk_parking_floors_lot",
                        column: x => x.lot_id,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parking_slots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    floor_id = table.Column<int>(type: "integer", nullable: false),
                    slot_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    physical_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "EMPTY"),
                    position_x = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    position_y = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    position_z = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parking_slots", x => x.id);
                    table.CheckConstraint("chk_parking_slots_physical_status", "physical_status IN ('EMPTY', 'OCCUPIED', 'MAINTENANCE', 'UNAVAILABLE')");
                    table.ForeignKey(
                        name: "fk_parking_slots_floor",
                        column: x => x.floor_id,
                        principalTable: "parking_floors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parking_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lot_id = table.Column<int>(type: "integer", nullable: false),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: true),
                    plate_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    check_in_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    check_out_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_slot_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    total_fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    check_in_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    check_out_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parking_sessions", x => x.id);
                    table.CheckConstraint("chk_parking_sessions_checkout_time", "check_out_time IS NULL OR check_out_time >= check_in_time");
                    table.CheckConstraint("chk_parking_sessions_fee", "total_fee >= 0");
                    table.CheckConstraint("chk_parking_sessions_plate_normalized", "plate_number = UPPER(TRIM(plate_number))");
                    table.CheckConstraint("chk_parking_sessions_status", "status IN ('ACTIVE', 'PAID', 'COMPLETED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "fk_parking_sessions_lot",
                        column: x => x.lot_id,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parking_sessions_slot",
                        column: x => x.current_slot_id,
                        principalTable: "parking_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parking_sessions_vehicle",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parking_dispatches",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_slot_id = table.Column<long>(type: "bigint", nullable: false),
                    actual_slot_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    dispatch_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "AUTO"),
                    dispatched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parking_dispatches", x => x.id);
                    table.CheckConstraint("chk_parking_dispatches_mode", "dispatch_mode IN ('AUTO', 'MANUAL')");
                    table.CheckConstraint("chk_parking_dispatches_status", "status IN ('PENDING', 'ASSIGNED', 'OCCUPIED', 'WRONG_SLOT', 'CANCELLED')");
                    table.ForeignKey(
                        name: "fk_parking_dispatches_actual_slot",
                        column: x => x.actual_slot_id,
                        principalTable: "parking_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parking_dispatches_assigned_slot",
                        column: x => x.assigned_slot_id,
                        principalTable: "parking_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parking_dispatches_session",
                        column: x => x.session_id,
                        principalTable: "parking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_id",
                table: "audit_logs",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_target",
                table: "audit_logs",
                columns: new[] { "target_entity", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_parking_dispatches_actual_slot_id",
                table: "parking_dispatches",
                column: "actual_slot_id");

            migrationBuilder.CreateIndex(
                name: "uq_parking_dispatches_active_session",
                table: "parking_dispatches",
                column: "session_id",
                unique: true,
                filter: "status IN ('PENDING', 'ASSIGNED')");

            migrationBuilder.CreateIndex(
                name: "uq_parking_dispatches_assigned_slot",
                table: "parking_dispatches",
                column: "assigned_slot_id",
                unique: true,
                filter: "status = 'ASSIGNED'");

            migrationBuilder.CreateIndex(
                name: "uq_parking_floors_lot_floor_number",
                table: "parking_floors",
                columns: new[] { "lot_id", "floor_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_parking_floors_lot_name",
                table: "parking_floors",
                columns: new[] { "lot_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_parking_lots_name",
                table: "parking_lots",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parking_sessions_check_in_time",
                table: "parking_sessions",
                column: "check_in_time");

            migrationBuilder.CreateIndex(
                name: "ix_parking_sessions_current_slot_id",
                table: "parking_sessions",
                column: "current_slot_id");

            migrationBuilder.CreateIndex(
                name: "ix_parking_sessions_lot_status",
                table: "parking_sessions",
                columns: new[] { "lot_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_parking_sessions_vehicle_id",
                table: "parking_sessions",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "uq_parking_sessions_active_plate",
                table: "parking_sessions",
                column: "plate_number",
                unique: true,
                filter: "status IN ('ACTIVE', 'PAID')");

            migrationBuilder.CreateIndex(
                name: "ix_parking_slots_status_active",
                table: "parking_slots",
                columns: new[] { "physical_status", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_parking_slots_floor_slot_code",
                table: "parking_slots",
                columns: new[] { "floor_id", "slot_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_user_id",
                table: "vehicles",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_vehicles_plate_number",
                table: "vehicles",
                column: "plate_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "parking_dispatches");

            migrationBuilder.DropTable(
                name: "parking_sessions");

            migrationBuilder.DropTable(
                name: "parking_slots");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "parking_floors");

            migrationBuilder.DropTable(
                name: "parking_lots");
        }
    }
}
