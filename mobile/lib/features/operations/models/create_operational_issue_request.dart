import 'operational_issue_model.dart';

/// Request payload for reporting an operational issue from the field (POST /api/v1/operational-issues).
/// Server-controlled attributes (driverId, status, resolution, timestamps) are omitted.
class CreateOperationalIssueRequest {
  final OperationalIssueType issueType;
  final String title;
  final String description;
  final double? latitude;
  final double? longitude;
  final String? locationDescription;

  const CreateOperationalIssueRequest({
    required this.issueType,
    required this.title,
    required this.description,
    this.latitude,
    this.longitude,
    this.locationDescription,
  });

  Map<String, dynamic> toJson() {
    return {
      'issueType': issueType.toJsonValue(),
      'title': title,
      'description': description,
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
      if (locationDescription != null && locationDescription!.trim().isNotEmpty)
        'locationDescription': locationDescription!.trim(),
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is CreateOperationalIssueRequest &&
          runtimeType == other.runtimeType &&
          issueType == other.issueType &&
          title == other.title &&
          description == other.description &&
          latitude == other.latitude &&
          longitude == other.longitude &&
          locationDescription == other.locationDescription;

  @override
  int get hashCode =>
      issueType.hashCode ^
      title.hashCode ^
      description.hashCode ^
      latitude.hashCode ^
      longitude.hashCode ^
      locationDescription.hashCode;
}
