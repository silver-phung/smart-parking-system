using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartParking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffEntryWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_vehicle_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecognitionId = table.Column<long>(type: "bigint", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: false),
                    ParkingSessionId = table.Column<long>(type: "bigint", nullable: true),
                    StaffUserId = table.Column<long>(type: "bigint", nullable: false),
                    PlateNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ParkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_vehicle_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_vehicle_entries_parking_lots_LotId",
                        column: x => x.LotId,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_vehicle_entries_parking_sessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "parking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_vehicle_entries_LotId_Status",
                table: "staff_vehicle_entries",
                columns: new[] { "LotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_vehicle_entries_ParkingSessionId",
                table: "staff_vehicle_entries",
                column: "ParkingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_vehicle_entries_RecognitionId",
                table: "staff_vehicle_entries",
                column: "RecognitionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_vehicle_entries");
        }
    }
}
