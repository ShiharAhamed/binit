import 'package:dio/dio.dart';
import '../constants/api_constants.dart';
import '../storage/secure_storage_service.dart';

/// Centralized Dio HTTP client configured with base URL, timeouts, and JWT Bearer interceptor.
class DioClient {
  final Dio _dio;
  final SecureStorageService _storageService;

  DioClient({
    Dio? dio,
    SecureStorageService? storageService,
    String? baseUrl,
  })  : _dio = dio ?? Dio(),
        _storageService = storageService ?? const SecureStorageService() {
    _dio.options = BaseOptions(
      baseUrl: baseUrl ?? ApiConstants.baseUrl,
      connectTimeout: ApiConstants.connectTimeout,
      receiveTimeout: ApiConstants.receiveTimeout,
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
    );

    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final token = await _storageService.getAccessToken();
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          return handler.next(options);
        },
        onError: (DioException error, handler) {
          return handler.next(error);
        },
      ),
    );
  }

  Dio get dio => _dio;
}
