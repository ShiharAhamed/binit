import 'report_attachment_model.dart';
import 'waste_report_priority.dart';
import 'waste_report_status.dart';
import 'waste_type.dart';

/// Full detail model for a specific waste report.
/// Mirrors ASP.NET Core WasteReportDetailDto response contract.
class WasteReportDetailModel {
  final String id;
  final String? reportReference;
  final String citizenId;
  final String citizenName;
  final String description;
  final WasteType wasteType;
  final double latitude;
  final double longitude;
  final String? addressText;
  final WasteReportStatus status;
  final WasteReportPriority? priority;
  final String? verifiedByUserId;
  final String? verifiedByUserName;
  final DateTime? verifiedAt;
  final List<ReportAttachmentModel> attachments;
  final DateTime createdAt;
  final DateTime? updatedAt;

  const WasteReportDetailModel({
    required this.id,
    this.reportReference,
    required this.citizenId,
    required this.citizenName,
    required this.description,
    required this.wasteType,
    required this.latitude,
    required this.longitude,
    this.addressText,
    required this.status,
    this.priority,
    this.verifiedByUserId,
    this.verifiedByUserName,
    this.verifiedAt,
    this.attachments = const [],
    required this.createdAt,
    this.updatedAt,
  });

  factory WasteReportDetailModel.fromJson(Map<String, dynamic> json) {
    final id = json['id'];
    if (id == null || id is! String || id.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'id'");
    }
    final citizenId = json['citizenId'];
    if (citizenId == null || citizenId is! String || citizenId.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'citizenId'");
    }
    final citizenName = json['citizenName'];
    if (citizenName == null || citizenName is! String) {
      throw const FormatException("Missing or invalid required field 'citizenName'");
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

    final statusRaw = json['status'];
    if (statusRaw == null) {
      throw const FormatException("Missing required field 'status'");
    }
    final status = WasteReportStatus.fromJsonValue(statusRaw.toString());

    final priority = WasteReportPriority.fromJsonValue(json['priority']);

    final createdAtRaw = json['createdAt'];
    if (createdAtRaw == null) {
      throw const FormatException("Missing required field 'createdAt'");
    }
    final createdAt = DateTime.tryParse(createdAtRaw.toString());
    if (createdAt == null) {
      throw FormatException("Invalid DateTime format for 'createdAt': '$createdAtRaw'");
    }

    final verifiedAtRaw = json['verifiedAt'];
    final DateTime? verifiedAt;
    if (verifiedAtRaw != null && verifiedAtRaw.toString().trim().isNotEmpty) {
      final parsed = DateTime.tryParse(verifiedAtRaw.toString());
      if (parsed == null) {
        throw FormatException("Invalid DateTime format for 'verifiedAt': '$verifiedAtRaw'");
      }
      verifiedAt = parsed.toUtc();
    } else {
      verifiedAt = null;
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

    final attachmentsRaw = json['attachments'];
    final List<ReportAttachmentModel> attachments;
    if (attachmentsRaw == null) {
      attachments = const [];
    } else if (attachmentsRaw is List) {
      attachments = attachmentsRaw.map((item) {
        if (item is! Map<String, dynamic>) {
          throw const FormatException("Invalid attachment item in list");
        }
        return ReportAttachmentModel.fromJson(item);
      }).toList();
    } else {
      throw const FormatException("Field 'attachments' must be a list");
    }

    return WasteReportDetailModel(
      id: id,
      reportReference: json['reportReference'] as String?,
      citizenId: citizenId,
      citizenName: citizenName,
      description: description,
      wasteType: wasteType,
      latitude: latitude,
      longitude: longitude,
      addressText: json['addressText'] as String?,
      status: status,
      priority: priority,
      verifiedByUserId: json['verifiedByUserId'] as String?,
      verifiedByUserName: json['verifiedByUserName'] as String?,
      verifiedAt: verifiedAt,
      attachments: attachments,
      createdAt: createdAt.toUtc(),
      updatedAt: updatedAt,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      if (reportReference != null) 'reportReference': reportReference,
      'citizenId': citizenId,
      'citizenName': citizenName,
      'description': description,
      'wasteType': wasteType.toJsonValue(),
      'latitude': latitude,
      'longitude': longitude,
      if (addressText != null) 'addressText': addressText,
      'status': status.toJsonValue(),
      if (priority != null) 'priority': priority!.toJsonValue(),
      if (verifiedByUserId != null) 'verifiedByUserId': verifiedByUserId,
      if (verifiedByUserName != null) 'verifiedByUserName': verifiedByUserName,
      if (verifiedAt != null) 'verifiedAt': verifiedAt!.toIso8601String(),
      'attachments': attachments.map((a) => a.toJson()).toList(),
      'createdAt': createdAt.toIso8601String(),
      if (updatedAt != null) 'updatedAt': updatedAt!.toIso8601String(),
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is WasteReportDetailModel &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          reportReference == other.reportReference &&
          citizenId == other.citizenId &&
          description == other.description &&
          wasteType == other.wasteType &&
          latitude == other.latitude &&
          longitude == other.longitude &&
          status == other.status &&
          priority == other.priority;

  @override
  int get hashCode =>
      id.hashCode ^
      reportReference.hashCode ^
      citizenId.hashCode ^
      description.hashCode ^
      wasteType.hashCode ^
      latitude.hashCode ^
      longitude.hashCode ^
      status.hashCode ^
      priority.hashCode;
}
