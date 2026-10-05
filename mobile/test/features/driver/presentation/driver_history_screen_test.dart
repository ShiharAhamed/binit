import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/driver/data/driver_repository.dart';
import 'package:mobile/features/driver/models/assignment_detail_model.dart';
import 'package:mobile/features/driver/models/assignment_summary_model.dart';
import 'package:mobile/features/driver/models/assignment_task_model.dart';
import 'package:mobile/features/driver/models/collection_assignment_status.dart';
import 'package:mobile/features/driver/models/paged_assignments_model.dart';
import 'package:mobile/features/driver/models/route_read_model.dart';
import 'package:mobile/features/driver/models/route_stop_model.dart';
import 'package:mobile/features/driver/models/route_stop_status.dart';
import 'package:mobile/features/driver/presentation/driver_history_detail_screen.dart';
import 'package:mobile/features/driver/presentation/driver_history_screen.dart';

class _HistoryRepository extends DriverRepository {
  final List<AssignmentSummaryModel> summaries;
  final AssignmentDetailModel detail;
  final List<int> pagesRequested = [];

  _HistoryRepository({required this.summaries, required this.detail});

  @override
  Future<PagedAssignmentsModel> getMyAssignments({
    CollectionAssignmentStatus? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    pagesRequested.add(page);
    final matching = status == null
        ? summaries
        : summaries.where((item) => item.status == status).toList();
    return PagedAssignmentsModel(
      items: matching,
      page: page,
      pageSize: pageSize,
      totalCount: matching.length,
      totalPages: page == 1 ? 2 : 2,
    );
  }

  @override
  Future<AssignmentDetailModel> getAssignmentDetail(String assignmentId) async => detail;
}

AssignmentSummaryModel _summary(
  String id,
  CollectionAssignmentStatus status,
) =>
    AssignmentSummaryModel(
      id: id,
      status: status,
      driverId: 'driver-1',
      driverName: 'Driver One',
      vehicleId: 'vehicle-1',
      vehicleRegistrationNumber: 'WP CAB-1234',
      stopCount: 2,
      completedStopCount: status == CollectionAssignmentStatus.failed ? 0 : 1,
      failedStopCount: status == CollectionAssignmentStatus.completed ? 0 : 1,
      assignedAt: DateTime.utc(2026, 9, 26, 8),
    );

AssignmentDetailModel _detail() {
  final reportTask = AssignmentTaskModel(
    id: 'task-1',
    taskCode: 'TSK-1',
    targetType: 'Report',
    wasteReportId: 'report-1',
    collectionReason: 'Scheduled',
    status: 'Failed',
    scheduledAt: DateTime.utc(2026, 9, 26, 8),
    addressText: '12 Lake Road',
  );
  final binTask = AssignmentTaskModel(
    id: 'task-2',
    taskCode: 'TSK-2',
    targetType: 'Bin',
    wasteBinId: 'bin-1',
    collectionReason: 'Scheduled',
    status: 'Completed',
    scheduledAt: DateTime.utc(2026, 9, 26, 9),
    addressText: 'Market Street',
  );
  return AssignmentDetailModel(
    id: 'history-assignment',
    status: CollectionAssignmentStatus.partiallyCompleted,
    driverId: 'driver-1',
    driverName: 'Driver One',
    vehicleId: 'vehicle-1',
    vehicleRegistrationNumber: 'WP CAB-1234',
    stopCount: 2,
    completedStopCount: 1,
    failedStopCount: 1,
    assignedAt: DateTime.utc(2026, 9, 26, 8),
    route: RouteReadModel(
      id: 'route-1',
      collectionAssignmentId: 'history-assignment',
      routingMethod: 'ManualOrder',
      stops: [
        RouteStopModel(
          id: 'stop-2',
          sequence: 2,
          status: RouteStopStatus.completed,
          completedAt: DateTime.utc(2026, 9, 26, 10),
          task: binTask,
        ),
        RouteStopModel(
          id: 'stop-1',
          sequence: 1,
          status: RouteStopStatus.failed,
          failedAt: DateTime.utc(2026, 9, 26, 9),
          failureReason: 'Access was safely blocked at the collection point.',
          task: reportTask,
        ),
      ],
    ),
  );
}

Widget _app(Widget child, DriverRepository repository) => ProviderScope(
      overrides: [driverRepositoryProvider.overrideWithValue(repository)],
      child: MaterialApp(home: child),
    );

void main() {
  group('Driver history', () {
    testWidgets('shows terminal results without exposing internal identifiers', (tester) async {
      final detail = _detail();
      final repository = _HistoryRepository(
        summaries: [
          _summary('active-asn', CollectionAssignmentStatus.inProgress),
          _summary('completed-asn', CollectionAssignmentStatus.completed),
          _summary('failed-asn', CollectionAssignmentStatus.failed),
        ],
        detail: detail,
      );

      await tester.pumpWidget(_app(const DriverHistoryScreen(), repository));
      await tester.pumpAndSettle();

      expect(find.text('Completed'), findsOneWidget);
      expect(find.text('Failed'), findsOneWidget);
      expect(find.text('In Progress'), findsNothing);
      expect(find.textContaining('completed-asn'), findsNothing);
      expect(find.text('WP CAB-1234'), findsNWidgets(2));
    });

    testWidgets('uses server pagination when moving to the next results page', (tester) async {
      final repository = _HistoryRepository(
        summaries: [_summary('completed-asn', CollectionAssignmentStatus.completed)],
        detail: _detail(),
      );

      await tester.pumpWidget(_app(const DriverHistoryScreen(), repository));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('driver_history_next_page')));
      await tester.pumpAndSettle();

      expect(repository.pagesRequested, containsAllInOrder([1, 2]));
    });

    testWidgets('renders ordered historical stops and never exposes execution actions', (tester) async {
      final repository = _HistoryRepository(
        summaries: [_summary('partial-asn', CollectionAssignmentStatus.partiallyCompleted)],
        detail: _detail(),
      );

      await tester.pumpWidget(_app(
        const DriverHistoryDetailScreen(assignmentId: 'history-assignment'),
        repository,
      ));
      await tester.pumpAndSettle();

      expect(find.text('Partially Completed'), findsOneWidget);
      expect(find.textContaining('Access was safely blocked'), findsOneWidget);
      expect(find.byKey(const Key('driver_stop_sequence_1')), findsOneWidget);
      await tester.scrollUntilVisible(
        find.byKey(const Key('driver_stop_sequence_2')),
        240,
        scrollable: find.byType(Scrollable),
      );
      expect(find.byKey(const Key('driver_stop_sequence_2')), findsOneWidget);
      expect(find.text('Complete stop'), findsNothing);
      expect(find.text('Fail stop'), findsNothing);
      expect(find.textContaining('history-assignment'), findsNothing);
    });
  });
}
