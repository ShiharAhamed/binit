namespace SmartWaste.Domain.Common;

/// <summary>
/// Supported client platforms for role-aware login validation.
/// </summary>
public static class ClientTypes
{
    public const string Web = "web";
    public const string Mobile = "mobile";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Web,
        Mobile
    };
}
