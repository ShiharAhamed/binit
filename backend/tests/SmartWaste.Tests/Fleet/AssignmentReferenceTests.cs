using System.Text.Json;
using FluentAssertions;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Fleet;

namespace SmartWaste.Tests.Collection;

public class AssignmentReferenceTests
{
    [Theory]
    [InlineData(1, "Assignment 001")]
    [InlineData(2, "Assignment 002")]
    [InlineData(42, "Assignment 042")]
    [InlineData(999, "Assignment 999")]
    [InlineData(1000, "Assignment 1000")]
    public void FormatsPersistedNumberWithoutTruncation(long number, string expected) =>
        AssignmentReference.FromNumber(number).Should().Be(expected);

    [Fact]
    public void ListAndDetailDtosSerializeNumberAndReferenceAlongsideUuid()
    {
        var id = Guid.NewGuid();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        foreach (var dto in new AssignmentSummaryDto[]
                 {
                     new AssignmentSummaryDto { Id = id, AssignmentNumber = 42 },
                     new AssignmentDetailDto { Id = id, AssignmentNumber = 42 }
                 })
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, options));
            json.RootElement.GetProperty("id").GetGuid().Should().Be(id);
            json.RootElement.GetProperty("assignmentNumber").GetInt64().Should().Be(42);
            json.RootElement.GetProperty("assignmentReference").GetString().Should().Be("Assignment 042");
        }
    }
}
