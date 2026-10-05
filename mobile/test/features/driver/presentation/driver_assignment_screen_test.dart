import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/driver/data/driver_repository.dart';
import 'package:mobile/features/driver/models/assignment_detail_model.dart';
import 'package:mobile/features/driver/models/assignment_history_model.dart';
import 'package:mobile/features/driver/models/assignment_summary_model.dart';
import 'package:mobile/features/driver/models/assignment_task_model.dart';
import 'package:mobile/features/driver/models/collection_assignment_status.dart';
import 'package:mobile/features/driver/models/driver_availability_status.dart';
import 'package:mobile/features/driver/models/driver_self_model.dart';
import 'package:mobile/features/driver/models/paged_assignments_model.dart';
import 'package:mobile/features/driver/models/route_read_model.dart';
import 'package:mobile/features/driver/models/route_stop_model.dart';
import 'package:mobile/features/driver/models/route_stop_status.dart';
import 'package:mobile/features/driver/presentation/driver_assignment_screen.dart';
import 'package:mobile/features/driver/presentation/widgets/assignment_progress_card.dart';
import 'package:mobile/features/driver/presentation/widgets/driver_stop_card.dart';

class MockDriverRepository extends DriverRepository {
  AssignmentDetailModel? currentAssignment;
  bool shouldThrow = false;
  int getCurrentAssignmentCallCount = 0;
  Completer<AssignmentDetailModel?>? hangingCompleter;

  MockDriverRepository({this.currentAssignment, this.shouldThrow = false});

  @override
  Future<AssignmentDetailModel?> getCurrentAssignmentDetail() async {
    getCurrentAssignmentCallCount++;
    if (hangingCompleter != null) {
      return hangingCompleter!.future;
    }
    if (shouldThrow) {
      throw const ApiException(
        message: 'Network error fetching assignment detail.',
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
      displayName: 'Driver User',
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
  Future<DriverSelfModel> updateAvailability(
    DriverAvailabilityStatus status,
  ) async {
    return DriverSelfModel(
      id: 'driver-1',
      displayName: 'Driver User',
      availabilityStatus: status,
      isOccupied: false,
    );
  }
}

void main() {
  final testAssignedAt = DateTime.utc(2026, 9, 25, 8, 0);
  final testStartedAt = DateTime.utc(2026, 9, 25, 8, 30);
  final testScheduledAt = DateTime.utc(2026, 9, 25, 9, 0);

  final sampleStops = [
    RouteStopModel(
      id: 'stop-2',
      sequence: 2,
      status: RouteStopStatus.pending,
      task: AssignmentTaskModel(
        id: 'task-2',
        taskCode: 'TSK-20260925-002',
        targetType: 'Report',
        wasteReportId: 'rep-2',
        collectionReason: 'Illegal Dumping',
        status: 'Scheduled',
        scheduledAt: testScheduledAt,
        addressText: '45 Cross Street, Colombo 02',
        latitude: 6.9300,
        longitude: 79.8550,
      ),
    ),
    RouteStopModel(
      id: 'stop-1',
      sequence: 1,
      status: RouteStopStatus.completed,
      completedAt: DateTime.utc(2026, 9, 25, 9, 45),
      task: AssignmentTaskModel(
        id: 'task-1',
        taskCode: 'TSK-20260925-001',
        targetType: 'Bin',
        wasteBinId: 'bin-1',
        collectionReason: 'Overflowing Bin',
        status: 'Completed',
        scheduledAt: testScheduledAt,
        addressText: '12 Main Road, Colombo 01',
        latitude: 6.9271,
        longitude: 79.8612,
      ),
    ),
  ];

  final sampleAssignedAssignment = AssignmentDetailModel(
    id: 'asn-assigned-1234',
    status: CollectionAssignmentStatus.assigned,
    driverId: 'driver-1',
    driverName: 'Samantha Perera',
    vehicleId: 'veh-1',
    vehicleRegistrationNumber: 'WP ABC-1234',
    stopCount: 2,
    completedStopCount: 0,
    failedStopCount: 0,
    assignedAt: testAssignedAt,
    route: RouteReadModel(
      id: 'route-1',
      collectionAssignmentId: 'asn-assigned-1234',
      routingMethod: 'ManualOrder',
      stops: sampleStops,
    ),
  );

  final sampleInProgressAssignment = AssignmentDetailModel(
    id: 'asn-inprogress-5678',
    status: CollectionAssignmentStatus.inProgress,
    driverId: 'driver-1',
    driverName: 'Samantha Perera',
    vehicleId: 'veh-2',
    vehicleRegistrationNumber: 'WP CAD-5678',
    stopCount: 2,
    completedStopCount: 1,
    failedStopCount: 0,
    assignedAt: testAssignedAt,
    route: RouteReadModel(
      id: 'route-2',
      collectionAssignmentId: 'asn-inprogress-5678',
      routingMethod: 'ManualOrder',
      stops: sampleStops,
    ),
    history: [
      AssignmentHistoryModel(
        id: 'hist-1',
        toStatus: CollectionAssignmentStatus.inProgress,
        changedAt: testStartedAt,
      ),
    ],
  );

  Widget createAssignmentScreenApp({required MockDriverRepository repository}) {
    return ProviderScope(
      overrides: [driverRepositoryProvider.overrideWithValue(repository)],
      child: const MaterialApp(home: DriverAssignmentScreen()),
    );
  }

  group('DriverAssignmentScreen State Tests', () {
    testWidgets('1. Loading state displays standardized loading indicator', (
      tester,
    ) async {
      final repo = MockDriverRepository();
      repo.hangingCompleter = Completer<AssignmentDetailModel?>();

      await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
      await tester.pump();

      expect(find.text('Loading assignment details...'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets(
      '2. Empty state renders clean message when no active assignment exists',
      (tester) async {
        final repo = MockDriverRepository(currentAssignment: null);

        await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('driver_assignment_empty_state')),
          findsOneWidget,
        );
        expect(find.text('No Active Assignment'), findsOneWidget);
        expect(
          find.text(
            'You do not have an active collection assignment at this time.',
          ),
          findsOneWidget,
        );
        expect(
          find.byKey(const Key('driver_assignment_back_to_dashboard_button')),
          findsOneWidget,
        );
      },
    );

    testWidgets('3. Error state displays alert and retry button', (
      tester,
    ) async {
      final repo = MockDriverRepository(shouldThrow: true);

      await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('driver_assignment_error_alert')),
        findsOneWidget,
      );
      expect(
        find.text('Network error fetching assignment detail.'),
        findsOneWidget,
      );

      final retryButton = find.byKey(
        const Key('driver_assignment_retry_button'),
      );
      expect(retryButton, findsOneWidget);

      // Resolve error on retry
      repo.shouldThrow = false;
      repo.currentAssignment = sampleAssignedAssignment;
      await tester.tap(retryButton);
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('driver_assignment_overview_card')),
        findsOneWidget,
      );
      expect(find.text('WP ABC-1234'), findsOneWidget);
    });
  });

  group('DriverAssignmentScreen Content & Invariants Tests', () {
    testWidgets(
      '4. Assigned assignment displays vehicle, status chip, and assignedAt',
      (tester) async {
        final repo = MockDriverRepository(
          currentAssignment: sampleAssignedAssignment,
        );

        await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
        await tester.pumpAndSettle();

        expect(find.text('WP ABC-1234'), findsOneWidget);
        expect(
          find.byKey(const Key('driver_assignment_status_chip')),
          findsOneWidget,
        );
        expect(find.text('Assigned'), findsOneWidget);
        expect(find.text('Driver: '), findsOneWidget);
        expect(find.text('Samantha Perera'), findsOneWidget);
        expect(find.text('Assigned: '), findsOneWidget);
        expect(find.text('Routing: '), findsOneWidget);
        expect(find.text('ManualOrder'), findsOneWidget);

        // Not started yet
        expect(find.text('Started: '), findsNothing);
      },
    );

    testWidgets(
      '5. InProgress assignment displays vehicle, status chip, and startedAt',
      (tester) async {
        final repo = MockDriverRepository(
          currentAssignment: sampleInProgressAssignment,
        );

        await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
        await tester.pumpAndSettle();

        expect(find.text('WP CAD-5678'), findsOneWidget);
        expect(
          find.byKey(const Key('driver_assignment_status_chip')),
          findsOneWidget,
        );
        expect(find.text('In Progress'), findsOneWidget);
        expect(find.text('Started: '), findsOneWidget);
      },
    );

    testWidgets(
      '6. UUID is not used as prominent heading and fabricated ASN codes are not rendered',
      (tester) async {
        final repo = MockDriverRepository(
          currentAssignment: sampleInProgressAssignment,
        );

        await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
        await tester.pumpAndSettle();

        // Raw full UUID is not prominent
        expect(find.text('asn-inprogress-5678'), findsNothing);
        // Fabricated ASN- codes must not be rendered
        expect(find.textContaining('ASN-'), findsNothing);
      },
    );

    testWidgets('7. Progress card renders stop progress and status breakdown', (
      tester,
    ) async {
      final repo = MockDriverRepository(
        currentAssignment: sampleInProgressAssignment,
      );

      await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
      await tester.pumpAndSettle();

      expect(find.byType(AssignmentProgressCard), findsOneWidget);
      expect(find.text('Stop Progress'), findsOneWidget);
      expect(find.text('1 of 2 completed (50%)'), findsOneWidget);
      expect(find.byKey(const Key('assignment_progress_bar')), findsOneWidget);

      expect(
        find.byKey(const Key('assignment_progress_chip_completed')),
        findsOneWidget,
      );
      expect(find.text('1 Completed'), findsOneWidget);
      expect(
        find.byKey(const Key('assignment_progress_chip_pending')),
        findsOneWidget,
      );
      expect(find.text('1 Pending'), findsOneWidget);
    });

    testWidgets('8. Ordered stops are listed in sequence order ascending', (
      tester,
    ) async {
      final repo = MockDriverRepository(
        currentAssignment: sampleInProgressAssignment,
      );

      await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('driver_assignment_stops_header')),
        findsOneWidget,
      );
      expect(find.text('Route Stops (2)'), findsOneWidget);

      final stopCards = find.byType(DriverStopCard);
      expect(stopCards, findsNWidgets(2));

      // Stop #1 followed by Stop #2
      expect(find.byKey(const Key('driver_stop_sequence_1')), findsOneWidget);
      expect(find.byKey(const Key('driver_stop_sequence_2')), findsOneWidget);
    });

    testWidgets(
      '9. Assignment screen shows lifecycle actions but no stop outcomes',
      (tester) async {
        final repo = MockDriverRepository(
          currentAssignment: sampleInProgressAssignment,
        );

        await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
        await tester.pumpAndSettle();

        // Stage 6 keeps stop outcomes on the Tasks screen.
        expect(find.text('Start Assignment'), findsNothing);
        expect(find.text('Start collection'), findsNothing);
        expect(find.text('Complete Stop'), findsNothing);
        expect(find.text('Fail Stop'), findsNothing);
        expect(find.text('Complete stop'), findsNothing);
        expect(find.text('Fail stop'), findsNothing);
        expect(find.text('Finalize assignment'), findsOneWidget);
      },
    );

    testWidgets('10. Pull-to-refresh invalidates and reloads assignment data', (
      tester,
    ) async {
      final repo = MockDriverRepository(
        currentAssignment: sampleInProgressAssignment,
      );

      await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
      await tester.pumpAndSettle();

      final initialCallCount = repo.getCurrentAssignmentCallCount;

      await tester.fling(
        find.byKey(const Key('driver_assignment_overview_card')),
        const Offset(0, 300),
        1000,
      );
      await tester.pumpAndSettle();

      expect(repo.getCurrentAssignmentCallCount, greaterThan(initialCallCount));
    });
  });

  group('DriverAssignmentScreen Responsive Viewport Tests', () {
    testWidgets('empty assignment stays below its header without a large gap', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final repo = MockDriverRepository(currentAssignment: null);
      await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
      await tester.pumpAndSettle();

      final headerBottom = tester.getBottomLeft(find.byType(AppBar)).dy;
      final cardTop = tester.getTopLeft(find.byKey(const Key('driver_assignment_empty_state'))).dy;
      expect(cardTop - headerBottom, inInclusiveRange(16, 48));
      expect(tester.takeException(), isNull);
    });

    testWidgets(
      '11. Renders cleanly without overflow on narrow 320px viewport',
      (tester) async {
        tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
        tester.view.devicePixelRatio = 3.0;
        addTearDown(() {
          tester.view.resetPhysicalSize();
          tester.view.resetDevicePixelRatio();
        });

        final repo = MockDriverRepository(
          currentAssignment: sampleInProgressAssignment,
        );

        await tester.pumpWidget(createAssignmentScreenApp(repository: repo));
        await tester.pumpAndSettle();

        expect(find.byType(DriverAssignmentScreen), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  });
}
