namespace SmartWaste.Application.Common.Exceptions;

/// <summary>
/// Thrown when an operation is rejected due to the resource's current business state.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class BusinessRuleConflictException : Exception
{
    public BusinessRuleConflictException(string message)
        : base(message)
    {
    }

    public BusinessRuleConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
