import 'complaint_model.dart';

/// Request payload for creating a new citizen complaint (POST /api/v1/complaints).
/// Server-controlled attributes (citizenId, status, resolution, timestamps) are omitted.
class CreateComplaintRequest {
  final ComplaintCategory category;
  final String subject;
  final String description;
  final double? latitude;
  final double? longitude;
  final String? locationDescription;

  const CreateComplaintRequest({
    required this.category,
    required this.subject,
    required this.description,
    this.latitude,
    this.longitude,
    this.locationDescription,
  });

  Map<String, dynamic> toJson() {
    return {
      'category': category.toJsonValue(),
      'subject': subject,
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
      other is CreateComplaintRequest &&
          runtimeType == other.runtimeType &&
          category == other.category &&
          subject == other.subject &&
          description == other.description &&
          latitude == other.latitude &&
          longitude == other.longitude &&
          locationDescription == other.locationDescription;

  @override
  int get hashCode =>
      category.hashCode ^
      subject.hashCode ^
      description.hashCode ^
      latitude.hashCode ^
      longitude.hashCode ^
      locationDescription.hashCode;
}
