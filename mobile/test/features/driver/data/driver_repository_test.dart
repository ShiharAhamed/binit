import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/driver/data/driver_api.dart';
import 'package:mobile/features/driver/data/driver_repository.dart';
import 'package:mobile/features/driver/models/driver_models.dart';

class _FakeDriverApi extends DriverApi {
  DriverSelfModel? selfResult;
  DriverSelfModel? updateAvailabilityResult;
  PagedAssignmentsModel? myAssignmentsResult;
  AssignmentDetailModel? detailResult;
  RouteReadModel? routeResult;
  Exception? errorToThrow;

  // Recorded query arguments
  final List<CollectionAssignmentStatus?> recordedStatuses = [];
  final List<int> recordedPages = [];
  final List<int> recordedPageSizes = [];
  String? recordedDriverId;
  DriverAvailabilityStatus? recordedAvailabilityUpdate;
  String? recordedAssignmentId;
  String? recordedRouteId;

  // Custom mapping for status-based assignment queries
  PagedAssignmentsModel? inProgressAssignments;
  PagedAssignmentsModel? assignedAssignments;

  @override
  Future<DriverSelfModel> getDriverSelf(String driverId) async {
    recordedDriverId = driverId;
    if (errorToThrow != null) throw errorToThrow!;
    return selfResult!;
  }

  @override
  Future<DriverSelfModel> updateAvailability(DriverAvailabilityStatus status) async {
    recordedAvailabilityUpdate = status;
    if (errorToThrow != null) throw errorToThrow!;
    return updateAvailabilityResult!;
  }

  @override
  Future<PagedAssignmentsModel> getMyAssignments({
    CollectionAssignmentStatus? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    recordedStatuses.add(status);
    recordedPages.add(page);
    recordedPageSizes.add(pageSize);
    if (errorToThrow != null) throw errorToThrow!;

    if (status == CollectionAssignmentStatus.inProgress &&
        inProgressAssignments != null) {
      return inProgressAssignments!;
    }
    if (status == CollectionAssignmentStatus.assigned &&
        assignedAssignments != null) {
      return assignedAssignments!;
    }

    return myAssignmentsResult ??
        const PagedAssignmentsModel(
          items: [],
          page: 1,
          pageSize: 20,
          totalCount: 0,
          totalPages: 0,
        );
  }

  @override
  Future<AssignmentDetailModel> getAssignmentDetail(String assignmentId) async {
    recordedAssignmentId = assignmentId;
    if (errorToThrow != null) throw errorToThrow!;
    return detailResult!;
  }

  @override
  Future<RouteReadModel> getRoute(String routeId) async {
    recordedRouteId = routeId;
    if (errorToThrow != null) throw errorToThrow!;
    return routeResult!;
  }
}

void main() {
  group('DriverRepository Unit Tests', () {
    late _FakeDriverApi fakeApi;
    late DriverRepository repository;

    final sampleSummary = AssignmentSummaryModel(
      id: 'asg-active-1',
      status: CollectionAssignmentStatus.inProgress,
      driverId: 'drv-001',
      driverName: 'Samantha Perera',
      vehicleId: 'veh-001',
      vehicleRegistrationNumber: 'WP-NA-4567',
      stopCount: 4,
      completedStopCount: 1,
      failedStopCount: 0,
      assignedAt: DateTime.utc(2026, 9, 25, 8, 30),
    );

    final sampleAssignedSummary = AssignmentSummaryModel(
      id: 'asg-assigned-2',
      status: CollectionAssignmentStatus.assigned,
      driverId: 'drv-001',
      driverName: 'Samantha Perera',
      vehicleId: 'veh-001',
      vehicleRegistrationNumber: 'WP-NA-4567',
      stopCount: 3,
      completedStopCount: 0,
      failedStopCount: 0,
      assignedAt: DateTime.utc(2026, 9, 25, 8, 30),
    );

    setUp(() {
      fakeApi = _FakeDriverApi();
      repository = DriverRepository(api: fakeApi);
    });

    test('getDriverSelf delegates to DriverApi', () async {
      fakeApi.selfResult = const DriverSelfModel(
        id: 'drv-001',
        displayName: 'Samantha Perera',
        availabilityStatus: DriverAvailabilityStatus.available,
        isOccupied: false,
      );

      final result = await repository.getDriverSelf('drv-001');

      expect(fakeApi.recordedDriverId, 'drv-001');
      expect(result.id, 'drv-001');
      expect(result.availabilityStatus, DriverAvailabilityStatus.available);
    });

    test('updateAvailability delegates to DriverApi', () async {
      fakeApi.updateAvailabilityResult = const DriverSelfModel(
        id: 'drv-001',
        displayName: 'Samantha Perera',
        availabilityStatus: DriverAvailabilityStatus.offDuty,
        isOccupied: false,
      );

      final result = await repository.updateAvailability(
        DriverAvailabilityStatus.offDuty,
      );

      expect(
        fakeApi.recordedAvailabilityUpdate,
        DriverAvailabilityStatus.offDuty,
      );
      expect(result.availabilityStatus, DriverAvailabilityStatus.offDuty);
    });

    test('getMyAssignments delegates with parameters to DriverApi', () async {
      fakeApi.myAssignmentsResult = PagedAssignmentsModel(
        items: [sampleSummary],
        page: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1,
      );

      final result = await repository.getMyAssignments(
        status: CollectionAssignmentStatus.inProgress,
        page: 1,
        pageSize: 10,
      );

      expect(fakeApi.recordedStatuses, [CollectionAssignmentStatus.inProgress]);
      expect(fakeApi.recordedPages, [1]);
      expect(fakeApi.recordedPageSizes, [10]);
      expect(result.items.length, 1);
    });

    test('getAssignmentDetail delegates to DriverApi', () async {
      fakeApi.detailResult = AssignmentDetailModel(
        id: 'asg-active-1',
        status: CollectionAssignmentStatus.inProgress,
        driverId: 'drv-001',
        driverName: 'Samantha Perera',
        vehicleId: 'veh-001',
        vehicleRegistrationNumber: 'WP-NA-4567',
        stopCount: 4,
        completedStopCount: 1,
        failedStopCount: 0,
        assignedAt: DateTime.utc(2026, 9, 25, 8, 30),
      );

      final result = await repository.getAssignmentDetail('asg-active-1');

      expect(fakeApi.recordedAssignmentId, 'asg-active-1');
      expect(result.id, 'asg-active-1');
    });

    test('getRoute delegates to DriverApi', () async {
      fakeApi.routeResult = const RouteReadModel(
        id: 'rt-1',
        collectionAssignmentId: 'asg-active-1',
        routingMethod: 'ManualOrder',
        stops: [],
      );

      final result = await repository.getRoute('rt-1');

      expect(fakeApi.recordedRouteId, 'rt-1');
      expect(result.id, 'rt-1');
    });

    group('getCurrentAssignmentSummary priority strategy', () {
      test('returns InProgress assignment first without querying Assigned', () async {
        fakeApi.inProgressAssignments = PagedAssignmentsModel(
          items: [sampleSummary],
          page: 1,
          pageSize: 1,
          totalCount: 1,
          totalPages: 1,
        );
        fakeApi.assignedAssignments = PagedAssignmentsModel(
          items: [sampleAssignedSummary],
          page: 1,
          pageSize: 1,
          totalCount: 1,
          totalPages: 1,
        );

        final active = await repository.getCurrentAssignmentSummary();

        expect(active, isNotNull);
        expect(active!.id, 'asg-active-1');
        expect(active.status, CollectionAssignmentStatus.inProgress);

        // Crucial: Must only have queried InProgress
        expect(fakeApi.recordedStatuses, [CollectionAssignmentStatus.inProgress]);
        expect(fakeApi.recordedPageSizes, [1]);
      });

      test('falls back to Assigned assignment when InProgress is empty', () async {
        fakeApi.inProgressAssignments = const PagedAssignmentsModel(
          items: [],
          page: 1,
          pageSize: 1,
          totalCount: 0,
          totalPages: 0,
        );
        fakeApi.assignedAssignments = PagedAssignmentsModel(
          items: [sampleAssignedSummary],
          page: 1,
          pageSize: 1,
          totalCount: 1,
          totalPages: 1,
        );

        final active = await repository.getCurrentAssignmentSummary();

        expect(active, isNotNull);
        expect(active!.id, 'asg-assigned-2');
        expect(active.status, CollectionAssignmentStatus.assigned);

        // Checked InProgress first, then fell back to Assigned
        expect(fakeApi.recordedStatuses, [
          CollectionAssignmentStatus.inProgress,
          CollectionAssignmentStatus.assigned,
        ]);
        expect(fakeApi.recordedPageSizes, [1, 1]);
      });

      test('returns null when neither InProgress nor Assigned assignment exists', () async {
        fakeApi.inProgressAssignments = const PagedAssignmentsModel(
          items: [],
          page: 1,
          pageSize: 1,
          totalCount: 0,
          totalPages: 0,
        );
        fakeApi.assignedAssignments = const PagedAssignmentsModel(
          items: [],
          page: 1,
          pageSize: 1,
          totalCount: 0,
          totalPages: 0,
        );

        final active = await repository.getCurrentAssignmentSummary();

        expect(active, isNull);
        expect(fakeApi.recordedStatuses, [
          CollectionAssignmentStatus.inProgress,
          CollectionAssignmentStatus.assigned,
        ]);
      });
    });

    group('getCurrentAssignmentDetail helper', () {
      test('fetches detail when active assignment exists', () async {
        fakeApi.inProgressAssignments = PagedAssignmentsModel(
          items: [sampleSummary],
          page: 1,
          pageSize: 1,
          totalCount: 1,
          totalPages: 1,
        );
        fakeApi.detailResult = AssignmentDetailModel(
          id: 'asg-active-1',
          status: CollectionAssignmentStatus.inProgress,
          driverId: 'drv-001',
          driverName: 'Samantha Perera',
          vehicleId: 'veh-001',
          vehicleRegistrationNumber: 'WP-NA-4567',
          stopCount: 4,
          completedStopCount: 1,
          failedStopCount: 0,
          assignedAt: DateTime.utc(2026, 9, 25, 8, 30),
        );

        final detail = await repository.getCurrentAssignmentDetail();

        expect(detail, isNotNull);
        expect(detail!.id, 'asg-active-1');
        expect(fakeApi.recordedAssignmentId, 'asg-active-1');
      });

      test('returns null when no active assignment exists', () async {
        fakeApi.inProgressAssignments = const PagedAssignmentsModel(
          items: [],
          page: 1,
          pageSize: 1,
          totalCount: 0,
          totalPages: 0,
        );
        fakeApi.assignedAssignments = const PagedAssignmentsModel(
          items: [],
          page: 1,
          pageSize: 1,
          totalCount: 0,
          totalPages: 0,
        );

        final detail = await repository.getCurrentAssignmentDetail();

        expect(detail, isNull);
        expect(fakeApi.recordedAssignmentId, isNull);
      });
    });

    test('propagates ApiException when API fails', () async {
      fakeApi.errorToThrow = const ApiException(
        message: 'Network failure',
        statusCode: 500,
      );

      expect(
        () => repository.getDriverSelf('drv-001'),
        throwsA(isA<ApiException>()),
      );
    });
  });
}
