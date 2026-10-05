using FluentValidation;
using SmartWaste.Application.DTOs.Users;
using SmartWaste.Domain.Common;

namespace SmartWaste.Application.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] AllowedRoles =
    {
        AppRoles.Driver,
        AppRoles.WasteOfficer,
        AppRoles.MunicipalManager
    };

    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(50).WithMessage("Username cannot exceed 50 characters.")
            .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("Username can only contain letters, numbers, hyphens, periods, and underscores.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => AllowedRoles.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", AllowedRoles)}. Citizen accounts cannot be created internally.");
    }
}
