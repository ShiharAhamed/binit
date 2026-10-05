namespace SmartWaste.Application.Common.Exceptions;

public class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string email)
        : base($"A user with email '{email}' already exists.")
    {
    }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message = "Invalid email or password.")
        : base(message)
    {
    }
}

public class AccountInactiveException : Exception
{
    public AccountInactiveException(string message = "Account is inactive. Please contact municipal support.")
        : base(message)
    {
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}

public class IdentityOperationException : Exception
{
    public IEnumerable<string> Errors { get; }

    public IdentityOperationException(IEnumerable<string> errors)
        : base("One or more Identity errors occurred.")
    {
        Errors = errors;
    }
}

public class UnsupportedClientRoleException : Exception
{
    public string ErrorCode { get; } = "unsupported_client_role";

    public UnsupportedClientRoleException(string message)
        : base(message)
    {
    }
}

public class PasswordChangeRequiredException : Exception
{
    public PasswordChangeRequiredException(string message = "Password change required before accessing this resource.")
        : base(message)
    {
    }
}

public class InvalidRoleException : Exception
{
    public InvalidRoleException(string message)
        : base(message)
    {
    }
}

public class UserManagementException : Exception
{
    public UserManagementException(string message)
        : base(message)
    {
    }
}
