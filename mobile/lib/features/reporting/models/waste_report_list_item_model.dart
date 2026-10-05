import 'waste_report_priority.dart';
import 'waste_report_status.dart';
import 'waste_type.dart';

/// Lightweight summary model for a waste report in list views.
/// Mirrors ASP.NET Core WasteReportSummaryDto response contract.
class WasteReportListItemModel {
  final String id;
  final String? reportReference;
  final String description;
  final WasteType wasteType;
  final WasteReportStatus status;
  final WasteReportPriority? priority;
  final String? addressText;
  final double latitude;
  final double longitude;
  final String? citizenId;
  final String? citizenName;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final int attachmentCount;

  const WasteReportListItemModel({
    required this.id,
    this.reportReference,
    required this.description,
    required this.wasteType,
    required this.status,
    this.priority,
    this.addressText,
    required this.latitude,
    required this.longitude,
    this.citizenId,
    this.citizenName,
    required this.createdAt,
    this.updatedAt,
    this.attachmentCount = 0,
  });

  factory WasteReportListItemModel.fromJson(Map<String, dynamic> json) {
    final id = json['id'];
    if (id == null || id is! String || id.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'id'");
    }

    final description = json['description'];
    if (description == null || description is! String || description.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'description'");
    }

    final wasteTypeRaw = json['wasteType'];
    if (wasteTypeRaw == null) {
      throw const FormatException("Missing required field 'wasteType'");
    }
    final wasteType = WasteType.fromJsonValue(wasteTypeRaw.toString());

    final statusRaw = json['status'];
    if (statusRaw == null) {
      throw const FormatException("Missing required field 'status'");
    }
    final status = WasteReportStatus.fromJsonValue(statusRaw.toString());

    final priority = WasteReportPriority.fromJsonValue(json['priority']);

    final latRaw = json['latitude'];
    if (latRaw == null || latRaw is! num) {
      throw const FormatException("Missing or invalid required field 'latitude'");
    }
    final latitude = latRaw.toDouble();

    final lonRaw = json['longitude'];
    if (lonRaw == null || lonRaw is! num) {
      throw const FormatException("Missing or invalid required field 'longitude'");
    }
    final longitude = lonRaw.toDouble();

    final createdAtRaw = json['createdAt'];
    if (createdAtRaw == null) {
      throw const FormatException("Missing required field 'createdAt'");
    }
    final createdAt = DateTime.tryParse(createdAtRaw.toString());
    if (createdAt == null) {
      throw FormatException("Invalid DateTime format for 'createdAt': '$createdAtRaw'");
    }

    final updatedAtRaw = json['updatedAt'];
    final DateTime? updatedAt;
    if (updatedAtRaw != null && updatedAtRaw.toString().trim().isNotEmpty) {
      final parsed = DateTime.tryParse(updatedAtRaw.toString());
      if (parsed == null) {
        throw FormatException("Invalid DateTime format for 'updatedAt': '$updatedAtRaw'");
      }
      updatedAt = parsed.toUtc();
    } else {
      updatedAt = null;
    }

    final attachmentCountRaw = json['attachmentCount'];
    final int attachmentCount;
    if (attachmentCountRaw is int) {
      attachmentCount = attachmentCountRaw;
    } else if (json['attachments'] is List) {
      attachmentCount = (json['attachments'] as List).length;
    } else {
      attachmentCount = 0;
    }

    return WasteReportListItemModel(
      id: id,
      reportReference: json['reportReference'] as String?,
      description: description,
      wasteType: wasteType,
      status: status,
      priority: priority,
      addressText: json['addressText'] as String?,
      latitude: latitude,
      longitude: longitude,
      citizenId: json['citizenId'] as String?,
      citizenName: json['citizenName'] as String?,
      createdAt: createdAt.toUtc(),
      updatedAt: updatedAt,
      attachmentCount: attachmentCount,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      if (reportReference != null) 'reportReference': reportReference,
      'description': description,
      'wasteType': wasteType.toJsonValue(),
      'status': status.toJsonValue(),
      if (priority != null) 'priority': priority!.toJsonValue(),
      if (addressText != null) 'addressText': addressText,
      'latitude': latitude,
      'longitude': longitude,
      if (citizenId != null) 'citizenId': citizenId,
      if (citizenName != null) 'citizenName': citizenName,
      'createdAt': createdAt.toIso8601String(),
      if (updatedAt != null) 'updatedAt': updatedAt!.toIso8601String(),
      if (attachmentCount > 0) 'attachmentCount': attachmentCount,
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is WasteReportListItemModel &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          reportReference == other.reportReference &&
          description == other.description &&
          wasteType == other.wasteType &&
          status == other.status &&
          priority == other.priority &&
          addressText == other.addressText &&
          latitude == other.latitude &&
          longitude == other.longitude &&
          createdAt == other.createdAt &&
          attachmentCount == other.attachmentCount;

  @override
  int get hashCode =>
      id.hashCode ^
      reportReference.hashCode ^
      description.hashCode ^
      wasteType.hashCode ^
      status.hashCode ^
      priority.hashCode ^
      latitude.hashCode ^
      longitude.hashCode ^
      createdAt.hashCode ^
      attachmentCount.hashCode;
}
