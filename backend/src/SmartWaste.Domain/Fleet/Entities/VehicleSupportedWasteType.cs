using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Domain.Collection.Entities;

/// <summary>
/// Structured vehicle compatibility data using the shared C2 waste-type enum.
/// </summary>
public class VehicleSupportedWasteType
{
    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public WasteType WasteType { get; set; }
}
