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
            // Từ chối phục vụ nếu là xe lớn hơn 9 chỗ
            if (session.VehicleType == VehicleType.Over9)
            {
                throw new InvalidOperationException("Hệ thống không nhận trông giữ xe trên 9 chỗ.");
            }

            // Yêu cầu phải có thời gian xe ra khỏi bãi
            if (!session.CheckOutTime.HasValue)
            {
                throw new ArgumentException("Xe chưa Check-out, không thể tính phí.");
            }

            // Kiểm tra tính hợp lệ: Giờ ra phải lớn hơn hoặc bằng giờ vào
            if (session.CheckOutTime.Value < session.CheckInTime)
            {
                throw new ArgumentException("Dữ liệu sai: Giờ ra nhỏ hơn giờ vào.");
            }

            // Nếu là vé tháng thì không tính phí lượt
            if (session.IsMonthlyPass)
            {
                return 0m;
            }

            DateTime checkIn = session.CheckInTime;
            DateTime checkOut = session.CheckOutTime.Value;
            TimeSpan totalDuration = checkOut - checkIn;

            DateTime calcCheckOut = checkOut;
            TimeSpan outTime = checkOut.TimeOfDay;

            // Nếu xe ra đúng vào các mốc thời gian giao cắt (18:00, 00:00, 06:00), 
            // tự động cộng thêm 1 giây để thời điểm ra lọt vào khung giờ của đợt tiếp theo
            if (outTime == new TimeSpan(18, 0, 0) || outTime == new TimeSpan(0, 0, 0) || outTime == new TimeSpan(6, 0, 0))
            {
                calcCheckOut = checkOut.AddSeconds(1);
            }

            double dayMinutes = 0;
            double eveningMinutes = 0;
            int overnightTouches = 0;

            DateTime currentDay = checkIn.Date;
            DateTime endDay = calcCheckOut.Date;

            // Duyệt qua từng ngày xe lưu trú trong bãi
            for (DateTime date = currentDay; date <= endDay; date = date.AddDays(1))
            {
                // Xác định mốc thời gian bắt đầu và kết thúc của phiên đỗ trong phạm vi ngày hiện tại
                DateTime startOfFrame = (date > checkIn.Date) ? date : checkIn;
                DateTime endOfFrame = (date < endDay) ? date.AddDays(1) : calcCheckOut;

                if (startOfFrame >= endOfFrame) continue;

                // Khung giờ đêm (00:00 đến 05:59)
                DateTime nightStart = date;
                DateTime nightEnd = date.AddHours(6);
                // Đếm số lần xe chạm vào khung giờ đêm
                if (startOfFrame < nightEnd && endOfFrame > nightStart)
                {
                    overnightTouches++;
                }

                // Khung ban ngày (06:00 đến 17:59)
                DateTime dayStart = date.AddHours(6);
                DateTime dayEnd = date.AddHours(18);
                // Tính tổng số phút xe đỗ trong khung ban ngày
                if (startOfFrame < dayEnd && endOfFrame > dayStart)
                {
                    DateTime overlapStart = (startOfFrame > dayStart) ? startOfFrame : dayStart;
                    DateTime overlapEnd = (endOfFrame < dayEnd) ? endOfFrame : dayEnd;
                    dayMinutes += (overlapEnd - overlapStart).TotalMinutes;
                }

                // Khung buổi tối (18:00 đến 23:59)
                DateTime eveningStart = date.AddHours(18);
                DateTime eveningEnd = date.AddDays(1);
                // Tính tổng số phút xe đỗ trong khung buổi tối
                if (startOfFrame < eveningEnd && endOfFrame > eveningStart)
                {
                    DateTime overlapStart = (startOfFrame > eveningStart) ? startOfFrame : eveningStart;
                    DateTime overlapEnd = (endOfFrame < eveningEnd) ? endOfFrame : eveningEnd;
                    eveningMinutes += (overlapEnd - overlapStart).TotalMinutes;
                }
            }

            // Miễn phí nếu xe đỗ dưới mức ân hạn (vd: 15 phút) VÀ chưa từng chạm vào khung giờ đêm
            if (overnightTouches == 0 && totalDuration.TotalMinutes <= PricingRules.GracePeriodMinutes)
            {
                return 0m;
            }

            decimal totalFee = 0;

            // Tính phí khung ban ngày: 15.000đ cho mỗi block 2 giờ (lẻ làm tròn lên)
            if (dayMinutes > 0)
            {
                int dayBlocks = (int)Math.Ceiling(Math.Round(dayMinutes, 2) / 120.0);
                totalFee += dayBlocks * 15000m;
            }

            // Tính phí khung buổi tối: 20.000đ cho mỗi block 2 giờ (lẻ làm tròn lên)
            if (eveningMinutes > 0)
            {
                int eveningBlocks = (int)Math.Ceiling(Math.Round(eveningMinutes, 2) / 120.0);
                totalFee += eveningBlocks * 20000m;
            }

            // Tính phí qua đêm: 100.000đ cố định cho mỗi lần chạm khung đêm
            if (overnightTouches > 0)
            {
                totalFee += overnightTouches * 100000m;
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