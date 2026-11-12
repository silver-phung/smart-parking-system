// Domain/Constants/PricingRules.cs
namespace Domain.Constants
{
    public static class PricingRules
    {
        // Phí tháng
        public const decimal Monthly_Seat4_Basement = 1250000m;
        public const decimal Monthly_Seat4_Surface = 1000000m;
        public const decimal Monthly_Seat5To6 = 1500000m;
        public const decimal Monthly_Seat9 = 1750000m;

        // Phí lượt (2 giờ đầu)
        public const decimal Turn_Daytime = 15000m;    // 06:00 - 17:59
        public const decimal Turn_Evening = 20000m;    // 18:00 - 23:59
        public const decimal Turn_Overnight = 100000m; // 00:00 - 05:59 (Chọn mức 100k làm chuẩn)
        
        // Cấu hình thời gian
        public const int GracePeriodMinutes = 15;
        public const int FirstBlockHours = 2; // Tính phí theo lượt cho 2 giờ đầu
    }
}