namespace SmartWaste.Application.Collection.DTOs.Requests;
public sealed class ReorderRouteStopsRequest { public IReadOnlyList<RouteStopSequenceRequest> Stops { get; set; } = Array.Empty<RouteStopSequenceRequest>(); }
public sealed class RouteStopSequenceRequest { public Guid RouteStopId { get; set; } public int Sequence { get; set; } }
