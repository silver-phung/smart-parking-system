namespace SmartParking.Application.IoT;

public class IoTEventDto
{
    public string CarId { get; set; } = string.Empty; // Biển số xe, VD: "51B1-12345"
    public int ParkingLotId { get; set; }
    public string? SlotNumber { get; set; }
    public string EventType { get; set; } = "CarParked"; // CarParked, CarDeparted, Fault
    public DateTimeOffset? Timestamp { get; set; }
}

public class IoTEventResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string CarId { get; set; } = string.Empty;
    public int ParkingLotId { get; set; }
    public string ParkingLotName { get; set; } = string.Empty;
    public int AvailableSlots { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

public class IoTTelemetryDto
{
    public string CarId { get; set; } = string.Empty;
    public int ParkingLotId { get; set; }
    public string ParkingLotName { get; set; } = string.Empty;
    public string? SlotNumber { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Status { get; set; } = "Normal"; // Normal, Fault, Rejected
}

public class ParkedCarInfoDto
{
    public string CarId { get; set; } = string.Empty;
    public int ParkingLotId { get; set; }
    public string ParkingLotName { get; set; } = string.Empty;
    public string? SlotNumber { get; set; }
    public DateTimeOffset CheckInTime { get; set; }
}
