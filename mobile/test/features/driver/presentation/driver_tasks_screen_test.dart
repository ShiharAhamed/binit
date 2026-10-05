import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
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
import 'package:mobile/features/driver/presentation/driver_tasks_screen.dart';
import 'package:mobile/features/driver/presentation/widgets/driver_stop_card.dart';
import 'package:mobile/shared/widgets/app_button.dart';

class MockDriverRepository extends DriverRepository {
  AssignmentDetailModel? currentAssignment;
  bool shouldThrow = false;
  int getCurrentAssignmentCallCount = 0;
  int failStopCalls = 0;
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
        message: 'Network error fetching tasks.',
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
  Future<AssignmentDetailModel> failStop(
    String assignmentId,
    String stopId,
    String reason,
  ) async {
    failStopCalls++;
    final assignment = currentAssignment!;
    final route = assignment.route!;
    final stops = route.stops
        .map(
          (stop) => stop.id == stopId
              ? RouteStopModel(
                  id: stop.id,
                  sequence: stop.sequence,
                  status: RouteStopStatus.failed,
                  failedAt: DateTime.utc(2026, 9, 26, 12),
                  failureReason: reason,
                  task: stop.task,
                  history: stop.history,
                )
              : stop,
        )
        .toList(growable: false);
    currentAssignment = AssignmentDetailModel(
      id: assignment.id,
      status: assignment.status,
      driverId: assignment.driverId,
      driverName: assignment.driverName,
      vehicleId: assignment.vehicleId,
      vehicleRegistrationNumber: assignment.vehicleRegistrationNumber,
      stopCount: assignment.stopCount,
      completedStopCount: assignment.completedStopCount,
      failedStopCount: assignment.failedStopCount + 1,
      assignedAt: assignment.assignedAt,
      route: RouteReadModel(
        id: route.id,
        collectionAssignmentId: route.collectionAssignmentId,
        routingMethod: route.routingMethod,
        routeGeometry: route.routeGeometry,
        estimatedDistanceMeters: route.estimatedDistanceMeters,
        estimatedDurationSeconds: route.estimatedDurationSeconds,
        stops: stops,
      ),
      history: assignment.history,
    );
    return currentAssignment!;
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
  final testScheduledAt = DateTime.utc(2026, 9, 25, 9, 30);
  final testCompletedAt = DateTime.utc(2026, 9, 25, 10, 15);
  final testFailedAt = DateTime.utc(2026, 9, 25, 11, 0);

  final sampleStops = [
    // Delivered deliberately in scrambled order to verify sequence sorting
    RouteStopModel(
      id: 'stop-3',
      sequence: 3,
      status: RouteStopStatus.pending,
      task: AssignmentTaskModel(
        id: 'task-3',
        taskCode: 'TSK-20260925-003',
        targetType: 'Report',
        wasteReportId: 'rep-3',
        collectionReason: 'Hazardous Waste',
        status: 'Scheduled',
        scheduledAt: testScheduledAt,
        addressText: '78 High Street, Colombo 03',
        latitude: 6.9150,
        longitude: 79.8600,
      ),
    ),
    RouteStopModel(
      id: 'stop-1',
      sequence: 1,
      status: RouteStopStatus.completed,
      completedAt: testCompletedAt,
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
    RouteStopModel(
      id: 'stop-4',
      sequence: 4,
      status: RouteStopStatus.skipped,
      task: AssignmentTaskModel(
        id: 'task-4',
        taskCode: 'TSK-20260925-004',
        targetType: 'Bin',
        wasteBinId: 'bin-4',
        collectionReason: 'Routine Cycle',
        status: 'Skipped',
        scheduledAt: testScheduledAt,
        addressText: '100 Galle Face, Colombo 03',
        latitude: 6.9200,
        longitude: 79.8450,
      ),
    ),
    RouteStopModel(
      id: 'stop-2',
      sequence: 2,
      status: RouteStopStatus.failed,
      failedAt: testFailedAt,
      failureReason: 'Gate locked, inaccessible property',
      task: AssignmentTaskModel(
        id: 'task-2',
        taskCode: 'TSK-20260925-002',
        targetType: 'Report',
        wasteReportId: 'rep-2',
        collectionReason: 'Illegal Dumping',
        status: 'Failed',
        scheduledAt: testScheduledAt,
        addressText: '45 Cross Street, Colombo 02',
        latitude: 6.9300,
        longitude: 79.8550,
      ),
    ),
  ];

  final sampleAssignment = AssignmentDetailModel(
    id: 'asn-real-1234',
    status: CollectionAssignmentStatus.inProgress,
    driverId: 'driver-1',
    driverName: 'Samantha Perera',
    vehicleId: 'veh-1',
    vehicleRegistrationNumber: 'WP CAD-5678',
    stopCount: 4,
    completedStopCount: 1,
    failedStopCount: 1,
    assignedAt: DateTime.utc(2026, 9, 25, 8, 0),
    route: RouteReadModel(
      id: 'route-1',
      collectionAssignmentId: 'asn-real-1234',
      routingMethod: 'ManualOrder',
      stops: sampleStops,
    ),
  );

  Widget createTasksScreenApp({required MockDriverRepository repository}) {
    return ProviderScope(
      overrides: [driverRepositoryProvider.overrideWithValue(repository)],
      child: const MaterialApp(home: Scaffold(body: DriverTasksScreen())),
    );
  }

  group('DriverTasksScreen State Tests', () {
    testWidgets('1. Loading state displays standardized loading indicator', (
      tester,
    ) async {
      final repo = MockDriverRepository();
      repo.hangingCompleter = Completer<AssignmentDetailModel?>();

      await tester.pumpWidget(createTasksScreenApp(repository: repo));
      await tester.pump();

      expect(find.text('Loading collection tasks...'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets(
      '2. Empty state renders clean message when no active assignment exists',
      (tester) async {
        final repo = MockDriverRepository(currentAssignment: null);

        await tester.pumpWidget(createTasksScreenApp(repository: repo));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('driver_tasks_empty_state')),
          findsOneWidget,
        );
        expect(find.text('No collection tasks assigned'), findsOneWidget);
        expect(
          find.text(
            'You do not have an active collection assignment at this time. New tasks will appear here when an assignment is dispatched.',
          ),
          findsOneWidget,
        );
        // No stop cards or fake tasks
        expect(find.byType(DriverStopCard), findsNothing);
      },
    );

    testWidgets('3. Error state displays alert and retry button', (
      tester,
    ) async {
      final repo = MockDriverRepository(shouldThrow: true);

      await tester.pumpWidget(createTasksScreenApp(repository: repo));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_tasks_error_alert')), findsOneWidget);
      expect(find.text('Network error fetching tasks.'), findsOneWidget);

      final retryButton = find.byKey(const Key('driver_tasks_retry_button'));
      expect(retryButton, findsOneWidget);

      // Trigger retry after fixing error
      repo.shouldThrow = false;
      repo.currentAssignment = sampleAssignment;
      await tester.tap(retryButton);
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('driver_tasks_context_banner')),
        findsOneWidget,
      );
      expect(find.text('WP CAD-5678'), findsOneWidget);
    });
  });

  group('DriverTasksScreen Ordered Stops & Stop Cards Tests', () {
    testWidgets(
      '4. Stops are rendered strictly in sequence order ascending (1, 2, 3, 4)',
      (tester) async {
        final repo = MockDriverRepository(currentAssignment: sampleAssignment);

        await tester.pumpWidget(createTasksScreenApp(repository: repo));
        await tester.pumpAndSettle();

        // Find all stop cards
        final stopCards = find.byType(DriverStopCard);
        expect(stopCards, findsNWidgets(4));

        // Verify sequence ordering #1, #2, #3, #4
        expect(find.byKey(const Key('driver_stop_sequence_1')), findsOneWidget);
        expect(find.byKey(const Key('driver_stop_sequence_2')), findsOneWidget);
        expect(find.byKey(const Key('driver_stop_sequence_3')), findsOneWidget);
        expect(find.byKey(const Key('driver_stop_sequence_4')), findsOneWidget);

        // Verify task codes are rendered as prominent titles
        expect(find.text('TSK-20260925-001'), findsOneWidget);
        expect(find.text('TSK-20260925-002'), findsOneWidget);
        expect(find.text('TSK-20260925-003'), findsOneWidget);
        expect(find.text('TSK-20260925-004'), findsOneWidget);

        // Verify raw UUIDs are not used as main headings
        expect(find.text('task-1'), findsNothing);
        expect(find.text('asn-real-1234'), findsNothing);
      },
    );

    testWidgets('5. Displays target types: Waste Bin vs Citizen Report', (
      tester,
    ) async {
      final repo = MockDriverRepository(currentAssignment: sampleAssignment);

      await tester.pumpWidget(createTasksScreenApp(repository: repo));
      await tester.pumpAndSettle();

      // Bin targets (stops 1 & 4) and Report targets (stops 2 & 3)
      expect(find.text('Waste Bin'), findsNWidgets(2));
      expect(find.text('Citizen Report'), findsNWidgets(2));
    });

    testWidgets('6. Displays addresses and collection reasons', (tester) async {
      final repo = MockDriverRepository(currentAssignment: sampleAssignment);

      await tester.pumpWidget(createTasksScreenApp(repository: repo));
      await tester.pumpAndSettle();

      expect(find.text('12 Main Road, Colombo 01'), findsOneWidget);
      expect(find.text('Reason: Overflowing Bin'), findsOneWidget);

      expect(find.text('45 Cross Street, Colombo 02'), findsOneWidget);
      expect(find.text('Reason: Illegal Dumping'), findsOneWidget);
    });

    testWidgets('7. Displays all 4 route stop status badges correctly', (
      tester,
    ) async {
      final repo = MockDriverRepository(currentAssignment: sampleAssignment);

      await tester.pumpWidget(createTasksScreenApp(repository: repo));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('stop_status_badge_completed')),
        findsOneWidget,
      );
      expect(find.byKey(const Key('stop_status_badge_failed')), findsOneWidget);
      expect(
        find.byKey(const Key('stop_status_badge_pending')),
        findsOneWidget,
      );
      expect(
        find.byKey(const Key('stop_status_badge_skipped')),
        findsOneWidget,
      );
    });

    testWidgets(
      '8. Failed stop renders compact failure box with reason and timestamp',
      (tester) async {
        final repo = MockDriverRepository(currentAssignment: sampleAssignment);

        await tester.pumpWidget(createTasksScreenApp(repository: repo));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('driver_stop_failed_box_2')),
          findsOneWidget,
        );
        expect(
          find.text('Failed: Gate locked, inaccessible property'),
          findsOneWidget,
        );
        expect(find.textContaining('Failed at:'), findsOneWidget);
      },
    );

    testWidgets('9. Completed stop renders completion timestamp info', (
      tester,
    ) async {
      final repo = MockDriverRepository(currentAssignment: sampleAssignment);

      await tester.pumpWidget(createTasksScreenApp(repository: repo));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('driver_stop_completed_box_1')),
        findsOneWidget,
      );
      expect(find.textContaining('Completed at:'), findsOneWidget);
    });

    testWidgets(
      '10. In-progress pending stops expose only their outcome actions',
      (tester) async {
        final repo = MockDriverRepository(currentAssignment: sampleAssignment);

        await tester.pumpWidget(createTasksScreenApp(repository: repo));
        await tester.pumpAndSettle();

        // Stage 6: only the pending stop exposes outcome controls.
        expect(find.text('Start Assignment'), findsNothing);
        expect(find.text('Start Collection'), findsNothing);
        expect(find.text('Complete stop'), findsOneWidget);
        expect(find.text('Fail stop'), findsOneWidget);
        expect(find.text('Finalize'), findsNothing);
      },
    );

    testWidgets('pending stop actions stack with matching geometry at 320px', (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final repo = MockDriverRepository(currentAssignment: sampleAssignment);
      await tester.pumpWidget(createTasksScreenApp(repository: repo));
      await tester.pumpAndSettle();

      final fail = find.byKey(const Key('driver_stop_fail_3'));
      final complete = find.byKey(const Key('driver_stop_complete_3'));
      expect(fail, findsOneWidget);
      expect(complete, findsOneWidget);
      final failRect = tester.getRect(fail);
      final completeRect = tester.getRect(complete);
      expect(failRect.top, lessThan(completeRect.top));
      expect(failRect.left, completeRect.left);
      expect(failRect.width, completeRect.width);
      expect(failRect.height, completeRect.height);
      expect(tester.widget<AppButton>(fail).variant, AppButtonVariant.destructive);
      expect(tester.widget<AppButton>(complete).variant, AppButtonVariant.primary);
      expect(tester.widget<AppButton>(fail).height, tester.widget<AppButton>(complete).height);
      expect(tester.takeException(), isNull);
    });

    testWidgets(
      '11. failing a stop closes its dialog safely after the authoritative refresh',
      (tester) async {
        final repo = MockDriverRepository(currentAssignment: sampleAssignment);
        await tester.pumpWidget(createTasksScreenApp(repository: repo));
        await tester.pumpAndSettle();

        final failStopButton = find.byKey(const Key('driver_stop_fail_3'));
        await tester.ensureVisible(failStopButton);
        await tester.tap(failStopButton);
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byKey(const Key('driver_fail_stop_reason_field')),
          'Access to the collection point is safely blocked.',
        );
        await tester.tap(
          find.byKey(const Key('driver_confirm_fail_stop_button')),
        );
        await tester.pumpAndSettle();

        expect(repo.failStopCalls, 1);
        expect(find.byType(AlertDialog), findsNothing);
        expect(find.text('Stop marked as failed.'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );

    testWidgets('12. Pull-to-refresh invalidates and reloads assignment data', (
      tester,
    ) async {
      final repo = MockDriverRepository(currentAssignment: sampleAssignment);

      await tester.pumpWidget(createTasksScreenApp(repository: repo));
      await tester.pumpAndSettle();

      final initialCallCount = repo.getCurrentAssignmentCallCount;

      await tester.fling(
        find.byKey(const Key('driver_tasks_context_banner')),
        const Offset(0, 300),
        1000,
      );
      await tester.pumpAndSettle();

      expect(repo.getCurrentAssignmentCallCount, greaterThan(initialCallCount));
    });
  });

  group('DriverTasksScreen Responsive Viewport Tests', () {
    testWidgets(
      '12. Renders cleanly without overflow on narrow 320px viewport',
      (tester) async {
        tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
        tester.view.devicePixelRatio = 3.0;
        addTearDown(() {
          tester.view.resetPhysicalSize();
          tester.view.resetDevicePixelRatio();
        });

        final repo = MockDriverRepository(currentAssignment: sampleAssignment);

        await tester.pumpWidget(createTasksScreenApp(repository: repo));
        await tester.pumpAndSettle();

        expect(find.byType(DriverTasksScreen), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  });
}
