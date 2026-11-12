using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Invoice
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; } // ID của ParkingSessionBilling
        public ParkingSessionBilling Session { get; set; }

        public decimal TotalAmount { get; set; }
        public string Status { get; set; } // "Pending", "Paid", "Cancelled"
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }

        public required ICollection<PaymentTransaction> Transactions { get; set; }
    }
}