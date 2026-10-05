using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Fleet.Queries;

public class DriverListQuery
{
    public string? Search { get; set; }
    public DriverAvailabilityStatus? AvailabilityStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class VehicleListQuery
{
    public string? Search { get; set; }
    public VehicleOperationalStatus? OperationalStatus { get; set; }
    public VehicleType? VehicleType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
