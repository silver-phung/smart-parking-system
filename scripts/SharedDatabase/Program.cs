using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Npgsql;
using SmartParking.SharedDatabase;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    try
    {
        var root = Directory.GetCurrentDirectory();
        var configure = false;
        var runBackend = false;
        string? username = null;
        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "--root" when i + 1 < arguments.Length: root = arguments[++i]; break;
                case "--username" when i + 1 < arguments.Length: username = arguments[++i]; break;
                case "--configure": configure = true; break;
                case "--run": runBackend = true; break;
                case "--help":
                    Console.WriteLine("Windows: .\\scripts\\Connect-SharedDatabase.ps1 [-Configure] [-RunBackend] [-Username USER]");
                    Console.WriteLine("macOS: bash scripts/connect-shared-database.sh [--configure] [--run] [--username USER]");
                    Console.WriteLine("--configure: nhap va luu thong tin. Mac dinh: kiem tra thong tin da luu. --run: chay backend sau khi kiem tra.");
                    return 0;
                default: throw new InvalidOperationException("Tham so khong hop le. Dung --help de xem huong dan.");
            }
        }

        if (username is not null && !configure)
            throw new InvalidOperationException("--username chi dung cung --configure.");

        root = Path.GetFullPath(root);
        var apiProject = Path.Combine(root, "backend", "src", "SmartParking.Api", "SmartParking.Api.csproj");
        var targetPath = Path.Combine(root, "scripts", "shared-database.json");
        if (!File.Exists(apiProject) || !File.Exists(targetPath))
            throw new InvalidOperationException("Khong tim thay project API/cau hinh. Hay chay script tu repo smart-parking-system.");
        var target = JsonSerializer.Deserialize<SharedDatabaseTarget>(await File.ReadAllTextAsync(targetPath))
            ?? throw new InvalidOperationException("Khong doc duoc shared-database.json.");

        Console.WriteLine($"DB chung: {target.Database} | Neon branch: {target.Branch}");
        Console.WriteLine($"Host: {target.Host}");
        string connectionString;
        if (configure)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.Write("Username DB duoc cap tren Neon: ");
                username = Console.ReadLine();
            }
            Console.Write("Password DB (khong hien khi go): ");
            connectionString = target.CreateConnectionString(username ?? "", ReadPassword());
        }
        else
        {
            connectionString = await LoadSecretAsync(apiProject, target.SecretKey);
        }

        Console.WriteLine("Dang mo ket noi TLS va chay query kiem tra tren Neon...");
        var identity = await SharedDatabaseSetup.VerifyAsync(target, connectionString, ProbeAsync,
            configure ? value => SaveSecretAsync(apiProject, target.SecretKey, value) : null);
        Console.WriteLine($"OK: query thanh cong. Database: {identity.Database} | User: {identity.Username}");
        if (configure)
            Console.WriteLine($"Da luu {target.SecretKey} vao User Secrets tren may nay.");

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(target.EnvironmentKey)))
            Console.WriteLine($"Luu y: may dang co {target.EnvironmentKey}. Script --run se dung cau hinh vua kiem tra; chay API truc tiep co the dung bien cu.");

        if (!runBackend)
        {
            Console.WriteLine("Them --run (Windows: -RunBackend) de kiem tra DB va chay backend.");
            return 0;
        }

        Console.WriteLine("Dang chay backend. Giu terminal mo; Ctrl+C de dung backend.");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = root };
        foreach (var value in new[] { "run", "--project", apiProject, "--launch-profile", "http", "--property:UseAppHost=false" })
            start.ArgumentList.Add(value);
        start.Environment[target.EnvironmentKey] = connectionString;
        using var backend = Process.Start(start)
            ?? throw new InvalidOperationException("Khong khoi dong duoc backend.");
        await backend.WaitForExitAsync();
        return backend.ExitCode;
    }
    catch (PostgresException error)
    {
        Console.Error.WriteLine(error.SqlState switch
        {
            "28P01" or "28000" => "LOI: Neon tu choi username/password. Lay thong tin DB trong Connect tren Neon roi chay lai --configure.",
            "3D000" => "LOI: Khong tim thay database smartparking tren server nay.",
            "42501" => "LOI: Tai khoan DB chua duoc cap quyen ket noi/truy van.",
            _ => $"LOI: Query Neon that bai (SQLSTATE {error.SqlState}). Khong luu thong tin moi."
        });
        return 1;
    }
    catch (Exception error) when (error is NpgsqlException or TimeoutException)
    {
        Console.Error.WriteLine("LOI: Khong ket noi duoc Neon qua TLS. Kiem tra Internet, hostname va cong 5432. Khong luu thong tin moi.");
        return 1;
    }
    catch (InvalidOperationException error)
    {
        Console.Error.WriteLine($"LOI: {error.Message}");
        return 1;
    }
    catch (Exception)
    {
        // Never print raw process/provider errors, which can contain connection credentials.
        Console.Error.WriteLine("LOI: Khong doc/luu duoc cau hinh hoac chay cong cu. Kiem tra .NET 10 SDK va quyen truy cap User Secrets.");
        return 1;
    }
}

static string ReadPassword()
{
    if (Console.IsInputRedirected)
        return Console.ReadLine() ?? "";
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
        else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
}

static async Task<DatabaseIdentity> ProbeAsync(string connectionString)
{
    await using var dataSource = NpgsqlDataSource.Create(connectionString);
    await using var command = dataSource.CreateCommand("SELECT current_database(), current_user;");
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        throw new InvalidOperationException("Query khong tra ve ket qua. Khong luu cau hinh.");
    return new DatabaseIdentity(reader.GetString(0), reader.GetString(1));
}

static async Task<string> LoadSecretAsync(string project, string key)
{
    var result = await RunSecretCommandAsync(project, ["list", "--json"]);
    if (result.ExitCode != 0)
        throw new InvalidOperationException("Khong doc duoc User Secrets. Hay chay lai voi --configure.");
    // dotnet user-secrets can wrap its JSON output with //BEGIN and //END markers.
    var start = result.Output.IndexOf('{');
    var end = result.Output.LastIndexOf('}');
    if (start < 0 || end < start)
        throw new InvalidOperationException("Chua co thong tin DB tren may nay. Hay chay voi --configure.");
    using var secrets = JsonDocument.Parse(result.Output[start..(end + 1)]);
    if (!secrets.RootElement.TryGetProperty(key, out var value) || string.IsNullOrWhiteSpace(value.GetString()))
        throw new InvalidOperationException("Chua co thong tin DB tren may nay. Hay chay voi --configure.");
    return value.GetString()!;
}

static async Task SaveSecretAsync(string project, string key, string connectionString)
{
    var payload = JsonSerializer.Serialize(new Dictionary<string, string> { [key] = connectionString });
    var result = await RunSecretCommandAsync(project, ["set"], payload);
    if (result.ExitCode != 0)
        throw new InvalidOperationException("Query DB thanh cong nhung luu User Secrets that bai. Kiem tra quyen ghi roi chay lai --configure.");
}

static async Task<(int ExitCode, string Output)> RunSecretCommandAsync(
    string project, string[] command, string? input = null)
{
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false, RedirectStandardOutput = true,
        RedirectStandardError = true, RedirectStandardInput = input is not null
    };
    start.ArgumentList.Add("user-secrets");
    foreach (var value in command) start.ArgumentList.Add(value);
    start.ArgumentList.Add("--project");
    start.ArgumentList.Add(project);
    using var process = Process.Start(start)
        ?? throw new InvalidOperationException("Khong chay duoc dotnet user-secrets.");
    var output = process.StandardOutput.ReadToEndAsync();
    var errors = process.StandardError.ReadToEndAsync();
    if (input is not null)
    {
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
    }
    await process.WaitForExitAsync();
    await errors; // Consume errors without echoing secrets to the terminal.
    return (process.ExitCode, await output);
}
