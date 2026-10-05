import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';

void main() {
  group('ApiException Tests', () {
    test('extracts detail from ProblemDetails response', () {
      final requestOptions = RequestOptions(path: '/api/v1/auth/login');
      final dioException = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: requestOptions,
          statusCode: 401,
          data: {
            'type': 'https://tools.ietf.org/html/rfc7235#section-3.1',
            'title': 'Unauthorized',
            'status': 401,
            'detail': 'Invalid email or password',
          },
        ),
      );

      final apiException = ApiException.fromDioError(dioException);

      expect(apiException.statusCode, 401);
      expect(apiException.message, 'Invalid email or password');
    });

    test('extracts title when detail is absent in ProblemDetails', () {
      final requestOptions = RequestOptions(path: '/api/v1/auth/register');
      final dioException = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: requestOptions,
          statusCode: 400,
          data: {
            'title': 'Validation Error',
            'status': 400,
          },
        ),
      );

      final apiException = ApiException.fromDioError(dioException);

      expect(apiException.statusCode, 400);
      expect(apiException.message, 'Validation Error');
    });

    test('handles connection timeouts', () {
      final requestOptions = RequestOptions(path: '/api/v1/auth/me');
      final dioException = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.connectionTimeout,
      );

      final apiException = ApiException.fromDioError(dioException);

      expect(apiException.statusCode, 408);
      expect(apiException.message, contains('timed out'));
    });

    test('handles connection errors (backend unreachable)', () {
      final requestOptions = RequestOptions(path: '/api/v1/auth/me');
      final dioException = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.connectionError,
      );

      final apiException = ApiException.fromDioError(dioException);

      expect(apiException.message, contains('Unable to connect to the Smart Waste server'));
    });

    test('handles 403 Forbidden and 404 Not Found', () {
      final requestOptions = RequestOptions(path: '/api/v1/admin');
      final dioException403 = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: requestOptions, statusCode: 403),
      );
      final apiException403 = ApiException.fromDioError(dioException403);
      expect(apiException403.statusCode, 403);
      expect(apiException403.message, 'Access denied. Your account lacks required permissions.');

      final dioException404 = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: requestOptions, statusCode: 404),
      );
      final apiException404 = ApiException.fromDioError(dioException404);
      expect(apiException404.statusCode, 404);
      expect(apiException404.message, contains('not found'));
    });

    test('maps 403 with unsupported_client_role errorCode to web application message', () {
      final requestOptions = RequestOptions(path: '/api/v1/auth/login');
      final dioException = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: requestOptions,
          statusCode: 403,
          data: {
            'type': 'https://httpstatuses.com/403',
            'title': 'Unsupported Client Role',
            'status': 403,
            'detail': 'This account is for the SmartWaste web application.',
            'instance': '/api/v1/auth/login',
            'errorCode': 'unsupported_client_role',
          },
        ),
      );

      final apiException = ApiException.fromDioError(dioException);

      expect(apiException.statusCode, 403);
      expect(apiException.errorCode, 'unsupported_client_role');
      expect(apiException.message, 'This account is for the SmartWaste web application.');
    });

    test('maps 403 with extensions.errorCode unsupported_client_role to web application message', () {
      final requestOptions = RequestOptions(path: '/api/v1/auth/login');
      final dioException = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: requestOptions,
          statusCode: 403,
          data: {
            'title': 'Unsupported Client Role',
            'status': 403,
            'detail': 'This account is for the SmartWaste web application.',
            'extensions': {'errorCode': 'unsupported_client_role'},
          },
        ),
      );

      final apiException = ApiException.fromDioError(dioException);

      expect(apiException.statusCode, 403);
      expect(apiException.errorCode, 'unsupported_client_role');
      expect(apiException.message, 'This account is for the SmartWaste web application.');
    });

    test('retains generic access-denied message for unrelated 403 errors', () {
      final requestOptions = RequestOptions(path: '/api/v1/protected-resource');
      final dioException = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: requestOptions,
          statusCode: 403,
          data: {
            'title': 'Forbidden',
            'status': 403,
            'detail': 'User does not have role X.',
          },
        ),
      );

      final apiException = ApiException.fromDioError(dioException);

      expect(apiException.statusCode, 403);
      expect(apiException.message, 'Access denied. Your account lacks required permissions.');
    });

    test('handles 409 Conflict', () {
      final requestOptions = RequestOptions(path: '/api/v1/auth/register');
      final dioException = DioException(
        requestOptions: requestOptions,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: requestOptions,
          statusCode: 409,
          data: {'detail': 'User already exists'},
        ),
      );

      final apiException = ApiException.fromDioError(dioException);

      expect(apiException.statusCode, 409);
      expect(apiException.message, 'User already exists');
    });
  });
}
