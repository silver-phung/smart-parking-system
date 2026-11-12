using System;
using Domain.Enums;

namespace Domain.Entities
{
    public class ParkingSessionBilling
    {
        public Guid Id { get; set; } // Giữ cùng ID với Parking Operations Service
        public required string LicensePlate { get; set; }
        public VehicleType VehicleType { get; set; } // Dùng Enum 
        
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }

        public bool IsMonthlyPass { get; set; }

        // Khóa ngoại trỏ về chính sách giá đã dùng lúc xe vào
        public Guid? AppliedPricingPolicyId { get; set; }
        public required PricingPolicy AppliedPricingPolicy { get; set; }
    }
}