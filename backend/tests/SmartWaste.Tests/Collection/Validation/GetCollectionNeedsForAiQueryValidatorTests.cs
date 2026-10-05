using FluentAssertions;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Collection.Validation;
using Xunit;

namespace SmartWaste.Tests.Collection.Validation;

public class GetCollectionNeedsForAiQueryValidatorTests
{
    private readonly GetCollectionNeedsForAiQueryValidator _validator = new();

    [Fact]
    public async Task ValidBoundedQuery_IsAccepted()
    {
        var result = await _validator.ValidateAsync(new GetCollectionNeedsForAiQuery
        {
            TargetType = "Report",
            CollectionReason = "VerifiedReport",
            TargetDate = new DateOnly(2026, 1, 5),
            Page = 1,
            PageSize = 50
        });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task OutOfBoundsPagination_IsRejected(int page, int pageSize)
    {
        var result = await _validator.ValidateAsync(new GetCollectionNeedsForAiQuery
        {
            Page = page,
            PageSize = pageSize
        });

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Citizen")]
    [InlineData("Vehicle")]
    public async Task UnsupportedTargetType_IsRejected(string targetType)
    {
        var result = await _validator.ValidateAsync(new GetCollectionNeedsForAiQuery { TargetType = targetType });

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("UnknownReason")]
    public async Task UnsupportedCollectionReason_IsRejected(string collectionReason)
    {
        var result = await _validator.ValidateAsync(new GetCollectionNeedsForAiQuery { CollectionReason = collectionReason });

        result.IsValid.Should().BeFalse();
    }
}
