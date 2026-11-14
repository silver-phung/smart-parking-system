using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCashHandover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_handovers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<int>(type: "integer", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_shift_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to_shift_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    outgoing_staff_id = table.Column<long>(type: "bigint", nullable: false),
                    incoming_staff_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by_manager_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    outgoing_counted_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    outgoing_confirmed_by = table.Column<long>(type: "bigint", nullable: true),
                    outgoing_confirmed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    incoming_counted_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    incoming_confirmed_by = table.Column<long>(type: "bigint", nullable: true),
                    incoming_confirmed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dispute_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    disputed_by = table.Column<long>(type: "bigint", nullable: true),
                    disputed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_handovers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_handovers_lot_date_status",
                table: "cash_handovers",
                columns: new[] { "lot_id", "business_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_cash_handovers_lot_date_from_shift",
                table: "cash_handovers",
                columns: new[] { "lot_id", "business_date", "from_shift_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_handovers");
        }
    }
}
