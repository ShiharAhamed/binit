using System.Text.Json;
using FluentAssertions;
using SmartWaste.Application.Reporting;
using SmartWaste.Application.Reporting.DTOs.Responses;
using Xunit;

namespace SmartWaste.Tests.Reporting;

public class WasteReportReferenceTests
{
    [Fact]
    public void FromId_UsesFirstEightUppercaseHexCharactersWithoutChangingIdentity()
    {
        var id = Guid.Parse("c17add4f-79f3-4a0a-a9e0-150fde7d7827");
        WasteReportReference.FromId(id).Should().Be("C17ADD4F");

        var json = JsonSerializer.Serialize(new VerifiedWasteReportToolItemDto { Id = id },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("id").GetGuid().Should().Be(id);
        parsed.RootElement.GetProperty("reportReference").GetString().Should().Be("C17ADD4F");
    }
}
