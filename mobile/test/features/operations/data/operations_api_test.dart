import 'dart:convert';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/network/dio_client.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';
import 'package:mobile/features/operations/data/operations_api.dart';
import 'package:mobile/features/operations/data/operations_repository.dart';
import 'package:mobile/features/operations/models/create_operational_issue_request.dart';
import 'package:mobile/features/operations/models/operational_issue_model.dart';

class FakeSecureStorageService extends SecureStorageService {
  String? token;

  FakeSecureStorageService({this.token = 'test-driver-token'});

  @override
  Future<String?> getAccessToken() async => token;
}

class MockHttpClientAdapter implements HttpClientAdapter {
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
  group('OperationsApi & Repository Unit Tests', () {
    late MockHttpClientAdapter mockAdapter;
    late FakeSecureStorageService fakeStorage;
    late Dio dio;
    late DioClient dioClient;
    late OperationsApi api;
    late OperationsRepository repository;

    setUp(() {
      mockAdapter = MockHttpClientAdapter();
      fakeStorage = FakeSecureStorageService(token: 'test-driver-token');

      dio = Dio(BaseOptions(baseUrl: 'http://localhost:5276/api/v1'));
      dio.httpClientAdapter = mockAdapter;

      dioClient = DioClient(
        storageService: fakeStorage,
        dio: dio,
      );

      api = OperationsApi(client: dioClient);
      repository = OperationsRepository(api: api);
    });

    test('createOperationalIssue POSTs to /operational-issues and returns OperationalIssueDetailModel', () async {
      mockAdapter.statusCode = 201;
      mockAdapter.responseBody = jsonEncode({
        'id': 'issue-101',
        'driverId': 'driver-101',
        'issueType': 'VehicleProblem',
        'title': 'Brake warning indicator on truck 04',
        'description': 'Brake pad sensor flashing on dashboard during morning route.',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'locationDescription': 'Near depot gate 2',
        'status': 'Reported',
        'createdAt': '2026-10-15T08:00:00.000Z',
      });

      const request = CreateOperationalIssueRequest(
        issueType: OperationalIssueType.vehicleProblem,
        title: 'Brake warning indicator on truck 04',
        description: 'Brake pad sensor flashing on dashboard during morning route.',
        latitude: 6.9271,
        longitude: 79.8612,
        locationDescription: 'Near depot gate 2',
      );

      final result = await repository.createOperationalIssue(request);

      expect(mockAdapter.lastRequest?.method, 'POST');
      expect(mockAdapter.lastRequest?.path, '/operational-issues');
      expect(mockAdapter.lastRequest?.data, {
        'issueType': 'VehicleProblem',
        'title': 'Brake warning indicator on truck 04',
        'description': 'Brake pad sensor flashing on dashboard during morning route.',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'locationDescription': 'Near depot gate 2',
      });

      expect(result.id, 'issue-101');
      expect(result.issueType, OperationalIssueType.vehicleProblem);
      expect(result.status, OperationalIssueStatus.reported);
      expect(result.latitude, 6.9271);
      expect(result.locationDescription, 'Near depot gate 2');
    });

    test('getMyOperationalIssues GETs /operational-issues/mine with query params', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'items': [
          {
            'id': 'issue-101',
            'driverId': 'driver-101',
            'issueType': 'RoadOrAccessIssue',
            'title': 'Fallen tree blocking lane',
            'status': 'InReview',
            'createdAt': '2026-10-15T08:00:00.000Z',
          }
        ],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'totalPages': 1,
      });

      final result = await repository.getMyOperationalIssues(
        page: 1,
        pageSize: 20,
        status: OperationalIssueStatus.inReview,
        issueType: OperationalIssueType.roadOrAccessIssue,
        search: 'tree',
      );

      expect(mockAdapter.lastRequest?.method, 'GET');
      expect(mockAdapter.lastRequest?.path, '/operational-issues/mine');
      expect(mockAdapter.lastRequest?.queryParameters, {
        'page': 1,
        'pageSize': 20,
        'status': 'InReview',
        'issueType': 'RoadOrAccessIssue',
        'search': 'tree',
        'sortBy': 'createdAt',
        'sortDirection': 'desc',
      });

      expect(result.items.length, 1);
      expect(result.page, 1);
      expect(result.totalPages, 1);
      expect(result.hasMore, isFalse);
      expect(result.items.first.title, 'Fallen tree blocking lane');
    });

    test('getOperationalIssueById GETs /operational-issues/{id}', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'id': 'issue-555',
        'driverId': 'driver-101',
        'issueType': 'SafetyConcern',
        'title': 'Chemical spill in alleyway',
        'description': 'Hazardous material leak observed near commercial bin.',
        'status': 'Resolved',
        'resolutionNote': 'Hazmat containment unit cleared the spill safely.',
        'resolvedAt': '2026-10-15T12:00:00.000Z',
        'resolvedByUserName': 'Officer Perera',
        'createdAt': '2026-10-15T08:00:00.000Z',
      });

      final result = await repository.getOperationalIssueById('issue-555');

      expect(mockAdapter.lastRequest?.method, 'GET');
      expect(mockAdapter.lastRequest?.path, '/operational-issues/issue-555');
      expect(result.id, 'issue-555');
      expect(result.status, OperationalIssueStatus.resolved);
      expect(result.resolutionNote, 'Hazmat containment unit cleared the spill safely.');
      expect(result.resolvedByUserName, 'Officer Perera');
    });

    test('Throws ApiException on HTTP 400 validation error', () async {
      mockAdapter.statusCode = 400;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Bad Request',
        'detail': 'Title is required.',
      });

      const request = CreateOperationalIssueRequest(
        issueType: OperationalIssueType.other,
        title: '',
        description: 'Testing invalid title validation error.',
      );

      expect(
        () => repository.createOperationalIssue(request),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 400)),
      );
    });

    test('Throws ApiException on HTTP 404 not found error', () async {
      mockAdapter.statusCode = 404;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Not Found',
        'detail': 'Operational issue not found.',
      });

      expect(
        () => repository.getOperationalIssueById('non-existent-id'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 404)),
      );
    });
  });
}
