import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';

class InMemorySecureStorageService extends SecureStorageService {
  final Map<String, String> _store = {};

  @override
  Future<void> saveAccessToken(String token) async {
    _store['smartwaste_access_token'] = token;
  }

  @override
  Future<String?> getAccessToken() async {
    return _store['smartwaste_access_token'];
  }

  @override
  Future<void> deleteAccessToken() async {
    _store.remove('smartwaste_access_token');
  }

  @override
  Future<bool> hasAccessToken() async {
    final token = await getAccessToken();
    return token != null && token.isNotEmpty;
  }
}

void main() {
  group('SecureStorageService Tests', () {
    test('saves, retrieves, and clears access token', () async {
      final storage = InMemorySecureStorageService();

      expect(await storage.hasAccessToken(), isFalse);
      expect(await storage.getAccessToken(), isNull);

      await storage.saveAccessToken('sample-jwt-token');
      expect(await storage.hasAccessToken(), isTrue);
      expect(await storage.getAccessToken(), 'sample-jwt-token');

      await storage.deleteAccessToken();
      expect(await storage.hasAccessToken(), isFalse);
      expect(await storage.getAccessToken(), isNull);
    });
  });
}
