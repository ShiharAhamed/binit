import 'driver_json_helpers.dart';
import 'route_stop_status.dart';

/// Audit trail history record for a route stop status transition.
class RouteStopHistoryModel {
  final String id;
  final RouteStopStatus? fromStatus;
  final RouteStopStatus toStatus;
  final String? changedByUserId;
  final DateTime changedAt;
  final String? notes;

  const RouteStopHistoryModel({
    required this.id,
    this.fromStatus,
    required this.toStatus,
    this.changedByUserId,
    required this.changedAt,
    this.notes,
  });

  factory RouteStopHistoryModel.fromJson(Map<String, dynamic> json) {
    return RouteStopHistoryModel(
      id: requiredJsonString(json, 'id'),
      fromStatus: json['fromStatus'] != null
          ? RouteStopStatus.fromJsonValue(json['fromStatus'].toString())
          : null,
      toStatus: RouteStopStatus.fromJsonValue(
        requiredJsonString(json, 'toStatus'),
      ),
      changedByUserId: json['changedByUserId'] as String?,
      changedAt: requiredJsonDateTime(json['changedAt'], 'changedAt'),
      notes: json['notes'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    if (fromStatus != null) 'fromStatus': fromStatus!.toJsonValue(),
    'toStatus': toStatus.toJsonValue(),
    if (changedByUserId != null) 'changedByUserId': changedByUserId,
    'changedAt': changedAt.toIso8601String(),
    if (notes != null) 'notes': notes,
  };
}
