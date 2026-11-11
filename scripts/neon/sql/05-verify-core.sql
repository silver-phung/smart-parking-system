-- Read-only. Run in the database selected for the current .NET core API.
SELECT current_database() AS database_name, current_user AS database_role, current_schema() AS schema_name;

SELECT "MigrationId", "ProductVersion"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT expected.table_name, to_regclass('public.' || expected.table_name) IS NOT NULL AS exists_in_public
FROM (VALUES ('users'), ('roles'), ('parking_lots'), ('parking_zones'), ('parking_floors'),
             ('parking_slots'), ('parking_sessions'), ('parking_tickets'), ('parking_policies'),
             ('bookings'), ('stored_images'), ('staff_lot_assignments')) AS expected(table_name);

SELECT 'floor_without_zone' AS check_name, count(*) AS violations FROM public.parking_floors WHERE "ZoneId" IS NULL
UNION ALL
SELECT 'floor_zone_wrong_lot', count(*) FROM public.parking_floors f JOIN public.parking_zones z ON z."Id" = f."ZoneId" WHERE f.lot_id <> z."LotId"
UNION ALL
SELECT 'legacy_session_without_zone', count(*) FROM public.parking_sessions WHERE "VerificationStatus" = 'LEGACY' AND "ZoneId" IS NULL;

SELECT indexname, indexdef FROM pg_indexes
WHERE schemaname = 'public' AND tablename IN ('parking_sessions', 'parking_tickets', 'parking_zones')
ORDER BY tablename, indexname;

-- No lots/zones/policies are seeded on an empty database. An empty result is expected until configured.
SELECT "Id", "LotId", "Code", "Type", "Layout", "IsActive" FROM public.parking_zones ORDER BY "LotId", "Id";
SELECT "Id", "LotId", "ZoneId", "EffectiveFrom", "ApprovedAt" FROM public.parking_policies ORDER BY "Id";
