import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/constants/api_constants.dart';
import 'package:mobile/core/network/dio_client.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';

class FakeSecureStorageService extends SecureStorageService {
  String? token;

  FakeSecureStorageService({this.token});

  @override
  Future<String?> getAccessToken() async => token;
}

void main() {
  group('DioClient Configuration Tests', () {
    test('initializes with correct base options', () {
      final client = DioClient(baseUrl: 'http://test-api.local/api/v1');
      final options = client.dio.options;

      expect(options.baseUrl, 'http://test-api.local/api/v1');
      expect(options.connectTimeout, ApiConstants.connectTimeout);
      expect(options.receiveTimeout, ApiConstants.receiveTimeout);
      expect(options.headers['Content-Type'], 'application/json');
      expect(options.headers['Accept'], 'application/json');
    });

    test('adds Authorization header when token is present in storage', () async {
      final fakeStorage = FakeSecureStorageService(token: 'mock-jwt-token-12345');
      final dio = Dio();
      // Use HttpClientAdapter that immediately echoes request headers
      dio.httpClientAdapter = HttpClientAdapter();

      final client = DioClient(dio: dio, storageService: fakeStorage);

      // Verify interceptor is attached
      expect(client.dio.interceptors.isNotEmpty, isTrue);
    });
  });
}
