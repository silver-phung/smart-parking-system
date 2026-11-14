using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_cash_handovers_lot_date_from_shift",
                table: "cash_handovers");

            migrationBuilder.AddColumn<Guid>(
                name: "incoming_shift_id",
                table: "cash_handovers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "outgoing_shift_id",
                table: "cash_handovers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<int>(type: "integer", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_manager_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shifts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    duty_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assigned_by_manager_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_assignments_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_handovers_incoming_shift_id",
                table: "cash_handovers",
                column: "incoming_shift_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_handovers_outgoing_shift_id",
                table: "cash_handovers",
                column: "outgoing_shift_id");

            migrationBuilder.CreateIndex(
                name: "ux_cash_handovers_lot_date_outgoing_shift",
                table: "cash_handovers",
                columns: new[] { "lot_id", "business_date", "outgoing_shift_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_shift_assignments_shift_staff",
                table: "shift_assignments",
                columns: new[] { "shift_id", "staff_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_shifts_lot_business_date_code",
                table: "shifts",
                columns: new[] { "lot_id", "business_date", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_handovers_shifts_incoming_shift_id",
                table: "cash_handovers",
                column: "incoming_shift_id",
                principalTable: "shifts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_handovers_shifts_outgoing_shift_id",
                table: "cash_handovers",
                column: "outgoing_shift_id",
                principalTable: "shifts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_handovers_shifts_incoming_shift_id",
                table: "cash_handovers");

            migrationBuilder.DropForeignKey(
                name: "FK_cash_handovers_shifts_outgoing_shift_id",
                table: "cash_handovers");

            migrationBuilder.DropTable(
                name: "shift_assignments");

            migrationBuilder.DropTable(
                name: "shifts");

            migrationBuilder.DropIndex(
                name: "IX_cash_handovers_incoming_shift_id",
                table: "cash_handovers");

            migrationBuilder.DropIndex(
                name: "IX_cash_handovers_outgoing_shift_id",
                table: "cash_handovers");

            migrationBuilder.DropIndex(
                name: "ux_cash_handovers_lot_date_outgoing_shift",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "incoming_shift_id",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "outgoing_shift_id",
                table: "cash_handovers");

            migrationBuilder.CreateIndex(
                name: "ux_cash_handovers_lot_date_from_shift",
                table: "cash_handovers",
                columns: new[] { "lot_id", "business_date", "from_shift_code" },
                unique: true);
        }
    }
}
