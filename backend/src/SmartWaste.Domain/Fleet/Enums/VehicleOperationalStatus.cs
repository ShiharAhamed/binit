namespace SmartWaste.Domain.Collection.Enums;

/// <summary>
/// Staff-managed operational state of a municipal vehicle.
/// Assignment occupancy is derived separately from future assignments.
/// </summary>
public enum VehicleOperationalStatus
{
    Available,
    Maintenance,
    Inactive
}
