using System.Net;

namespace SmartParking.Application.Common.Exceptions;

public sealed class AiServiceException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
