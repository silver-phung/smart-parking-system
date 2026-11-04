using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartParking.Infrastructure.Migrations.SmartParkingDb
{
    /// <inheritdoc />
    public partial class AddZonesPoliciesTicketsAndVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_parking_sessions_current_slot_id",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "uq_parking_sessions_active_plate",
                table: "parking_sessions");

            migrationBuilder.AddColumn<string>(
                name: "AllowedFuelType",
                table: "parking_slots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ANY");

            migrationBuilder.AddColumn<Guid>(
                name: "EntryEventId",
                table: "parking_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EntryImageId",
                table: "parking_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntryRequestHash",
                table: "parking_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FuelType",
                table: "parking_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PolicyId",
                table: "parking_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecognitionId",
                table: "parking_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "parking_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TemporaryPlateNumber",
                table: "parking_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "parking_sessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LEGACY");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "parking_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VerifiedBy",
                table: "parking_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ZoneId",
                table: "parking_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ZoneId",
                table: "parking_floors",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PolicyId",
                table: "bookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_parking_sessions_id_lot_id",
                table: "parking_sessions",
                columns: new[] { "id", "lot_id" });

            migrationBuilder.CreateTable(
                name: "parking_tickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LotId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IssuedBy = table.Column<long>(type: "bigint", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedBy = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_tickets", x => x.Id);
                    table.CheckConstraint("ck_ticket_status", "\"Status\" IN ('ISSUED','LOST','REPLACED','REVOKED')");
                    table.ForeignKey(
                        name: "FK_parking_tickets_parking_sessions_SessionId_LotId",
                        columns: x => new { x.SessionId, x.LotId },
                        principalTable: "parking_sessions",
                        principalColumns: new[] { "id", "lot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parking_zones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LotId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Layout = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_zones", x => x.Id);
                    table.UniqueConstraint("AK_parking_zones_Id_LotId", x => new { x.Id, x.LotId });
                    table.CheckConstraint("ck_zone_layout", "\"Layout\" IN ('INSIDE','EXTERNAL')");
                    table.CheckConstraint("ck_zone_type", "\"Type\" IN ('TRANSIENT','FIXED')");
                    table.ForeignKey(
                        name: "FK_parking_zones_parking_lots_LotId",
                        column: x => x.LotId,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parking_policies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LotId = table.Column<int>(type: "integer", nullable: false),
                    ZoneId = table.Column<int>(type: "integer", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HoldMinutes = table.Column<int>(type: "integer", nullable: true),
                    EarlyArrivalMinutes = table.Column<int>(type: "integer", nullable: true),
                    LateArrivalMinutes = table.Column<int>(type: "integer", nullable: true),
                    CancellationNoticeMinutes = table.Column<int>(type: "integer", nullable: true),
                    DepositAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    NearFullThreshold = table.Column<int>(type: "integer", nullable: true),
                    FeeUnitMinutes = table.Column<int>(type: "integer", nullable: true),
                    FeeRounding = table.Column<string>(type: "text", nullable: true),
                    NoShowAction = table.Column<string>(type: "text", nullable: true),
                    CancellationAction = table.Column<string>(type: "text", nullable: true),
                    RateSelection = table.Column<string>(type: "text", nullable: true),
                    SlotReleaseRule = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_policies", x => x.Id);
                    table.CheckConstraint("ck_policy_values", "(\"HoldMinutes\" IS NULL OR \"HoldMinutes\" > 0) AND (\"FeeUnitMinutes\" IS NULL OR \"FeeUnitMinutes\" > 0) AND (\"EarlyArrivalMinutes\" IS NULL OR \"EarlyArrivalMinutes\" >= 0) AND (\"LateArrivalMinutes\" IS NULL OR \"LateArrivalMinutes\" >= 0) AND (\"CancellationNoticeMinutes\" IS NULL OR \"CancellationNoticeMinutes\" >= 0) AND (\"DepositAmount\" IS NULL OR \"DepositAmount\" >= 0) AND (\"NearFullThreshold\" IS NULL OR \"NearFullThreshold\" >= 0)");
                    table.ForeignKey(
                        name: "FK_parking_policies_parking_lots_LotId",
                        column: x => x.LotId,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parking_policies_parking_zones_ZoneId_LotId",
                        columns: x => new { x.ZoneId, x.LotId },
                        principalTable: "parking_zones",
                        principalColumns: new[] { "Id", "LotId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_parking_sessions_current_slot_id",
                table: "parking_sessions",
                column: "current_slot_id",
                unique: true,
                filter: "status IN ('ACTIVE','PAID') AND \"VerificationStatus\" = 'VERIFIED'");

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_EntryEventId",
                table: "parking_sessions",
                column: "EntryEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_PolicyId",
                table: "parking_sessions",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_RecognitionId",
                table: "parking_sessions",
                column: "RecognitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_ZoneId_lot_id",
                table: "parking_sessions",
                columns: new[] { "ZoneId", "lot_id" });

            migrationBuilder.CreateIndex(
                name: "uq_parking_sessions_active_plate",
                table: "parking_sessions",
                column: "plate_number",
                unique: true,
                filter: "status IN ('ACTIVE', 'PAID') AND \"VerificationStatus\" <> 'WAITING_VERIFICATION'");

            migrationBuilder.CreateIndex(
                name: "IX_parking_floors_ZoneId_lot_id",
                table: "parking_floors",
                columns: new[] { "ZoneId", "lot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_PolicyId",
                table: "bookings",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_parking_policies_LotId_ZoneId_EffectiveFrom",
                table: "parking_policies",
                columns: new[] { "LotId", "ZoneId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_parking_policies_ZoneId_LotId",
                table: "parking_policies",
                columns: new[] { "ZoneId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_parking_tickets_SessionId_LotId",
                table: "parking_tickets",
                columns: new[] { "SessionId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "uq_ticket_code",
                table: "parking_tickets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_ticket_open_session",
                table: "parking_tickets",
                column: "SessionId",
                unique: true,
                filter: "\"Status\" = 'ISSUED'");

            migrationBuilder.CreateIndex(
                name: "IX_parking_zones_LotId_Code",
                table: "parking_zones",
                columns: new[] { "LotId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_parking_policies_PolicyId",
                table: "bookings",
                column: "PolicyId",
                principalTable: "parking_policies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_parking_floors_parking_zones_ZoneId_lot_id",
                table: "parking_floors",
                columns: new[] { "ZoneId", "lot_id" },
                principalTable: "parking_zones",
                principalColumns: new[] { "Id", "LotId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_parking_sessions_parking_policies_PolicyId",
                table: "parking_sessions",
                column: "PolicyId",
                principalTable: "parking_policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_parking_sessions_parking_zones_ZoneId_lot_id",
                table: "parking_sessions",
                columns: new[] { "ZoneId", "lot_id" },
                principalTable: "parking_zones",
                principalColumns: new[] { "Id", "LotId" },
                onDelete: ReferentialAction.Restrict);
            // Preserve all existing IDs and history; only attach parent metadata.
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM parking_sessions WHERE "EntryEventId" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot downgrade after ticketed intake. Restore the pre-deployment backup in an isolated database or deploy a forward fix.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_parking_policies_PolicyId",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_parking_floors_parking_zones_ZoneId_lot_id",
                table: "parking_floors");

            migrationBuilder.DropForeignKey(
                name: "FK_parking_sessions_parking_policies_PolicyId",
                table: "parking_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_parking_sessions_parking_zones_ZoneId_lot_id",
                table: "parking_sessions");

            migrationBuilder.DropTable(
                name: "parking_policies");

            migrationBuilder.DropTable(
                name: "parking_tickets");

            migrationBuilder.DropTable(
                name: "parking_zones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_parking_sessions_id_lot_id",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "ix_parking_sessions_current_slot_id",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "IX_parking_sessions_EntryEventId",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "IX_parking_sessions_PolicyId",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "IX_parking_sessions_RecognitionId",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "IX_parking_sessions_ZoneId_lot_id",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "uq_parking_sessions_active_plate",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "IX_parking_floors_ZoneId_lot_id",
                table: "parking_floors");

            migrationBuilder.DropIndex(
                name: "IX_bookings_PolicyId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "AllowedFuelType",
                table: "parking_slots");

            migrationBuilder.DropColumn(
                name: "EntryEventId",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "EntryImageId",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "EntryRequestHash",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "FuelType",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "PolicyId",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "RecognitionId",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "TemporaryPlateNumber",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "VerifiedBy",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "parking_floors");

            migrationBuilder.DropColumn(
                name: "PolicyId",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "ix_parking_sessions_current_slot_id",
                table: "parking_sessions",
                column: "current_slot_id");

            migrationBuilder.CreateIndex(
                name: "uq_parking_sessions_active_plate",
                table: "parking_sessions",
                column: "plate_number",
                unique: true,
                filter: "status IN ('ACTIVE', 'PAID')");
        }
    }
}
