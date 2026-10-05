namespace SmartWaste.Application.Dashboard.DTOs;

public sealed record MunicipalManagerDashboardOverviewDto(
    int AiWorkflowsAwaitingApproval,
    int ActiveCollectionAssignments,
    int AvailableVehicles,
    int OpenOperationalIncidents,
    int UnresolvedComplaints);
