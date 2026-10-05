using FluentAssertions;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Collection.Validation;
using SmartWaste.Domain.Collection.Enums;
using Xunit;

namespace SmartWaste.Tests.Collection.Validation;

public class BinObservationValidationTests
{
    private readonly RecordBinObservationRequestValidator _recordValidator = new();
    private readonly ObservationListQueryValidator _queryValidator = new();

    #region RecordBinObservationRequest Tests

    [Theory]
    [InlineData(0, BinCondition.Good)]
    [InlineData(25, BinCondition.Good)]
    [InlineData(50, BinCondition.Damaged)]
    [InlineData(75, BinCondition.Blocked)]
    [InlineData(100, BinCondition.Missing)]
    public void RecordBinObservationRequest_ValidDiscreteFillLevels_ShouldPassValidation(int fillLevel, BinCondition condition)
    {
        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = fillLevel,
            Condition = condition,
            Notes = "Routine inspection."
        };

        var result = _recordValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(85)]
    [InlineData(-25)]
    [InlineData(125)]
    public void RecordBinObservationRequest_NonPermittedFillLevels_ShouldFailValidation(int fillLevel)
    {
        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = fillLevel,
            Condition = BinCondition.Good
        };

        var result = _recordValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("0, 25, 50, 75, or 100", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RecordBinObservationRequest_NullFillLevel_ShouldFailValidation()
    {
        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = null,
            Condition = BinCondition.Good
        };

        var result = _recordValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Fill level percent is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RecordBinObservationRequest_NotesExceeding500Chars_ShouldFailValidation()
    {
        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 50,
            Condition = BinCondition.Good,
            Notes = new string('A', 501)
        };

        var result = _recordValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 500 characters", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region ObservationListQuery Tests

    [Fact]
    public void ObservationListQuery_ValidQuery_ShouldPassValidation()
    {
        var query = new ObservationListQuery
        {
            Page = 1,
            PageSize = 20
        };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ObservationListQuery_InvalidPageAndSize_ShouldFailValidation()
    {
        var query = new ObservationListQuery
        {
            Page = 0,
            PageSize = 101
        };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Page must be greater than or equal to 1", StringComparison.OrdinalIgnoreCase));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("between 1 and 100", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
