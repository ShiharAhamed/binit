import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Secure token persistence service utilizing platform keychain/keystore.
class SecureStorageService {
  final FlutterSecureStorage _storage;

  static const String _accessTokenKey = 'smartwaste_access_token';

  const SecureStorageService({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  /// Securely saves the JWT access token.
  Future<void> saveAccessToken(String token) async {
    await _storage.write(key: _accessTokenKey, value: token);
  }

  /// Retrieves the stored JWT access token, or null if not found.
  Future<String?> getAccessToken() async {
    return await _storage.read(key: _accessTokenKey);
  }

  /// Deletes the stored JWT access token.
  Future<void> deleteAccessToken() async {
    await _storage.delete(key: _accessTokenKey);
  }

  /// Returns true if an access token is stored.
  Future<bool> hasAccessToken() async {
    final token = await getAccessToken();
    return token != null && token.isNotEmpty;
  }
}
