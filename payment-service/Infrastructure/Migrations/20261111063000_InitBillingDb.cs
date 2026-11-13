using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitBillingDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ParkingLotId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParkingZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GracePeriodMinutes = table.Column<int>(type: "integer", nullable: false),
                    DayBlockRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EveningBlockRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OvernightTouchRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParkingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicensePlate = table.Column<string>(type: "text", nullable: false),
                    VehicleType = table.Column<int>(type: "integer", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckOutTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsMonthlyPass = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedPricingPolicyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingSessions_PricingPolicies_AppliedPricingPolicyId",
                        column: x => x.AppliedPricingPolicyId,
                        principalTable: "PricingPolicies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_ParkingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    TransactionNo = table.Column<string>(type: "text", nullable: false),
                    ResponseCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SessionId",
                table: "Invoices",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_AppliedPricingPolicyId",
                table: "ParkingSessions",
                column: "AppliedPricingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_InvoiceId",
                table: "PaymentTransactions",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "ParkingSessions");

            migrationBuilder.DropTable(
                name: "PricingPolicies");
        }
    }
}
