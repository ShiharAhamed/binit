import 'waste_type.dart';

/// Request DTO for creating a new waste report (POST /api/v1/waste-reports).
/// Encapsulates Citizen-submitted fields only. Server authoritatively assigns
/// citizenId, status (Submitted), priority (null), and verification metadata.
class CreateWasteReportRequest {
  final String description;
  final WasteType wasteType;
  final double latitude;
  final double longitude;
  final String? addressText;

  const CreateWasteReportRequest({
    required this.description,
    required this.wasteType,
    required this.latitude,
    required this.longitude,
    this.addressText,
  });

  /// Serializes to the exact JSON payload expected by ASP.NET Core.
  /// Enums are serialized as strings (e.g., "General", "Organic").
  Map<String, dynamic> toJson() {
    return {
      'description': description.trim(),
      'wasteType': wasteType.toJsonValue(),
      'latitude': latitude,
      'longitude': longitude,
      if (addressText != null && addressText!.trim().isNotEmpty)
        'addressText': addressText!.trim(),
    };
  }

  factory CreateWasteReportRequest.fromJson(Map<String, dynamic> json) {
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
    final lonRaw = json['longitude'];
    if (lonRaw == null || lonRaw is! num) {
      throw const FormatException("Missing or invalid required field 'longitude'");
    }

    return CreateWasteReportRequest(
      description: description,
      wasteType: wasteType,
      latitude: latRaw.toDouble(),
      longitude: lonRaw.toDouble(),
      addressText: json['addressText'] as String?,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is CreateWasteReportRequest &&
          runtimeType == other.runtimeType &&
          description == other.description &&
          wasteType == other.wasteType &&
          latitude == other.latitude &&
          longitude == other.longitude &&
          addressText == other.addressText;

  @override
  int get hashCode =>
      description.hashCode ^
      wasteType.hashCode ^
      latitude.hashCode ^
      longitude.hashCode ^
      addressText.hashCode;
}
