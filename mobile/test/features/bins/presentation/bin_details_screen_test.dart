import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/bins/data/public_waste_bins_repository.dart';
import 'package:mobile/features/bins/models/public_bin_availability.dart';
import 'package:mobile/features/bins/models/public_waste_bin_detail_model.dart';
import 'package:mobile/features/bins/presentation/bin_details_screen.dart';
import 'package:mobile/features/bins/services/external_directions_launcher.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';

class _FakePublicWasteBinsRepository extends PublicWasteBinsRepository {
  _FakePublicWasteBinsRepository(this.detail);

  PublicWasteBinDetailModel detail;
  final List<String> requestedIds = [];
  ApiException? failure;
  int delayMs = 0;

  @override
  Future<PublicWasteBinDetailModel> getPublicWasteBin(String binId) async {
    requestedIds.add(binId);
    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }
    if (failure != null) throw failure!;
    return detail;
  }
}

class _FakeDirectionsLauncher implements ExternalDirectionsLauncher {
  final List<({double latitude, double longitude})> destinations = [];
  bool result;
  Future<bool>? pendingResult;

  _FakeDirectionsLauncher({this.result = true});

  @override
  Future<bool> openDirections({
    required double latitude,
    required double longitude,
  }) {
    destinations.add((latitude: latitude, longitude: longitude));
    return pendingResult ?? Future<bool>.value(result);
  }
}

PublicWasteBinDetailModel _detail({
  String id = 'bin-123',
  PublicBinAvailability availability = PublicBinAvailability.usable,
  String? address = 'Main Street, Pettah',
  bool scheduled = false,
  DateTime? lastObservedAt,
  double latitude = 6.9271,
  double longitude = 79.8612,
  String binCode = 'BIN-123',
  List<WasteType> wasteTypes = const [WasteType.general, WasteType.recyclable],
}) => PublicWasteBinDetailModel(
  id: id,
  binCode: binCode,
  latitude: latitude,
  longitude: longitude,
  addressText: address,
  capacityLiters: 660,
  acceptedWasteTypes: wasteTypes,
  publicAvailability: availability,
  lastObservedAt: lastObservedAt,
  isCollectionScheduled: scheduled,
);

Widget _app(
  _FakePublicWasteBinsRepository repository, {
  String id = 'bin-123',
  Key? key,
  ExternalDirectionsLauncher? directionsLauncher,
  TextScaler? textScaler,
}) => MaterialApp(
  theme: AppTheme.lightTheme,
  builder: (context, child) {
    if (textScaler == null) return child!;
    return MediaQuery(
      data: MediaQuery.of(context).copyWith(textScaler: textScaler),
      child: child!,
    );
  },
  home: BinDetailsScreen(
    key: key,
    binId: id,
    repository: repository,
    directionsLauncher: directionsLauncher,
  ),
);

Future<void> _tapDirections(WidgetTester tester, Key key) async {
  final finder = find.byKey(key);
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
}

void main() {
  group('UrlLauncherExternalDirectionsLauncher', () {
    test('builds navigation and browser fallback destinations from saved coordinates', () {
      final navigationUri =
          UrlLauncherExternalDirectionsLauncher.navigationUriFor(
            latitude: 6.9271,
            longitude: 79.8612,
          );
      final browserUri =
          UrlLauncherExternalDirectionsLauncher.browserDirectionsUriFor(
            latitude: 6.9271,
            longitude: 79.8612,
          );

      expect(navigationUri.toString(), 'google.navigation:q=6.9271,79.8612');
      expect(browserUri.queryParameters['api'], '1');
      expect(browserUri.queryParameters['destination'], '6.9271,79.8612');
    });
  });

  group('BinDetailsScreen', () {
    testWidgets(
      'loads the requested public bin and renders public-only detail fields',
      (tester) async {
        final repository = _FakePublicWasteBinsRepository(
          _detail(
            scheduled: true,
            lastObservedAt: DateTime.utc(2026, 9, 23, 4, 30),
          ),
        );
        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();

        expect(repository.requestedIds, ['bin-123']);
        expect(find.text('BIN-123'), findsOneWidget);
        expect(find.text('660 L'), findsOneWidget);
        expect(find.text('General Waste'), findsOneWidget);
        expect(find.text('Recyclable Waste'), findsOneWidget);
        expect(find.text('Available for use.'), findsOneWidget);
        expect(
          find.byKey(const Key('collection_scheduled_indicator')),
          findsOneWidget,
        );
        expect(
          find.byKey(const Key('bin_detail_location_marker')),
          findsOneWidget,
        );
        expect(find.text('OpenStreetMap contributors'), findsOneWidget);
      },
    );

    testWidgets(
      'fits all four detail sections, map, and directions in a normal phone viewport',
      (tester) async {
        tester.view.physicalSize = const Size(412 * 3, 915 * 3);
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final repository = _FakePublicWasteBinsRepository(
          _detail(
            scheduled: true,
            lastObservedAt: DateTime.utc(2026, 9, 23, 4, 30),
          ),
        );

        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();

        for (final key in const [
          Key('bin_detail_status_section'),
          Key('bin_detail_information_section'),
          Key('bin_detail_waste_types_section'),
          Key('bin_detail_location_section'),
          Key('bin_detail_location_marker'),
          Key('get_directions_button'),
        ]) {
          expect(find.byKey(key), findsOneWidget);
        }
        final viewportHeight =
            tester.view.physicalSize.height / tester.view.devicePixelRatio;
        expect(
          tester.getRect(find.byKey(const Key('get_directions_button'))).bottom,
          lessThanOrEqualTo(viewportHeight),
        );
      },
    );

    testWidgets(
      'remains scrollable without overflow on short screens and larger text',
      (tester) async {
        tester.view.physicalSize = const Size(320 * 3, 540 * 3);
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final repository = _FakePublicWasteBinsRepository(
          _detail(
            binCode: 'BIN-WITH-A-LONG-IDENTIFIER-THAT-MUST-WRAP',
            address: 'A long saved address that remains readable on a smaller screen without clipping important location information.',
            lastObservedAt: DateTime.utc(2026, 9, 23, 4, 30),
            wasteTypes: WasteType.values,
          ),
        );

        await tester.pumpWidget(
          _app(repository, textScaler: const TextScaler.linear(1.5)),
        );
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
        expect(
          repository.detail.acceptedWasteTypes,
          hasLength(WasteType.values.length),
        );
        await tester.drag(
          find.byKey(const Key('bin_detail_scroll_view')),
          const Offset(0, -1000),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
      },
    );

    testWidgets(
      'presents every backend availability value with its citizen explanation',
      (tester) async {
        final expectations = <PublicBinAvailability, String>{
          PublicBinAvailability.usable: 'Available for use.',
          PublicBinAvailability.warning:
              'Getting full. Consider another bin if possible.',
          PublicBinAvailability.full: 'Full. Choose another bin.',
          PublicBinAvailability.unavailable: 'Currently unavailable.',
          PublicBinAvailability.unknown:
              'Current condition has not been confirmed.',
        };

        for (final entry in expectations.entries) {
          final repository = _FakePublicWasteBinsRepository(
            _detail(availability: entry.key),
          );
          await tester.pumpWidget(_app(repository, key: ValueKey(entry.key)));
          await tester.pumpAndSettle();
          expect(find.text(entry.key.value), findsOneWidget);
          expect(find.text(entry.value), findsOneWidget);
        }
      },
    );

    testWidgets(
      'uses coordinates as the location fallback and does not invent optional values',
      (tester) async {
        final repository = _FakePublicWasteBinsRepository(
          _detail(address: null),
        );
        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('bin_detail_location_text')),
          findsOneWidget,
        );
        expect(find.text('6.9271, 79.8612'), findsOneWidget);
        expect(find.text('Last observed'), findsNothing);
        expect(
          find.byKey(const Key('collection_scheduled_indicator')),
          findsNothing,
        );
        expect(find.textContaining('away'), findsNothing);
      },
    );

    testWidgets(
      'refresh retrieves current data without retaining the old availability as success',
      (tester) async {
        final repository = _FakePublicWasteBinsRepository(
          _detail(availability: PublicBinAvailability.warning),
        );
        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();
        expect(find.text('Warning'), findsOneWidget);

        repository.detail = _detail(availability: PublicBinAvailability.full);
        await tester.tap(find.byKey(const Key('refresh_bin_detail_button')));
        await tester.pumpAndSettle();
        expect(repository.requestedIds.length, 2);
        expect(find.text('Full'), findsOneWidget);
        expect(find.text('Full. Choose another bin.'), findsOneWidget);
      },
    );

    testWidgets('shows loading and a retryable not-found error', (
      tester,
    ) async {
      final repository = _FakePublicWasteBinsRepository(_detail())
        ..delayMs = 1000;
      await tester.pumpWidget(_app(repository));
      await tester.pump();
      expect(find.byKey(const Key('bin_detail_loading')), findsOneWidget);

      repository.failure = const ApiException(
        message: 'Requested resource was not found.',
        statusCode: 404,
      );
      await tester.pump(const Duration(milliseconds: 1100));
      await tester.pumpAndSettle();
      expect(find.text('This bin is no longer available.'), findsOneWidget);
      expect(
        find.byKey(const Key('retry_load_bin_detail_button')),
        findsOneWidget,
      );

      repository.failure = null;
      await tester.tap(find.byKey(const Key('retry_load_bin_detail_button')));
      await tester.pumpAndSettle();
      expect(find.text('BIN-123'), findsOneWidget);
    });

    testWidgets(
      'shows a location-unavailable state for invalid coordinates without placing a marker',
      (tester) async {
        final repository = _FakePublicWasteBinsRepository(
          _detail(latitude: 99, longitude: 79.8612),
        );
        final launcher = _FakeDirectionsLauncher();
        await tester.pumpWidget(_app(repository, directionsLauncher: launcher));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('bin_detail_location_unavailable')),
          findsOneWidget,
        );
        expect(
          find.byKey(const Key('bin_detail_location_marker')),
          findsNothing,
        );
        expect(
          find.byKey(const Key('get_directions_unavailable_button')),
          findsOneWidget,
        );
        expect(launcher.destinations, isEmpty);
      },
    );

    testWidgets('opens directions with the current bin saved coordinates', (
      tester,
    ) async {
      final repository = _FakePublicWasteBinsRepository(
        _detail(latitude: 6.912345, longitude: 79.876543),
      );
      final launcher = _FakeDirectionsLauncher();
      await tester.pumpWidget(_app(repository, directionsLauncher: launcher));
      await tester.pumpAndSettle();

      await _tapDirections(tester, const Key('get_directions_button'));
      await tester.pumpAndSettle();

      expect(launcher.destinations, [
        (latitude: 6.912345, longitude: 79.876543),
      ]);
      expect(find.byType(BinDetailsScreen), findsOneWidget);
      expect(repository.requestedIds, ['bin-123']);
    });

    testWidgets('uses a different destination for each selected bin', (
      tester,
    ) async {
      final firstLauncher = _FakeDirectionsLauncher();
      await tester.pumpWidget(
        _app(
          _FakePublicWasteBinsRepository(
            _detail(id: 'first', latitude: 6.9, longitude: 79.8),
          ),
          key: const ValueKey('first'),
          directionsLauncher: firstLauncher,
        ),
      );
      await tester.pumpAndSettle();
      await _tapDirections(tester, const Key('get_directions_button'));
      await tester.pumpAndSettle();

      final secondLauncher = _FakeDirectionsLauncher();
      await tester.pumpWidget(
        _app(
          _FakePublicWasteBinsRepository(
            _detail(id: 'second', latitude: 7.1, longitude: 80.1),
          ),
          key: const ValueKey('second'),
          directionsLauncher: secondLauncher,
        ),
      );
      await tester.pumpAndSettle();
      await _tapDirections(tester, const Key('get_directions_button'));
      await tester.pumpAndSettle();

      expect(firstLauncher.destinations, [(latitude: 6.9, longitude: 79.8)]);
      expect(secondLauncher.destinations, [(latitude: 7.1, longitude: 80.1)]);
    });

    testWidgets('shows a nonblocking error when directions cannot be opened', (
      tester,
    ) async {
      final launcher = _FakeDirectionsLauncher(result: false);
      await tester.pumpWidget(
        _app(
          _FakePublicWasteBinsRepository(_detail()),
          directionsLauncher: launcher,
        ),
      );
      await tester.pumpAndSettle();

      await _tapDirections(tester, const Key('get_directions_button'));
      await tester.pumpAndSettle();

      expect(
        find.text('Unable to open directions. Please try another maps app.'),
        findsOneWidget,
      );
      expect(find.byType(BinDetailsScreen), findsOneWidget);
    });

    testWidgets('prevents duplicate launches while directions are opening', (
      tester,
    ) async {
      final launcher = _FakeDirectionsLauncher()
        ..pendingResult = Future<bool>.delayed(
          const Duration(seconds: 1),
          () => true,
        );
      await tester.pumpWidget(
        _app(
          _FakePublicWasteBinsRepository(_detail()),
          directionsLauncher: launcher,
        ),
      );
      await tester.pumpAndSettle();

      await _tapDirections(tester, const Key('get_directions_button'));
      await tester.pump();
      await tester.tap(
        find.byKey(const Key('get_directions_button')),
        warnIfMissed: false,
      );
      await tester.pump();

      expect(launcher.destinations, hasLength(1));
      await tester.pump(const Duration(seconds: 2));
    });
  });
}
