// Application/Services/BillingEngine.cs
using System;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Application.Interfaces;

namespace Application.Services
{
    public class BillingEngine : IBillingEngine
    {
        public decimal CalculateFee(ParkingSession session)
        {
            // 1. Kiểm tra xe vượt quá 9 chỗ (Quy định không nhận trông giữ)
            if (session.VehicleType == VehicleType.Over9)
            {
                throw new InvalidOperationException("Hệ thống không nhận trông giữ xe trên 9 chỗ.");
            }

            // 2. Nếu là xe đã đăng ký vé tháng -> Không tính phí lượt
            if (session.IsMonthlyPass)
            {
                return 0m; 
            }

            // 3. Tính phí theo lượt cho khách vãng lai
            if (!session.CheckOutTime.HasValue)
            {
                throw new ArgumentException("Xe chưa Check-out, không thể tính phí.");
            }

            TimeSpan duration = session.CheckOutTime.Value - session.CheckInTime;

            // Áp dụng Grace Period (Miễn phí 15 phút đầu nếu quay đầu ra ngay)
            if (duration.TotalMinutes <= PricingRules.GracePeriodMinutes)
            {
                return 0m;
            }

            // Tính phí cho lượt (2 giờ đầu) dựa vào thời điểm Check-in
            decimal totalFee = GetTurnFeeByTimeFrame(session.CheckInTime);

            // Xử lý logic nếu đỗ quá 2 giờ (Ví dụ: Cộng thêm phí block tiếp theo - Tùy chỉnh theo nghiệp vụ cụ thể)
            if (duration.TotalHours > PricingRules.FirstBlockHours)
            {
                // TODO: Triển khai logic tính tiền cho các giờ tiếp theo lũy tiến nếu có.
                // Tạm thời giữ nguyên mức phí của lượt đầu tiên.
            }

            return totalFee;
        }

        // Tính toán chi phí đăng ký vé tháng
        public decimal CalculateMonthlyFee(VehicleType vehicleType, ParkingZone zone)
        {
            if (vehicleType == VehicleType.Over9)
            {
                throw new InvalidOperationException("Hệ thống không nhận trông giữ xe trên 9 chỗ.");
            }

            return vehicleType switch
            {
                VehicleType.Seat4 => zone == ParkingZone.BasementAndSurface 
                                     ? PricingRules.Monthly_Seat4_Basement 
                                     : PricingRules.Monthly_Seat4_Surface,
                VehicleType.Seat5To6 => PricingRules.Monthly_Seat5To6,
                VehicleType.Seat9 => PricingRules.Monthly_Seat9,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        // Phân loại giá tiền theo khung giờ Check-in
        private decimal GetTurnFeeByTimeFrame(DateTime checkInTime)
        {
            TimeSpan timeOfDay = checkInTime.TimeOfDay;

            // Khung ban ngày: 06:00 - 17:59
            if (timeOfDay >= new TimeSpan(6, 0, 0) && timeOfDay <= new TimeSpan(17, 59, 59))
            {
                return PricingRules.Turn_Daytime;
            }
            // Khung buổi tối: 18:00 - 23:59
            else if (timeOfDay >= new TimeSpan(18, 0, 0) && timeOfDay <= new TimeSpan(23, 59, 59))
            {
                return PricingRules.Turn_Evening;
            }
            // Khung qua đêm: 00:00 - 05:59
            else
            {
                return PricingRules.Turn_Overnight;
            }
        }
    }
}