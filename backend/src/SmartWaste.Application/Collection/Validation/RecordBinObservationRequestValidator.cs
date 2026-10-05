using FluentValidation;
using SmartWaste.Application.Collection.DTOs.Requests;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for recording an append-only manual field observation for a bin.
/// </summary>
public class RecordBinObservationRequestValidator : AbstractValidator<RecordBinObservationRequest>
{
    private static readonly int[] AllowedFillLevels = { 0, 25, 50, 75, 100 };

    public RecordBinObservationRequestValidator()
    {
        RuleFor(x => x.FillLevelPercent)
            .NotNull().WithMessage("Fill level percent is required.")
            .Must(level => level.HasValue && AllowedFillLevels.Contains(level.Value))
            .WithMessage("Fill level percent must be exactly 0, 25, 50, 75, or 100.");

        RuleFor(x => x.Condition)
            .NotNull().WithMessage("Condition is required.")
            .IsInEnum().WithMessage("A valid bin condition is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
