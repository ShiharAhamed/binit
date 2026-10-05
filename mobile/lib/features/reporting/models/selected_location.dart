/// Value object representing a geographic location selected by the Citizen.
/// Kept provider-independent so API requests and presentation logic
/// are decoupled from map or location vendor types.
class SelectedLocation {
  final double latitude;
  final double longitude;

  const SelectedLocation({
    required this.latitude,
    required this.longitude,
  });

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is SelectedLocation &&
          runtimeType == other.runtimeType &&
          latitude == other.latitude &&
          longitude == other.longitude;

  @override
  int get hashCode => Object.hash(latitude, longitude);

  @override
  String toString() => 'SelectedLocation($latitude, $longitude)';
}
