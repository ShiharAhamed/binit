import 'collection_assignment_status.dart';
import 'driver_json_helpers.dart';

/// Audit trail history record for a collection assignment status transition.
class AssignmentHistoryModel {
  final String id;
  final CollectionAssignmentStatus? fromStatus;
  final CollectionAssignmentStatus toStatus;
  final String? changedByUserId;
  final DateTime changedAt;
  final String? notes;

  const AssignmentHistoryModel({
    required this.id,
    this.fromStatus,
    required this.toStatus,
    this.changedByUserId,
    required this.changedAt,
    this.notes,
  });

  factory AssignmentHistoryModel.fromJson(Map<String, dynamic> json) {
    return AssignmentHistoryModel(
      id: requiredJsonString(json, 'id'),
      fromStatus: json['fromStatus'] != null
          ? CollectionAssignmentStatus.fromJsonValue(
              json['fromStatus'].toString(),
            )
          : null,
      toStatus: CollectionAssignmentStatus.fromJsonValue(
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
