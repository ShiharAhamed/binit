import 'waste_type.dart';

/// Request DTO for updating an existing submitted waste report (PATCH /api/v1/waste-reports/{id}).
/// All fields are optional to support partial updates.
///
/// AddressText semantics:
/// - null: omitted / no change to existing address
/// - "": explicitly clears address to null in database
/// - non-empty string: updates address to the trimmed string
class UpdateWasteReportRequest {
  final String? description;
  final WasteType? wasteType;
  final double? latitude;
  final double? longitude;
  final String? addressText;

  const UpdateWasteReportRequest({
    this.description,
    this.wasteType,
    this.latitude,
    this.longitude,
    this.addressText,
  });

  /// Indicates whether all update fields are null (i.e., no modifications).
  bool get isEmpty =>
      description == null &&
      wasteType == null &&
      latitude == null &&
      longitude == null &&
      addressText == null;

  /// Indicates whether at least one field has been modified.
  bool get isNotEmpty => !isEmpty;

  /// Serializes only non-null fields to the JSON payload expected by ASP.NET Core.
  /// If [addressText] is "", it serializes as `""` to signal address clearance.
  Map<String, dynamic> toJson() {
    final map = <String, dynamic>{};
    if (description != null) {
      map['description'] = description!.trim();
    }
    if (wasteType != null) {
      map['wasteType'] = wasteType!.toJsonValue();
    }
    if (latitude != null) {
      map['latitude'] = latitude;
    }
    if (longitude != null) {
      map['longitude'] = longitude;
    }
    if (addressText != null) {
      map['addressText'] = addressText!.trim();
    }
    return map;
  }

  factory UpdateWasteReportRequest.fromJson(Map<String, dynamic> json) {
    return UpdateWasteReportRequest(
      description: json['description'] as String?,
      wasteType: json['wasteType'] != null
          ? WasteType.fromJsonValue(json['wasteType'].toString())
          : null,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      addressText: json['addressText'] as String?,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is UpdateWasteReportRequest &&
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
