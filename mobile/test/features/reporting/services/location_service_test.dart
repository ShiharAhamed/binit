import 'dart:async';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:mobile/features/reporting/models/selected_location.dart';
import 'package:mobile/features/reporting/services/location_service.dart';
// ignore: depend_on_referenced_packages
import 'package:plugin_platform_interface/plugin_platform_interface.dart';

class FakeGeolocatorPlatform extends GeolocatorPlatform with MockPlatformInterfaceMixin {
  bool serviceEnabled = true;
  LocationPermission permission = LocationPermission.whileInUse;
  LocationPermission requestPermissionResult = LocationPermission.whileInUse;
  Position? positionToReturn;
  Exception? exceptionToThrow;
  bool appSettingsResult = true;
  bool locationSettingsResult = true;

  @override
  Future<bool> isLocationServiceEnabled() async => serviceEnabled;

  @override
  Future<LocationPermission> checkPermission() async => permission;

  @override
  Future<LocationPermission> requestPermission() async => requestPermissionResult;

  @override
  Future<Position> getCurrentPosition({LocationSettings? locationSettings}) async {
    if (exceptionToThrow != null) {
      throw exceptionToThrow!;
    }
    return positionToReturn ??
        Position(
          latitude: 6.9271,
          longitude: 79.8612,
          timestamp: DateTime.now(),
          accuracy: 5.0,
          altitude: 10.0,
          altitudeAccuracy: 1.0,
          heading: 0.0,
          headingAccuracy: 1.0,
          speed: 0.0,
          speedAccuracy: 0.0,
        );
  }

  @override
  Future<bool> openAppSettings() async => appSettingsResult;

  @override
  Future<bool> openLocationSettings() async => locationSettingsResult;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late FakeGeolocatorPlatform fakePlatform;
  late GeolocatorLocationService service;

  setUp(() {
    fakePlatform = FakeGeolocatorPlatform();
    GeolocatorPlatform.instance = fakePlatform;
    service = const GeolocatorLocationService();
  });

  group('SelectedLocation Model Tests', () {
    test('SelectedLocation value equality and hashCode', () {
      const loc1 = SelectedLocation(latitude: 6.9271, longitude: 79.8612);
      const loc2 = SelectedLocation(latitude: 6.9271, longitude: 79.8612);
      const loc3 = SelectedLocation(latitude: 6.9000, longitude: 79.8000);

      expect(loc1, equals(loc2));
      expect(loc1.hashCode, equals(loc2.hashCode));
      expect(loc1, isNot(equals(loc3)));
      expect(loc1.toString(), contains('SelectedLocation(6.9271, 79.8612)'));
    });
  });

  group('GeolocatorLocationService Permission & Flow Tests', () {
    test('returns LocationSuccess when permission is already granted', () async {
      fakePlatform.serviceEnabled = true;
      fakePlatform.permission = LocationPermission.whileInUse;
      fakePlatform.positionToReturn = Position(
        latitude: 6.9271,
        longitude: 79.8612,
        timestamp: DateTime.now(),
        accuracy: 5.0,
        altitude: 0.0,
        altitudeAccuracy: 0.0,
        heading: 0.0,
        headingAccuracy: 0.0,
        speed: 0.0,
        speedAccuracy: 0.0,
      );

      final result = await service.getCurrentLocation();

      expect(result, isA<LocationSuccess>());
      final success = result as LocationSuccess;
      expect(success.location.latitude, equals(6.9271));
      expect(success.location.longitude, equals(79.8612));
    });

    test('returns LocationSuccess when permission is denied initially but granted on request', () async {
      fakePlatform.serviceEnabled = true;
      fakePlatform.permission = LocationPermission.denied;
      fakePlatform.requestPermissionResult = LocationPermission.whileInUse;

      final result = await service.getCurrentLocation();

      expect(result, isA<LocationSuccess>());
      final success = result as LocationSuccess;
      expect(success.location.latitude, equals(6.9271));
    });

    test('returns LocationFailure (serviceDisabled) when device location is turned off', () async {
      fakePlatform.serviceEnabled = false;

      final result = await service.getCurrentLocation();

      expect(result, isA<LocationFailure>());
      final failure = result as LocationFailure;
      expect(failure.reason, equals(LocationFailureReason.serviceDisabled));
      expect(failure.message, contains('Location services are turned off'));
    });

    test('returns LocationFailure (permissionDenied) when user denies permission request', () async {
      fakePlatform.serviceEnabled = true;
      fakePlatform.permission = LocationPermission.denied;
      fakePlatform.requestPermissionResult = LocationPermission.denied;

      final result = await service.getCurrentLocation();

      expect(result, isA<LocationFailure>());
      final failure = result as LocationFailure;
      expect(failure.reason, equals(LocationFailureReason.permissionDenied));
      expect(failure.message, contains('Location permission is needed'));
    });

    test('returns LocationFailure (permissionDeniedForever) when permission is permanently denied', () async {
      fakePlatform.serviceEnabled = true;
      fakePlatform.permission = LocationPermission.deniedForever;

      final result = await service.getCurrentLocation();

      expect(result, isA<LocationFailure>());
      final failure = result as LocationFailure;
      expect(failure.reason, equals(LocationFailureReason.permissionDeniedForever));
      expect(failure.message, contains('permanently denied'));
    });

    test('returns LocationFailure (timeout) when position request times out', () async {
      fakePlatform.serviceEnabled = true;
      fakePlatform.permission = LocationPermission.always;
      fakePlatform.exceptionToThrow = TimeoutException('Location timed out');

      final result = await service.getCurrentLocation();

      expect(result, isA<LocationFailure>());
      final failure = result as LocationFailure;
      expect(failure.reason, equals(LocationFailureReason.timeout));
      expect(failure.message, contains('timed out'));
    });

    test('returns LocationFailure (unavailable) on unexpected platform exception', () async {
      fakePlatform.serviceEnabled = true;
      fakePlatform.permission = LocationPermission.always;
      fakePlatform.exceptionToThrow = Exception('GPS hardware error');

      final result = await service.getCurrentLocation();

      expect(result, isA<LocationFailure>());
      final failure = result as LocationFailure;
      expect(failure.reason, equals(LocationFailureReason.unavailable));
      expect(failure.message, contains('Unable to determine your current location'));
    });

    test('delegates openAppSettings and openLocationSettings to platform', () async {
      expect(await service.openAppSettings(), isTrue);
      expect(await service.openLocationSettings(), isTrue);
    });
  });
}
