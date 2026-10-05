import 'driver_availability_status.dart';
import 'driver_json_helpers.dart';

/// Driver self profile model returned by GET /api/v1/drivers/{id}
/// and PATCH /api/v1/drivers/me/availability.
class DriverSelfModel {
  final String id;
  final String displayName;
  final DriverAvailabilityStatus availabilityStatus;
  final bool isOccupied;

  const DriverSelfModel({
    required this.id,
    required this.displayName,
    required this.availabilityStatus,
    required this.isOccupied,
  });

  factory DriverSelfModel.fromJson(Map<String, dynamic> json) {
    final occupied = json['isOccupied'];
    if (occupied is! bool) {
      throw const FormatException("Missing or invalid required field 'isOccupied'");
    }
    return DriverSelfModel(
      id: requiredJsonString(json, 'id'),
      displayName: requiredJsonString(json, 'displayName'),
      availabilityStatus: DriverAvailabilityStatus.fromJsonValue(
        requiredJsonString(json, 'availabilityStatus'),
      ),
      isOccupied: occupied,
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'displayName': displayName,
    'availabilityStatus': availabilityStatus.toJsonValue(),
    'isOccupied': isOccupied,
  };

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is DriverSelfModel &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          displayName == other.displayName &&
          availabilityStatus == other.availabilityStatus &&
          isOccupied == other.isOccupied;

  @override
  int get hashCode =>
      id.hashCode ^
      displayName.hashCode ^
      availabilityStatus.hashCode ^
      isOccupied.hashCode;
}
