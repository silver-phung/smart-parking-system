using SmartParking.Domain.Entities;

namespace SmartParking.Application.Common.Interfaces;

public interface IParkingLotRepository
{
    Task<IEnumerable<SimulatorParkingLot>> GetAllAsync();
    Task<SimulatorParkingLot?> GetByIdAsync(int id);
    Task<bool> UpdateAvailableSlotsAsync(int id, int delta);
    Task<bool> ResetToDefaultAsync();
}
