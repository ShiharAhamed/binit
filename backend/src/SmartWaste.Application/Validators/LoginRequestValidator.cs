using FluentValidation;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Domain.Common;

namespace SmartWaste.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Username))
            .WithMessage("Username or email is required.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("A valid email address is required.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email) && string.IsNullOrWhiteSpace(x.Username));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");

        RuleFor(x => x.ClientType)
            .Must(ct => string.IsNullOrWhiteSpace(ct) || ClientTypes.All.Contains(ct.Trim().ToLowerInvariant()))
            .WithMessage("Client type must be 'web' or 'mobile'.");
    }
}
