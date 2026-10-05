/// Backend-authoritative public operational availability for a roadside bin.
/// Flutter intentionally does not derive this from an observation fill level.
enum PublicBinAvailability {
  usable('Usable'),
  warning('Warning'),
  full('Full'),
  unavailable('Unavailable'),
  unknown('Unknown');

  final String value;

  const PublicBinAvailability(this.value);

  static PublicBinAvailability fromJsonValue(String value) {
    for (final availability in values) {
      if (availability.value.toLowerCase() == value.trim().toLowerCase()) {
        return availability;
      }
    }
    throw FormatException("Unknown public bin availability value: '$value'");
  }

  @override
  String toString() => value;
}
