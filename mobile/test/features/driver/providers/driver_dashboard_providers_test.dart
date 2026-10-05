import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
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
import 'package:mobile/features/driver/providers/driver_dashboard_providers.dart';

class MockTestDriverRepository extends DriverRepository {
  DriverSelfModel? mockSelf;
  AssignmentDetailModel? mockCurrentAssignment;
  bool shouldThrowAvailability = false;
  DriverAvailabilityStatus? recordedStatus;

  @override
  Future<DriverSelfModel> getDriverSelf(String driverId) async {
    return mockSelf ??
        DriverSelfModel(
          id: driverId,
          displayName: 'Test Driver',
          availabilityStatus: DriverAvailabilityStatus.available,
          isOccupied: false,
        );
  }

  @override
  Future<DriverSelfModel> updateAvailability(DriverAvailabilityStatus status) async {
    if (shouldThrowAvailability) {
      throw const ApiException(message: 'Update failed on backend', statusCode: 400);
    }
    recordedStatus = status;
    mockSelf = DriverSelfModel(
      id: mockSelf?.id ?? 'drv-1',
      displayName: mockSelf?.displayName ?? 'Test Driver',
      availabilityStatus: status,
      isOccupied: mockSelf?.isOccupied ?? false,
    );
    return mockSelf!;
  }

  @override
  Future<AssignmentDetailModel?> getCurrentAssignmentDetail() async {
    return mockCurrentAssignment;
  }

  @override
  Future<AssignmentDetailModel> getAssignmentDetail(String assignmentId) async {
    if (mockCurrentAssignment != null) return mockCurrentAssignment!;
    throw const ApiException(message: 'Not found', statusCode: 404);
  }

  @override
  Future<AssignmentSummaryModel?> getCurrentAssignmentSummary() async {
    return mockCurrentAssignment?.toSummary();
  }

  @override
  Future<PagedAssignmentsModel> getMyAssignments({
    CollectionAssignmentStatus? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    return PagedAssignmentsModel(
      items: mockCurrentAssignment != null ? [mockCurrentAssignment!.toSummary()] : [],
      page: page,
      pageSize: pageSize,
      totalCount: mockCurrentAssignment != null ? 1 : 0,
      totalPages: 1,
    );
  }

  @override
  Future<RouteReadModel> getRoute(String routeId) async {
    throw UnimplementedError();
  }
}

class TestAuthNotifier extends AuthNotifier {
  final AuthUser? _user;
  TestAuthNotifier([this._user]);

  @override
  AuthState build() {
    if (_user != null) {
      return AuthState.authenticated(_user);
    }
    return const AuthState.unauthenticated();
  }
}

AssignmentTaskModel _createSampleTask(String id, String code) {
  return AssignmentTaskModel(
    id: id,
    taskCode: code,
    targetType: 'Bin',
    wasteBinId: 'bin-1',
    collectionReason: 'Scheduled',
    status: 'Scheduled',
    scheduledAt: DateTime.utc(2026, 9, 26, 8, 0),
    latitude: 6.9271,
    longitude: 79.8612,
  );
}

void main() {
  group('DriverStopProgress Unit Tests', () {
    test('calculates correct stop counts from RouteReadModel.stops including Skipped', () {
      final assignment = AssignmentDetailModel(
        id: 'asn-101',
        status: CollectionAssignmentStatus.inProgress,
        driverId: 'drv-1',
        driverName: 'Samantha Perera',
        vehicleId: 'veh-1',
        vehicleRegistrationNumber: 'WP CAD-5678',
        stopCount: 5,
        completedStopCount: 2,
        failedStopCount: 1,
        assignedAt: DateTime.utc(2026, 9, 26, 8, 0),
        route: RouteReadModel(
          id: 'route-1',
          collectionAssignmentId: 'asn-101',
          routingMethod: 'ManualOrder',
          stops: [
            RouteStopModel(
              id: 'stop-1',
              sequence: 1,
              status: RouteStopStatus.completed,
              task: _createSampleTask('task-1', 'TSK-001'),
            ),
            RouteStopModel(
              id: 'stop-2',
              sequence: 2,
              status: RouteStopStatus.completed,
              task: _createSampleTask('task-2', 'TSK-002'),
            ),
            RouteStopModel(
              id: 'stop-3',
              sequence: 3,
              status: RouteStopStatus.failed,
              task: _createSampleTask('task-3', 'TSK-003'),
            ),
            RouteStopModel(
              id: 'stop-4',
              sequence: 4,
              status: RouteStopStatus.skipped,
              task: _createSampleTask('task-4', 'TSK-004'),
            ),
            RouteStopModel(
              id: 'stop-5',
              sequence: 5,
              status: RouteStopStatus.pending,
              task: _createSampleTask('task-5', 'TSK-005'),
            ),
          ],
        ),
      );

      final progress = DriverStopProgress.fromAssignment(assignment);

      expect(progress.totalStops, 5);
      expect(progress.completedStops, 2);
      expect(progress.failedStops, 1);
      expect(progress.skippedStops, 1);
      expect(progress.pendingStops, 1);
      expect(progress.progressFraction, (2 + 1 + 1) / 5);
    });

    test('falls back safely to summary properties when route stops are empty', () {
      final assignment = AssignmentDetailModel(
        id: 'asn-102',
        status: CollectionAssignmentStatus.assigned,
        driverId: 'drv-1',
        driverName: 'Samantha Perera',
        vehicleId: 'veh-1',
        vehicleRegistrationNumber: 'WP CAD-5678',
        stopCount: 3,
        completedStopCount: 1,
        failedStopCount: 0,
        assignedAt: DateTime.utc(2026, 9, 26, 8, 0),
        route: null,
      );

      final progress = DriverStopProgress.fromAssignment(assignment);

      expect(progress.totalStops, 3);
      expect(progress.completedStops, 1);
      expect(progress.failedStops, 0);
      expect(progress.pendingStops, 2);
      expect(progress.skippedStops, 0);
      expect(progress.progressFraction, 1 / 3);
    });
  });

  group('Driver Dashboard Providers & Controller Tests', () {
    test('driverSelfProvider returns null when user is unauthenticated', () async {
      final mockRepo = MockTestDriverRepository();
      final container = ProviderContainer(
        overrides: [
          authProvider.overrideWith(() => TestAuthNotifier(null)),
          driverRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );
      addTearDown(container.dispose);

      final result = await container.read(driverSelfProvider.future);
      expect(result, isNull);
    });

    test('driverSelfProvider loads authenticated driver self profile', () async {
      final mockRepo = MockTestDriverRepository();
      const user = AuthUser(
        id: 'driver-123',
        fullName: 'Samantha Perera',
        email: 'samantha@smartwaste.lk',
        role: AppRoles.driver,
      );
      final container = ProviderContainer(
        overrides: [
          authProvider.overrideWith(() => TestAuthNotifier(user)),
          driverRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );
      addTearDown(container.dispose);

      final result = await container.read(driverSelfProvider.future);
      expect(result, isNotNull);
      expect(result!.id, 'driver-123');
      expect(result.availabilityStatus, DriverAvailabilityStatus.available);
      expect(result.isOccupied, isFalse);
    });

    test('driverCurrentAssignmentProvider returns active assignment from repository', () async {
      final mockRepo = MockTestDriverRepository();
      mockRepo.mockCurrentAssignment = AssignmentDetailModel(
        id: 'asn-active',
        status: CollectionAssignmentStatus.inProgress,
        driverId: 'driver-123',
        driverName: 'Samantha Perera',
        vehicleId: 'veh-1',
        vehicleRegistrationNumber: 'WP CAD-1234',
        stopCount: 4,
        completedStopCount: 2,
        failedStopCount: 0,
        assignedAt: DateTime.utc(2026, 9, 26, 9, 0),
      );

      final container = ProviderContainer(
        overrides: [
          driverRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );
      addTearDown(container.dispose);

      final assignment = await container.read(driverCurrentAssignmentProvider.future);
      expect(assignment, isNotNull);
      expect(assignment!.id, 'asn-active');
      expect(assignment.vehicleRegistrationNumber, 'WP CAD-1234');
      expect(assignment.status, CollectionAssignmentStatus.inProgress);
    });

    test('DriverAvailabilityController updates availability and invalidates self provider', () async {
      final mockRepo = MockTestDriverRepository();
      const user = AuthUser(
        id: 'driver-123',
        fullName: 'Samantha Perera',
        email: 'samantha@smartwaste.lk',
        role: AppRoles.driver,
      );
      final container = ProviderContainer(
        overrides: [
          authProvider.overrideWith(() => TestAuthNotifier(user)),
          driverRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );
      addTearDown(container.dispose);

      // Initial read
      final initial = await container.read(driverSelfProvider.future);
      expect(initial!.availabilityStatus, DriverAvailabilityStatus.available);

      // Mutate to off duty
      final controller = container.read(driverAvailabilityControllerProvider.notifier);
      final success = await controller.updateAvailability(DriverAvailabilityStatus.offDuty);

      expect(success, isTrue);
      expect(mockRepo.recordedStatus, DriverAvailabilityStatus.offDuty);

      // Re-read after invalidation
      final updated = await container.read(driverSelfProvider.future);
      expect(updated!.availabilityStatus, DriverAvailabilityStatus.offDuty);
    });

    test('DriverAvailabilityController handles repository failure cleanly', () async {
      final mockRepo = MockTestDriverRepository();
      mockRepo.shouldThrowAvailability = true;
      const user = AuthUser(
        id: 'driver-123',
        fullName: 'Samantha Perera',
        email: 'samantha@smartwaste.lk',
        role: AppRoles.driver,
      );
      final container = ProviderContainer(
        overrides: [
          authProvider.overrideWith(() => TestAuthNotifier(user)),
          driverRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );
      addTearDown(container.dispose);

      final controller = container.read(driverAvailabilityControllerProvider.notifier);
      final success = await controller.updateAvailability(DriverAvailabilityStatus.offDuty);

      expect(success, isFalse);
      final state = container.read(driverAvailabilityControllerProvider);
      expect(state.hasError, isTrue);
    });
  });
}
