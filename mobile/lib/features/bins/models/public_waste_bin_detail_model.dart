import '../../reporting/models/waste_type.dart';
import 'public_bin_availability.dart';
import 'public_waste_bin_list_item_model.dart';

/// Public bin detail returned by GET /api/v1/bins/public/{id}.
class PublicWasteBinDetailModel {
  final String id;
  final String binCode;
  final double latitude;
  final double longitude;
  final String? addressText;
  final int capacityLiters;
  final List<WasteType> acceptedWasteTypes;
  final PublicBinAvailability publicAvailability;
  final DateTime? lastObservedAt;
  final bool isCollectionScheduled;

  const PublicWasteBinDetailModel({
    required this.id,
    required this.binCode,
    required this.latitude,
    required this.longitude,
    this.addressText,
    required this.capacityLiters,
    required this.acceptedWasteTypes,
    required this.publicAvailability,
    this.lastObservedAt,
    required this.isCollectionScheduled,
  });

  factory PublicWasteBinDetailModel.fromJson(Map<String, dynamic> json) {
    final scheduled = json['isCollectionScheduled'];
    if (scheduled is! bool) {
      throw const FormatException("Missing or invalid required field 'isCollectionScheduled'");
    }
    return PublicWasteBinDetailModel(
      id: requiredJsonString(json, 'id'),
      binCode: requiredJsonString(json, 'binCode'),
      latitude: requiredJsonNumber(json, 'latitude'),
      longitude: requiredJsonNumber(json, 'longitude'),
      addressText: json['addressText'] as String?,
      capacityLiters: requiredJsonInt(json, 'capacityLiters'),
      acceptedWasteTypes: parseWasteTypes(json['acceptedWasteTypes']),
      publicAvailability: PublicBinAvailability.fromJsonValue(requiredJsonString(json, 'publicAvailability')),
      lastObservedAt: optionalJsonDateTime(json['lastObservedAt'], 'lastObservedAt'),
      isCollectionScheduled: scheduled,
    );
  }
}
