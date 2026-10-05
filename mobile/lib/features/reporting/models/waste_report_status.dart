/// Lifecycle status of a waste report.
/// Matches ASP.NET Core WasteReportStatus enum string representations exactly.
enum WasteReportStatus {
  submitted('Submitted', 'Submitted'),
  underReview('UnderReview', 'Under Review'),
  verified('Verified', 'Verified'),
  rejected('Rejected', 'Rejected'),
  scheduled('Scheduled', 'Scheduled'),
  inProgress('InProgress', 'In Progress'),
  resolved('Resolved', 'Resolved'),
  cancelled('Cancelled', 'Cancelled');

  final String value;
  final String displayName;

  const WasteReportStatus(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known WasteReportStatus.
  static WasteReportStatus fromJsonValue(String value) {
    final trimmed = value.trim();
    for (final status in WasteReportStatus.values) {
      if (status.value.toLowerCase() == trimmed.toLowerCase()) {
        return status;
      }
    }
    throw FormatException("Unknown WasteReportStatus value: '$value'");
  }

  bool get isSubmitted => this == WasteReportStatus.submitted;
  bool get isUnderReview => this == WasteReportStatus.underReview;
  bool get isVerified => this == WasteReportStatus.verified;
  bool get isRejected => this == WasteReportStatus.rejected;
  bool get isScheduled => this == WasteReportStatus.scheduled;
  bool get isInProgress => this == WasteReportStatus.inProgress;
  bool get isResolved => this == WasteReportStatus.resolved;
  bool get isCancelled => this == WasteReportStatus.cancelled;

  @override
  String toString() => value;
}
