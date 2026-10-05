using FluentAssertions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Infrastructure.Workflow.Services;

namespace SmartWaste.Tests.Workflow.Services;

public class CollectionSchedulePolicyTests
{
    private static readonly CollectionSchedulePolicy Policy = new(new CollectionScheduleOptions());
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 16, 30, 0, TimeSpan.Zero); // 22:00 Colombo

    [Theory]
    [InlineData("2026-10-05T19:00:00Z", false)] // 00:30 local
    [InlineData("2026-10-05T21:00:00Z", false)] // 02:30 local
    [InlineData("2026-10-06T02:29:00Z", false)] // 07:59 local
    [InlineData("2026-10-06T02:30:00Z", true)]  // 08:00 local
    [InlineData("2026-10-06T05:00:00Z", true)]  // 10:30 local
    [InlineData("2026-10-06T10:29:00Z", true)] // 15:59 local
    [InlineData("2026-10-06T10:30:00Z", false)] // 16:00 local
    [InlineData("2026-10-06T09:00:00+05:30", true)] // same instant as 03:30 UTC
    public void ValidatesMunicipalityLocalWindowAfterOffsetConversion(string proposed, bool valid)
    {
        Action act = () => Policy.ValidateProposedStart(proposed, Now);
        if (valid)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<BusinessRuleConflictException>();
        }
    }

    [Fact]
    public void PreservesExactUtcInstantFromNonUtcOffset()
    {
        Policy.ValidateProposedStart("2026-10-06T09:00:00+05:30", Now)
            .Should().Be(new DateTime(2026, 10, 6, 3, 30, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("2026-10-06T09:00:00")]
    [InlineData("not-a-time")]
    [InlineData(null)]
    public void RejectsMissingOrAmbiguousOffset(string? proposed)
    {
        Action act = () => Policy.ValidateProposedStart(proposed, Now);
        act.Should().Throw<BusinessRuleConflictException>().WithMessage("*explicit UTC offset*");
    }

    [Fact]
    public void RejectsPlanThatBecameStaleBeforeApproval()
    {
        Action act = () => Policy.ValidateProposedStart(
            "2026-10-06T09:00:00+05:30", new DateTimeOffset(2026, 10, 6, 4, 0, 0, TimeSpan.Zero));
        act.Should().Throw<BusinessRuleConflictException>().WithMessage("*past*");
    }

    [Fact]
    public void RejectsInvalidMunicipalConfiguration()
    {
        foreach (var options in new[]
        {
            new CollectionScheduleOptions { TimeZoneId = "" },
            new CollectionScheduleOptions { TimeZoneId = "Invalid/Zone" },
            new CollectionScheduleOptions { CollectionWindowStart = "16:00", CollectionWindowEnd = "08:00" },
            new CollectionScheduleOptions { CollectionWindowStart = "08:00", CollectionWindowEnd = "08:00" },
            new CollectionScheduleOptions { CollectionWindowStart = "invalid" }
        })
        {
            CollectionSchedulePolicy.IsValidConfiguration(options).Should().BeFalse();
        }
    }
}
