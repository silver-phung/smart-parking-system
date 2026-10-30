// Application/Interfaces/IBillingEngine.cs
using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces
{
    public interface IBillingEngine
    {
        decimal CalculateFee(ParkingSession session);
        decimal CalculateMonthlyFee(VehicleType vehicleType, ParkingZone zone);
    }
}