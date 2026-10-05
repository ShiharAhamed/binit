import 'package:dio/dio.dart';

/// Centralized application API exception handling HTTP and network failure modes.
class ApiException implements Exception {
  final String message;
  final int? statusCode;
  final String? errorCode;
  final dynamic details;

  const ApiException({
    required this.message,
    this.statusCode,
    this.errorCode,
    this.details,
  });

  factory ApiException.fromDioError(DioException error) {
    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return const ApiException(
          message: 'Connection timed out. Please check your network.',
          statusCode: 408,
        );

      case DioExceptionType.connectionError:
        return const ApiException(
          message: 'Unable to connect to the Smart Waste server. Please verify backend is running.',
        );

      case DioExceptionType.badResponse:
        final statusCode = error.response?.statusCode;
        final responseData = error.response?.data;

        String extractedMessage = 'An unexpected server error occurred.';
        String? errorCode;

        if (responseData is Map<String, dynamic>) {
          if (responseData['errorCode'] != null) {
            errorCode = responseData['errorCode'].toString();
          } else if (responseData['extensions'] is Map<String, dynamic> &&
              responseData['extensions']['errorCode'] != null) {
            errorCode = responseData['extensions']['errorCode'].toString();
          }

          if (responseData['detail'] != null) {
            extractedMessage = responseData['detail'].toString();
          } else if (responseData['title'] != null) {
            extractedMessage = responseData['title'].toString();
          } else if (responseData['message'] != null) {
            extractedMessage = responseData['message'].toString();
          }
        }

        switch (statusCode) {
          case 400:
            return ApiException(
              message: extractedMessage.isNotEmpty
                  ? extractedMessage
                  : 'Invalid request data. Please check your input.',
              statusCode: 400,
              errorCode: errorCode,
              details: responseData,
            );
          case 401:
            return ApiException(
              message: extractedMessage.isNotEmpty
                  ? extractedMessage
                  : 'Invalid email or password.',
              statusCode: 401,
              errorCode: errorCode,
            );
          case 403:
            if (errorCode == 'unsupported_client_role' ||
                extractedMessage == 'This account is for the SmartWaste web application.') {
              return ApiException(
                message: 'This account is for the SmartWaste web application.',
                statusCode: 403,
                errorCode: errorCode ?? 'unsupported_client_role',
                details: responseData,
              );
            }
            return ApiException(
              message: 'Access denied. Your account lacks required permissions.',
              statusCode: 403,
              errorCode: errorCode,
              details: responseData,
            );
          case 404:
            return const ApiException(
              message: 'Requested resource was not found.',
              statusCode: 404,
            );
          case 409:
            return ApiException(
              message: extractedMessage.isNotEmpty
                  ? extractedMessage
                  : 'A conflict occurred. This email may already be registered.',
              statusCode: 409,
            );
          case 500:
          default:
            return const ApiException(
              message: 'Server error. Please try again later.',
              statusCode: 500,
            );
        }

      case DioExceptionType.cancel:
        return const ApiException(message: 'Request was cancelled.');

      case DioExceptionType.unknown:
      default:
        return const ApiException(
          message: 'A network communication error occurred.',
        );
    }
  }

  @override
  String toString() => message;
}
