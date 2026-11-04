using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartParking.Infrastructure.Migrations.SmartParkingDb
{
    /// <inheritdoc />
    public partial class LinkEntryEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentSha256",
                table: "stored_images",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_EntryImageId",
                table: "parking_sessions",
                column: "EntryImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_parking_sessions_stored_images_EntryImageId",
                table: "parking_sessions",
                column: "EntryImageId",
                principalTable: "stored_images",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_parking_sessions_stored_images_EntryImageId",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "IX_parking_sessions_EntryImageId",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "ContentSha256",
                table: "stored_images");
        }
    }
}
