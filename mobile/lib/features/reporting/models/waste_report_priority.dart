/// Priority level of a waste report.
/// Matches ASP.NET Core WasteReportPriority enum string representations exactly.
///
/// Priority is nullable:
/// - Initial Component 1 submission, review, and verification do NOT assign priority.
/// - Priority may be assigned later by an authoritative backend operational or
///   planning workflow (e.g. collection scheduling or dispatch).
enum WasteReportPriority {
  low('Low', 'Low'),
  medium('Medium', 'Medium'),
  high('High', 'High'),
  urgent('Urgent', 'Urgent');

  final String value;
  final String displayName;

  const WasteReportPriority(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from nullable string representation returned by ASP.NET Core.
  /// Returns null if [value] is null or empty/blank.
  /// Throws [FormatException] if [value] is an unknown non-null string.
  static WasteReportPriority? fromJsonValue(dynamic value) {
    if (value == null) return null;
    final str = value.toString().trim();
    if (str.isEmpty) return null;

    for (final priority in WasteReportPriority.values) {
      if (priority.value.toLowerCase() == str.toLowerCase()) {
        return priority;
      }
    }
    throw FormatException("Unknown WasteReportPriority value: '$value'");
  }

  @override
  String toString() => value;
}
