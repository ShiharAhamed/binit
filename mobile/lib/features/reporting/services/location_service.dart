import 'dart:async';
import 'package:geolocator/geolocator.dart';
import '../models/selected_location.dart';

/// Reasons for location acquisition failure.
enum LocationFailureReason {
  serviceDisabled,
  permissionDenied,
  permissionDeniedForever,
  timeout,
  unavailable,
  unknown,
}

/// Sealed result returned by [LocationService.getCurrentLocation].
sealed class LocationResult {
  const LocationResult();
}

/// Successful location acquisition.
class LocationSuccess extends LocationResult {
  final SelectedLocation location;
  const LocationSuccess(this.location);
}

/// Failed location acquisition with user-friendly message and failure reason.
class LocationFailure extends LocationResult {
  final LocationFailureReason reason;
  final String message;

  const LocationFailure({
    required this.reason,
    required this.message,
  });
}

/// Abstract contract for location capabilities required by SmartWaste Reporting.
/// Encapsulates platform location services and permissions so presentation
/// layers remain decoupled and testable with mock/fake implementations.
abstract class LocationService {
  /// Checks whether device-level location services (GPS) are enabled.
  Future<bool> isLocationServiceEnabled();

  /// Checks current foreground location permission status.
  Future<LocationPermission> checkPermission();

  /// Requests foreground location permission from the Citizen.
  Future<LocationPermission> requestPermission();

  /// Retrieves the current device position.
  Future<SelectedLocation> getCurrentPosition();

  /// High-level orchestration that checks services, permissions, and fetches position.
  /// Returns a [LocationSuccess] or a safe, citizen-friendly [LocationFailure].
  Future<LocationResult> getCurrentLocation();

  /// Opens application settings so Citizen can manually grant permanently denied permissions.
  Future<bool> openAppSettings();

  /// Opens device location settings so Citizen can turn on location services.
  Future<bool> openLocationSettings();
}

/// Production implementation of [LocationService] backed by the `geolocator` plugin.
class GeolocatorLocationService implements LocationService {
  final Duration timeout;
  final LocationAccuracy accuracy;

  const GeolocatorLocationService({
    this.timeout = const Duration(seconds: 15),
    this.accuracy = LocationAccuracy.high,
  });

  @override
  Future<bool> isLocationServiceEnabled() async {
    try {
      return await Geolocator.isLocationServiceEnabled();
    } catch (_) {
      return false;
    }
  }

  @override
  Future<LocationPermission> checkPermission() async {
    try {
      return await Geolocator.checkPermission();
    } catch (_) {
      return LocationPermission.denied;
    }
  }

  @override
  Future<LocationPermission> requestPermission() async {
    try {
      return await Geolocator.requestPermission();
    } catch (_) {
      return LocationPermission.denied;
    }
  }

  @override
  Future<SelectedLocation> getCurrentPosition() async {
    final position = await Geolocator.getCurrentPosition(
      locationSettings: LocationSettings(
        accuracy: accuracy,
        timeLimit: timeout,
      ),
    );
    return SelectedLocation(
      latitude: position.latitude,
      longitude: position.longitude,
    );
  }

  @override
  Future<LocationResult> getCurrentLocation() async {
    // 1. Check if location services are enabled
    final isEnabled = await isLocationServiceEnabled();
    if (!isEnabled) {
      return const LocationFailure(
        reason: LocationFailureReason.serviceDisabled,
        message: 'Location services are turned off. Please enable location on your device.',
      );
    }

    // 2. Check & request permission
    var permission = await checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await requestPermission();
      if (permission == LocationPermission.denied) {
        return const LocationFailure(
          reason: LocationFailureReason.permissionDenied,
          message: 'Location permission is needed to use your current location.',
        );
      }
    }

    if (permission == LocationPermission.deniedForever) {
      return const LocationFailure(
        reason: LocationFailureReason.permissionDeniedForever,
        message: 'Location permission is permanently denied. Please enable it in app settings.',
      );
    }

    // 3. Fetch position with safe error handling
    try {
      final location = await getCurrentPosition();
      return LocationSuccess(location);
    } on TimeoutException {
      return const LocationFailure(
        reason: LocationFailureReason.timeout,
        message: 'Location request timed out. Please try again or choose on map.',
      );
    } on LocationServiceDisabledException {
      return const LocationFailure(
        reason: LocationFailureReason.serviceDisabled,
        message: 'Location services are turned off. Please enable location on your device.',
      );
    } on PermissionDeniedException {
      return const LocationFailure(
        reason: LocationFailureReason.permissionDenied,
        message: 'Location permission was denied.',
      );
    } catch (_) {
      return const LocationFailure(
        reason: LocationFailureReason.unavailable,
        message: 'Unable to determine your current location. Please choose on map.',
      );
    }
  }

  @override
  Future<bool> openAppSettings() async {
    try {
      return await Geolocator.openAppSettings();
    } catch (_) {
      return false;
    }
  }

  @override
  Future<bool> openLocationSettings() async {
    try {
      return await Geolocator.openLocationSettings();
    } catch (_) {
      return false;
    }
  }
}
