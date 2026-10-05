using FluentAssertions;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.Validation;
using Xunit;

namespace SmartWaste.Tests.Workflow.Validation;

public class ExecuteCollectionPlanRequestValidatorTests
{
    private readonly ExecuteCollectionPlanRequestValidator _validator = new();

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void ExecuteCollectionPlanRequestValidator_ValidatesExpectedVersion(int version, bool expectedValid)
    {
        var request = new ExecuteCollectionPlanRequest { ExpectedVersion = version };
        var result = _validator.Validate(request);
        result.IsValid.Should().Be(expectedValid);
    }
}
