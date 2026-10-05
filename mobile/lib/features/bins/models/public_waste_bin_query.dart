import '../../reporting/models/waste_type.dart';

/// Query parameters supported by GET /api/v1/bins/public.
class PublicWasteBinQuery {
  final double? latitude;
  final double? longitude;
  final double? radiusKm;
  final WasteType? wasteType;
  final int page;
  final int pageSize;

  const PublicWasteBinQuery({
    this.latitude,
    this.longitude,
    this.radiusKm,
    this.wasteType,
    this.page = 1,
    this.pageSize = 20,
  });

  /// Serializes exactly the public endpoint's supported query parameters.
  /// Radius searches require both coordinates, matching the backend validator.
  Map<String, dynamic> toQueryParameters() {
    if (page < 1) {
      throw ArgumentError.value(page, 'page', 'Page must be at least 1.');
    }
    if (pageSize < 1 || pageSize > 50) {
      throw ArgumentError.value(pageSize, 'pageSize', 'Page size must be between 1 and 50.');
    }
    if (latitude != null && (latitude! < -90 || latitude! > 90)) {
      throw ArgumentError.value(latitude, 'latitude', 'Latitude must be between -90 and 90.');
    }
    if (longitude != null && (longitude! < -180 || longitude! > 180)) {
      throw ArgumentError.value(longitude, 'longitude', 'Longitude must be between -180 and 180.');
    }
    if (radiusKm != null) {
      if (latitude == null || longitude == null) {
        throw ArgumentError('Latitude and longitude are required when radiusKm is provided.');
      }
      if (radiusKm! < 0.1 || radiusKm! > 50) {
        throw ArgumentError.value(radiusKm, 'radiusKm', 'Radius must be between 0.1 and 50 km.');
      }
    }

    return {
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
      if (radiusKm != null) 'radiusKm': radiusKm,
      if (wasteType != null) 'wasteType': wasteType!.toJsonValue(),
      'page': page,
      'pageSize': pageSize,
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is PublicWasteBinQuery &&
          latitude == other.latitude &&
          longitude == other.longitude &&
          radiusKm == other.radiusKm &&
          wasteType == other.wasteType &&
          page == other.page &&
          pageSize == other.pageSize;

  @override
  int get hashCode => Object.hash(latitude, longitude, radiusKm, wasteType, page, pageSize);
}
