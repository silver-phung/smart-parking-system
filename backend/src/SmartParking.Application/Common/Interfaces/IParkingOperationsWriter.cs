namespace SmartParking.Application.Common.Interfaces;

public interface IParkingOperationsWriter
{
    Task<T> ExecuteAsync<T>(int lotId, Func<Task<T>> operation, CancellationToken cancellationToken);
}
