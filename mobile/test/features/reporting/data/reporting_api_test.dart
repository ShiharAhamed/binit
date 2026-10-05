import 'dart:convert';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/network/dio_client.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';
import 'package:mobile/features/reporting/data/reporting_api.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/create_waste_report_request.dart';
import 'package:mobile/features/reporting/models/update_waste_report_request.dart';
import 'package:mobile/features/reporting/models/waste_report_priority.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';

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
  group('ReportingApi Unit Tests', () {
    late MockHttpClientAdapter mockAdapter;
    late FakeSecureStorageService fakeStorage;
    late Dio dio;
    late DioClient dioClient;
    late ReportingApi api;

    setUp(() {
      mockAdapter = MockHttpClientAdapter();
      fakeStorage = FakeSecureStorageService(token: 'test-citizen-token');
      dio = Dio(BaseOptions(baseUrl: 'http://localhost:5276/api/v1'));
      dio.httpClientAdapter = mockAdapter;
      dioClient = DioClient(
        dio: dio,
        storageService: fakeStorage,
        baseUrl: 'http://localhost:5276/api/v1',
      );
      api = ReportingApi(client: dioClient);
    });

    test('createWasteReport sends POST /waste-reports with JSON body and returns WasteReportDetailModel', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'rep-12345',
        'citizenId': 'cit-54321',
        'citizenName': 'Nimal Silva',
        'description': 'Overflowing bin on street corner',
        'wasteType': 'General',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'addressText': 'Galle Face Green',
        'status': 'Submitted',
        'priority': null,
        'verifiedByUserId': null,
        'verifiedByUserName': null,
        'verifiedAt': null,
        'attachments': <dynamic>[],
        'createdAt': '2026-09-16T12:00:00.000Z',
        'updatedAt': null,
      });

      const request = CreateWasteReportRequest(
        description: 'Overflowing bin on street corner',
        wasteType: WasteType.general,
        latitude: 6.9271,
        longitude: 79.8612,
        addressText: 'Galle Face Green',
      );

      final result = await api.createWasteReport(request);

      expect(mockAdapter.lastRequest, isNotNull);
      expect(mockAdapter.lastRequest!.method, 'POST');
      expect(mockAdapter.lastRequest!.path, '/waste-reports');
      expect(mockAdapter.lastRequest!.data['description'], 'Overflowing bin on street corner');
      expect(mockAdapter.lastRequest!.data['wasteType'], 'General'); // String enum value
      expect(mockAdapter.lastRequest!.data['latitude'], 6.9271);
      expect(mockAdapter.lastRequest!.data['longitude'], 79.8612);
      expect(mockAdapter.lastRequest!.data['addressText'], 'Galle Face Green');

      expect(result.id, 'rep-12345');
      expect(result.citizenId, 'cit-54321');
      expect(result.status, WasteReportStatus.submitted);
      expect(result.priority, isNull);
    });

    test('uploadAttachmentBytes sends multipart POST with canonical image/jpeg Content-Type', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'att-jpeg',
        'wasteReportId': 'rep-12345',
        'fileUrl': 'https://supabase.local/sign/att-jpeg.jpg?token=abc',
        'fileType': 'image/jpeg',
        'createdAt': '2026-09-16T12:05:00.000Z',
      });

      final dummyBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]; // JPEG magic bytes

      final attachment = await api.uploadAttachmentBytes(
        reportId: 'rep-12345',
        bytes: dummyBytes,
        fileName: 'evidence.jpg',
        fileType: 'image/jpeg',
      );

      expect(mockAdapter.lastRequest, isNotNull);
      expect(mockAdapter.lastRequest!.method, 'POST');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-12345/attachments');
      expect(mockAdapter.lastRequest!.data, isA<FormData>());

      final formData = mockAdapter.lastRequest!.data as FormData;
      final fileEntry = formData.files.firstWhere((f) => f.key == 'file');
      expect(fileEntry.value.contentType?.mimeType, 'image/jpeg');
      expect(attachment.id, 'att-jpeg');
      expect(attachment.fileType, 'image/jpeg');
    });

    test('uploadAttachmentBytes sends multipart POST with canonical image/png Content-Type', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'att-png',
        'wasteReportId': 'rep-12345',
        'fileUrl': 'https://supabase.local/sign/att-png.png?token=abc',
        'fileType': 'image/png',
        'createdAt': '2026-09-16T12:05:00.000Z',
      });

      final dummyBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]; // PNG magic bytes

      final attachment = await api.uploadAttachmentBytes(
        reportId: 'rep-12345',
        bytes: dummyBytes,
        fileName: 'evidence.png',
        fileType: 'image/png',
      );

      final formData = mockAdapter.lastRequest!.data as FormData;
      final fileEntry = formData.files.firstWhere((f) => f.key == 'file');
      expect(fileEntry.value.contentType?.mimeType, 'image/png');
      expect(attachment.id, 'att-png');
      expect(attachment.fileType, 'image/png');
    });

    test('uploadAttachmentBytes sends multipart POST with canonical image/webp Content-Type', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'att-webp',
        'wasteReportId': 'rep-12345',
        'fileUrl': 'https://supabase.local/sign/att-webp.webp?token=abc',
        'fileType': 'image/webp',
        'createdAt': '2026-09-16T12:05:00.000Z',
      });

      final dummyBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]; // WebP

      final attachment = await api.uploadAttachmentBytes(
        reportId: 'rep-12345',
        bytes: dummyBytes,
        fileName: 'evidence.webp',
        fileType: 'image/webp',
      );

      final formData = mockAdapter.lastRequest!.data as FormData;
      final fileEntry = formData.files.firstWhere((f) => f.key == 'file');
      expect(fileEntry.value.contentType?.mimeType, 'image/webp');
      expect(attachment.id, 'att-webp');
      expect(attachment.fileType, 'image/webp');
    });

    test('getWasteReport sends GET /waste-reports/{id} and returns detail', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'rep-12345',
        'citizenId': 'cit-54321',
        'citizenName': 'Nimal Silva',
        'description': 'Hazardous battery pile',
        'wasteType': 'Hazardous',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'addressText': null,
        'status': 'Scheduled',
        'priority': 'Urgent',
        'verifiedByUserId': 'off-1',
        'verifiedByUserName': 'Officer Perera',
        'verifiedAt': '2026-09-16T12:30:00.000Z',
        'attachments': <dynamic>[],
        'createdAt': '2026-09-16T12:00:00.000Z',
        'updatedAt': '2026-09-16T12:30:00.000Z',
      });

      final detail = await api.getWasteReport('rep-12345');

      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-12345');
      expect(detail.id, 'rep-12345');
      expect(detail.wasteType, WasteType.hazardous);
      expect(detail.status, WasteReportStatus.scheduled);
      expect(detail.priority, WasteReportPriority.urgent);
    });

    test('deleteAttachment sends DELETE /waste-reports/{id}/attachments/{attId}', () async {
      mockAdapter.responseBody = jsonEncode({
        'message': 'Attachment removed successfully.',
      });

      await api.deleteAttachment(reportId: 'rep-12345', attachmentId: 'att-789');

      expect(mockAdapter.lastRequest!.method, 'DELETE');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-12345/attachments/att-789');
    });

    test('throws ApiException on 400 Bad Request with server ProblemDetails detail', () async {
      mockAdapter.statusCode = 400;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Bad Request',
        'detail': 'Description cannot exceed 1000 characters.',
        'status': 400,
      });

      const request = CreateWasteReportRequest(
        description: 'Valid description',
        wasteType: WasteType.general,
        latitude: 6.9271,
        longitude: 79.8612,
      );

      expect(
        () => api.createWasteReport(request),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 400)
              .having((e) => e.message, 'message', 'Description cannot exceed 1000 characters.'),
        ),
      );
    });

    test('throws ApiException on 409 Conflict with state violation ProblemDetails', () async {
      mockAdapter.statusCode = 409;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Conflict',
        'detail': 'Attachments can only be uploaded while the report is in Submitted status.',
        'status': 409,
      });

      expect(
        () => api.uploadAttachmentBytes(
          reportId: 'rep-locked',
          bytes: [1, 2, 3],
          fileName: 'photo.jpg',
        ),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 409)
              .having((e) => e.message, 'message', 'Attachments can only be uploaded while the report is in Submitted status.'),
        ),
      );
    });

    test('getWasteReports sends GET /waste-reports with default parameters and returns PagedWasteReportsModel', () async {
      mockAdapter.responseBody = jsonEncode({
        'items': [
          {
            'id': 'rep-list-1',
            'description': 'Overflowing bin near central station',
            'wasteType': 'General',
            'status': 'Submitted',
            'priority': null,
            'addressText': 'Station Road',
            'latitude': 6.9344,
            'longitude': 79.8428,
            'citizenId': null,
            'citizenName': null,
            'createdAt': '2026-09-16T14:00:00.000Z',
            'updatedAt': null,
          }
        ],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'totalPages': 1,
      });

      final result = await api.getWasteReports();

      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.path, '/waste-reports');
      expect(mockAdapter.lastRequest!.queryParameters['page'], 1);
      expect(mockAdapter.lastRequest!.queryParameters['pageSize'], 20);
      expect(mockAdapter.lastRequest!.queryParameters['sortBy'], 'createdAt');
      expect(mockAdapter.lastRequest!.queryParameters['sortDirection'], 'desc');
      expect(mockAdapter.lastRequest!.queryParameters.containsKey('status'), isFalse);
      expect(mockAdapter.lastRequest!.queryParameters.containsKey('wasteType'), isFalse);
      expect(mockAdapter.lastRequest!.queryParameters.containsKey('search'), isFalse);

      expect(result.items.length, 1);
      expect(result.items.first.id, 'rep-list-1');
      expect(result.items.first.description, 'Overflowing bin near central station');
      expect(result.items.first.wasteType, WasteType.general);
      expect(result.items.first.status, WasteReportStatus.submitted);
      expect(result.page, 1);
      expect(result.pageSize, 20);
      expect(result.totalCount, 1);
      expect(result.totalPages, 1);
      expect(result.hasMore, isFalse);
    });

    test('getWasteReports passes filters, search, and custom pagination query parameters', () async {
      mockAdapter.responseBody = jsonEncode({
        'items': <dynamic>[],
        'page': 2,
        'pageSize': 10,
        'totalCount': 25,
        'totalPages': 3,
      });

      final result = await api.getWasteReports(
        page: 2,
        pageSize: 10,
        status: WasteReportStatus.underReview,
        wasteType: WasteType.recyclable,
        search: 'central',
        sortBy: 'updatedAt',
        sortDirection: 'asc',
      );

      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.queryParameters['page'], 2);
      expect(mockAdapter.lastRequest!.queryParameters['pageSize'], 10);
      expect(mockAdapter.lastRequest!.queryParameters['status'], 'UnderReview');
      expect(mockAdapter.lastRequest!.queryParameters['wasteType'], 'Recyclable');
      expect(mockAdapter.lastRequest!.queryParameters['search'], 'central');
      expect(mockAdapter.lastRequest!.queryParameters['sortBy'], 'updatedAt');
      expect(mockAdapter.lastRequest!.queryParameters['sortDirection'], 'asc');

      expect(result.page, 2);
      expect(result.pageSize, 10);
      expect(result.totalCount, 25);
      expect(result.totalPages, 3);
      expect(result.hasMore, isTrue);
    });

    test('getWasteReports translates 401 Unauthorized into ApiException', () async {
      mockAdapter.statusCode = 401;
      mockAdapter.responseBody = jsonEncode({
        'type': 'https://tools.ietf.org/html/rfc9110#section-15.5.2',
        'title': 'Unauthorized',
        'status': 401,
        'detail': 'Full authentication is required to access this resource.',
      });

      expect(
        () => api.getWasteReports(),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 401)),
      );
    });

    test('getWasteReportHistory sends GET /waste-reports/{id}/history and returns list of models', () async {
      mockAdapter.responseBody = jsonEncode([
        {
          'id': 'hist-1',
          'wasteReportId': 'rep-1',
          'fromStatus': null,
          'toStatus': 'Submitted',
          'changedByUserId': 'cit-1',
          'changedByUserName': 'Jane Citizen',
          'notes': null,
          'changedAt': '2026-09-16T12:00:00.000Z',
        },
        {
          'id': 'hist-2',
          'wasteReportId': 'rep-1',
          'fromStatus': 'Submitted',
          'toStatus': 'UnderReview',
          'changedByUserId': 'off-1',
          'changedByUserName': 'Officer John',
          'notes': 'Under site inspection',
          'changedAt': '2026-09-16T12:30:00.000Z',
        },
      ]);

      final result = await api.getWasteReportHistory('rep-1');

      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-1/history');
      expect(result.length, 2);
      expect(result[0].id, 'hist-1');
      expect(result[0].fromStatus, isNull);
      expect(result[0].toStatus, WasteReportStatus.submitted);
      expect(result[1].id, 'hist-2');
      expect(result[1].fromStatus, WasteReportStatus.submitted);
      expect(result[1].toStatus, WasteReportStatus.underReview);
      expect(result[1].notes, 'Under site inspection');
    });

    test('getWasteReportHistory translates 404 NotFound into ApiException', () async {
      mockAdapter.statusCode = 404;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Not Found',
        'status': 404,
        'detail': 'Report not found.',
      });

      expect(
        () => api.getWasteReportHistory('rep-nonexistent'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 404)),
      );
    });

    test('updateWasteReport sends PATCH /waste-reports/{id} with JSON body and returns updated model', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'rep-edit-1',
        'citizenId': 'cit-1',
        'citizenName': 'Jane Citizen',
        'description': 'Updated detailed description',
        'wasteType': 'Recyclable',
        'latitude': 6.9300,
        'longitude': 79.8500,
        'addressText': 'New Address',
        'status': 'Submitted',
        'priority': null,
        'verifiedByUserId': null,
        'verifiedByUserName': null,
        'verifiedAt': null,
        'attachments': <dynamic>[],
        'createdAt': '2026-09-16T12:00:00.000Z',
        'updatedAt': '2026-09-17T10:00:00.000Z',
      });

      const request = UpdateWasteReportRequest(
        description: 'Updated detailed description',
        wasteType: WasteType.recyclable,
        latitude: 6.9300,
        longitude: 79.8500,
        addressText: 'New Address',
      );

      final updated = await api.updateWasteReport(reportId: 'rep-edit-1', request: request);

      expect(mockAdapter.lastRequest!.method, 'PATCH');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-edit-1');
      expect(mockAdapter.lastRequest!.data, request.toJson());
      expect(updated.id, 'rep-edit-1');
      expect(updated.description, 'Updated detailed description');
      expect(updated.wasteType, WasteType.recyclable);
      expect(updated.latitude, 6.9300);
      expect(updated.longitude, 79.8500);
      expect(updated.addressText, 'New Address');
      expect(updated.updatedAt, isNotNull);
    });

    test('updateWasteReport sends empty string to clear address', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'rep-edit-2',
        'citizenId': 'cit-1',
        'citizenName': 'Jane Citizen',
        'description': 'Existing description',
        'wasteType': 'General',
        'latitude': 6.9300,
        'longitude': 79.8500,
        'addressText': null,
        'status': 'Submitted',
        'priority': null,
        'verifiedByUserId': null,
        'verifiedByUserName': null,
        'verifiedAt': null,
        'attachments': <dynamic>[],
        'createdAt': '2026-09-16T12:00:00.000Z',
        'updatedAt': '2026-09-17T10:00:00.000Z',
      });

      const request = UpdateWasteReportRequest(
        addressText: '',
      );

      final updated = await api.updateWasteReport(reportId: 'rep-edit-2', request: request);

      expect(mockAdapter.lastRequest!.method, 'PATCH');
      expect(mockAdapter.lastRequest!.data, {'addressText': ''});
      expect(updated.addressText, isNull);
    });

    test('updateWasteReport translates 403 Forbidden into ApiException when not owner', () async {
      mockAdapter.statusCode = 403;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Forbidden',
        'status': 403,
        'detail': 'You can only update your own waste reports.',
      });

      expect(
        () => api.updateWasteReport(
          reportId: 'rep-other-user',
          request: const UpdateWasteReportRequest(description: 'Illegal edit attempt'),
        ),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 403)
              .having((e) => e.message, 'message', 'Access denied. Your account lacks required permissions.'),
        ),
      );
    });

    test('updateWasteReport translates 409 Conflict into ApiException when status is no longer Submitted', () async {
      mockAdapter.statusCode = 409;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Conflict',
        'status': 409,
        'detail': 'Waste report cannot be updated. Current status does not allow evidence edits.',
      });

      expect(
        () => api.updateWasteReport(
          reportId: 'rep-already-verified',
          request: const UpdateWasteReportRequest(description: 'Too late edit attempt'),
        ),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 409)
              .having((e) => e.message, 'message', contains('does not allow evidence edits')),
        ),
      );
    });

    test('cancelWasteReport sends DELETE /waste-reports/{id} with no body and completes successfully', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'message': 'Waste report cancelled successfully.',
        'status': 'Cancelled',
      });

      await api.cancelWasteReport('rep-cancel-1');

      expect(mockAdapter.lastRequest!.method, 'DELETE');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-cancel-1');
      expect(mockAdapter.lastRequest!.data, isNull);
    });

    test('cancelWasteReport translates 409 Conflict into ApiException when status is no longer Submitted', () async {
      mockAdapter.statusCode = 409;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Conflict',
        'status': 409,
        'detail': 'Waste report cannot be cancelled. Current status does not allow cancellation.',
      });

      expect(
        () => api.cancelWasteReport('rep-cancel-conflict'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 409)
              .having((e) => e.message, 'message', contains('does not allow cancellation')),
        ),
      );
    });

    test('cancelWasteReport translates 403 Forbidden into ApiException when not owner', () async {
      mockAdapter.statusCode = 403;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Forbidden',
        'status': 403,
        'detail': 'You can only cancel your own waste reports.',
      });

      expect(
        () => api.cancelWasteReport('rep-cancel-not-owner'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 403)
              .having((e) => e.message, 'message', 'Access denied. Your account lacks required permissions.'),
        ),
      );
    });

    test('cancelWasteReport translates 404 NotFound into ApiException when report does not exist', () async {
      mockAdapter.statusCode = 404;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Not Found',
        'status': 404,
        'detail': 'Waste report not found.',
      });

      expect(
        () => api.cancelWasteReport('rep-cancel-404'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 404)
              .having((e) => e.message, 'message', contains('not found')),
        ),
      );
    });

    test('cancelWasteReport translates 500 into ApiException on server failure', () async {
      mockAdapter.statusCode = 500;
      mockAdapter.responseBody = jsonEncode({
        'title': 'Internal Server Error',
        'status': 500,
        'detail': 'Database connection error.',
      });

      expect(
        () => api.cancelWasteReport('rep-cancel-500'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 500),
        ),
      );
    });
  });

  group('ReportingRepository Tests', () {
    late MockHttpClientAdapter mockAdapter;
    late ReportingApi api;
    late ReportingRepository repository;

    setUp(() {
      mockAdapter = MockHttpClientAdapter();
      final dio = Dio(BaseOptions(baseUrl: 'http://localhost:5276/api/v1'));
      dio.httpClientAdapter = mockAdapter;
      final dioClient = DioClient(
        dio: dio,
        storageService: FakeSecureStorageService(),
        baseUrl: 'http://localhost:5276/api/v1',
      );
      api = ReportingApi(client: dioClient);
      repository = ReportingRepository(api: api);
    });

    test('delegates create, upload, get, and delete to ReportingApi', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'rep-repo-1',
        'citizenId': 'cit-1',
        'citizenName': 'Citizen A',
        'description': 'Test report via repository',
        'wasteType': 'Organic',
        'latitude': 6.9000,
        'longitude': 79.8500,
        'addressText': null,
        'status': 'Submitted',
        'priority': null,
        'verifiedByUserId': null,
        'verifiedByUserName': null,
        'verifiedAt': null,
        'attachments': <dynamic>[],
        'createdAt': '2026-09-16T12:00:00.000Z',
        'updatedAt': null,
      });

      const request = CreateWasteReportRequest(
        description: 'Test report via repository',
        wasteType: WasteType.organic,
        latitude: 6.9000,
        longitude: 79.8500,
      );

      final report = await repository.createWasteReport(request);
      expect(report.id, 'rep-repo-1');
      expect(report.wasteType, WasteType.organic);

      // Upload attachment
      mockAdapter.responseBody = jsonEncode({
        'id': 'att-repo-1',
        'wasteReportId': 'rep-repo-1',
        'fileUrl': 'https://storage/signed-url',
        'fileType': 'image/jpeg',
        'createdAt': '2026-09-16T12:01:00.000Z',
      });
      final attachment = await repository.uploadAttachmentBytes(
        reportId: 'rep-repo-1',
        bytes: [0xFF, 0xD8, 0xFF],
        fileName: 'test.jpg',
        fileType: 'image/jpeg',
      );
      expect(attachment.id, 'att-repo-1');

      // Get detail
      mockAdapter.responseBody = jsonEncode({
        'id': 'rep-repo-1',
        'citizenId': 'cit-1',
        'citizenName': 'Citizen A',
        'description': 'Test report via repository',
        'wasteType': 'Organic',
        'latitude': 6.9000,
        'longitude': 79.8500,
        'addressText': null,
        'status': 'Submitted',
        'priority': null,
        'verifiedByUserId': null,
        'verifiedByUserName': null,
        'verifiedAt': null,
        'attachments': <dynamic>[],
        'createdAt': '2026-09-16T12:00:00.000Z',
        'updatedAt': null,
      });
      final detail = await repository.getWasteReport('rep-repo-1');
      expect(detail.id, 'rep-repo-1');

      // Delete attachment
      mockAdapter.responseBody = jsonEncode({'message': 'Attachment removed successfully.'});
      await repository.deleteAttachment(reportId: 'rep-repo-1', attachmentId: 'att-repo-1');
      expect(mockAdapter.lastRequest!.method, 'DELETE');
    });

    test('getWasteReports delegates directly to ReportingApi', () async {
      mockAdapter.responseBody = jsonEncode({
        'items': [
          {
            'id': 'rep-repo-list-1',
            'description': 'Plastic waste dump',
            'wasteType': 'Recyclable',
            'status': 'Verified',
            'priority': 'Medium',
            'addressText': 'Coastal Road',
            'latitude': 6.9100,
            'longitude': 79.8600,
            'citizenId': null,
            'citizenName': null,
            'createdAt': '2026-09-16T15:00:00.000Z',
            'updatedAt': null,
          }
        ],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'totalPages': 1,
      });

      final result = await repository.getWasteReports(
        status: WasteReportStatus.verified,
        search: 'coastal',
      );

      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.queryParameters['status'], 'Verified');
      expect(mockAdapter.lastRequest!.queryParameters['search'], 'coastal');
      expect(result.items.first.id, 'rep-repo-list-1');
      expect(result.items.first.status, WasteReportStatus.verified);
    });

    test('getWasteReportHistory delegates directly to ReportingApi', () async {
      mockAdapter.responseBody = jsonEncode([
        {
          'id': 'hist-repo-1',
          'wasteReportId': 'rep-repo-1',
          'fromStatus': null,
          'toStatus': 'Submitted',
          'changedByUserId': 'cit-1',
          'changedByUserName': 'Jane Citizen',
          'notes': null,
          'changedAt': '2026-09-16T12:00:00.000Z',
        }
      ]);

      final result = await repository.getWasteReportHistory('rep-repo-1');

      expect(mockAdapter.lastRequest!.method, 'GET');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-repo-1/history');
      expect(result.length, 1);
      expect(result.first.id, 'hist-repo-1');
      expect(result.first.toStatus, WasteReportStatus.submitted);
    });

    test('updateWasteReport delegates directly to ReportingApi', () async {
      mockAdapter.responseBody = jsonEncode({
        'id': 'rep-repo-edit-1',
        'citizenId': 'cit-1',
        'citizenName': 'Jane Citizen',
        'description': 'Updated description',
        'wasteType': 'Bulky',
        'latitude': 6.9400,
        'longitude': 79.8700,
        'addressText': 'Old Furniture Spot',
        'status': 'Submitted',
        'priority': null,
        'verifiedByUserId': null,
        'verifiedByUserName': null,
        'verifiedAt': null,
        'attachments': <dynamic>[],
        'createdAt': '2026-09-16T12:00:00.000Z',
        'updatedAt': '2026-09-17T11:00:00.000Z',
      });

      const request = UpdateWasteReportRequest(
        description: 'Updated description',
        wasteType: WasteType.bulky,
      );

      final result = await repository.updateWasteReport(
        reportId: 'rep-repo-edit-1',
        request: request,
      );

      expect(mockAdapter.lastRequest!.method, 'PATCH');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-repo-edit-1');
      expect(result.id, 'rep-repo-edit-1');
      expect(result.wasteType, WasteType.bulky);
    });

    test('cancelWasteReport delegates directly to ReportingApi', () async {
      mockAdapter.statusCode = 200;
      mockAdapter.responseBody = jsonEncode({
        'message': 'Waste report cancelled successfully.',
        'status': 'Cancelled',
      });

      await repository.cancelWasteReport('rep-repo-cancel-1');

      expect(mockAdapter.lastRequest!.method, 'DELETE');
      expect(mockAdapter.lastRequest!.path, '/waste-reports/rep-repo-cancel-1');
    });
  });
}
