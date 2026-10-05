using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Fleet.DTOs.Requests;

public class UpdateDriverAvailabilityRequest
{
    public DriverAvailabilityStatus? AvailabilityStatus { get; set; }
}
