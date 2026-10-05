using SmartWaste.Application.Dashboard.DTOs;

namespace SmartWaste.Application.Dashboard.Interfaces;

public interface IWasteOfficerDashboardService
{
    Task<WasteOfficerDashboardOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WasteOfficerNeedsAttentionItemDto>> GetNeedsAttentionAsync(CancellationToken cancellationToken = default);
}
