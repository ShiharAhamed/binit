import '../../reporting/models/waste_type.dart';
import 'public_bin_availability.dart';

/// Public bin list item returned by GET /api/v1/bins/public.
class PublicWasteBinListItemModel {
  final String id;
  final String binCode;
  final double latitude;
  final double longitude;
  final String? addressText;
  final int capacityLiters;
  final List<WasteType> acceptedWasteTypes;
  final PublicBinAvailability publicAvailability;
  final DateTime? lastObservedAt;
  final double? distanceMeters;

  const PublicWasteBinListItemModel({
    required this.id,
    required this.binCode,
    required this.latitude,
    required this.longitude,
    this.addressText,
    required this.capacityLiters,
    required this.acceptedWasteTypes,
    required this.publicAvailability,
    this.lastObservedAt,
    this.distanceMeters,
  });

  factory PublicWasteBinListItemModel.fromJson(Map<String, dynamic> json) {
    return PublicWasteBinListItemModel(
      id: requiredJsonString(json, 'id'),
      binCode: requiredJsonString(json, 'binCode'),
      latitude: requiredJsonNumber(json, 'latitude'),
      longitude: requiredJsonNumber(json, 'longitude'),
      addressText: json['addressText'] as String?,
      capacityLiters: requiredJsonInt(json, 'capacityLiters'),
      acceptedWasteTypes: parseWasteTypes(json['acceptedWasteTypes']),
      publicAvailability: PublicBinAvailability.fromJsonValue(requiredJsonString(json, 'publicAvailability')),
      lastObservedAt: optionalJsonDateTime(json['lastObservedAt'], 'lastObservedAt'),
      distanceMeters: optionalJsonNumber(json['distanceMeters'], 'distanceMeters'),
    );
  }
}

String requiredJsonString(Map<String, dynamic> json, String field) {
  final value = json[field];
  if (value is! String || value.trim().isEmpty) {
    throw FormatException("Missing or invalid required field '$field'");
  }
  return value;
}

double requiredJsonNumber(Map<String, dynamic> json, String field) {
  final value = json[field];
  if (value is! num) {
    throw FormatException("Missing or invalid required field '$field'");
  }
  return value.toDouble();
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

DateTime? optionalJsonDateTime(dynamic value, String field) {
  if (value == null || value.toString().trim().isEmpty) return null;
  final parsed = DateTime.tryParse(value.toString());
  if (parsed == null) throw FormatException("Invalid DateTime format for '$field': '$value'");
  return parsed.toUtc();
}

List<WasteType> parseWasteTypes(dynamic value) {
  if (value is! List) {
    throw const FormatException("Missing or invalid required field 'acceptedWasteTypes'");
  }
  return value.map((item) => WasteType.fromJsonValue(item.toString())).toList(growable: false);
}
