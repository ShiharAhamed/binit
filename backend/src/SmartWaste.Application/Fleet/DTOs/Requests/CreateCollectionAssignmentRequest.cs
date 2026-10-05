namespace SmartWaste.Application.Collection.DTOs.Requests;
public sealed class CreateCollectionAssignmentRequest { public Guid DriverId { get; set; } public Guid VehicleId { get; set; } public IReadOnlyList<Guid> CollectionTaskIds { get; set; } = Array.Empty<Guid>(); public IReadOnlyList<CreateRouteStopRequest> Stops { get; set; } = Array.Empty<CreateRouteStopRequest>(); public string? CompatibilityAcknowledgement { get; set; } }
public sealed class CreateRouteStopRequest { public Guid CollectionTaskId { get; set; } public int Sequence { get; set; } }
