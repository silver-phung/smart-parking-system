using System;

namespace Domain.Entities
{
    public class Invoice
    {
        public Guid InvoiceId { get; set; }
        public Guid SessionId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime IssueDate { get; set; }
        public bool IsPaid { get; set; }
    }
}