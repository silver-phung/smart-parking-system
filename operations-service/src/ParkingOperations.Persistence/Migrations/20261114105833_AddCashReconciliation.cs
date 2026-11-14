using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_reconciliations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_cash = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    cash_receipts = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    cash_refunds = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    cash_disbursements = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    expected_closing_cash = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    counted_closing_cash = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    variance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    payment_summary_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payment_summary_captured_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    variance_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    submitted_by_manager_id = table.Column<long>(type: "bigint", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_by_manager_id = table.Column<long>(type: "bigint", nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approval_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_reconciliations", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_reconciliations_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_reconciliations_status_submitted_at",
                table: "cash_reconciliations",
                columns: new[] { "status", "submitted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_cash_reconciliations_shift",
                table: "cash_reconciliations",
                column: "shift_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_reconciliations");
        }
    }
}
