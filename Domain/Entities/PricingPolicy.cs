using System;

namespace Domain.Entities
{
    public class PricingPolicy
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public int Version { get; set; } // Tăng dần khi có giá mới
        public Guid? ParkingLotId { get; set; }
        public Guid? ParkingZoneId { get; set; }
        
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }

        public int GracePeriodMinutes { get; set; } = 15;
        public decimal DayBlockRate { get; set; }
        public decimal EveningBlockRate { get; set; }
        public decimal OvernightTouchRate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}