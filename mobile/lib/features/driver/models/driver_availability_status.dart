/// Driver availability duty status matching ASP.NET Core enum string representations.
enum DriverAvailabilityStatus {
  available('Available', 'Available'),
  offDuty('OffDuty', 'Off Duty');

  final String value;
  final String displayName;

  const DriverAvailabilityStatus(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known status.
  static DriverAvailabilityStatus fromJsonValue(String value) {
    final trimmed = value.trim();
    for (final status in DriverAvailabilityStatus.values) {
      if (status.value.toLowerCase() == trimmed.toLowerCase()) {
        return status;
      }
    }
    throw FormatException("Unknown DriverAvailabilityStatus value: '$value'");
  }

  bool get isAvailable => this == DriverAvailabilityStatus.available;
  bool get isOffDuty => this == DriverAvailabilityStatus.offDuty;

  @override
  String toString() => value;
}
