namespace SmartParking.Application.DTOs.Sessions;

public class MonthlyTrafficStatsDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int? LotId { get; set; }
    public int TotalCheckIns { get; set; }
    public int TotalCheckOuts { get; set; }
    public decimal TotalRevenue { get; set; }
    public IReadOnlyList<DailyTrafficDto> Days { get; set; } = [];
    // 24 phần tử, lượt vào theo giờ trong ngày (giờ Việt Nam).
    public IReadOnlyList<int> CheckInsByHour { get; set; } = [];
}

public class DailyTrafficDto
{
    public string Date { get; set; } = string.Empty;
    public int CheckIns { get; set; }
    public int CheckOuts { get; set; }
    public decimal Revenue { get; set; }
}
