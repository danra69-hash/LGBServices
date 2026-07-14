namespace LGBApp.Backend.Services;

/// <summary>
/// Expected business-rule failure mapped to an HTTP status by the global exception handler.
/// </summary>
public sealed class DomainException : Exception
{
    public int StatusCode { get; }

    public DomainException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
