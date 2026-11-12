using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;

namespace API.Controllers
{
    [ApiController]
    [Route("api/billing")]
    public class BillingController : ControllerBase
    {
        private readonly IBillingEngine _billingEngine;
        private readonly IVnPayService _vnPayService;

        private readonly PaymentDbContext _context;

        public BillingController(IBillingEngine billingEngine, IVnPayService vnPayService, PaymentDbContext context)
        {
            _billingEngine = billingEngine;
            _vnPayService = vnPayService;
            _context = context;
        }

        [HttpPost("calculate-turn-fee")]
        public IActionResult CalculateTurnFee([FromBody] ParkingSessionBilling request)
        {
            try
            {
                decimal fee = _billingEngine.CalculateFee(request);
                return Ok(new { SessionId = request.Id, FeeAmount = fee, Currency = "VND" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("monthly-fee-quote")]
        public IActionResult GetMonthlyFeeQuote([FromQuery] VehicleType vehicleType, [FromQuery] ParkingZone zone)
        {
            try
            {
                decimal fee = _billingEngine.CalculateMonthlyFee(vehicleType, zone);
                return Ok(new { VehicleType = vehicleType.ToString(), Zone = zone.ToString(), FeeAmount = fee, Currency = "VND/tháng" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("create-payment-url")]
        public async Task<IActionResult> CreatePaymentUrl([FromQuery] Guid sessionId, [FromQuery] decimal amount)
        {
            try
            {
                var invoice = new Invoice
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    TotalAmount = amount,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    Session = null!,
                    Transactions = new List<PaymentTransaction>()
                };

                _context.Add(invoice);
                await _context.SaveChangesAsync();

                var url = _vnPayService.CreatePaymentUrl(HttpContext, invoice.Id.ToString(), sessionId.ToString(), amount);
                return Ok(new { paymentUrl = url, invoiceId = invoice.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("vnpay-ipn")]
        public async Task<IActionResult> PaymentIpn()
        {
            try
            {
                var isValidSignature = _vnPayService.ValidateCallback(Request.Query);
                if (!isValidSignature)
                {
                    return Ok(new { RspCode = "97", Message = "Invalid signature" });
                }

                var vnp_TxnRef = Request.Query["vnp_TxnRef"].ToString();
                var vnp_ResponseCode = Request.Query["vnp_ResponseCode"].ToString();
                var vnp_TransactionNo = Request.Query["vnp_TransactionNo"].ToString();
                var vnp_AmountStr = Request.Query["vnp_Amount"].ToString();
                var vnp_Amount = decimal.Parse(vnp_AmountStr) / 100;

                if (!Guid.TryParse(vnp_TxnRef, out Guid invoiceId))
                    return Ok(new { RspCode = "01", Message = "Order not found" });

                var invoice = await _context.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == invoiceId);
                if (invoice == null)
                    return Ok(new { RspCode = "01", Message = "Order not found" });

                if (invoice.Status == "Paid")
                    return Ok(new { RspCode = "02", Message = "Order already confirmed" });

                if (vnp_ResponseCode == "00")
                {
                    invoice.Status = "Paid";
                    invoice.PaidAt = DateTime.UtcNow;

                    var paymentTxn = new PaymentTransaction
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = invoice.Id,
                        Invoice = invoice,
                        Amount = vnp_Amount,
                        PaymentMethod = "VNPAY",
                        TransactionNo = vnp_TransactionNo,
                        ResponseCode = vnp_ResponseCode,
                        Status = "Success",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Add(paymentTxn);

                    var session = await _context.Set<ParkingSessionBilling>().FirstOrDefaultAsync(s => s.Id == invoice.SessionId);
                    if (session != null)
                    {
                        session.CheckOutTime = DateTime.UtcNow;
                    }
                }
                else
                {
                    invoice.Status = "Failed";
                }

                await _context.SaveChangesAsync();
                return Ok(new { RspCode = "00", Message = "Confirm Success" });
            }
            catch (Exception)
            {
                return Ok(new { RspCode = "99", Message = "Unknown error" });
            }
        }

        [HttpGet("vnpay-return")]
        public IActionResult PaymentCallback()
        {
            try
            {
                var isValidSignature = _vnPayService.ValidateCallback(Request.Query);

                if (!isValidSignature)
                {
                    return BadRequest(new { message = "Lỗi xác thực chữ ký (Invalid Signature)." });
                }

                var vnp_ResponseCode = Request.Query["vnp_ResponseCode"].ToString();
                var vnp_OrderInfo = Request.Query["vnp_OrderInfo"].ToString();
                var vnp_TransactionNo = Request.Query["vnp_TransactionNo"].ToString();

                if (vnp_ResponseCode == "00")
                {
                    return Ok(new
                    {
                        message = "Thanh toán thành công!",
                        orderInfo = vnp_OrderInfo,
                        transactionNo = vnp_TransactionNo
                    });
                }

                return BadRequest(new { message = $"Thanh toán thất bại. Mã lỗi VNPAY: {vnp_ResponseCode}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}