// Domain/Entities/ParkingSession.cs
using System;
using Domain.Enums;

namespace Domain.Entities
{
    public class ParkingSession
    {
        public Guid SessionId { get; set; }
        public required string LicensePlate { get; set; }
        public bool IsMonthlyPass { get; set; }
        public VehicleType VehicleType { get; set; }
        public ParkingZone Zone { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
    }
}