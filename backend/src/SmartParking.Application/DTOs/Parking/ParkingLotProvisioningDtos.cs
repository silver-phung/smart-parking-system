namespace SmartParking.Application.DTOs.Parking;

/// <summary>
/// Kết quả cấu hình bố cục chuẩn (3 tầng A/B/C x 100 ô) cho một bãi đỗ xe. Thao tác idempotent.
/// </summary>
public class ProvisionLotLayoutResultDto
{
    public int LotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public int FloorsCreated { get; set; }
    public int SlotsCreated { get; set; }
    public int SlotsAlreadyExisting { get; set; }
    public int PhysicalSlotsCount { get; set; }
    public int ConfiguredCapacity { get; set; }
    public int OperationalCapacity { get; set; }
}
