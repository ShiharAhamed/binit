import 'waste_report_status.dart';

/// Status transition audit record for a waste report.
/// Mirrors ASP.NET Core WasteReportStatusHistoryDto response contract.
class WasteReportStatusHistoryModel {
  final String id;
  final String wasteReportId;
  final WasteReportStatus? fromStatus;
  final WasteReportStatus toStatus;
  final String? changedByUserId;
  final String? changedByUserName;
  final String? notes;
  final DateTime changedAt;

  const WasteReportStatusHistoryModel({
    required this.id,
    required this.wasteReportId,
    this.fromStatus,
    required this.toStatus,
    this.changedByUserId,
    this.changedByUserName,
    this.notes,
    required this.changedAt,
  });

  factory WasteReportStatusHistoryModel.fromJson(Map<String, dynamic> json) {
    final id = json['id'];
    if (id == null || id is! String || id.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'id'");
    }
    final wasteReportId = json['wasteReportId'];
    if (wasteReportId == null || wasteReportId is! String || wasteReportId.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'wasteReportId'");
    }
    final toStatusRaw = json['toStatus'];
    if (toStatusRaw == null) {
      throw const FormatException("Missing required field 'toStatus'");
    }
    final toStatus = WasteReportStatus.fromJsonValue(toStatusRaw.toString());

    final fromStatusRaw = json['fromStatus'];
    final WasteReportStatus? fromStatus;
    if (fromStatusRaw != null && fromStatusRaw.toString().trim().isNotEmpty) {
      fromStatus = WasteReportStatus.fromJsonValue(fromStatusRaw.toString());
    } else {
      fromStatus = null;
    }

    final changedAtRaw = json['changedAt'];
    if (changedAtRaw == null) {
      throw const FormatException("Missing required field 'changedAt'");
    }
    final changedAt = DateTime.tryParse(changedAtRaw.toString());
    if (changedAt == null) {
      throw FormatException("Invalid DateTime format for 'changedAt': '$changedAtRaw'");
    }

    return WasteReportStatusHistoryModel(
      id: id,
      wasteReportId: wasteReportId,
      fromStatus: fromStatus,
      toStatus: toStatus,
      changedByUserId: json['changedByUserId'] as String?,
      changedByUserName: json['changedByUserName'] as String?,
      notes: json['notes'] as String?,
      changedAt: changedAt.toUtc(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'wasteReportId': wasteReportId,
      if (fromStatus != null) 'fromStatus': fromStatus!.toJsonValue(),
      'toStatus': toStatus.toJsonValue(),
      if (changedByUserId != null) 'changedByUserId': changedByUserId,
      if (changedByUserName != null) 'changedByUserName': changedByUserName,
      if (notes != null) 'notes': notes,
      'changedAt': changedAt.toIso8601String(),
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is WasteReportStatusHistoryModel &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          wasteReportId == other.wasteReportId &&
          fromStatus == other.fromStatus &&
          toStatus == other.toStatus &&
          changedByUserId == other.changedByUserId &&
          changedByUserName == other.changedByUserName &&
          notes == other.notes &&
          changedAt == other.changedAt;

  @override
  int get hashCode =>
      id.hashCode ^
      wasteReportId.hashCode ^
      fromStatus.hashCode ^
      toStatus.hashCode ^
      changedByUserId.hashCode ^
      changedByUserName.hashCode ^
      notes.hashCode ^
      changedAt.hashCode;
}
