using System.Text.Json;

namespace SmartWaste.Application.Workflow.DTOs.Helpers;

/// <summary>
/// Safe JSON parser for jsonb database fields into native JSON response elements.
/// </summary>
public static class JsonElementHelper
{
    /// <summary>
    /// Parses a JSON string into a cloned JsonElement, or returns null if null/empty/invalid.
    /// </summary>
    public static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
