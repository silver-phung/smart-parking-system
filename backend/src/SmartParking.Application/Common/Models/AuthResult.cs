namespace SmartParking.Application.Common.Models;

public class AuthResult<T>
{
    public bool Succeeded { get; private set; }
    public T? Data { get; private set; }
    public string? Message { get; private set; }
    public int StatusCode { get; private set; }

    public static AuthResult<T> Success(T data, string? message = null, int statusCode = 200)
    {
        return new AuthResult<T>
        {
            Succeeded = true,
            Data = data,
            Message = message,
            StatusCode = statusCode
        };
    }

    public static AuthResult<T> Failure(string message, int statusCode = 400)
    {
        return new AuthResult<T>
        {
            Succeeded = false,
            Data = default,
            Message = message,
            StatusCode = statusCode
        };
    }
}
