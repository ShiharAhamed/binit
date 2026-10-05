import 'collection_assignment_status.dart';
import 'driver_json_helpers.dart';

/// Summary view of an assignment returned in lists and dashboard views.
class AssignmentSummaryModel {
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

  const AssignmentSummaryModel({
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
  });

  int get pendingStopCount {
    final pending = stopCount - completedStopCount - failedStopCount;
    return pending < 0 ? 0 : pending;
  }

  factory AssignmentSummaryModel.fromJson(Map<String, dynamic> json) {
    return AssignmentSummaryModel(
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
  };

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AssignmentSummaryModel &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          status == other.status &&
          driverId == other.driverId &&
          vehicleId == other.vehicleId &&
          stopCount == other.stopCount &&
          completedStopCount == other.completedStopCount &&
          failedStopCount == other.failedStopCount &&
          assignedAt == other.assignedAt;

  @override
  int get hashCode =>
      id.hashCode ^
      status.hashCode ^
      driverId.hashCode ^
      vehicleId.hashCode ^
      stopCount.hashCode ^
      completedStopCount.hashCode ^
      failedStopCount.hashCode ^
      assignedAt.hashCode;
}
