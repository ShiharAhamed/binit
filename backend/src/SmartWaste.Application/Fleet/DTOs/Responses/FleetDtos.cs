using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Fleet.DTOs.Responses;

public class DriverSummaryDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DriverAvailabilityStatus AvailabilityStatus { get; set; }
    public bool IsOccupied { get; set; }
}

public class DriverDetailDto : DriverSummaryDto { }

public class DriverSelfDto : DriverSummaryDto { }

public class VehicleSummaryDto
{
    public Guid Id { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public VehicleOperationalStatus OperationalStatus { get; set; }
    public IReadOnlyList<string> SupportedWasteTypes { get; set; } = Array.Empty<string>();
    public bool IsOccupied { get; set; }
}

public class VehicleDetailDto : VehicleSummaryDto
{
    public int CapacityLiters { get; set; }
    public string? Notes { get; set; }
    public Guid? CurrentAssignmentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
