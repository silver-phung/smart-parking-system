using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartParking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDutyToStaffLotAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "duty",
                table: "staff_lot_assignments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "GATE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duty",
                table: "staff_lot_assignments");
        }
    }
}
