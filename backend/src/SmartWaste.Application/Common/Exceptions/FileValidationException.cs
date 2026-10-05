namespace SmartWaste.Application.Common.Exceptions;

/// <summary>
/// Thrown when an uploaded file violates validation constraints (e.g. empty file,
/// file size exceeds maximum limit, unsupported file type, or invalid magic bytes).
/// Mapped to HTTP 400 Bad Request.
/// </summary>
public class FileValidationException : Exception
{
    public FileValidationException(string message) : base(message)
    {
    }

    public FileValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
