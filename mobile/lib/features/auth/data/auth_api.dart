import 'package:dio/dio.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/dio_client.dart';
import '../models/auth_response.dart';
import '../models/auth_user.dart';

/// Data source executing raw HTTP calls for authentication against ASP.NET Core endpoints.
class AuthApi {
  final DioClient _client;

  AuthApi({DioClient? client}) : _client = client ?? DioClient();

  /// Registers a citizen. Public registration strictly creates a Citizen role.
  Future<AuthResponse> registerCitizen({
    required String fullName,
    required String email,
    required String phoneNumber,
    required String password,
  }) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.register,
        data: {
          'fullName': fullName,
          'email': email,
          'phoneNumber': phoneNumber,
          'password': password,
        },
      );
      return AuthResponse.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Authenticates with email and password.
  Future<AuthResponse> login({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.login,
        data: {
          'email': email,
          'password': password,
          'clientType': 'mobile',
        },
      );
      return AuthResponse.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Changes password for currently authenticated user.
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    try {
      await _client.dio.post(
        ApiConstants.changePassword,
        data: {
          'currentPassword': currentPassword,
          'newPassword': newPassword,
        },
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Fetches profile of currently authenticated user using JWT token.
  Future<AuthUser> getCurrentUser() async {
    try {
      final response = await _client.dio.get(ApiConstants.me);
      return AuthUser.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }
}
