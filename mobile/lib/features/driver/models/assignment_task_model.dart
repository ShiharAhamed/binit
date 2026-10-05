import 'driver_json_helpers.dart';

/// Represents a collection task linked to an assignment route stop.
class AssignmentTaskModel {
  final String id;
  final String taskCode;
  final String targetType; // "Report" or "Bin"
  final String? wasteReportId;
  final String? wasteBinId;
  final String collectionReason;
  final String status;
  final DateTime scheduledAt;
  final String? addressText;
  final double? latitude;
  final double? longitude;

  const AssignmentTaskModel({
    required this.id,
    required this.taskCode,
    required this.targetType,
    this.wasteReportId,
    this.wasteBinId,
    required this.collectionReason,
    required this.status,
    required this.scheduledAt,
    this.addressText,
    this.latitude,
    this.longitude,
  });

  /// True if both latitude and longitude are non-null and within valid Earth geographic bounds.
  bool get hasValidCoordinates =>
      latitude != null &&
      longitude != null &&
      latitude! >= -90.0 &&
      latitude! <= 90.0 &&
      longitude! >= -180.0 &&
      longitude! <= 180.0;

  bool get isBinTask => targetType.toLowerCase() == 'bin' || wasteBinId != null;
  bool get isReportTask =>
      targetType.toLowerCase() == 'report' || wasteReportId != null;

  factory AssignmentTaskModel.fromJson(Map<String, dynamic> json) {
    return AssignmentTaskModel(
      id: requiredJsonString(json, 'id'),
      taskCode: json['taskCode'] as String? ?? '',
      targetType: json['targetType'] as String? ?? '',
      wasteReportId: json['wasteReportId'] as String?,
      wasteBinId: json['wasteBinId'] as String?,
      collectionReason: json['collectionReason']?.toString() ?? '',
      status: json['status']?.toString() ?? '',
      scheduledAt: requiredJsonDateTime(json['scheduledAt'], 'scheduledAt'),
      addressText: json['addressText'] as String?,
      latitude: optionalJsonNumber(json['latitude'], 'latitude'),
      longitude: optionalJsonNumber(json['longitude'], 'longitude'),
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'taskCode': taskCode,
    'targetType': targetType,
    if (wasteReportId != null) 'wasteReportId': wasteReportId,
    if (wasteBinId != null) 'wasteBinId': wasteBinId,
    'collectionReason': collectionReason,
    'status': status,
    'scheduledAt': scheduledAt.toIso8601String(),
    if (addressText != null) 'addressText': addressText,
    if (latitude != null) 'latitude': latitude,
    if (longitude != null) 'longitude': longitude,
  };
}
