namespace SmartWaste.Application.Common.Exceptions;

/// <summary>
/// Thrown when an underlying cloud object storage provider operation fails.
/// Mapped to HTTP 500 Internal Server Error without leaking provider credentials or internal diagnostics.
/// </summary>
public class StorageServiceException : Exception
{
    public StorageServiceException(string message) : base(message)
    {
    }

    public StorageServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
