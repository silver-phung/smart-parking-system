using SmartParking.Domain.Entities;

namespace SmartParking.Application.Common.Interfaces;

public interface IParkingLotRepository
{
    Task<IEnumerable<ParkingLot>> GetAllAsync();
    Task<ParkingLot?> GetByIdAsync(int id);
    Task<bool> UpdateAvailableSlotsAsync(int id, int delta);
    Task<bool> ResetToDefaultAsync();
}
