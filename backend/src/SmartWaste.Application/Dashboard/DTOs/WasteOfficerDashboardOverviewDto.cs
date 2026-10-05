namespace SmartWaste.Application.Dashboard.DTOs;

public sealed record WasteOfficerDashboardOverviewDto(
    int ReportsAwaitingReview,
    int ActiveBins,
    int ScheduledCollections,
    int OpenCollectionTasks,
    int OpenComplaints);
