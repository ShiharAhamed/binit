import 'driver_json_helpers.dart';
import 'route_stop_model.dart';

/// Full route entity returned with an assignment or via GET /api/v1/routes/{id}.
class RouteReadModel {
  final String id;
  final String collectionAssignmentId;
  final String routingMethod;
  final String? routeGeometry;
  final double? estimatedDistanceMeters;
  final double? estimatedDurationSeconds;
  final List<RouteStopModel> stops;

  const RouteReadModel({
    required this.id,
    required this.collectionAssignmentId,
    required this.routingMethod,
    this.routeGeometry,
    this.estimatedDistanceMeters,
    this.estimatedDurationSeconds,
    required this.stops,
  });

  /// All stops ordered strictly by sequence ascending.
  List<RouteStopModel> get orderedStops =>
      List<RouteStopModel>.from(stops)..sort((a, b) => a.sequence.compareTo(b.sequence));

  /// Filtered stops that have valid map coordinates for OpenStreetMap rendering.
  List<RouteStopModel> get mappableStops =>
      orderedStops.where((s) => s.hasValidCoordinates).toList(growable: false);

  factory RouteReadModel.fromJson(Map<String, dynamic> json) {
    final rawStops = json['stops'];
    final stopsList = rawStops is List
        ? rawStops
            .map((s) => RouteStopModel.fromJson(s as Map<String, dynamic>))
            .toList(growable: false)
        : const <RouteStopModel>[];

    return RouteReadModel(
      id: requiredJsonString(json, 'id'),
      collectionAssignmentId:
          requiredJsonString(json, 'collectionAssignmentId'),
      routingMethod: json['routingMethod']?.toString() ?? 'ManualOrder',
      routeGeometry: json['routeGeometry'] as String?,
      estimatedDistanceMeters:
          optionalJsonNumber(json['estimatedDistanceMeters'], 'estimatedDistanceMeters'),
      estimatedDurationSeconds:
          optionalJsonNumber(json['estimatedDurationSeconds'], 'estimatedDurationSeconds'),
      stops: stopsList,
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'collectionAssignmentId': collectionAssignmentId,
    'routingMethod': routingMethod,
    if (routeGeometry != null) 'routeGeometry': routeGeometry,
    if (estimatedDistanceMeters != null)
      'estimatedDistanceMeters': estimatedDistanceMeters,
    if (estimatedDurationSeconds != null)
      'estimatedDurationSeconds': estimatedDurationSeconds,
    'stops': stops.map((s) => s.toJson()).toList(),
  };
}
