import 'dart:async';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/bins/services/external_directions_launcher.dart';
import 'package:mobile/features/driver/data/driver_repository.dart';
import 'package:mobile/features/driver/models/assignment_detail_model.dart';
import 'package:mobile/features/driver/models/assignment_summary_model.dart';
import 'package:mobile/features/driver/models/assignment_task_model.dart';
import 'package:mobile/features/driver/models/collection_assignment_status.dart';
import 'package:mobile/features/driver/models/driver_availability_status.dart';
import 'package:mobile/features/driver/models/driver_self_model.dart';
import 'package:mobile/features/driver/models/paged_assignments_model.dart';
import 'package:mobile/features/driver/models/route_read_model.dart';
import 'package:mobile/features/driver/models/route_stop_model.dart';
import 'package:mobile/features/driver/models/route_stop_status.dart';
import 'package:mobile/features/driver/presentation/driver_route_screen.dart';
import 'package:mobile/features/driver/presentation/widgets/driver_stop_card.dart';
import 'package:mobile/shared/widgets/app_button.dart';
import 'package:mobile/shared/widgets/app_loading_indicator.dart';

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

class FakeDirectionsLauncher implements ExternalDirectionsLauncher {
  final List<({double latitude, double longitude})> calls = [];
  bool shouldSucceed;

  FakeDirectionsLauncher({this.shouldSucceed = true});

  @override
  Future<bool> openDirections({
    required double latitude,
    required double longitude,
  }) async {
    calls.add((latitude: latitude, longitude: longitude));
    return shouldSucceed;
  }
}

class MockDriverRepository extends DriverRepository {
  AssignmentDetailModel? currentAssignment;
  bool shouldThrow = false;
  int getCurrentAssignmentCallCount = 0;
  Completer<AssignmentDetailModel?>? hangingCompleter;

  MockDriverRepository({
    this.currentAssignment,
    this.shouldThrow = false,
  });

  @override
  Future<AssignmentDetailModel?> getCurrentAssignmentDetail() async {
    getCurrentAssignmentCallCount++;
    if (hangingCompleter != null) {
      return hangingCompleter!.future;
    }
    if (shouldThrow) {
      throw const ApiException(
        message: 'Failed to load route. Please verify your connection.',
        statusCode: 500,
      );
    }
    return currentAssignment;
  }

  @override
  Future<AssignmentSummaryModel?> getCurrentAssignmentSummary() async {
    return currentAssignment?.toSummary();
  }

  @override
  Future<AssignmentDetailModel> getAssignmentDetail(String assignmentId) async {
    if (currentAssignment != null && currentAssignment!.id == assignmentId) {
      return currentAssignment!;
    }
    throw const ApiException(message: 'Not found', statusCode: 404);
  }

  @override
  Future<DriverSelfModel> getDriverSelf(String driverId) async {
    return DriverSelfModel(
      id: driverId,
      displayName: 'Samantha Silva',
      availabilityStatus: DriverAvailabilityStatus.available,
      isOccupied: currentAssignment != null,
    );
  }

  @override
  Future<PagedAssignmentsModel> getMyAssignments({
    CollectionAssignmentStatus? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    return PagedAssignmentsModel(
      items: currentAssignment != null ? [currentAssignment!.toSummary()] : [],
      page: page,
      pageSize: pageSize,
      totalCount: currentAssignment != null ? 1 : 0,
      totalPages: 1,
    );
  }

  @override
  Future<RouteReadModel> getRoute(String routeId) async {
    if (currentAssignment?.route != null) {
      return currentAssignment!.route!;
    }
    throw const ApiException(message: 'Route not found', statusCode: 404);
  }

  @override
  Future<DriverSelfModel> updateAvailability(DriverAvailabilityStatus status) async {
    return DriverSelfModel(
      id: 'driver-1',
      displayName: 'Samantha Silva',
      availabilityStatus: status,
      isOccupied: false,
    );
  }
}

void main() {
  AssignmentTaskModel createSampleTask({
    required String id,
    required String code,
    double? latitude = 6.9271,
    double? longitude = 79.8612,
    String? addressText = 'Pettah Main Street, Colombo',
  }) {
    return AssignmentTaskModel(
      id: id,
      taskCode: code,
      targetType: 'Bin',
      collectionReason: 'Scheduled routine collection',
      status: 'Pending',
      scheduledAt: DateTime.utc(2026, 9, 26, 8, 30),
      addressText: addressText,
      latitude: latitude,
      longitude: longitude,
    );
  }

  AssignmentDetailModel createSampleAssignment({
    String id = 'asn-route-001',
    String vehicleRegistrationNumber = 'WP-CAB-1234',
    CollectionAssignmentStatus status = CollectionAssignmentStatus.inProgress,
    List<RouteStopModel>? stops,
  }) {
    final defaultStops = [
      RouteStopModel(
        id: 'stop-1',
        sequence: 1,
        status: RouteStopStatus.completed,
        task: createSampleTask(
          id: 'tsk-1',
          code: 'TSK-101',
          latitude: 6.9271,
          longitude: 79.8612,
          addressText: '1st Cross Street, Pettah',
        ),
      ),
      RouteStopModel(
        id: 'stop-2',
        sequence: 2,
        status: RouteStopStatus.pending,
        task: createSampleTask(
          id: 'tsk-2',
          code: 'TSK-102',
          latitude: 6.9300,
          longitude: 79.8650,
          addressText: '2nd Cross Street, Pettah',
        ),
      ),
      RouteStopModel(
        id: 'stop-3',
        sequence: 3,
        status: RouteStopStatus.pending,
        task: createSampleTask(
          id: 'tsk-3',
          code: 'TSK-103',
          latitude: 6.9350,
          longitude: 79.8700,
          addressText: 'Olcott Mawatha, Colombo',
        ),
      ),
    ];

    final effectiveStops = stops ?? defaultStops;

    return AssignmentDetailModel(
      id: id,
      status: status,
      driverId: 'driver-profile-1',
      driverName: 'Samantha Silva',
      vehicleId: 'veh-001',
      vehicleRegistrationNumber: vehicleRegistrationNumber,
      stopCount: effectiveStops.length,
      completedStopCount: effectiveStops.where((s) => s.status.isCompleted).length,
      failedStopCount: effectiveStops.where((s) => s.status.isFailed).length,
      assignedAt: DateTime.utc(2026, 9, 26, 7, 0),
      route: RouteReadModel(
        id: 'route-001',
        collectionAssignmentId: id,
        routingMethod: 'ManualOrder',
        stops: effectiveStops,
      ),
    );
  }

  Widget createRouteTestApp({
    MockDriverRepository? repositoryOverride,
    ExternalDirectionsLauncher? directionsLauncher,
    TileProvider? tileProvider,
  }) {
    return ProviderScope(
      overrides: [
        driverRepositoryProvider.overrideWithValue(
          repositoryOverride ?? MockDriverRepository(currentAssignment: createSampleAssignment()),
        ),
      ],
      child: MaterialApp(
        home: Scaffold(
          body: DriverRouteScreen(
            directionsLauncher: directionsLauncher,
            tileProvider: tileProvider ?? FakeTestTileProvider(),
          ),
        ),
      ),
    );
  }

  group('DriverRouteScreen State Tests', () {
    testWidgets('1. Loading state displays standardized loading indicator', (tester) async {
      final repository = MockDriverRepository(
        currentAssignment: createSampleAssignment(),
      )..hangingCompleter = Completer<AssignmentDetailModel?>();

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pump();

      expect(find.byType(AppLoadingIndicator), findsOneWidget);
      expect(find.text('Loading collection route...'), findsOneWidget);
    });

    testWidgets('2. Error state displays alert and retry button', (tester) async {
      final repository = MockDriverRepository()..shouldThrow = true;

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_error_alert')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_retry_button')), findsOneWidget);

      // Tap Retry
      repository.shouldThrow = false;
      repository.currentAssignment = createSampleAssignment();
      await tester.tap(find.byKey(const Key('driver_route_retry_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_error_alert')), findsNothing);
      expect(find.byKey(const Key('driver_route_header_card')), findsOneWidget);
    });

    testWidgets('3. Empty state when assignment is null renders "No active route assigned"', (tester) async {
      final repository = MockDriverRepository(currentAssignment: null);

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_empty_state')), findsOneWidget);
      expect(find.text('No active route assigned'), findsOneWidget);
      expect(find.textContaining('You do not have an active collection route'), findsOneWidget);
    });

    testWidgets('4. Empty state when route is null renders "No active route assigned"', (tester) async {
      final assignmentWithoutRoute = AssignmentDetailModel(
        id: 'asn-no-route',
        status: CollectionAssignmentStatus.assigned,
        driverId: 'driver-1',
        driverName: 'Samantha Silva',
        vehicleId: 'veh-1',
        vehicleRegistrationNumber: 'WP-CAB-1234',
        stopCount: 0,
        completedStopCount: 0,
        failedStopCount: 0,
        assignedAt: DateTime.utc(2026, 9, 26),
        route: null,
      );

      final repository = MockDriverRepository(currentAssignment: assignmentWithoutRoute);

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_empty_state')), findsOneWidget);
      expect(find.text('No active route assigned'), findsOneWidget);
    });

    testWidgets('5. Empty state when route stops list is empty renders "No active route assigned"', (tester) async {
      final assignmentWithEmptyStops = AssignmentDetailModel(
        id: 'asn-empty-stops',
        status: CollectionAssignmentStatus.assigned,
        driverId: 'driver-1',
        driverName: 'Samantha Silva',
        vehicleId: 'veh-1',
        vehicleRegistrationNumber: 'WP-CAB-1234',
        stopCount: 0,
        completedStopCount: 0,
        failedStopCount: 0,
        assignedAt: DateTime.utc(2026, 9, 26),
        route: const RouteReadModel(
          id: 'route-empty',
          collectionAssignmentId: 'asn-empty-stops',
          routingMethod: 'ManualOrder',
          stops: [],
        ),
      );

      final repository = MockDriverRepository(currentAssignment: assignmentWithEmptyStops);

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_empty_state')), findsOneWidget);
      expect(find.text('No active route assigned'), findsOneWidget);
    });
  });

  group('Operational Context Header Banner Tests', () {
    testWidgets('6. Displays vehicle registration plate, status chip, and stops summary count', (tester) async {
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_header_card')), findsOneWidget);
      expect(find.text('WP-CAB-1234'), findsOneWidget);
      expect(find.text('In Progress'), findsOneWidget);
      expect(find.text('3 stops · 3 mapped'), findsOneWidget);
    });

    testWidgets('7. Displays "Unassigned Vehicle" when vehicle registration number is empty', (tester) async {
      final repository = MockDriverRepository(
        currentAssignment: createSampleAssignment(vehicleRegistrationNumber: ''),
      );

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.text('Unassigned Vehicle'), findsOneWidget);
    });
  });

  group('OpenStreetMap & Marker Layer Tests', () {
    testWidgets('8. Renders FlutterMap, TileLayer, PolylineLayer, and numbered markers for mapped stops', (tester) async {
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_map_container')), findsOneWidget);
      expect(find.byType(FlutterMap), findsOneWidget);
      expect(find.byType(TileLayer), findsOneWidget);
      expect(find.byType(PolylineLayer), findsOneWidget);

      // Verify numbered markers for 3 stops
      expect(find.byKey(const Key('driver_route_marker_1')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_marker_2')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_marker_3')), findsOneWidget);

      expect(find.text('#1'), findsAtLeastNWidgets(1));
      expect(find.text('#2'), findsAtLeastNWidgets(1));
      expect(find.text('#3'), findsAtLeastNWidgets(1));
    });

    testWidgets('9. Stop marker sequence strictly reflects backend stop.sequence', (tester) async {
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_marker_text_1')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_marker_text_2')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_marker_text_3')), findsOneWidget);
    });

    testWidgets('10. Single mapped stop renders map with 1 marker and does not crash polyline', (tester) async {
      final singleStopAssignment = createSampleAssignment(
        stops: [
          RouteStopModel(
            id: 'stop-single',
            sequence: 1,
            status: RouteStopStatus.pending,
            task: createSampleTask(
              id: 'tsk-s1',
              code: 'TSK-SINGLE',
              latitude: 6.9271,
              longitude: 79.8612,
            ),
          ),
        ],
      );

      final repository = MockDriverRepository(currentAssignment: singleStopAssignment);

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byType(FlutterMap), findsOneWidget);
      expect(find.byKey(const Key('driver_route_marker_1')), findsOneWidget);
      // No polyline rendered for single point
      expect(find.byType(PolylineLayer), findsNothing);
      expect(find.text('1 stops · 1 mapped'), findsOneWidget);
    });

    testWidgets('11. Zero mapped stops renders clear empty map placeholder card', (tester) async {
      final noCoordsAssignment = createSampleAssignment(
        stops: [
          RouteStopModel(
            id: 'stop-no-coord-1',
            sequence: 1,
            status: RouteStopStatus.pending,
            task: createSampleTask(
              id: 'tsk-nc1',
              code: 'TSK-NC1',
              latitude: null,
              longitude: null,
            ),
          ),
          RouteStopModel(
            id: 'stop-no-coord-2',
            sequence: 2,
            status: RouteStopStatus.pending,
            task: createSampleTask(
              id: 'tsk-nc2',
              code: 'TSK-NC2',
              latitude: null,
              longitude: null,
            ),
          ),
        ],
      );

      final repository = MockDriverRepository(currentAssignment: noCoordsAssignment);

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      // Empty map placeholder card rendered instead of unanchored map
      expect(find.byKey(const Key('driver_route_no_mappable_stops')), findsOneWidget);
      expect(find.text('No mapped stop locations available'), findsOneWidget);
      expect(find.byType(FlutterMap), findsNothing);

      // Stop list still displays all stops
      expect(find.text('2 stops · 0 mapped'), findsOneWidget);
      expect(find.byKey(const Key('driver_stop_card_1')), findsOneWidget);
      expect(find.byKey(const Key('driver_stop_card_2')), findsOneWidget);
    });

    testWidgets('12. Mixed coordinates: Stop 1 and 3 have markers (#1 and #3), Stop 2 has no marker, all 3 in list', (tester) async {
      final mixedAssignment = createSampleAssignment(
        stops: [
          RouteStopModel(
            id: 'stop-mix-1',
            sequence: 1,
            status: RouteStopStatus.pending,
            task: createSampleTask(
              id: 'tsk-m1',
              code: 'TSK-M1',
              latitude: 6.9271,
              longitude: 79.8612,
            ),
          ),
          RouteStopModel(
            id: 'stop-mix-2',
            sequence: 2,
            status: RouteStopStatus.pending,
            task: createSampleTask(
              id: 'tsk-m2',
              code: 'TSK-M2',
              latitude: null,
              longitude: null,
            ),
          ),
          RouteStopModel(
            id: 'stop-mix-3',
            sequence: 3,
            status: RouteStopStatus.pending,
            task: createSampleTask(
              id: 'tsk-m3',
              code: 'TSK-M3',
              latitude: 6.9350,
              longitude: 79.8700,
            ),
          ),
        ],
      );

      final repository = MockDriverRepository(currentAssignment: mixedAssignment);

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      // Markers for #1 and #3 exist, but NOT #2
      expect(find.byKey(const Key('driver_route_marker_1')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_marker_2')), findsNothing);
      expect(find.byKey(const Key('driver_route_marker_3')), findsOneWidget);

      // Sequence numbers preserved without renumbering
      expect(find.byKey(const Key('driver_route_marker_text_1')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_marker_text_3')), findsOneWidget);

      // Stop summary shows 3 stops · 2 mapped
      expect(find.text('3 stops · 2 mapped'), findsOneWidget);

      // All 3 stops are in the textual stop list
      expect(find.byKey(const Key('driver_stop_card_1')), findsOneWidget);
      expect(find.byKey(const Key('driver_stop_card_2')), findsOneWidget);
      expect(find.byKey(const Key('driver_stop_card_3')), findsOneWidget);

      // Stop 2 indicates coordinates are not available
      expect(find.byKey(const Key('driver_stop_no_coords_2')), findsOneWidget);
      expect(find.text('Coordinates not available'), findsOneWidget);
    });
  });

  group('Mandatory Sequence Disclaimer Tests', () {
    testWidgets('13. Prominently renders mandatory disclaimer label: "Stop sequence — not driving directions."', (tester) async {
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_route_sequence_disclaimer')), findsOneWidget);
      expect(find.text('Stop sequence — not driving directions.'), findsOneWidget);
    });
  });

  group('External Directions & Maps Launcher Tests', () {
    testWidgets('14. Tapping Open in Maps on stop with valid coordinates invokes launcher with exact lat & lng', (tester) async {
      final launcher = FakeDirectionsLauncher();
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(
        repositoryOverride: repository,
        directionsLauncher: launcher,
      ));
      await tester.pumpAndSettle();

      final openMapsBtn = find.byKey(const Key('driver_stop_open_maps_1'));
      await tester.ensureVisible(openMapsBtn);
      await tester.tap(openMapsBtn);
      await tester.pumpAndSettle();

      expect(launcher.calls.length, 1);
      expect(launcher.calls.first.latitude, 6.9271);
      expect(launcher.calls.first.longitude, 79.8612);
    });

    testWidgets('15. Stop without coordinates has Open in Maps button disabled', (tester) async {
      final mixedAssignment = createSampleAssignment(
        stops: [
          RouteStopModel(
            id: 'stop-no-coord',
            sequence: 1,
            status: RouteStopStatus.pending,
            task: createSampleTask(
              id: 'tsk-nc',
              code: 'TSK-NO-COORD',
              latitude: null,
              longitude: null,
            ),
          ),
        ],
      );

      final launcher = FakeDirectionsLauncher();
      final repository = MockDriverRepository(currentAssignment: mixedAssignment);

      await tester.pumpWidget(createRouteTestApp(
        repositoryOverride: repository,
        directionsLauncher: launcher,
      ));
      await tester.pumpAndSettle();

      final openMapsBtn = find.byKey(const Key('driver_stop_open_maps_1'));
      await tester.ensureVisible(openMapsBtn);

      final buttonWidget = tester.widget<AppButton>(openMapsBtn);
      expect(buttonWidget.onPressed, isNull);

      // Tap disabled button
      await tester.tap(openMapsBtn);
      await tester.pumpAndSettle();

      expect(launcher.calls.isEmpty, isTrue);
    });

    testWidgets('16. Failure to open external directions displays snackbar feedback', (tester) async {
      final launcher = FakeDirectionsLauncher(shouldSucceed: false);
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(
        repositoryOverride: repository,
        directionsLauncher: launcher,
      ));
      await tester.pumpAndSettle();

      final openMapsBtn = find.byKey(const Key('driver_stop_open_maps_1'));
      await tester.ensureVisible(openMapsBtn);
      await tester.tap(openMapsBtn);
      await tester.pumpAndSettle();

      expect(launcher.calls.length, 1);
      expect(
        find.text('Unable to open external maps. Please check your map apps.'),
        findsOneWidget,
      );
    });
  });

  group('Stop Selection & Interaction Tests', () {
    testWidgets('17. Tapping marker on map selects the stop', (tester) async {
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      // Tap marker 2
      final marker2 = find.byKey(const Key('driver_route_marker_2'));
      await tester.tap(marker2);
      await tester.pumpAndSettle();

      // Verify DriverStopCard 2 is selected
      final stopCard2 = tester.widget<DriverStopCard>(
        find.byKey(const Key('driver_stop_card_2')),
      );
      expect(stopCard2.isSelected, isTrue);
    });

    testWidgets('18. Tapping stop card in the list selects the stop', (tester) async {
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      final stopCard1Finder = find.byKey(const Key('driver_stop_card_1'));
      await tester.ensureVisible(stopCard1Finder);
      await tester.tap(stopCard1Finder);
      await tester.pumpAndSettle();

      final stopCard1 = tester.widget<DriverStopCard>(stopCard1Finder);
      expect(stopCard1.isSelected, isTrue);
    });

    testWidgets('19. Pull-to-refresh invalidates and reloads route data', (tester) async {
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(repository.getCurrentAssignmentCallCount, 1);

      // Perform pull-to-refresh
      await tester.fling(
        find.byKey(const Key('driver_route_header_card')),
        const Offset(0, 300),
        1000,
      );
      await tester.pumpAndSettle();

      expect(repository.getCurrentAssignmentCallCount, greaterThanOrEqualTo(2));
    });
  });

  group('Read-Only Invariants Tests', () {
    testWidgets('20. Read-only guard: No execution action buttons exist on screen', (tester) async {
      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      // No execution buttons (Stage 6 actions)
      expect(find.text('Start Route'), findsNothing);
      expect(find.text('Complete Stop'), findsNothing);
      expect(find.text('Fail Stop'), findsNothing);
      expect(find.text('Skip Stop'), findsNothing);
      expect(find.text('Finalize Assignment'), findsNothing);
      expect(find.text('Complete Assignment'), findsNothing);
    });
  });

  group('Responsive Viewport Tests', () {
    testWidgets('21. Renders cleanly without overflow on narrow 320px viewport', (tester) async {
      tester.view.physicalSize = const Size(320, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byKey(const Key('driver_route_header_card')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_map_container')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_sequence_disclaimer')), findsOneWidget);
    });

    testWidgets('22. Renders cleanly without overflow on standard 390px viewport', (tester) async {
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final repository = MockDriverRepository(currentAssignment: createSampleAssignment());

      await tester.pumpWidget(createRouteTestApp(repositoryOverride: repository));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byKey(const Key('driver_route_header_card')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_map_container')), findsOneWidget);
      expect(find.byKey(const Key('driver_route_sequence_disclaimer')), findsOneWidget);
    });
  });
}
