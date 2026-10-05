namespace SmartWaste.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated actor is not permitted to perform an action on a resource.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message)
    {
    }
}
