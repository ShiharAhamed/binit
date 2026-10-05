/// Waste categories recognized by the SmartWaste system.
/// Matches ASP.NET Core WasteType enum string representations exactly.
enum WasteType {
  general('General', 'General Waste'),
  organic('Organic', 'Organic Waste'),
  recyclable('Recyclable', 'Recyclable Waste'),
  hazardous('Hazardous', 'Hazardous Waste'),
  bulky('Bulky', 'Bulky Waste'),
  other('Other', 'Other Waste');

  final String value;
  final String displayName;

  const WasteType(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known WasteType.
  static WasteType fromJsonValue(String value) {
    final trimmed = value.trim();
    for (final type in WasteType.values) {
      if (type.value.toLowerCase() == trimmed.toLowerCase()) {
        return type;
      }
    }
    throw FormatException("Unknown WasteType value: '$value'");
  }

  @override
  String toString() => value;
}
