using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartParking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingsAndCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "booking_id",
                table: "parking_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lot_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: false),
                    plate_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    checked_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("chk_bookings_plate_normalized", "plate_number = UPPER(TRIM(plate_number))");
                    table.CheckConstraint("chk_bookings_status", "status IN ('PENDING', 'CHECKED_IN', 'CANCELLED', 'EXPIRED')");
                    table.CheckConstraint("chk_bookings_time_order", "end_time IS NULL OR end_time >= start_time");
                    table.ForeignKey(
                        name: "fk_bookings_lot",
                        column: x => x.lot_id,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_vehicle",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_booking_id",
                table: "parking_sessions",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_lot_status",
                table: "bookings",
                columns: new[] { "lot_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_plate_status",
                table: "bookings",
                columns: new[] { "plate_number", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_user_id",
                table: "bookings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_bookings_pending_vehicle",
                table: "bookings",
                column: "vehicle_id",
                unique: true,
                filter: "status = 'PENDING'");

            migrationBuilder.AddForeignKey(
                name: "fk_parking_sessions_booking",
                table: "parking_sessions",
                column: "booking_id",
                principalTable: "bookings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_parking_sessions_booking",
                table: "parking_sessions");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_parking_sessions_booking_id",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "booking_id",
                table: "parking_sessions");
        }
    }
}
