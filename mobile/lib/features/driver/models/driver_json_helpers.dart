/// Helper functions for strict and safe JSON decoding in Driver models.
String requiredJsonString(Map<String, dynamic> json, String field) {
  final value = json[field];
  if (value is! String || value.trim().isEmpty) {
    throw FormatException("Missing or invalid required field '$field'");
  }
  return value;
}

int requiredJsonInt(Map<String, dynamic> json, String field) {
  final value = json[field];
  if (value is! num || value.toInt() != value) {
    throw FormatException("Missing or invalid required field '$field'");
  }
  return value.toInt();
}

double? optionalJsonNumber(dynamic value, String field) {
  if (value == null) return null;
  if (value is! num) throw FormatException("Invalid optional field '$field'");
  return value.toDouble();
}

DateTime requiredJsonDateTime(dynamic value, String field) {
  if (value == null || value.toString().trim().isEmpty) {
    throw FormatException("Missing or invalid required DateTime field '$field'");
  }
  final parsed = DateTime.tryParse(value.toString());
  if (parsed == null) {
    throw FormatException("Invalid DateTime format for '$field': '$value'");
  }
  return parsed.toUtc();
}

DateTime? optionalJsonDateTime(dynamic value, String field) {
  if (value == null || value.toString().trim().isEmpty) return null;
  final parsed = DateTime.tryParse(value.toString());
  if (parsed == null) {
    throw FormatException("Invalid DateTime format for '$field': '$value'");
  }
  return parsed.toUtc();
}
