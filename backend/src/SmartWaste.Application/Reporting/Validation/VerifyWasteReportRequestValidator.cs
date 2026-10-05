using FluentValidation; using SmartWaste.Application.Reporting.DTOs.Requests;
namespace SmartWaste.Application.Reporting.Validation;
public class VerifyWasteReportRequestValidator : AbstractValidator<VerifyWasteReportRequest> { public VerifyWasteReportRequestValidator() { RuleFor(x => x.Priority).NotNull().IsInEnum(); } }
