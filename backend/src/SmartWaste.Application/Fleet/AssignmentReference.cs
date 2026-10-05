using System.Globalization;

namespace SmartWaste.Application.Fleet;

/// <summary>Display-only assignment reference; the UUID remains authoritative.</summary>
public static class AssignmentReference
{
    public static string FromNumber(long number) => $"Assignment {number.ToString("D3", CultureInfo.InvariantCulture)}";
}
