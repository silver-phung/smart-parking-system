using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Application.Services;

public class ParkingLotService
{
    private readonly IParkingLotRepository _repository;

    public ParkingLotService(IParkingLotRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<SimulatorParkingLot>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<SimulatorParkingLot?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<bool> UpdateAvailableSlotsAsync(int id, int delta)
    {
        return await _repository.UpdateAvailableSlotsAsync(id, delta);
    }

    public async Task<bool> ResetToDefaultAsync()
    {
        return await _repository.ResetToDefaultAsync();
    }
}
