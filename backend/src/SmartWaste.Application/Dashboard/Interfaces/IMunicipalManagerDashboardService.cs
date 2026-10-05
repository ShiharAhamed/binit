using SmartWaste.Application.Dashboard.DTOs;

namespace SmartWaste.Application.Dashboard.Interfaces;

public interface IMunicipalManagerDashboardService
{
    Task<MunicipalManagerDashboardOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WasteOfficerNeedsAttentionItemDto>> GetNeedsAttentionAsync(CancellationToken cancellationToken = default);
}
