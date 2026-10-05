using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Fleet.DTOs.Requests;

public class CreateVehicleRequest
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public VehicleType? VehicleType { get; set; }
    public int? CapacityLiters { get; set; }
    public IReadOnlyList<WasteType> SupportedWasteTypes { get; set; } = Array.Empty<WasteType>();
    public string? Notes { get; set; }
}

public class UpdateVehicleRequest : CreateVehicleRequest { }

public class UpdateVehicleOperationalStatusRequest
{
    public VehicleOperationalStatus? OperationalStatus { get; set; }
}
