namespace SmartWaste.Domain.Common;

/// <summary>
/// Standard application roles for the Smart Waste Management System.
/// </summary>
public static class AppRoles
{
    public const string Citizen = "Citizen";
    public const string WasteOfficer = "WasteOfficer";
    public const string Driver = "Driver";
    public const string MunicipalManager = "MunicipalManager";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Citizen,
        WasteOfficer,
        Driver,
        MunicipalManager
    };

    /// <summary>
    /// Comma-separated roles authorized to review, approve, request revision, reject,
    /// and execute agentic AI workflows (equal authority for MunicipalManager and WasteOfficer).
    /// </summary>
    public const string AgentWorkflowAuthorityRoles = $"{MunicipalManager},{WasteOfficer}";

    /// <summary>
    /// Evaluates whether the given role holds authoritative decision and execution rights
    /// over the Agentic AI workflow lifecycle.
    /// </summary>
    public static bool IsAgentWorkflowAuthority(string? role) =>
        !string.IsNullOrWhiteSpace(role) && (role == MunicipalManager || role == WasteOfficer);
}
