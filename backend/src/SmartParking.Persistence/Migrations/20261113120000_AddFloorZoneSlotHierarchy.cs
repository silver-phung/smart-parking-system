using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartParking.Persistence.Migrations;

[DbContext(typeof(SmartParking.Persistence.Data.SmartParkingDbContext))]
[Migration("20261113120000_AddFloorZoneSlotHierarchy")]
public sealed class AddFloorZoneSlotHierarchy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "FloorId", table: "parking_zones", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "zone_id", table: "parking_slots", type: "integer", nullable: true);
        migrationBuilder.Sql("""
            INSERT INTO parking_floors (lot_id, floor_number, name, total_slots, is_active, created_at, updated_at)
            SELECT l.id, 0, 'Tầng G', 0, l.is_active, now(), now()
            FROM parking_lots l WHERE NOT EXISTS (SELECT 1 FROM parking_floors f WHERE f.lot_id = l.id);

            -- Map the old floor -> zone association to the new zone -> floor association.
            UPDATE parking_zones z SET "FloorId" = source.floor_id
            FROM (SELECT "ZoneId" AS zone_id, MIN(id) AS floor_id FROM parking_floors WHERE "ZoneId" IS NOT NULL GROUP BY "ZoneId") source
            WHERE z."Id" = source.zone_id;

            -- A legacy zone without a floor is attached to the first floor in its lot.
            UPDATE parking_zones z SET "FloorId" = (SELECT f.id FROM parking_floors f WHERE f.lot_id = z."LotId" ORDER BY f.floor_number, f.id LIMIT 1)
            WHERE z."FloorId" IS NULL;

            -- Give lots with unassigned floors a zone on each floor. Ground floor G is monthly by default.
            INSERT INTO parking_zones ("LotId", "FloorId", "Code", "Name", "Type", "Layout", "IsActive", "UpdatedAt")
            SELECT f.lot_id, f.id, 'AUTO-F' || f.id, CASE WHEN f.floor_number = 0 OR f.name ILIKE '%tầng g%' THEN 'Khu vé tháng' ELSE 'Khu thường ' || f.name END,
                   CASE WHEN f.floor_number = 0 OR f.name ILIKE '%tầng g%' THEN 'FIXED' ELSE 'TRANSIENT' END,
                   'INSIDE', f.is_active, now()
            FROM parking_floors f
            WHERE NOT EXISTS (SELECT 1 FROM parking_zones z WHERE z."FloorId" = f.id);

            -- Carry the old zone assignment over to each slot. Fall back to that floor's new default zone.
            UPDATE parking_slots s SET zone_id = COALESCE(
                (SELECT z."Id" FROM parking_zones z WHERE z."FloorId" = s.floor_id AND z."Id" = f."ZoneId" LIMIT 1),
                (SELECT z."Id" FROM parking_zones z WHERE z."FloorId" = s.floor_id ORDER BY z."Id" LIMIT 1))
            FROM parking_floors f WHERE f.id = s.floor_id;

            -- Normalize existing Ground floor zones as monthly parking.
            UPDATE parking_zones z SET "Type" = 'FIXED', "Name" = CASE WHEN z."Name" = 'Khu vực bình thường' THEN 'Khu vé tháng' ELSE z."Name" END
            FROM parking_floors f
            WHERE z."FloorId" = f.id AND (f.floor_number = 0 OR f.name ILIKE '%tầng g%');
            """);
        migrationBuilder.AlterColumn<int>(name: "FloorId", table: "parking_zones", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
        migrationBuilder.AlterColumn<int>(name: "zone_id", table: "parking_slots", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
        migrationBuilder.AddUniqueConstraint(name: "AK_parking_floors_id_lot_id", table: "parking_floors", columns: new[] { "id", "lot_id" });
        migrationBuilder.AddUniqueConstraint(name: "AK_parking_zones_Id_FloorId", table: "parking_zones", columns: new[] { "Id", "FloorId" });
        migrationBuilder.CreateIndex(name: "IX_parking_zones_FloorId_LotId", table: "parking_zones", columns: new[] { "FloorId", "LotId" });
        migrationBuilder.CreateIndex(name: "IX_parking_slots_zone_id_floor_id", table: "parking_slots", columns: new[] { "zone_id", "floor_id" });
        migrationBuilder.AddForeignKey(name: "FK_parking_zones_parking_floors_FloorId_LotId", table: "parking_zones",
            columns: new[] { "FloorId", "LotId" }, principalTable: "parking_floors", principalColumns: new[] { "id", "lot_id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "fk_parking_slots_zone_floor", table: "parking_slots",
            columns: new[] { "zone_id", "floor_id" }, principalTable: "parking_zones", principalColumns: new[] { "Id", "FloorId" }, onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "fk_parking_slots_zone_floor", table: "parking_slots");
        migrationBuilder.DropForeignKey(name: "FK_parking_zones_parking_floors_FloorId_LotId", table: "parking_zones");
        migrationBuilder.DropIndex(name: "IX_parking_zones_FloorId_LotId", table: "parking_zones");
        migrationBuilder.DropIndex(name: "IX_parking_slots_zone_id_floor_id", table: "parking_slots");
        migrationBuilder.DropUniqueConstraint(name: "AK_parking_zones_Id_FloorId", table: "parking_zones");
        migrationBuilder.DropUniqueConstraint(name: "AK_parking_floors_id_lot_id", table: "parking_floors");
        migrationBuilder.DropColumn(name: "zone_id", table: "parking_slots");
        migrationBuilder.DropColumn(name: "FloorId", table: "parking_zones");
    }
}
