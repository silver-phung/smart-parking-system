using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartParking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeStaffAssignmentsToFloorAndZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "floor_id",
                table: "staff_lot_assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "zone_id",
                table: "staff_lot_assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_lot_assignments_floor_id",
                table: "staff_lot_assignments",
                column: "floor_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_lot_assignments_zone_id",
                table: "staff_lot_assignments",
                column: "zone_id");

            migrationBuilder.AddForeignKey(
                name: "FK_staff_lot_assignments_parking_floors_floor_id",
                table: "staff_lot_assignments",
                column: "floor_id",
                principalTable: "parking_floors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_staff_lot_assignments_parking_zones_zone_id",
                table: "staff_lot_assignments",
                column: "zone_id",
                principalTable: "parking_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_staff_lot_assignments_parking_floors_floor_id",
                table: "staff_lot_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_staff_lot_assignments_parking_zones_zone_id",
                table: "staff_lot_assignments");

            migrationBuilder.DropIndex(
                name: "IX_staff_lot_assignments_floor_id",
                table: "staff_lot_assignments");

            migrationBuilder.DropIndex(
                name: "IX_staff_lot_assignments_zone_id",
                table: "staff_lot_assignments");

            migrationBuilder.DropColumn(
                name: "floor_id",
                table: "staff_lot_assignments");

            migrationBuilder.DropColumn(
                name: "zone_id",
                table: "staff_lot_assignments");
        }
    }
}
