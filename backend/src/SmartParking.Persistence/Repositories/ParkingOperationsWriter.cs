using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.Operations;
using SmartParking.Persistence.Data;

namespace SmartParking.Persistence.Repositories;

public sealed class ParkingOperationsWriter(IApplicationDbContext db) : IParkingOperationsWriter
{
    public async Task<T> ExecuteAsync<T>(int lotId, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var gate = ParkingLotLockHelper.GetSemaphore(lotId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await db.BeginTransactionAsync(cancellationToken);
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({ParkingLotLockHelper.LotLockBase + lotId})", cancellationToken);

            try
            {
                var result = await operation();
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ParkingOperationException(409, "STALE_DATA", "Dữ liệu vừa thay đổi. Tải lại trước khi thao tác.");
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ParkingOperationException(409, "DUPLICATE", "Vé, sự kiện, biển số hoặc ô vừa được sử dụng. Tải lại để đối chiếu.");
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
