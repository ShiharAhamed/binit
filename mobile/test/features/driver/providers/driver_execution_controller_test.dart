import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/driver/data/driver_repository.dart';
import 'package:mobile/features/driver/models/assignment_detail_model.dart';
import 'package:mobile/features/driver/models/assignment_task_model.dart';
import 'package:mobile/features/driver/models/collection_assignment_status.dart';
import 'package:mobile/features/driver/models/route_read_model.dart';
import 'package:mobile/features/driver/models/route_stop_model.dart';
import 'package:mobile/features/driver/models/route_stop_status.dart';
import 'package:mobile/features/driver/providers/driver_dashboard_providers.dart';
import 'package:mobile/features/driver/providers/driver_execution_controller.dart';

class _ExecutionRepository extends DriverRepository {
  _ExecutionRepository(this.currentAssignment);

  AssignmentDetailModel? currentAssignment;
  int currentAssignmentReads = 0;
  int completeCalls = 0;
  bool throwConflict = false;

  @override
  Future<AssignmentDetailModel?> getCurrentAssignmentDetail() async {
    currentAssignmentReads++;
    return currentAssignment;
  }

  @override
  Future<AssignmentDetailModel> completeStop(
    String assignmentId,
    String stopId,
  ) async {
    completeCalls++;
    if (throwConflict) {
      throw const ApiException(
        message: 'Stop was already processed.',
        statusCode: 409,
      );
    }
    currentAssignment = _assignment(
      CollectionAssignmentStatus.inProgress,
      RouteStopStatus.completed,
    );
    return currentAssignment!;
  }
}

AssignmentDetailModel _assignment(
  CollectionAssignmentStatus status,
  RouteStopStatus stopStatus,
) {
  return AssignmentDetailModel(
    id: 'assignment-1',
    status: status,
    driverId: 'driver-1',
    driverName: 'Driver One',
    vehicleId: 'vehicle-1',
    vehicleRegistrationNumber: 'WP-TEST-1',
    stopCount: 1,
    completedStopCount: stopStatus.isCompleted ? 1 : 0,
    failedStopCount: stopStatus.isFailed ? 1 : 0,
    assignedAt: DateTime.utc(2026, 9, 26),
    route: RouteReadModel(
      id: 'route-1',
      collectionAssignmentId: 'assignment-1',
      routingMethod: 'ManualOrder',
      stops: [
        RouteStopModel(
          id: 'stop-1',
          sequence: 1,
          status: stopStatus,
          task: AssignmentTaskModel(
            id: 'task-1',
            taskCode: 'TASK-1',
            targetType: 'Bin',
            wasteBinId: 'bin-1',
            collectionReason: 'Scheduled',
            status: 'InProgress',
            scheduledAt: DateTime.utc(2026, 9, 26),
          ),
        ),
      ],
    ),
  );
}

void main() {
  test('complete-stop makes one request then refreshes authoritative assignment state', () async {
    final repository = _ExecutionRepository(
      _assignment(
        CollectionAssignmentStatus.inProgress,
        RouteStopStatus.pending,
      ),
    );
    final container = ProviderContainer(
      overrides: [driverRepositoryProvider.overrideWithValue(repository)],
    );
    addTearDown(container.dispose);

    await container.read(driverCurrentAssignmentProvider.future);
    final succeeded = await container
        .read(driverExecutionControllerProvider.notifier)
        .completeStop('assignment-1', 'stop-1');

    expect(succeeded, isTrue);
    expect(repository.completeCalls, 1);
    expect(repository.currentAssignmentReads, greaterThanOrEqualTo(2));
    final refreshed = await container.read(
      driverCurrentAssignmentProvider.future,
    );
    expect(refreshed!.route!.stops.single.status, RouteStopStatus.completed);
    expect(
      container.read(driverExecutionControllerProvider).isLoading,
      isFalse,
    );
    expect(
      container.read(driverExecutionControllerProvider).wasSuccessful,
      isTrue,
    );
  });

  test('conflict refreshes state without retrying the mutation', () async {
    final repository = _ExecutionRepository(
      _assignment(
        CollectionAssignmentStatus.inProgress,
        RouteStopStatus.pending,
      ),
    )..throwConflict = true;
    final container = ProviderContainer(
      overrides: [driverRepositoryProvider.overrideWithValue(repository)],
    );
    addTearDown(container.dispose);

    await container.read(driverCurrentAssignmentProvider.future);
    final succeeded = await container
        .read(driverExecutionControllerProvider.notifier)
        .completeStop('assignment-1', 'stop-1');

    expect(succeeded, isFalse);
    expect(repository.completeCalls, 1);
    expect(repository.currentAssignmentReads, greaterThanOrEqualTo(2));
    final state = container.read(driverExecutionControllerProvider);
    expect(state.error, isA<ApiException>());
    expect(
      driverExecutionErrorMessage(state.error),
      contains('latest assignment state has been refreshed'),
    );
  });
}
