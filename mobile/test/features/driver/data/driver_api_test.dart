import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/network/dio_client.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';
import 'package:mobile/features/driver/data/driver_api.dart';
import 'package:mobile/features/driver/models/driver_models.dart';

class _FakeSecureStorageService extends SecureStorageService {
  final String? token;
  _FakeSecureStorageService({this.token = 'test-driver-token'});

  @override
  Future<String?> getAccessToken() async => token;
}

class _MockHttpClientAdapter implements HttpClientAdapter {
  RequestOptions? lastRequest;
  int statusCode = 200;
  String responseBody = '{}';

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    lastRequest = options;
    return ResponseBody.fromString(
      responseBody,
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  group('DriverApi Unit Tests', () {
    late _MockHttpClientAdapter mockAdapter;
    late _FakeSecureStorageService fakeStorage;
    late Dio dio;
    late DioClient dioClient;
    late DriverApi api;

    setUp(() {
      mockAdapter = _MockHttpClientAdapter();
      fakeStorage = _FakeSecureStorageService(token: 'test-driver-token');
      dio = Dio(BaseOptions(baseUrl: 'http://localhost:5276/api/v1'));
      dio.httpClientAdapter = mockAdapter;
      dioClient = DioClient(
        dio: dio,
        storageService: fakeStorage,
        baseUrl: 'http://localhost:5276/api/v1',
      );
      api = DriverApi(client: dioClient);
    });

    test('getDriverSelf sends GET /drivers/{id} with bearer token', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'id': 'drv-101',
        'displayName': 'Samantha Perera',
        'availabilityStatus': 'Available',
        'isOccupied': false,
      });

      final result = await api.getDriverSelf('drv-101');

      expect(mockAdapter.lastRequest, isNotNull);
      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.path, '/drivers/drv-101');
      expect(
        mockAdapter.lastRequest!.headers['Authorization'],
        'Bearer test-driver-token',
      );

      expect(result.id, 'drv-101');
      expect(result.displayName, 'Samantha Perera');
      expect(result.availabilityStatus, DriverAvailabilityStatus.available);
      expect(result.isOccupied, isFalse);
    });

    test(
      'updateAvailability sends PATCH /drivers/me/availability with body',
      () async {
        mockAdapter.statusCode = 200;
        mockAdapter.responseBody = jsonEncode({
          'id': 'drv-101',
          'displayName': 'Samantha Perera',
          'availabilityStatus': 'OffDuty',
          'isOccupied': false,
        });

        final result = await api.updateAvailability(
          DriverAvailabilityStatus.offDuty,
        );

        expect(mockAdapter.lastRequest, isNotNull);
        expect(mockAdapter.lastRequest!.method, 'PATCH');
        expect(mockAdapter.lastRequest!.path, '/drivers/me/availability');
        expect(mockAdapter.lastRequest!.data, {
          'availabilityStatus': 'OffDuty',
        });

        expect(result.availabilityStatus, DriverAvailabilityStatus.offDuty);
      },
    );

    test('getMyAssignments sends GET /assignments/mine with parameters and clamps pageSize', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'items': [
          {
            'id': 'asg-1',
            'status': 'InProgress',
            'driverId': 'drv-101',
            'driverName': 'Samantha Perera',
            'vehicleId': 'veh-1',
            'vehicleRegistrationNumber': 'WP-NA-4567',
            'stopCount': 5,
            'completedStopCount': 2,
            'failedStopCount': 0,
            'assignedAt': '2026-09-25T08:00:00Z',
          },
        ],
        'page': 2,
        'pageSize': 50,
        'totalCount': 15,
        'totalPages': 1,
      });

      // Pass pageSize 100; should be clamped to 50
      final result = await api.getMyAssignments(
        status: CollectionAssignmentStatus.inProgress,
        page: 2,
        pageSize: 100,
      );

      expect(mockAdapter.lastRequest, isNotNull);
      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.path, '/assignments/mine');
      expect(mockAdapter.lastRequest!.queryParameters['status'], 'InProgress');
      expect(mockAdapter.lastRequest!.queryParameters['page'], 2);
      expect(mockAdapter.lastRequest!.queryParameters['pageSize'], 50);

      expect(result.items.length, 1);
      expect(result.items.first.id, 'asg-1');
      expect(result.items.first.status, CollectionAssignmentStatus.inProgress);
    });

    test('getAssignmentDetail sends GET /assignments/{id}', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'id': 'asg-202',
        'status': 'Assigned',
        'driverId': 'drv-101',
        'driverName': 'Samantha Perera',
        'vehicleId': 'veh-1',
        'vehicleRegistrationNumber': 'WP-NA-4567',
        'stopCount': 1,
        'completedStopCount': 0,
        'failedStopCount': 0,
        'assignedAt': '2026-09-25T08:00:00Z',
        'route': null,
        'history': [],
      });

      final result = await api.getAssignmentDetail('asg-202');

      expect(mockAdapter.lastRequest, isNotNull);
      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.path, '/assignments/asg-202');
      expect(result.id, 'asg-202');
      expect(result.status, CollectionAssignmentStatus.assigned);
    });

    test('getRoute sends GET /routes/{id}', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'id': 'rt-303',
        'collectionAssignmentId': 'asg-202',
        'routingMethod': 'ManualOrder',
        'routeGeometry': null,
        'estimatedDistanceMeters': 2300.0,
        'estimatedDurationSeconds': 420.0,
        'stops': [],
      });

      final result = await api.getRoute('rt-303');

      expect(mockAdapter.lastRequest, isNotNull);
      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.path, '/routes/rt-303');
      expect(result.id, 'rt-303');
      expect(result.routingMethod, 'ManualOrder');
    });

    test(
      'startAssignment sends a bodyless POST to the exact start endpoint',
      () async {
        mockAdapter.statusCode = 200;
        mockAdapter.responseBody = jsonEncode(
          _assignmentResponse('InProgress'),
        );

        final result = await api.startAssignment('asg-202');

        expect(mockAdapter.lastRequest!.method, 'POST');
        expect(mockAdapter.lastRequest!.path, '/assignments/asg-202/start');
        expect(mockAdapter.lastRequest!.data, isNull);
        expect(result.status, CollectionAssignmentStatus.inProgress);
      },
    );

    test(
      'completeStop sends a bodyless POST to the exact stop endpoint',
      () async {
        mockAdapter.statusCode = 200;
        mockAdapter.responseBody = jsonEncode(
          _assignmentResponse('InProgress'),
        );

        await api.completeStop('asg-202', 'stop-9');

        expect(mockAdapter.lastRequest!.method, 'POST');
        expect(
          mockAdapter.lastRequest!.path,
          '/assignments/asg-202/stops/stop-9/complete',
        );
        expect(mockAdapter.lastRequest!.data, isNull);
      },
    );

    test(
      'failStop sends the required reason body to the exact stop endpoint',
      () async {
        mockAdapter.statusCode = 200;
        mockAdapter.responseBody = jsonEncode(
          _assignmentResponse('InProgress'),
        );

        await api.failStop(
          'asg-202',
          'stop-9',
          'Gate is locked for collection.',
        );

        expect(mockAdapter.lastRequest!.method, 'POST');
        expect(
          mockAdapter.lastRequest!.path,
          '/assignments/asg-202/stops/stop-9/fail',
        );
        expect(mockAdapter.lastRequest!.data, {
          'reason': 'Gate is locked for collection.',
        });
      },
    );

    test(
      'finalizeAssignment sends a bodyless POST to the exact finalize endpoint',
      () async {
        mockAdapter.statusCode = 200;
        mockAdapter.responseBody = jsonEncode(
          _assignmentResponse('PartiallyCompleted'),
        );

        final result = await api.finalizeAssignment('asg-202');

        expect(mockAdapter.lastRequest!.method, 'POST');
        expect(mockAdapter.lastRequest!.path, '/assignments/asg-202/finalize');
        expect(mockAdapter.lastRequest!.data, isNull);
        expect(result.status, CollectionAssignmentStatus.partiallyCompleted);
      },
    );

    test('converts 404 response to ApiException', () async {
      mockAdapter.statusCode = 404;
      mockAdapter.responseBody = jsonEncode({
        'type': 'https://httpstatuses.com/404',
        'title': 'Not Found',
        'status': 404,
        'detail': 'Collection assignment was not found.',
      });

      expect(
        () => api.getAssignmentDetail('non-existent-id'),
        throwsA(
          isA<ApiException>().having((e) => e.statusCode, 'statusCode', 404),
        ),
      );
    });

    test('converts 403 response to ApiException', () async {
      mockAdapter.statusCode = 403;
      mockAdapter.responseBody = jsonEncode({
        'type': 'https://httpstatuses.com/403',
        'title': 'Forbidden',
        'status': 403,
        'detail': 'Drivers can view only their own profile.',
      });

      expect(
        () => api.getDriverSelf('other-driver-id'),
        throwsA(
          isA<ApiException>().having((e) => e.statusCode, 'statusCode', 403),
        ),
      );
    });
  });
}

Map<String, dynamic> _assignmentResponse(String status) => {
  'id': 'asg-202',
  'status': status,
  'driverId': 'drv-101',
  'driverName': 'Samantha Perera',
  'vehicleId': 'veh-1',
  'vehicleRegistrationNumber': 'WP-NA-4567',
  'stopCount': 1,
  'completedStopCount': 0,
  'failedStopCount': 0,
  'assignedAt': '2026-09-25T08:00:00Z',
  'route': null,
  'history': [],
};
