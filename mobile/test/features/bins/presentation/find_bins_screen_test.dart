import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/bins/data/public_waste_bins_repository.dart';
import 'package:mobile/features/bins/models/paged_public_waste_bins_model.dart';
import 'package:mobile/features/bins/models/public_bin_availability.dart';
import 'package:mobile/features/bins/models/public_waste_bin_detail_model.dart';
import 'package:mobile/features/bins/models/public_waste_bin_list_item_model.dart';
import 'package:mobile/features/bins/models/public_waste_bin_query.dart';
import 'package:mobile/features/bins/presentation/find_bins_screen.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/models/selected_location.dart';
import 'package:mobile/features/reporting/services/location_service.dart';

class _FakeLocationService implements LocationService {
  LocationResult result;
  int calls = 0;

  _FakeLocationService(this.result);

  @override
  Future<LocationResult> getCurrentLocation() async {
    calls++;
    return result;
  }

  @override
  Future<bool> isLocationServiceEnabled() async => true;
  @override
  Future<LocationPermission> checkPermission() async =>
      LocationPermission.whileInUse;
  @override
  Future<LocationPermission> requestPermission() async =>
      LocationPermission.whileInUse;
  @override
  Future<SelectedLocation> getCurrentPosition() async =>
      const SelectedLocation(latitude: 6.9271, longitude: 79.8612);
  @override
  Future<bool> openAppSettings() async => true;
  @override
  Future<bool> openLocationSettings() async => true;
}

class _FakePublicWasteBinsRepository extends PublicWasteBinsRepository {
  final Map<int, List<PublicWasteBinListItemModel>> pages = {};
  final List<PublicWasteBinQuery> queries = [];
  int? failPage;
  bool failAll = false;
  int delayMs = 0;
  final List<String> detailIds = [];

  @override
  Future<PagedPublicWasteBinsModel> getPublicWasteBins(
    PublicWasteBinQuery query,
  ) async {
    queries.add(query);
    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }
    if (failAll || failPage == query.page) {
      throw const ApiException(
        message: 'Unable to reach public bins.',
        statusCode: 500,
      );
    }
    final items = pages[query.page] ?? [];
    final totalPages = pages.keys.isEmpty
        ? 1
        : pages.keys.reduce((a, b) => a > b ? a : b);
    return PagedPublicWasteBinsModel(
      items: items,
      page: query.page,
      pageSize: query.pageSize,
      totalCount: pages.values.fold(
        0,
        (total, pageItems) => total + pageItems.length,
      ),
      totalPages: totalPages,
    );
  }

  @override
  Future<PublicWasteBinDetailModel> getPublicWasteBin(String binId) async {
    detailIds.add(binId);
    return PublicWasteBinDetailModel(
      id: binId,
      binCode: 'BIN-DETAIL',
      latitude: 6.9271,
      longitude: 79.8612,
      capacityLiters: 660,
      acceptedWasteTypes: const [WasteType.general],
      publicAvailability: PublicBinAvailability.unknown,
      isCollectionScheduled: false,
    );
  }
}

PublicWasteBinListItemModel _bin({
  required String id,
  String code = 'BIN-001',
  PublicBinAvailability availability = PublicBinAvailability.unknown,
  String? addressText = 'Main Street, Pettah',
  double? distanceMeters,
  List<WasteType> wasteTypes = const [WasteType.general],
}) => PublicWasteBinListItemModel(
  id: id,
  binCode: code,
  latitude: 6.9271,
  longitude: 79.8612,
  addressText: addressText,
  capacityLiters: 660,
  acceptedWasteTypes: wasteTypes,
  publicAvailability: availability,
  distanceMeters: distanceMeters,
);

Widget _app(
  _FakePublicWasteBinsRepository repository, {
  LocationService? locationService,
  TileProvider? tileProvider,
}) => MaterialApp(
  theme: AppTheme.lightTheme,
  home: FindBinsScreen(
    repository: repository,
    locationService: locationService,
    tileProvider: tileProvider,
  ),
);

Widget _routedApp(_FakePublicWasteBinsRepository repository) {
  final router = GoRouter(
    initialLocation: '/citizen/nearby-bins',
    routes: [
      GoRoute(
        path: '/citizen/nearby-bins',
        builder: (_, _) => FindBinsScreen(repository: repository),
      ),
      GoRoute(
        path: '/citizen/nearby-bins/:id',
        builder: (_, state) =>
            Scaffold(body: Text('Detail ${state.pathParameters['id']}')),
      ),
    ],
  );
  return MaterialApp.router(theme: AppTheme.lightTheme, routerConfig: router);
}

void main() {
  group('FindBinsScreen', () {
    late _FakePublicWasteBinsRepository repository;

    setUp(() {
      repository = _FakePublicWasteBinsRepository();
    });

    testWidgets(
      'renders citizen public bin fields and all backend availability labels',
      (tester) async {
        repository.pages[1] = [
          _bin(
            id: 'usable',
            code: 'BIN-USABLE',
            availability: PublicBinAvailability.usable,
            distanceMeters: 260,
          ),
          _bin(
            id: 'warning',
            code: 'BIN-WARNING',
            availability: PublicBinAvailability.warning,
          ),
          _bin(
            id: 'full',
            code: 'BIN-FULL',
            availability: PublicBinAvailability.full,
          ),
          _bin(
            id: 'unavailable',
            code: 'BIN-UNAVAILABLE',
            availability: PublicBinAvailability.unavailable,
          ),
          _bin(
            id: 'unknown',
            code: 'BIN-UNKNOWN',
            availability: PublicBinAvailability.unknown,
            addressText: null,
          ),
        ];

        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();

        expect(find.text('Find a Bin'), findsOneWidget);
        expect(find.text('BIN-USABLE'), findsOneWidget);
        expect(find.text('Main Street, Pettah'), findsWidgets);
        expect(find.text('General Waste'), findsWidgets);
        expect(find.text('260 m away'), findsOneWidget);
        expect(find.text('Usable'), findsOneWidget);
        expect(find.text('Warning'), findsOneWidget);
        await tester.scrollUntilVisible(
          find.text('Full'),
          160,
          scrollable: find.byType(Scrollable).first,
        );
        expect(find.text('Full'), findsOneWidget);
        await tester.scrollUntilVisible(
          find.text('Unavailable'),
          160,
          scrollable: find.byType(Scrollable).first,
        );
        expect(find.text('Unavailable'), findsOneWidget);
        await tester.scrollUntilVisible(
          find.text('Unknown'),
          160,
          scrollable: find.byType(Scrollable).first,
        );
        expect(find.text('Unknown'), findsOneWidget);
        expect(find.text('6.9271, 79.8612'), findsOneWidget);
      },
    );

    testWidgets(
      'does not invent a distance when the public response omits it',
      (tester) async {
        repository.pages[1] = [_bin(id: 'no-distance', distanceMeters: null)];

        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();

        expect(find.textContaining('away'), findsNothing);
      },
    );

    testWidgets(
      'map view shows public-bin markers and the selected marker preview',
      (tester) async {
        repository.pages[1] = [
          _bin(
            id: 'map-bin',
            code: 'BIN-MAP',
            availability: PublicBinAvailability.warning,
            distanceMeters: 400,
          ),
        ];
        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();

        await tester.tap(find.text('Map'));
        await tester.pump();
        expect(
          find.byKey(const Key('public_bin_marker_map-bin')),
          findsOneWidget,
        );
        expect(find.text('OpenStreetMap contributors'), findsOneWidget);

        await tester.tap(find.byKey(const Key('public_bin_marker_map-bin')));
        await tester.pumpAndSettle();
        expect(find.text('BIN-MAP'), findsWidgets);
        expect(find.text('Availability: Warning'), findsOneWidget);
        expect(find.text('400 m away'), findsOneWidget);
      },
    );

    testWidgets(
      'opens the selected list bin and map preview bin using their stable IDs',
      (tester) async {
        repository.pages[1] = [_bin(id: 'correct-bin', code: 'BIN-CORRECT')];
        await tester.pumpWidget(_routedApp(repository));
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('public_bin_card_correct-bin')));
        await tester.pumpAndSettle();
        expect(find.text('Detail correct-bin'), findsOneWidget);

        final router = GoRouter.of(
          tester.element(find.text('Detail correct-bin')),
        );
        router.pop();
        await tester.pumpAndSettle();
        await tester.tap(find.text('Map'));
        await tester.pumpAndSettle();
        await tester.tap(
          find.byKey(const Key('public_bin_marker_correct-bin')),
        );
        await tester.pumpAndSettle();
        await tester.tap(
          find.byKey(const Key('view_bin_details_from_map_preview')),
        );
        await tester.pumpAndSettle();
        expect(find.text('Detail correct-bin'), findsOneWidget);
      },
    );

    testWidgets(
      'explicit nearby action uses location coordinates and valid radius without requesting GPS on load',
      (tester) async {
        repository.pages[1] = [_bin(id: 'nearby', distanceMeters: 130)];
        final location = _FakeLocationService(
          const LocationSuccess(
            SelectedLocation(latitude: 6.91, longitude: 79.84),
          ),
        );
        await tester.pumpWidget(_app(repository, locationService: location));
        await tester.pumpAndSettle();
        expect(location.calls, 0);

        await tester.tap(find.byKey(const Key('use_my_location_button')));
        await tester.pumpAndSettle();
        expect(location.calls, 1);
        expect(repository.queries.last.latitude, 6.91);
        expect(repository.queries.last.longitude, 79.84);
        expect(repository.queries.last.radiusKm, 5.0);
        expect(find.textContaining('Nearby search active'), findsOneWidget);

        await tester.tap(find.byKey(const Key('clear_nearby_search_button')));
        await tester.pumpAndSettle();
        expect(repository.queries.last.latitude, isNull);
        expect(repository.queries.last.longitude, isNull);
        expect(repository.queries.last.radiusKm, isNull);
      },
    );

    testWidgets(
      'location failure keeps browsing results and does not issue a nearby query',
      (tester) async {
        repository.pages[1] = [_bin(id: 'browse-bin')];
        final location = _FakeLocationService(
          const LocationFailure(
            reason: LocationFailureReason.permissionDenied,
            message:
                'Location permission is needed to use your current location.',
          ),
        );
        await tester.pumpWidget(_app(repository, locationService: location));
        await tester.pumpAndSettle();
        final initialCalls = repository.queries.length;

        await tester.tap(find.byKey(const Key('use_my_location_button')));
        await tester.pumpAndSettle();
        expect(location.calls, 1);
        expect(repository.queries.length, initialCalls);
        expect(
          find.byKey(const Key('public_bin_card_browse-bin')),
          findsOneWidget,
        );
        expect(
          find.text(
            'Location permission is needed to use your current location.',
          ),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'waste-type selection requests page one with the selected backend enum',
      (tester) async {
        repository.pages[1] = [_bin(id: 'general')];
        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('bin_waste_type_filter')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Organic Waste').last);
        await tester.pumpAndSettle();

        expect(repository.queries.last.page, 1);
        expect(repository.queries.last.wasteType, WasteType.organic);
      },
    );

    testWidgets('appends an additional page and preserves existing bins', (
      tester,
    ) async {
      repository.pages[1] = [_bin(id: 'first', code: 'BIN-FIRST')];
      repository.pages[2] = [_bin(id: 'second', code: 'BIN-SECOND')];
      await tester.pumpWidget(_app(repository));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('load_more_bins_button')));
      await tester.pumpAndSettle();

      expect(find.text('BIN-FIRST'), findsOneWidget);
      expect(find.text('BIN-SECOND'), findsOneWidget);
      expect(repository.queries.map((query) => query.page), [1, 2]);
    });

    testWidgets('pull-to-refresh reloads page one using the current filter', (
      tester,
    ) async {
      repository.pages[1] = [_bin(id: 'one')];
      await tester.pumpWidget(_app(repository));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('bin_waste_type_filter')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Recyclable Waste').last);
      await tester.pumpAndSettle();

      await tester.fling(
        find.byKey(const Key('find_bins_list_view')),
        const Offset(0, 400),
        1000,
      );
      await tester.pump();
      await tester.pump(const Duration(seconds: 1));

      expect(repository.queries.last.page, 1);
      expect(repository.queries.last.wasteType, WasteType.recyclable);
    });

    testWidgets(
      'shows loading, empty, filtered empty, and initial error states',
      (tester) async {
        repository.delayMs = 1000;
        await tester.pumpWidget(_app(repository));
        await tester.pump();
        expect(find.text('Loading public bins...'), findsOneWidget);
        await tester.pump(const Duration(milliseconds: 1100));
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('empty_bins_title')), findsOneWidget);

        await tester.tap(find.byKey(const Key('bin_waste_type_filter')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Bulky Waste').last);
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('filtered_empty_bins_title')),
          findsOneWidget,
        );
        expect(
          find.byKey(const Key('clear_bin_filter_button')),
          findsOneWidget,
        );

        repository.failAll = true;
        await tester.tap(find.byKey(const Key('clear_bin_filter_button')));
        await tester.pumpAndSettle();
        expect(find.text('Unable to Load Bins'), findsOneWidget);
        expect(find.byKey(const Key('retry_load_bins_button')), findsOneWidget);
      },
    );

    testWidgets(
      'additional-page error keeps the already loaded cards and can be retried',
      (tester) async {
        repository.pages[1] = [_bin(id: 'first', code: 'BIN-FIRST')];
        repository.pages[2] = [_bin(id: 'second', code: 'BIN-SECOND')];
        repository.failPage = 2;
        await tester.pumpWidget(_app(repository));
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('load_more_bins_button')));
        await tester.pumpAndSettle();
        expect(find.text('BIN-FIRST'), findsOneWidget);
        expect(find.text('Unable to reach public bins.'), findsOneWidget);

        repository.failPage = null;
        await tester.tap(find.byKey(const Key('retry_load_more_bins_button')));
        await tester.pumpAndSettle();
        expect(find.text('BIN-SECOND'), findsOneWidget);
      },
    );

    testWidgets('renders without overflow on a narrow phone', (tester) async {
      tester.view.physicalSize = const Size(280 * 3, 640 * 3);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      repository.pages[1] = [
        _bin(
          id: 'narrow',
          code: 'BIN-WITH-A-LONG-IDENTIFIER',
          wasteTypes: WasteType.values,
        ),
      ];

      await tester.pumpWidget(_app(repository));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    });
  });
}
