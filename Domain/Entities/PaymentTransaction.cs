using System;
namespace Domain.Entities
{
    public class PaymentTransaction
    {
        public Guid TransactionId { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public required string PaymentMethod { get; set; }
        public required string Status { get; set; }
    }
}