/// Operational status of a collection assignment matching ASP.NET Core enum values.
enum CollectionAssignmentStatus {
  assigned('Assigned', 'Assigned'),
  inProgress('InProgress', 'In Progress'),
  completed('Completed', 'Completed'),
  partiallyCompleted('PartiallyCompleted', 'Partially Completed'),
  failed('Failed', 'Failed'),
  cancelled('Cancelled', 'Cancelled');

  final String value;
  final String displayName;

  const CollectionAssignmentStatus(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known status.
  static CollectionAssignmentStatus fromJsonValue(String value) {
    final trimmed = value.trim();
    for (final status in CollectionAssignmentStatus.values) {
      if (status.value.toLowerCase() == trimmed.toLowerCase()) {
        return status;
      }
    }
    throw FormatException("Unknown CollectionAssignmentStatus value: '$value'");
  }

  bool get isAssigned => this == CollectionAssignmentStatus.assigned;
  bool get isInProgress => this == CollectionAssignmentStatus.inProgress;
  bool get isCompleted => this == CollectionAssignmentStatus.completed;
  bool get isPartiallyCompleted => this == CollectionAssignmentStatus.partiallyCompleted;
  bool get isFailed => this == CollectionAssignmentStatus.failed;
  bool get isCancelled => this == CollectionAssignmentStatus.cancelled;

  /// True if the assignment is actively running or awaiting start.
  bool get isUnfinished => isAssigned || isInProgress;

  /// True if the assignment has reached a terminal state.
  bool get isTerminal => isCompleted || isPartiallyCompleted || isFailed || isCancelled;

  @override
  String toString() => value;
}
