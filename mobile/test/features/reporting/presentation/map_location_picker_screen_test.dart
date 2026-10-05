import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/reporting/models/selected_location.dart';
import 'package:mobile/features/reporting/presentation/map_location_picker_screen.dart';
import 'package:mobile/features/reporting/services/location_service.dart';

final Uint8List _kTransparentImage = Uint8List.fromList(const [
  0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
  0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
  0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
  0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
  0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
  0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
]);

class FakeTestTileProvider extends TileProvider {
  @override
  ImageProvider getImage(TileCoordinates coordinates, TileLayer options) {
    return MemoryImage(_kTransparentImage);
  }
}

class FakeLocationService implements LocationService {
  bool serviceEnabled;
  LocationPermission permission;
  SelectedLocation? positionToReturn;
  LocationResult? currentLocationResult;
  bool appSettingsOpened = false;
  bool locationSettingsOpened = false;
  int getCurrentLocationCallCount = 0;

  FakeLocationService({
    this.serviceEnabled = true,
    this.permission = LocationPermission.whileInUse,
    this.positionToReturn,
    this.currentLocationResult,
  });

  @override
  Future<bool> isLocationServiceEnabled() async => serviceEnabled;

  @override
  Future<LocationPermission> checkPermission() async => permission;

  @override
  Future<LocationPermission> requestPermission() async => permission;

  @override
  Future<SelectedLocation> getCurrentPosition() async =>
      positionToReturn ?? const SelectedLocation(latitude: 6.9271, longitude: 79.8612);

  @override
  Future<LocationResult> getCurrentLocation() async {
    getCurrentLocationCallCount++;
    if (currentLocationResult != null) {
      return currentLocationResult!;
    }
    if (!serviceEnabled) {
      return const LocationFailure(
        reason: LocationFailureReason.serviceDisabled,
        message: 'Location services are turned off. Please enable location on your device.',
      );
    }
    if (permission == LocationPermission.denied) {
      return const LocationFailure(
        reason: LocationFailureReason.permissionDenied,
        message: 'Location permission is needed to use your current location.',
      );
    }
    if (permission == LocationPermission.deniedForever) {
      return const LocationFailure(
        reason: LocationFailureReason.permissionDeniedForever,
        message: 'Location permission is permanently denied. Please enable it in app settings.',
      );
    }
    return LocationSuccess(
      positionToReturn ?? const SelectedLocation(latitude: 6.9271, longitude: 79.8612),
    );
  }

  @override
  Future<bool> openAppSettings() async {
    appSettingsOpened = true;
    return true;
  }

  @override
  Future<bool> openLocationSettings() async {
    locationSettingsOpened = true;
    return true;
  }
}

Widget createMapPickerTestApp({
  double? initialLatitude,
  double? initialLongitude,
  LocationService? locationService,
}) {
  return MaterialApp(
    theme: AppTheme.lightTheme,
    home: MapLocationPickerScreen(
      initialLatitude: initialLatitude,
      initialLongitude: initialLongitude,
      locationService: locationService,
      tileProvider: FakeTestTileProvider(),
    ),
  );
}

void main() {
  group('MapLocationPickerScreen Rendering & Initial State Tests', () {
    testWidgets('renders AppBar, map, attribution, and empty selection panel', (tester) async {
      await tester.pumpWidget(createMapPickerTestApp());
      await tester.pumpAndSettle();

      // AppBar
      expect(find.text('Select Waste Location'), findsOneWidget);
      expect(find.byKey(const Key('map_picker_back_button')), findsOneWidget);

      // Attribution
      expect(find.text('OpenStreetMap contributors'), findsOneWidget);
      expect(find.textContaining('©'), findsOneWidget);

      // Map widget
      expect(find.byType(FlutterMap), findsOneWidget);

      // Empty selection state
      expect(find.byKey(const Key('map_no_selection_text')), findsOneWidget);
      expect(find.text('Tap Map to Select Location'), findsOneWidget);
      expect(find.byKey(const Key('selected_location_marker')), findsNothing);

      // Recenter button
      expect(find.byKey(const Key('map_recenter_button')), findsOneWidget);

      // Use This Location button is disabled
      final confirmBtn = find.byKey(const Key('use_this_location_button'));
      expect(confirmBtn, findsOneWidget);
      final appBtn = tester.widget<ElevatedButton>(find.descendant(
        of: confirmBtn,
        matching: find.byType(ElevatedButton),
      ));
      expect(appBtn.onPressed, isNull);
    });

    testWidgets('renders existing selected coordinates and marker when provided initially', (tester) async {
      await tester.pumpWidget(createMapPickerTestApp(
        initialLatitude: 6.9300,
        initialLongitude: 79.8500,
      ));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('selected_location_marker')), findsOneWidget);
      expect(find.byKey(const Key('map_selected_coordinates_text')), findsOneWidget);
      expect(find.text('6.93000, 79.85000'), findsOneWidget);
      expect(find.text('Selected Location'), findsOneWidget);

      // Button is enabled
      final confirmBtn = find.byKey(const Key('use_this_location_button'));
      final appBtn = tester.widget<ElevatedButton>(find.descendant(
        of: confirmBtn,
        matching: find.byType(ElevatedButton),
      ));
      expect(appBtn.onPressed, isNotNull);
    });
  });

  group('Map Tapping & Selection Tests', () {
    testWidgets('tapping on the map places marker and enables confirmation button', (tester) async {
      await tester.pumpWidget(createMapPickerTestApp());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('selected_location_marker')), findsNothing);

      // Tap near center of the map
      final mapFinder = find.byType(FlutterMap);
      await tester.tap(mapFinder);
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      // Marker appears
      expect(find.byKey(const Key('selected_location_marker')), findsOneWidget);
      expect(find.byKey(const Key('map_selected_coordinates_text')), findsOneWidget);

      // Confirmation button is now enabled
      final confirmBtn = find.byKey(const Key('use_this_location_button'));
      final appBtn = tester.widget<ElevatedButton>(find.descendant(
        of: confirmBtn,
        matching: find.byType(ElevatedButton),
      ));
      expect(appBtn.onPressed, isNotNull);
    });
  });

  group('Map Return Value & Navigation Tests', () {
    testWidgets('confirming selection returns SelectedLocation to caller', (tester) async {
      SelectedLocation? returnedLocation;

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Builder(
            builder: (context) {
              return Scaffold(
                body: ElevatedButton(
                  onPressed: () async {
                    returnedLocation = await Navigator.of(context).push<SelectedLocation>(
                      MaterialPageRoute(
                        builder: (_) => MapLocationPickerScreen(
                          initialLatitude: 6.9200,
                          initialLongitude: 79.8600,
                          tileProvider: FakeTestTileProvider(),
                        ),
                      ),
                    );
                  },
                  child: const Text('Open Map'),
                ),
              );
            },
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Open Map
      await tester.tap(find.text('Open Map'));
      await tester.pumpAndSettle();

      // Confirm initial selection
      final confirmBtn = find.byKey(const Key('use_this_location_button'));
      await tester.tap(confirmBtn);
      await tester.pumpAndSettle();

      // Returns to caller with SelectedLocation
      expect(find.text('Open Map'), findsOneWidget);
      expect(returnedLocation, isNotNull);
      expect(returnedLocation!.latitude, equals(6.9200));
      expect(returnedLocation!.longitude, equals(79.8600));
    });

    testWidgets('tapping back button returns null without setting location', (tester) async {
      SelectedLocation? returnedLocation;
      bool returned = false;

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Builder(
            builder: (context) {
              return Scaffold(
                body: ElevatedButton(
                  onPressed: () async {
                    returnedLocation = await Navigator.of(context).push<SelectedLocation>(
                      MaterialPageRoute(
                        builder: (_) => MapLocationPickerScreen(
                          initialLatitude: 6.9200,
                          initialLongitude: 79.8600,
                          tileProvider: FakeTestTileProvider(),
                        ),
                      ),
                    );
                    returned = true;
                  },
                  child: const Text('Open Map'),
                ),
              );
            },
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Open Map'));
      await tester.pumpAndSettle();

      // Tap back button
      await tester.tap(find.byKey(const Key('map_picker_back_button')));
      await tester.pumpAndSettle();

      expect(returned, isTrue);
      expect(returnedLocation, isNull);
    });
  });

  group('Map Recenter / Current Location Control Tests', () {
    testWidgets('tapping recenter button fetches location and places marker', (tester) async {
      final fakeLocationService = FakeLocationService(
        positionToReturn: const SelectedLocation(latitude: 6.9500, longitude: 79.8800),
      );

      await tester.pumpWidget(createMapPickerTestApp(
        locationService: fakeLocationService,
      ));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('selected_location_marker')), findsNothing);

      // Tap recenter button
      await tester.tap(find.byKey(const Key('map_recenter_button')));
      await tester.pumpAndSettle();

      expect(fakeLocationService.getCurrentLocationCallCount, equals(1));
      expect(find.byKey(const Key('selected_location_marker')), findsOneWidget);
      expect(find.text('6.95000, 79.88000'), findsOneWidget);
      expect(find.text('Selected current device location.'), findsOneWidget);
    });

    testWidgets('recenter button failure shows SnackBar with error message', (tester) async {
      final fakeLocationService = FakeLocationService(
        serviceEnabled: false,
      );

      await tester.pumpWidget(createMapPickerTestApp(
        locationService: fakeLocationService,
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('map_recenter_button')));
      await tester.pumpAndSettle();

      expect(find.textContaining('Location services are turned off'), findsOneWidget);
      expect(find.text('Settings'), findsOneWidget);
    });
  });
}
