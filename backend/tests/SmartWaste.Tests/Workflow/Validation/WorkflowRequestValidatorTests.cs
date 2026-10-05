using FluentAssertions;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.Validation;
using Xunit;

namespace SmartWaste.Tests.Workflow.Validation;

public class WorkflowRequestValidatorTests
{
    private readonly CreateAgentWorkflowRequestValidator _createValidator = new();
    private readonly AgentWorkflowListQueryValidator _queryValidator = new();

    [Theory]
    [InlineData("Plan urgent waste collection in District 5")]
    [InlineData("Valid 5")]
    [InlineData("Objective with exactly five")]
    public void CreateValidator_ValidObjective_PassesValidation(string objective)
    {
        var request = new CreateAgentWorkflowRequest { Objective = objective };
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Four")]
    [InlineData("1234")]
    public void CreateValidator_TooShortOrEmpty_FailsValidation(string objective)
    {
        var request = new CreateAgentWorkflowRequest { Objective = objective };
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateValidator_TooLongObjective_FailsValidation()
    {
        var request = new CreateAgentWorkflowRequest { Objective = new string('A', 1001) };
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAgentWorkflowRequest.Objective));
    }

    [Theory]
    [InlineData(1, 20, null)]
    [InlineData(2, 50, "Planning")]
    [InlineData(1, 1, "Completed")]
    [InlineData(5, 10, "Created")]
    [InlineData(1, 25, "collectionneedsrevision")]
    public void QueryValidator_ValidQuery_PassesValidation(int page, int pageSize, string? status)
    {
        var query = new AgentWorkflowListQuery
        {
            Page = page,
            PageSize = pageSize,
            Status = status
        };
        var result = _queryValidator.Validate(query);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(-1, 20, null)]
    public void QueryValidator_InvalidPage_FailsValidation(int page, int pageSize, string? status)
    {
        var query = new AgentWorkflowListQuery
        {
            Page = page,
            PageSize = pageSize,
            Status = status
        };
        var result = _queryValidator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AgentWorkflowListQuery.Page));
    }

    [Theory]
    [InlineData(1, 20, "NonExistentStatus")]
    [InlineData(1, 20, "Invalid123")]
    [InlineData(1, 20, "RandomText")]
    public void QueryValidator_InvalidStatus_FailsValidation(int page, int pageSize, string? status)
    {
        var query = new AgentWorkflowListQuery
        {
            Page = page,
            PageSize = pageSize,
            Status = status
        };
        var result = _queryValidator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AgentWorkflowListQuery.Status));
    }
}
