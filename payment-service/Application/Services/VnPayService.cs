using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Application.Helpers;

namespace Application.Services
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(HttpContext context, string invoiceId, string sessionId, decimal amount);
        bool ValidateCallback(IQueryCollection collections);
    }

    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _config;

        public VnPayService(IConfiguration config)
        {
            _config = config;
        }

        public string CreatePaymentUrl(HttpContext context, string invoiceId, string sessionId, decimal amount)
        {
            var vnpay = new VnPayLibrary();

            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", _config["VnPay:TmnCode"]!);
            vnpay.AddRequestData("vnp_Amount", (amount * 100).ToString("0"));
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan ve xe cho Session: {sessionId}");
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", _config["VnPay:ReturnUrl"]!);
            vnpay.AddRequestData("vnp_TxnRef", invoiceId);

            return vnpay.CreateRequestUrl(_config["VnPay:BaseUrl"]!, _config["VnPay:HashSecret"]!);
        }

        public bool ValidateCallback(IQueryCollection collections)
        {
            var vnpay = new VnPayLibrary();
            foreach (var (key, value) in collections)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, value.ToString());
                }
            }

            string vnp_SecureHash = collections["vnp_SecureHash"]!;
            return vnpay.ValidateSignature(vnp_SecureHash, _config["VnPay:HashSecret"]!);
        }
    }
}