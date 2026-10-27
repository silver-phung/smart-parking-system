using System.Collections.Concurrent;

namespace SmartParking.Infrastructure.Common;

public static class ParkingLotLockHelper
{
    public const long LotLockBase = 700_000_000L;
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> Semaphores = new();

    public static SemaphoreSlim GetSemaphore(long lotId)
    {
        return Semaphores.GetOrAdd(lotId, _ => new SemaphoreSlim(1, 1));
    }
}
