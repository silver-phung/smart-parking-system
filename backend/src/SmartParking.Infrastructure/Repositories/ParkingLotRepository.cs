using System.Text.Json;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Repositories;

public class ParkingLotRepository : IParkingLotRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _jsonFilePath;

    public ParkingLotRepository(string jsonFilePath)
    {
        _jsonFilePath = jsonFilePath;
    }

    public async Task<IEnumerable<ParkingLot>> GetAllAsync()
    {
        if (!File.Exists(_jsonFilePath))
        {
            throw new InvalidOperationException($"Không tìm thấy file dữ liệu bãi đỗ xe tại: {_jsonFilePath}");
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(_jsonFilePath);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Không thể đọc file dữ liệu bãi đỗ xe.", ex);
        }

        List<ParkingLot>? parkingLots;
        try
        {
            parkingLots = JsonSerializer.Deserialize<List<ParkingLot>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("File dữ liệu bãi đỗ xe không đúng định dạng JSON.", ex);
        }

        if (parkingLots is null || parkingLots.Count == 0)
        {
            throw new InvalidOperationException("File dữ liệu bãi đỗ xe không chứa danh sách hợp lệ.");
        }

        ValidateParkingLots(parkingLots);
        return parkingLots;
    }

    public async Task<ParkingLot?> GetByIdAsync(int id)
    {
        var lots = await GetAllAsync();
        return lots.FirstOrDefault(l => l.Id == id);
    }

    public async Task<bool> UpdateAvailableSlotsAsync(int id, int delta)
    {
        var lots = (await GetAllAsync()).ToList();
        var lot = lots.FirstOrDefault(l => l.Id == id);
        if (lot is null)
        {
            return false;
        }

        lot.AvailableSlots = Math.Max(0, lot.AvailableSlots + delta);
        await SaveAsync(lots);
        return true;
    }

    public async Task<bool> ResetToDefaultAsync()
    {
        var defaultLots = new List<ParkingLot>
        {
            new() { Id = 1, Name = "Bãi xe FPT Software Quận 9", AvailableSlots = 24, HourlyRate = 15000 },
            new() { Id = 2, Name = "Bãi xe Vincom Landmark 81", AvailableSlots = 0, HourlyRate = 25000 },
            new() { Id = 3, Name = "Bãi xe Aeon Mall Tân Phú", AvailableSlots = 8, HourlyRate = 18000 }
        };

        await SaveAsync(defaultLots);
        return true;
    }

    private async Task SaveAsync(List<ParkingLot> lots)
    {
        var json = JsonSerializer.Serialize(lots, WriteOptions);
        await File.WriteAllTextAsync(_jsonFilePath, json);
    }

    private static void ValidateParkingLots(List<ParkingLot> parkingLots)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lot in parkingLots)
        {
            if (string.IsNullOrWhiteSpace(lot.Name))
            {
                throw new InvalidOperationException($"Bãi đỗ xe Id={lot.Id} có tên rỗng.");
            }

            if (!names.Add(lot.Name.Trim()))
            {
                throw new InvalidOperationException($"Tên bãi đỗ xe bị trùng: {lot.Name}.");
            }

            if (lot.AvailableSlots < 0)
            {
                throw new InvalidOperationException($"Bãi '{lot.Name}' có số chỗ trống âm.");
            }

            if (lot.HourlyRate <= 0)
            {
                throw new InvalidOperationException($"Bãi '{lot.Name}' có giá theo giờ không hợp lệ.");
            }
        }
    }
}
