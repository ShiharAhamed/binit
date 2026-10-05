using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Collection.Queries;

/// <summary>
/// Query filter and pagination parameters for public citizen roadside bin discovery (GET /api/v1/bins/public).
/// </summary>
public class PublicWasteBinQuery
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? RadiusKm { get; set; }
    public WasteType? WasteType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
