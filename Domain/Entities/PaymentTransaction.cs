using System;

namespace Domain.Entities
{
    public class PaymentTransaction
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public required Invoice Invoice { get; set; }

        public decimal Amount { get; set; }
        public required string PaymentMethod { get; set; } // VD: "VNPAY"
        public required string TransactionNo { get; set; } // Mã giao dịch trả về từ VNPay
        public required string ResponseCode { get; set; } // Mã lỗi từ VNPay (VD: "00" là thành công)
        public required string Status { get; set; } // "Success", "Failed", "Pending"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}