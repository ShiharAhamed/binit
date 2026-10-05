import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/driver/models/driver_models.dart';

void main() {
  group('DriverAvailabilityStatus enum', () {
    test('parses Available and OffDuty case-insensitively', () {
      expect(
        DriverAvailabilityStatus.fromJsonValue('Available'),
        DriverAvailabilityStatus.available,
      );
      expect(
        DriverAvailabilityStatus.fromJsonValue('available'),
        DriverAvailabilityStatus.available,
      );
      expect(
        DriverAvailabilityStatus.fromJsonValue('OffDuty'),
        DriverAvailabilityStatus.offDuty,
      );
      expect(
        DriverAvailabilityStatus.fromJsonValue('offduty'),
        DriverAvailabilityStatus.offDuty,
      );
    });

    test('throws FormatException on unknown value', () {
      expect(
        () => DriverAvailabilityStatus.fromJsonValue('OnVacation'),
        throwsFormatException,
      );
    });

    test('serializes to exact backend string and evaluates getters', () {
      const avail = DriverAvailabilityStatus.available;
      const off = DriverAvailabilityStatus.offDuty;

      expect(avail.toJsonValue(), 'Available');
      expect(off.toJsonValue(), 'OffDuty');

      expect(avail.isAvailable, isTrue);
      expect(avail.isOffDuty, isFalse);

      expect(off.isAvailable, isFalse);
      expect(off.isOffDuty, isTrue);
    });
  });

  group('DriverSelfModel', () {
    test('parses JSON with all required fields', () {
      final json = {
        'id': 'drv-1234',
        'displayName': 'Samantha Perera',
        'availabilityStatus': 'Available',
        'isOccupied': false,
      };

      final model = DriverSelfModel.fromJson(json);

      expect(model.id, 'drv-1234');
      expect(model.displayName, 'Samantha Perera');
      expect(model.availabilityStatus, DriverAvailabilityStatus.available);
      expect(model.isOccupied, isFalse);

      final serialized = model.toJson();
      expect(serialized['id'], 'drv-1234');
      expect(serialized['displayName'], 'Samantha Perera');
      expect(serialized['availabilityStatus'], 'Available');
      expect(serialized['isOccupied'], false);
    });

    test('supports equality and occupied true', () {
      const a = DriverSelfModel(
        id: '1',
        displayName: 'Test',
        availabilityStatus: DriverAvailabilityStatus.offDuty,
        isOccupied: true,
      );
      const b = DriverSelfModel(
        id: '1',
        displayName: 'Test',
        availabilityStatus: DriverAvailabilityStatus.offDuty,
        isOccupied: true,
      );

      expect(a, equals(b));
      expect(a.hashCode, equals(b.hashCode));
      expect(a.isOccupied, isTrue);
    });

    test('throws FormatException when missing required fields', () {
      expect(
        () => DriverSelfModel.fromJson({
          'displayName': 'Test',
          'availabilityStatus': 'Available',
          'isOccupied': false,
        }),
        throwsFormatException,
      );
    });
  });

  group('CollectionAssignmentStatus enum', () {
    test('parses all 6 backend assignment status values', () {
      expect(
        CollectionAssignmentStatus.fromJsonValue('Assigned'),
        CollectionAssignmentStatus.assigned,
      );
      expect(
        CollectionAssignmentStatus.fromJsonValue('InProgress'),
        CollectionAssignmentStatus.inProgress,
      );
      expect(
        CollectionAssignmentStatus.fromJsonValue('Completed'),
        CollectionAssignmentStatus.completed,
      );
      expect(
        CollectionAssignmentStatus.fromJsonValue('PartiallyCompleted'),
        CollectionAssignmentStatus.partiallyCompleted,
      );
      expect(
        CollectionAssignmentStatus.fromJsonValue('Failed'),
        CollectionAssignmentStatus.failed,
      );
      expect(
        CollectionAssignmentStatus.fromJsonValue('Cancelled'),
        CollectionAssignmentStatus.cancelled,
      );
    });

    test('throws FormatException on invalid status', () {
      expect(
        () => CollectionAssignmentStatus.fromJsonValue('Draft'),
        throwsFormatException,
      );
    });

    test('evaluates isUnfinished and isTerminal helpers correctly', () {
      expect(CollectionAssignmentStatus.assigned.isUnfinished, isTrue);
      expect(CollectionAssignmentStatus.assigned.isTerminal, isFalse);

      expect(CollectionAssignmentStatus.inProgress.isUnfinished, isTrue);
      expect(CollectionAssignmentStatus.inProgress.isTerminal, isFalse);

      expect(CollectionAssignmentStatus.completed.isUnfinished, isFalse);
      expect(CollectionAssignmentStatus.completed.isTerminal, isTrue);

      expect(CollectionAssignmentStatus.partiallyCompleted.isTerminal, isTrue);
      expect(CollectionAssignmentStatus.failed.isTerminal, isTrue);
      expect(CollectionAssignmentStatus.cancelled.isTerminal, isTrue);
    });
  });

  group('AssignmentSummaryModel & PagedAssignmentsModel', () {
    final sampleSummaryJson = {
      'id': 'asg-001',
      'status': 'InProgress',
      'driverId': 'drv-001',
      'driverName': 'Samantha Perera',
      'vehicleId': 'veh-001',
      'vehicleRegistrationNumber': 'WP-NA-4567',
      'stopCount': 10,
      'completedStopCount': 6,
      'failedStopCount': 1,
      'assignedAt': '2026-09-25T08:30:00Z',
    };

    test('parses summary fields, dates, and computes pending stop count', () {
      final summary = AssignmentSummaryModel.fromJson(sampleSummaryJson);

      expect(summary.id, 'asg-001');
      expect(summary.status, CollectionAssignmentStatus.inProgress);
      expect(summary.driverId, 'drv-001');
      expect(summary.driverName, 'Samantha Perera');
      expect(summary.vehicleId, 'veh-001');
      expect(summary.vehicleRegistrationNumber, 'WP-NA-4567');
      expect(summary.stopCount, 10);
      expect(summary.completedStopCount, 6);
      expect(summary.failedStopCount, 1);
      expect(summary.pendingStopCount, 3); // 10 - 6 - 1 = 3
      expect(summary.assignedAt, DateTime.utc(2026, 9, 25, 8, 30));
    });

    test('parses paged envelope with multiple items and metadata', () {
      final pagedJson = {
        'items': [sampleSummaryJson],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'totalPages': 1,
      };

      final paged = PagedAssignmentsModel.fromJson(pagedJson);

      expect(paged.items.length, 1);
      expect(paged.page, 1);
      expect(paged.pageSize, 20);
      expect(paged.totalCount, 1);
      expect(paged.totalPages, 1);
      expect(paged.hasMore, isFalse);
      expect(paged.isEmpty, isFalse);
    });
  });

  group('RouteStopStatus enum', () {
    test('parses Pending, Completed, Failed, Skipped', () {
      expect(RouteStopStatus.fromJsonValue('Pending'), RouteStopStatus.pending);
      expect(
        RouteStopStatus.fromJsonValue('Completed'),
        RouteStopStatus.completed,
      );
      expect(RouteStopStatus.fromJsonValue('Failed'), RouteStopStatus.failed);
      expect(RouteStopStatus.fromJsonValue('Skipped'), RouteStopStatus.skipped);
    });

    test('evaluates isTerminal correctly', () {
      expect(RouteStopStatus.pending.isTerminal, isFalse);
      expect(RouteStopStatus.completed.isTerminal, isTrue);
      expect(RouteStopStatus.failed.isTerminal, isTrue);
      expect(RouteStopStatus.skipped.isTerminal, isTrue);
    });
  });

  group('AssignmentTaskModel & Coordinate Validation', () {
    test('validates coordinate boundaries accurately', () {
      // Valid coordinates within standard Earth bounds
      final validTask = AssignmentTaskModel(
        id: 'tsk-1',
        taskCode: 'TSK-001',
        targetType: 'Bin',
        collectionReason: 'OverflowAlert',
        status: 'Assigned',
        scheduledAt: DateTime.utc(2026, 9, 25, 9, 0),
        latitude: 6.9271,
        longitude: 79.8612,
      );
      expect(validTask.hasValidCoordinates, isTrue);

      // Boundary values (-90, 90, -180, 180)
      final boundaryTask = AssignmentTaskModel(
        id: 'tsk-2',
        taskCode: 'TSK-002',
        targetType: 'Report',
        collectionReason: 'CitizenReport',
        status: 'Assigned',
        scheduledAt: DateTime.utc(2026, 9, 25, 9, 0),
        latitude: 90.0,
        longitude: -180.0,
      );
      expect(boundaryTask.hasValidCoordinates, isTrue);

      // Null coordinates
      final nullCoordsTask = AssignmentTaskModel(
        id: 'tsk-3',
        taskCode: 'TSK-003',
        targetType: 'Bin',
        collectionReason: 'OfficerDiscretion',
        status: 'Assigned',
        scheduledAt: DateTime.utc(2026, 9, 25, 9, 0),
        latitude: null,
        longitude: null,
      );
      expect(nullCoordsTask.hasValidCoordinates, isFalse);

      // Latitude out of bounds (> 90 or < -90)
      final invalidLatTask = AssignmentTaskModel(
        id: 'tsk-4',
        taskCode: 'TSK-004',
        targetType: 'Bin',
        collectionReason: 'OfficerDiscretion',
        status: 'Assigned',
        scheduledAt: DateTime.utc(2026, 9, 25, 9, 0),
        latitude: 91.5,
        longitude: 79.86,
      );
      expect(invalidLatTask.hasValidCoordinates, isFalse);

      // Longitude out of bounds (> 180 or < -180)
      final invalidLngTask = AssignmentTaskModel(
        id: 'tsk-5',
        taskCode: 'TSK-005',
        targetType: 'Bin',
        collectionReason: 'OfficerDiscretion',
        status: 'Assigned',
        scheduledAt: DateTime.utc(2026, 9, 25, 9, 0),
        latitude: 6.92,
        longitude: -181.0,
      );
      expect(invalidLngTask.hasValidCoordinates, isFalse);
    });

    test('identifies target types correctly', () {
      final binTask = AssignmentTaskModel(
        id: 'tsk-1',
        taskCode: 'TSK-001',
        targetType: 'Bin',
        wasteBinId: 'bin-123',
        collectionReason: 'Overflow',
        status: 'Scheduled',
        scheduledAt: DateTime.utc(2026, 9, 25),
      );
      expect(binTask.isBinTask, isTrue);
      expect(binTask.isReportTask, isFalse);

      final reportTask = AssignmentTaskModel(
        id: 'tsk-2',
        taskCode: 'TSK-002',
        targetType: 'Report',
        wasteReportId: 'rep-456',
        collectionReason: 'CitizenReport',
        status: 'Scheduled',
        scheduledAt: DateTime.utc(2026, 9, 25),
      );
      expect(reportTask.isBinTask, isFalse);
      expect(reportTask.isReportTask, isTrue);
    });
  });

  group('RouteStopModel & Outcomes', () {
    test('parses completed stop with timestamps and task', () {
      final json = {
        'id': 'stp-001',
        'sequence': 1,
        'status': 'Completed',
        'completedAt': '2026-09-25T10:15:00Z',
        'failedAt': null,
        'failureReason': null,
        'task': {
          'id': 'tsk-001',
          'taskCode': 'TSK-001',
          'targetType': 'Bin',
          'collectionReason': 'OverflowAlert',
          'status': 'Completed',
          'scheduledAt': '2026-09-25T09:00:00Z',
          'latitude': 6.9271,
          'longitude': 79.8612,
        },
        'history': [
          {
            'id': 'h-1',
            'fromStatus': 'Pending',
            'toStatus': 'Completed',
            'changedByUserId': 'drv-001',
            'changedAt': '2026-09-25T10:15:00Z',
            'notes': null,
          }
        ],
      };

      final stop = RouteStopModel.fromJson(json);

      expect(stop.id, 'stp-001');
      expect(stop.sequence, 1);
      expect(stop.status, RouteStopStatus.completed);
      expect(stop.completedAt, DateTime.utc(2026, 9, 25, 10, 15));
      expect(stop.failedAt, isNull);
      expect(stop.failureReason, isNull);
      expect(stop.hasValidCoordinates, isTrue);
      expect(stop.history.length, 1);
      expect(stop.history.first.toStatus, RouteStopStatus.completed);
    });

    test('parses failed stop with reason and timestamps', () {
      final json = {
        'id': 'stp-002',
        'sequence': 2,
        'status': 'Failed',
        'completedAt': null,
        'failedAt': '2026-09-25T10:30:00Z',
        'failureReason': 'Road flooded, cannot reach bin location.',
        'task': {
          'id': 'tsk-002',
          'taskCode': 'TSK-002',
          'targetType': 'Report',
          'collectionReason': 'CitizenReport',
          'status': 'Failed',
          'scheduledAt': '2026-09-25T09:00:00Z',
          'latitude': null,
          'longitude': null,
        },
        'history': [],
      };

      final stop = RouteStopModel.fromJson(json);

      expect(stop.status, RouteStopStatus.failed);
      expect(stop.failedAt, DateTime.utc(2026, 9, 25, 10, 30));
      expect(
        stop.failureReason,
        'Road flooded, cannot reach bin location.',
      );
      expect(stop.hasValidCoordinates, isFalse);
    });
  });

  group('RouteReadModel & AssignmentDetailModel', () {
    final detailJson = {
      'id': 'asg-001',
      'status': 'InProgress',
      'driverId': 'drv-001',
      'driverName': 'Samantha Perera',
      'vehicleId': 'veh-001',
      'vehicleRegistrationNumber': 'WP-NA-4567',
      'stopCount': 2,
      'completedStopCount': 1,
      'failedStopCount': 0,
      'assignedAt': '2026-09-25T08:30:00Z',
      'route': {
        'id': 'rt-001',
        'collectionAssignmentId': 'asg-001',
        'routingMethod': 'ManualOrder',
        'routeGeometry': null,
        'estimatedDistanceMeters': 4500.0,
        'estimatedDurationSeconds': 900.0,
        'stops': [
          {
            'id': 'stp-2',
            'sequence': 2,
            'status': 'Pending',
            'task': {
              'id': 'tsk-2',
              'taskCode': 'TSK-002',
              'targetType': 'Report',
              'collectionReason': 'CitizenReport',
              'status': 'InProgress',
              'scheduledAt': '2026-09-25T09:00:00Z',
              'latitude': null,
              'longitude': null,
            },
            'history': [],
          },
          {
            'id': 'stp-1',
            'sequence': 1,
            'status': 'Completed',
            'completedAt': '2026-09-25T09:30:00Z',
            'task': {
              'id': 'tsk-1',
              'taskCode': 'TSK-001',
              'targetType': 'Bin',
              'collectionReason': 'OverflowAlert',
              'status': 'Completed',
              'scheduledAt': '2026-09-25T09:00:00Z',
              'latitude': 6.9271,
              'longitude': 79.8612,
            },
            'history': [],
          },
        ],
      },
      'history': [
        {
          'id': 'ah-1',
          'fromStatus': 'Assigned',
          'toStatus': 'InProgress',
          'changedByUserId': 'drv-001',
          'changedAt': '2026-09-25T08:45:00Z',
          'notes': 'Started by assigned Driver.',
        }
      ],
    };

    test('parses full AssignmentDetailModel and maintains orderedStops', () {
      final detail = AssignmentDetailModel.fromJson(detailJson);

      expect(detail.id, 'asg-001');
      expect(detail.status, CollectionAssignmentStatus.inProgress);
      expect(detail.route, isNotNull);
      expect(detail.history.length, 1);
      expect(
        detail.history.first.toStatus,
        CollectionAssignmentStatus.inProgress,
      );

      final route = detail.route!;
      expect(route.stops.length, 2);

      // orderedStops must sort strictly by sequence ascending
      final ordered = route.orderedStops;
      expect(ordered.first.sequence, 1);
      expect(ordered.first.id, 'stp-1');
      expect(ordered.last.sequence, 2);
      expect(ordered.last.id, 'stp-2');

      // mappableStops only includes stops with valid coordinates
      final mappable = route.mappableStops;
      expect(mappable.length, 1);
      expect(mappable.first.id, 'stp-1');
    });

    test('converts detail model to summary model via toSummary()', () {
      final detail = AssignmentDetailModel.fromJson(detailJson);
      final summary = detail.toSummary();

      expect(summary.id, detail.id);
      expect(summary.status, detail.status);
      expect(summary.driverId, detail.driverId);
      expect(summary.vehicleId, detail.vehicleId);
      expect(summary.stopCount, detail.stopCount);
      expect(summary.pendingStopCount, 1); // 2 - 1 - 0 = 1
    });
  });
}
