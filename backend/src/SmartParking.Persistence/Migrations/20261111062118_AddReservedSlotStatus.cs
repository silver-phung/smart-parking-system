using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartParking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservedSlotStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_parking_slots_physical_status",
                table: "parking_slots");

            migrationBuilder.AddCheckConstraint(
                name: "chk_parking_slots_physical_status",
                table: "parking_slots",
                sql: "physical_status IN ('EMPTY', 'OCCUPIED', 'MAINTENANCE', 'UNAVAILABLE', 'RESERVED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_parking_slots_physical_status",
                table: "parking_slots");

            migrationBuilder.AddCheckConstraint(
                name: "chk_parking_slots_physical_status",
                table: "parking_slots",
                sql: "physical_status IN ('EMPTY', 'OCCUPIED', 'MAINTENANCE', 'UNAVAILABLE')");
        }
    }
}
