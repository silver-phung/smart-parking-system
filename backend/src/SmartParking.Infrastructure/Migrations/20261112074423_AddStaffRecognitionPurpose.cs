using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartParking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffRecognitionPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParkingLotId",
                table: "ai_recognition_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "ai_recognition_records",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                defaultValue: "ENTRY");

            migrationBuilder.CreateIndex(
                name: "IX_ai_recognition_records_ParkingLotId_Purpose_CreatedAt",
                table: "ai_recognition_records",
                columns: new[] { "ParkingLotId", "Purpose", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_recognition_records_ParkingLotId_Purpose_CreatedAt",
                table: "ai_recognition_records");

            migrationBuilder.DropColumn(
                name: "ParkingLotId",
                table: "ai_recognition_records");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "ai_recognition_records");
        }
    }
}
