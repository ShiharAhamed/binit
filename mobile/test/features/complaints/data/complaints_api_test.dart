import 'dart:convert';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/network/dio_client.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';
import 'package:mobile/features/complaints/data/complaints_api.dart';
import 'package:mobile/features/complaints/data/complaints_repository.dart';
import 'package:mobile/features/complaints/models/complaint_model.dart';
import 'package:mobile/features/complaints/models/create_complaint_request.dart';

class FakeSecureStorageService extends SecureStorageService {
  String? token;

  FakeSecureStorageService({this.token = 'test-bearer-token'});

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
  group('ComplaintsApi & Repository Unit Tests', () {
    late MockHttpClientAdapter mockAdapter;
    late FakeSecureStorageService fakeStorage;
    late Dio dio;
    late DioClient dioClient;
    late ComplaintsApi api;
    late ComplaintsRepository repository;

    setUp(() {
      mockAdapter = MockHttpClientAdapter();
      fakeStorage = FakeSecureStorageService(token: 'test-citizen-token');

      dio = Dio(BaseOptions(baseUrl: 'http://localhost:5276/api/v1'));
      dio.httpClientAdapter = mockAdapter;

      dioClient = DioClient(
        storageService: fakeStorage,
        dio: dio,
      );

      api = ComplaintsApi(client: dioClient);
      repository = ComplaintsRepository(api: api);
    });

    test('createComplaint POSTs to /complaints and returns ComplaintDetailModel', () async {
      mockAdapter.statusCode = 201;
      mockAdapter.responseBody = jsonEncode({
        'id': 'comp-101',
        'citizenId': 'cit-101',
        'category': 'MissedCollection',
        'subject': 'Missed garbage collection',
        'description': 'Bin was out on schedule but not collected.',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'locationDescription': 'Near front gate',
        'status': 'Submitted',
        'createdAt': '2026-09-16T08:00:00.000Z',
      });

      const request = CreateComplaintRequest(
        category: ComplaintCategory.missedCollection,
        subject: 'Missed garbage collection',
        description: 'Bin was out on schedule but not collected.',
        latitude: 6.9271,
        longitude: 79.8612,
        locationDescription: 'Near front gate',
      );

      final result = await repository.createComplaint(request);

      expect(mockAdapter.lastRequest?.method, 'POST');
      expect(mockAdapter.lastRequest?.path, '/complaints');
      expect(mockAdapter.lastRequest?.data, {
        'category': 'MissedCollection',
        'subject': 'Missed garbage collection',
        'description': 'Bin was out on schedule but not collected.',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'locationDescription': 'Near front gate',
      });

      expect(result.id, 'comp-101');
      expect(result.category, ComplaintCategory.missedCollection);
      expect(result.status, ComplaintStatus.submitted);
      expect(result.latitude, 6.9271);
      expect(result.locationDescription, 'Near front gate');
    });

    test('getMyComplaints GETs /complaints with query params', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'items': [
          {
            'id': 'comp-101',
            'citizenId': 'cit-101',
            'category': 'DelayedService',
            'subject': 'Delayed pickup',
            'status': 'InReview',
            'createdAt': '2026-09-16T08:00:00.000Z',
          }
        ],
        'page': 2,
        'pageSize': 10,
        'totalCount': 15,
        'totalPages': 2,
      });

      final result = await repository.getMyComplaints(
        page: 2,
        pageSize: 10,
        status: ComplaintStatus.inReview,
        category: ComplaintCategory.delayedService,
        search: 'pickup',
      );

      expect(mockAdapter.lastRequest?.method, 'GET');
      expect(mockAdapter.lastRequest?.path, '/complaints');
      expect(mockAdapter.lastRequest?.queryParameters, {
        'page': 2,
        'pageSize': 10,
        'status': 'InReview',
        'category': 'DelayedService',
        'search': 'pickup',
        'sortBy': 'createdAt',
        'sortDirection': 'desc',
      });

      expect(result.items.length, 1);
      expect(result.page, 2);
      expect(result.totalPages, 2);
      expect(result.hasMore, isFalse);
      expect(result.items.first.subject, 'Delayed pickup');
    });

    test('getComplaintById GETs /complaints/{id}', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'id': 'comp-555',
        'citizenId': 'cit-101',
        'category': 'PoorService',
        'subject': 'Spilled waste on street',
        'description': 'Litter left behind after emptying.',
        'status': 'Resolved',
        'resolutionNote': 'Crew dispatched to clean roadway.',
        'resolvedAt': '2026-09-16T12:00:00.000Z',
        'resolvedByUserName': 'Manager Jane',
        'createdAt': '2026-09-16T08:00:00.000Z',
      });

      final result = await repository.getComplaintById('comp-555');

      expect(mockAdapter.lastRequest?.method, 'GET');
      expect(mockAdapter.lastRequest?.path, '/complaints/comp-555');
      expect(result.id, 'comp-555');
      expect(result.status, ComplaintStatus.resolved);
      expect(result.resolutionNote, 'Crew dispatched to clean roadway.');
      expect(result.resolvedByUserName, 'Manager Jane');
    });

    test('Throws ApiException on HTTP 400 error', () async {
      mockAdapter.statusCode = 400;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Bad Request',
        'detail': 'Subject is required.',
      });

      const request = CreateComplaintRequest(
        category: ComplaintCategory.other,
        subject: '',
        description: 'Testing invalid subject validation.',
      );

      expect(
        () => repository.createComplaint(request),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 400)),
      );
    });

    test('Throws ApiException on HTTP 404 error', () async {
      mockAdapter.statusCode = 404;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Not Found',
        'detail': 'Complaint not found.',
      });

      expect(
        () => repository.getComplaintById('non-existent-id'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 404)),
      );
    });
  });
}
