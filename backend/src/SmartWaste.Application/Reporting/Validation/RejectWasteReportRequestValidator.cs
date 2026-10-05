using FluentValidation;
using SmartWaste.Application.Reporting.DTOs.Requests;

namespace SmartWaste.Application.Reporting.Validation;

/// <summary>
/// Validator for waste report rejection requests by Waste Officers.
/// </summary>
public class RejectWasteReportRequestValidator : AbstractValidator<RejectWasteReportRequest>
{
    public RejectWasteReportRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required.")
            .MinimumLength(5).WithMessage("Rejection reason must be at least 5 characters.")
            .MaximumLength(500).WithMessage("Rejection reason cannot exceed 500 characters.");
    }
}
