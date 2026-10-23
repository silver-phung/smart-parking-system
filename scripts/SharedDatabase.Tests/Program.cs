using Npgsql;
using SmartParking.SharedDatabase;

// Offline regression checks: no Neon access and no credentials written to User Secrets.
var target = new SharedDatabaseTarget(
    "shared-pooler.ap-southeast-1.aws.neon.tech", "smartparking", "production", "DefaultConnection");
var passed = 0;

const string password = "secret;'\";$()\\ two spaces";
var escaped = target.CreateConnectionString("member", password);
target.Validate(escaped);
var parsed = new NpgsqlConnectionStringBuilder(escaped);
Check(parsed.Password == password && parsed.Host == target.Host && parsed.SslMode == SslMode.VerifyFull,
    "Special characters round-trip without changing host or TLS");

foreach (var (key, value) in new[]
{
    ("Host", "localhost"), ("Database", "other"), ("Port", "5433"),
    ("SSL Mode", "Disable"), ("Password", "")
})
{
    var connection = new NpgsqlConnectionStringBuilder(target.CreateConnectionString("member", "test"));
    connection[key] = value;
    var queried = false;
    await ExpectFailureAsync<InvalidOperationException>(() => SharedDatabaseSetup.VerifyAsync(
        target, connection.ConnectionString,
        _ => { queried = true; return Task.FromResult(new DatabaseIdentity("smartparking", "member")); }));
    Check(!queried, $"Invalid {key} rejected before any query");
}

var saved = false;
await ExpectFailureAsync<TimeoutException>(() => SharedDatabaseSetup.VerifyAsync(
    target, target.CreateConnectionString("member", "test"),
    _ => throw new TimeoutException(),
    _ => { saved = true; return Task.CompletedTask; }));
Check(!saved, "Failed query does not save credentials");

await ExpectFailureAsync<InvalidOperationException>(() => SharedDatabaseSetup.VerifyAsync(
    target, target.CreateConnectionString("member", "test"),
    _ => Task.FromResult(new DatabaseIdentity("other", "member")),
    _ => { saved = true; return Task.CompletedTask; }));
Check(!saved, "Wrong database response does not save credentials");

var valid = target.CreateConnectionString("member", password);
var queryCompleted = false;
string? stored = null;
var identity = await SharedDatabaseSetup.VerifyAsync(target, valid,
    _ => { queryCompleted = true; return Task.FromResult(new DatabaseIdentity("smartparking", "member")); },
    value =>
    {
        if (!queryCompleted) throw new Exception("Credentials saved before query completed");
        stored = value;
        return Task.CompletedTask;
    });
Check(stored == valid && identity.Database == "smartparking", "Exact connection saved only after successful query");

await ExpectFailureAsync<IOException>(() => SharedDatabaseSetup.VerifyAsync(
    target, valid,
    _ => Task.FromResult(new DatabaseIdentity("smartparking", "member")),
    _ => throw new IOException("Cannot save")));
Check(true, "Saving failure is reported as a failure");

Console.WriteLine($"PASS: {passed} offline checks. No database or secrets were modified.");

void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    passed++;
    Console.WriteLine($"PASS: {name}");
}

static async Task ExpectFailureAsync<TException>(Func<Task<DatabaseIdentity>> action) where TException : Exception
{
    try { await action(); }
    catch (TException) { return; }
    throw new Exception($"Expected {typeof(TException).Name}");
}
