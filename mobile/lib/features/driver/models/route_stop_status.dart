/// Execution status of an individual route stop matching ASP.NET Core enum values.
enum RouteStopStatus {
  pending('Pending', 'Pending'),
  completed('Completed', 'Completed'),
  failed('Failed', 'Failed'),
  skipped('Skipped', 'Skipped');

  final String value;
  final String displayName;

  const RouteStopStatus(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known status.
  static RouteStopStatus fromJsonValue(String value) {
    final trimmed = value.trim();
    for (final status in RouteStopStatus.values) {
      if (status.value.toLowerCase() == trimmed.toLowerCase()) {
        return status;
      }
    }
    throw FormatException("Unknown RouteStopStatus value: '$value'");
  }

  bool get isPending => this == RouteStopStatus.pending;
  bool get isCompleted => this == RouteStopStatus.completed;
  bool get isFailed => this == RouteStopStatus.failed;
  bool get isSkipped => this == RouteStopStatus.skipped;

  /// True if the stop has been resolved to a terminal state (Completed, Failed, or Skipped).
  bool get isTerminal => isCompleted || isFailed || isSkipped;

  @override
  String toString() => value;
}
