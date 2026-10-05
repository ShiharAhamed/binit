import 'assignment_history_model.dart';
import 'assignment_summary_model.dart';
import 'collection_assignment_status.dart';
import 'driver_json_helpers.dart';
import 'route_read_model.dart';

/// Full operational detail of a collection assignment returned by GET /api/v1/assignments/{id}.
class AssignmentDetailModel {
  final String id;
  final CollectionAssignmentStatus status;
  final String driverId;
  final String driverName;
  final String vehicleId;
  final String vehicleRegistrationNumber;
  final int stopCount;
  final int completedStopCount;
  final int failedStopCount;
  final DateTime assignedAt;
  final RouteReadModel? route;
  final List<AssignmentHistoryModel> history;

  const AssignmentDetailModel({
    required this.id,
    required this.status,
    required this.driverId,
    required this.driverName,
    required this.vehicleId,
    required this.vehicleRegistrationNumber,
    required this.stopCount,
    required this.completedStopCount,
    required this.failedStopCount,
    required this.assignedAt,
    this.route,
    this.history = const [],
  });

  int get pendingStopCount {
    final pending = stopCount - completedStopCount - failedStopCount;
    return pending < 0 ? 0 : pending;
  }

  /// Converts this detail model into an [AssignmentSummaryModel].
  AssignmentSummaryModel toSummary() => AssignmentSummaryModel(
        id: id,
        status: status,
        driverId: driverId,
        driverName: driverName,
        vehicleId: vehicleId,
        vehicleRegistrationNumber: vehicleRegistrationNumber,
        stopCount: stopCount,
        completedStopCount: completedStopCount,
        failedStopCount: failedStopCount,
        assignedAt: assignedAt,
      );

  factory AssignmentDetailModel.fromJson(Map<String, dynamic> json) {
    final rawRoute = json['route'];
    final rawHistory = json['history'];

    return AssignmentDetailModel(
      id: requiredJsonString(json, 'id'),
      status: CollectionAssignmentStatus.fromJsonValue(
        requiredJsonString(json, 'status'),
      ),
      driverId: requiredJsonString(json, 'driverId'),
      driverName: json['driverName'] as String? ?? '',
      vehicleId: requiredJsonString(json, 'vehicleId'),
      vehicleRegistrationNumber:
          json['vehicleRegistrationNumber'] as String? ?? '',
      stopCount: requiredJsonInt(json, 'stopCount'),
      completedStopCount: requiredJsonInt(json, 'completedStopCount'),
      failedStopCount: requiredJsonInt(json, 'failedStopCount'),
      assignedAt: requiredJsonDateTime(json['assignedAt'], 'assignedAt'),
      route: rawRoute is Map<String, dynamic>
          ? RouteReadModel.fromJson(rawRoute)
          : null,
      history: rawHistory is List
          ? rawHistory
              .map((h) =>
                  AssignmentHistoryModel.fromJson(h as Map<String, dynamic>))
              .toList(growable: false)
          : const <AssignmentHistoryModel>[],
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'status': status.toJsonValue(),
    'driverId': driverId,
    'driverName': driverName,
    'vehicleId': vehicleId,
    'vehicleRegistrationNumber': vehicleRegistrationNumber,
    'stopCount': stopCount,
    'completedStopCount': completedStopCount,
    'failedStopCount': failedStopCount,
    'assignedAt': assignedAt.toIso8601String(),
    if (route != null) 'route': route!.toJson(),
    'history': history.map((h) => h.toJson()).toList(),
  };
}
