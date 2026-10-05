import '../../../core/network/api_exception.dart';
import '../../../core/storage/secure_storage_service.dart';
import '../models/auth_response.dart';
import '../models/auth_user.dart';
import 'auth_api.dart';

/// Repository coordinating AuthApi requests and SecureStorageService token persistence.
class AuthRepository {
  final AuthApi _api;
  final SecureStorageService _storage;

  AuthRepository({
    AuthApi? api,
    SecureStorageService? storage,
  })  : _api = api ?? AuthApi(),
        _storage = storage ?? const SecureStorageService();

  /// Registers a new citizen and saves access token securely.
  Future<AuthResponse> registerCitizen({
    required String fullName,
    required String email,
    required String phoneNumber,
    required String password,
  }) async {
    final response = await _api.registerCitizen(
      fullName: fullName,
      email: email,
      phoneNumber: phoneNumber,
      password: password,
    );
    return response;
  }

  /// Logs in user and securely stores JWT access token.
  /// Rejects web-only roles (WasteOfficer, MunicipalManager) defensively.
  Future<AuthResponse> login({
    required String email,
    required String password,
  }) async {
    final response = await _api.login(email: email, password: password);
    if (response.user.role != AppRoles.citizen && response.user.role != AppRoles.driver) {
      await _storage.deleteAccessToken();
      throw const ApiException(
        message: 'This account is for the SmartWaste web application.',
        statusCode: 403,
        errorCode: 'unsupported_client_role',
      );
    }
    await _storage.saveAccessToken(response.accessToken);
    return response;
  }

  /// Changes user password and wipes stored token forcing re-login.
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    await _api.changePassword(
      currentPassword: currentPassword,
      newPassword: newPassword,
    );
    await _storage.deleteAccessToken();
  }

  /// Fetches profile of current user using stored token.
  Future<AuthUser> getCurrentUser() async {
    return await _api.getCurrentUser();
  }

  /// Clears stored credentials.
  Future<void> logout() async {
    await _storage.deleteAccessToken();
  }

  /// Returns true if a token is present in secure storage.
  Future<bool> hasToken() async {
    return await _storage.hasAccessToken();
  }
}
