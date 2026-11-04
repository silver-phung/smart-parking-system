using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SmartParking.Application.DTOs.Operations;
using SmartParking.Infrastructure.Data;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Tests;

public sealed class ParkingPostgresFactAttribute : FactAttribute
{
    public ParkingPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PARKING_TEST_POSTGRES")))
            Skip = "Set PARKING_TEST_POSTGRES to a dedicated PostgreSQL test database; no shared database is used by default.";
    }
}

public class ParkingOperationsPostgresTests
{
    private sealed class Database : IAsyncDisposable
    {
        private readonly string connection;
        private readonly string schema = "s4_test_" + Guid.NewGuid().ToString("N");
        private DbContextOptions<SmartParkingDbContext> options = null!;
        public int LotId { get; private set; }
        public int ZoneId { get; private set; }
        public long ActorId { get; private set; }
        public long SlotId { get; private set; }
        public long LegacySessionId { get; private set; }
        public long BookingId { get; private set; }
        private Database(string connection) => this.connection = connection;
        public SmartParkingDbContext Context() => new(options);
        public static async Task<Database> Create()
        {
            var test = new Database(Environment.GetEnvironmentVariable("PARKING_TEST_POSTGRES")!);
            await using var root = new NpgsqlConnection(test.connection); await root.OpenAsync();
            await new NpgsqlCommand($"CREATE SCHEMA \"{test.schema}\"", root).ExecuteNonQueryAsync();
            var cs = new NpgsqlConnectionStringBuilder(test.connection) { SearchPath = test.schema };
            test.options = new DbContextOptionsBuilder<SmartParkingDbContext>().UseNpgsql(cs.ConnectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", test.schema)).Options;
            try {
                await using var db = test.Context();
                await db.GetService<IMigrator>().MigrateAsync("20261029114209_AddDutyToStaffLotAssignment");
                await using var conn = new NpgsqlConnection(cs.ConnectionString); await conn.OpenAsync();
                async Task<long> Insert(string sql) => Convert.ToInt64(await new NpgsqlCommand(sql, conn).ExecuteScalarAsync());
                test.ActorId = await Insert("INSERT INTO users (username,email,password_hash,role_id,full_name,is_active,approval_status) VALUES ('ops_pg_admin','ops_pg@test.invalid','test-only',1,'Test Admin',true,'APPROVED') RETURNING id");
                test.LotId = (int)await Insert("INSERT INTO parking_lots (name,address,total_capacity) VALUES ('Isolated test lot','Test only',999) RETURNING id");
                var floor = await Insert($"INSERT INTO parking_floors (lot_id,floor_number,name) VALUES ({test.LotId},1,'Floor') RETURNING id");
                test.SlotId = await Insert($"INSERT INTO parking_slots (floor_id,slot_code) VALUES ({floor},'A1') RETURNING id");
                await Insert($"INSERT INTO parking_slots (floor_id,slot_code) VALUES ({floor},'A2') RETURNING id");
                await Insert($"INSERT INTO parking_slots (floor_id,slot_code) VALUES ({floor},'A3') RETURNING id");
                var vehicle = await Insert($"INSERT INTO vehicles (user_id,plate_number) VALUES ({test.ActorId},'OLD123') RETURNING id");
                test.BookingId = await Insert($"INSERT INTO bookings (lot_id,user_id,vehicle_id,plate_number,status) VALUES ({test.LotId},{test.ActorId},{vehicle},'OLD123','CHECKED_IN') RETURNING id");
                test.LegacySessionId = await Insert($"INSERT INTO parking_sessions (lot_id,vehicle_id,plate_number,booking_id) VALUES ({test.LotId},{vehicle},'OLD123',{test.BookingId}) RETURNING id");
                await db.Database.MigrateAsync();
                test.ZoneId = await db.ParkingZones.Where(z => z.LotId == test.LotId).Select(z => z.Id).SingleAsync();
                db.ParkingPolicies.Add(new() { LotId = test.LotId, CreatedBy = test.ActorId, ApprovedBy = test.ActorId,
                    ApprovedAt = DateTime.UtcNow, EffectiveFrom = DateTime.UtcNow.AddMinutes(-1), DecisionReference = "Isolated test fixture" });
                await db.SaveChangesAsync(); return test;
            }
            catch { await test.DisposeAsync(); throw; }
        }
        public async Task<OperationSessionDto> Enter(string ticket, Guid eventId)
        {
            await using var db = Context();
            await using var ai = new AiRecognitionDbContext(new DbContextOptionsBuilder<AiRecognitionDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            return await new ParkingOperationsService(db, new ParkingAuthorizationService(db), new RecognitionEvidenceReader(ai)).Enter(LotId,
                new(eventId, ZoneId, ticket, null, null, "Manual entry in isolated test"), ActorId, "ADMIN", default);
        }
        public async Task<OperationSessionDto?> TryVerify(OperationSessionDto s, string plate)
        {
            await using var db = Context();
            await using var ai = new AiRecognitionDbContext(new DbContextOptionsBuilder<AiRecognitionDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            try { return await new ParkingOperationsService(db, new ParkingAuthorizationService(db), new RecognitionEvidenceReader(ai)).Verify(LotId,
                s.Id, new(SlotId, plate, "PETROL", s.Revision, true, "Observed at slot"), ActorId, "ADMIN", default); }
            catch (ParkingOperationException e) when (e.Status == 409) { return null; }
        }
        public async ValueTask DisposeAsync()
        {
            // Only the random schema created by this fixture is removed, never public/application tables.
            if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^s4_test_[0-9a-f]{32}$"))
                throw new InvalidOperationException("Invalid test schema name.");
            await using var conn = new NpgsqlConnection(connection); await conn.OpenAsync();
            await new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", conn).ExecuteNonQueryAsync();
        }
    }
    [ParkingPostgresFact]
    public async Task Migration_backfills_parents_without_changing_slot_session_or_booking_ids_and_can_run_again()
    {
        await using var f = await Database.Create(); await using var db = f.Context();
        await db.Database.MigrateAsync(); // supported repeated deployment is a no-op
        var s = await db.ParkingSessions.SingleAsync();
        Assert.Equal(f.LegacySessionId, s.Id); Assert.Equal(f.BookingId, s.BookingId);
        Assert.Equal(f.ZoneId, s.ZoneId); Assert.Equal("LEGACY", s.VerificationStatus);
        Assert.Equal(f.SlotId, (await db.ParkingSlots.OrderBy(s => s.Id).FirstAsync()).Id);
        Assert.Equal(f.ZoneId, (await db.ParkingFloors.SingleAsync()).ZoneId);
    }
    [ParkingPostgresFact]
    public async Task Concurrent_event_replay_and_a_new_context_after_restart_keep_one_ticketed_session()
    {
        await using var f = await Database.Create(); var id = Guid.NewGuid();
        var results = await Task.WhenAll(f.Enter("VE-PG001", id), f.Enter("VE-PG001", id));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(results[0].Id, (await f.Enter("VE-PG001", id)).Id);
        await using var db = f.Context();
        Assert.Equal(1, await db.ParkingSessions.CountAsync(s => s.EntryEventId == id));
        Assert.Equal(1, await db.ParkingTickets.CountAsync());
        var ex = await Assert.ThrowsAsync<ParkingOperationException>(() => f.Enter("VE-PG001", Guid.NewGuid()));
        Assert.Equal("TICKET_IN_USE", ex.Code);
    }
    [ParkingPostgresFact]
    public async Task Two_independent_connections_cannot_verify_two_vehicles_in_one_slot()
    {
        await using var f = await Database.Create();
        var a = await f.Enter("VE-PG001", Guid.NewGuid()); var b = await f.Enter("VE-PG002", Guid.NewGuid());
        var results = await Task.WhenAll(f.TryVerify(a, "NEW111"), f.TryVerify(b, "NEW222"));
        Assert.Single(results.Where(s => s != null));
        await using var db = f.Context();
        Assert.Equal(1, await db.ParkingSessions.CountAsync(s => s.CurrentSlotId == f.SlotId));
        Assert.Equal("OCCUPIED", (await db.ParkingSlots.SingleAsync(s => s.Id == f.SlotId)).PhysicalStatus);
    }
}
