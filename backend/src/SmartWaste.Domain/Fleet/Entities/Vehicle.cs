using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Domain.Collection.Entities;

/// <summary>
/// Municipal fleet resource. Capacity is recorded in litres as reference information only.
/// </summary>
public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RegistrationNumber { get; set; } = string.Empty;

    public VehicleType VehicleType { get; set; }

    public int CapacityLiters { get; set; }

    public VehicleOperationalStatus OperationalStatus { get; set; } = VehicleOperationalStatus.Available;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<VehicleSupportedWasteType> SupportedWasteTypes { get; set; } = new List<VehicleSupportedWasteType>();
}
