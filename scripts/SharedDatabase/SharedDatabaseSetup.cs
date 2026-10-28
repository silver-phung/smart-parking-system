using Npgsql;

namespace SmartParking.SharedDatabase;

public sealed record SharedDatabaseTarget(
    string Host, string Database, string Branch, string ConnectionStringName)
{
    public string SecretKey => $"ConnectionStrings:{ConnectionStringName}";
    public string EnvironmentKey => $"ConnectionStrings__{ConnectionStringName}";

    public string CreateConnectionString(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Username va password khong duoc de trong.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = 5432,
            Database = Database,
            Username = username.Trim(),
            Password = password,
            SslMode = SslMode.VerifyFull,
            Timeout = 30,
            CommandTimeout = 30
        }.ConnectionString;
    }

    public void Validate(string connectionString)
    {
        NpgsqlConnectionStringBuilder saved;
        try { saved = new NpgsqlConnectionStringBuilder(connectionString); }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Cau hinh DB khong hop le. Hay chay lai voi --configure.");
        }

        if (!string.Equals(saved.Host, Host, StringComparison.OrdinalIgnoreCase)
            || saved.Port != 5432 || saved.Database != Database
            || saved.SslMode != SslMode.VerifyFull
            || string.IsNullOrWhiteSpace(saved.Username) || string.IsNullOrEmpty(saved.Password))
            throw new InvalidOperationException(
                "Cau hinh da luu khong trung DB chung hoac thieu TLS/thong tin dang nhap. Hay chay lai voi --configure.");
    }
}

public sealed record DatabaseIdentity(string Database, string Username);

public static class SharedDatabaseSetup
{
    // Only persist credentials after a real query has succeeded against the expected database.
    public static async Task<DatabaseIdentity> VerifyAsync(
        SharedDatabaseTarget target,
        string connectionString,
        Func<string, Task<DatabaseIdentity>> probe,
        Func<string, Task>? save = null)
    {
        target.Validate(connectionString);
        var identity = await probe(connectionString);
        if (identity.Database != target.Database)
            throw new InvalidOperationException("Server tra ve database khac DB chung. Khong luu cau hinh.");
        if (save is not null)
            await save(connectionString);
        return identity;
    }
}
