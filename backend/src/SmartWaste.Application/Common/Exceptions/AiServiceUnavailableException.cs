namespace SmartWaste.Application.Common.Exceptions;

/// <summary>
/// Thrown when the internal FastAPI AI service is unreachable, timed out, or returned an error response.
/// </summary>
public class AiServiceUnavailableException : Exception
{
    public AiServiceUnavailableException(string message)
        : base(message)
    {
    }

    public AiServiceUnavailableException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
