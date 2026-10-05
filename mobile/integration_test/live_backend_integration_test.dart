// ignore_for_file: avoid_print

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/network/dio_client.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';
import 'package:mobile/features/auth/data/auth_api.dart';
import 'package:mobile/features/auth/data/auth_repository.dart';
import 'package:mobile/features/auth/models/auth_user.dart';

class InMemorySecureStorageService extends SecureStorageService {
  final Map<String, String> _store = {};

  InMemorySecureStorageService([Map<String, String>? initial]) {
    if (initial != null) _store.addAll(initial);
  }

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
  const baseUrl = 'http://localhost:5276/api/v1';
  final timestamp = DateTime.now().millisecondsSinceEpoch;
  final testCitizenEmail = 'citizen_$timestamp@smartwaste.test';
  final testCitizenPassword = 'CitizenPassword123!';
  final testCitizenName = 'Integration Citizen $timestamp';
  final testCitizenPhone = '+9477$timestamp'.substring(0, 12);

  group('Live ASP.NET Core Backend Integration Tests', () {
    late InMemorySecureStorageService storage;
    late DioClient dioClient;
    late AuthApi authApi;
    late AuthRepository authRepo;

    setUp(() {
      storage = InMemorySecureStorageService();
      dioClient = DioClient(baseUrl: baseUrl, storageService: storage);
      authApi = AuthApi(client: dioClient);
      authRepo = AuthRepository(api: authApi, storage: storage);
    });

    test('Case 1: Register a new citizen from mobile app logic returns JWT and Citizen role', () async {
      print('\n[CASE 1] Registering citizen: $testCitizenEmail');
      final response = await authRepo.registerCitizen(
        fullName: testCitizenName,
        email: testCitizenEmail,
        phoneNumber: testCitizenPhone,
        password: testCitizenPassword,
      );

      print('Status: SUCCESS');
      print('Access Token: ${response.accessToken.substring(0, 25)}...');
      print('Expires At: ${response.expiresAt}');
      print('User ID: ${response.user.id}');
      print('User FullName: ${response.user.fullName}');
      print('User Role: ${response.user.role}');

      expect(response.accessToken.isNotEmpty, isTrue);
      expect(response.user.email.toLowerCase(), equals(testCitizenEmail.toLowerCase()));
      expect(response.user.role, equals(AppRoles.citizen));
    });

    test('Case 2 & 3: Login with registered citizen returns JWT and saves to secure storage', () async {
      print('\n[CASE 2 & 3] Logging in citizen: $testCitizenEmail');
      final response = await authRepo.login(
        email: testCitizenEmail,
        password: testCitizenPassword,
      );

      print('Status: SUCCESS');
      print('Access Token: ${response.accessToken.substring(0, 25)}...');
      print('User Role: ${response.user.role}');

      expect(response.accessToken.isNotEmpty, isTrue);
      expect(response.user.role, equals(AppRoles.citizen));

      // Case 3 verification: token stored securely
      final storedToken = await storage.getAccessToken();
      print('Case 3 Stored Token: ${storedToken?.substring(0, 25)}...');
      expect(storedToken, equals(response.accessToken));
      expect(await authRepo.hasToken(), isTrue);
    });

    test('Case 4: GET /auth/me with stored JWT returns citizen user profile', () async {
      print('\n[CASE 4] Calling GET /auth/me using stored JWT');
      // Pre-save token into storage
      final loginRes = await authRepo.login(
        email: testCitizenEmail,
        password: testCitizenPassword,
      );
      await storage.saveAccessToken(loginRes.accessToken);

      final user = await authRepo.getCurrentUser();
      print('Status: 200 OK');
      print('Profile FullName: ${user.fullName}');
      print('Profile Email: ${user.email}');
      print('Profile Role: ${user.role}');

      expect(user.email.toLowerCase(), equals(testCitizenEmail.toLowerCase()));
      expect(user.role, equals(AppRoles.citizen));
      expect(user.isCitizen, isTrue);
    });

    test('Case 5: Session restoration with new AuthRepository instance using stored token', () async {
      print('\n[CASE 5] Verifying session restoration with independent repository instance');
      // Acquire token
      final loginRes = await authRepo.login(
        email: testCitizenEmail,
        password: testCitizenPassword,
      );
      final persistedStorage = InMemorySecureStorageService({
        'smartwaste_access_token': loginRes.accessToken,
      });

      // Brand new instances simulating app restart
      final newDioClient = DioClient(baseUrl: baseUrl, storageService: persistedStorage);
      final newAuthApi = AuthApi(client: newDioClient);
      final newAuthRepo = AuthRepository(api: newAuthApi, storage: persistedStorage);

      expect(await newAuthRepo.hasToken(), isTrue);

      final restoredUser = await newAuthRepo.getCurrentUser();
      print('Status: Restored successfully');
      print('Restored User: ${restoredUser.fullName} (${restoredUser.role})');

      expect(restoredUser.email.toLowerCase(), equals(testCitizenEmail.toLowerCase()));
      expect(restoredUser.isCitizen, isTrue);
    });

    test('Case 6: Attempt login with invalid password returns 401 with ProblemDetails parsed by ApiException', () async {
      print('\n[CASE 6] Attempting login with incorrect password');
      try {
        await authRepo.login(
          email: testCitizenEmail,
          password: 'WrongPassword999!',
        );
        fail('Should have thrown ApiException');
      } on ApiException catch (e) {
        print('Status: 401 Caught ApiException');
        print('Error message: ${e.message}');
        print('Status code: ${e.statusCode}');

        expect(e.statusCode, 401);
        expect(e.message, isNotEmpty);
      }
    });

    test('Case 7: Logout clears secure storage and subsequent hasToken returns false', () async {
      print('\n[CASE 7] Executing logout');
      await authRepo.login(
        email: testCitizenEmail,
        password: testCitizenPassword,
      );
      expect(await authRepo.hasToken(), isTrue);

      await authRepo.logout();

      final tokenAfterLogout = await storage.getAccessToken();
      print('Token after logout: $tokenAfterLogout');
      expect(tokenAfterLogout, isNull);
      expect(await authRepo.hasToken(), isFalse);
    });

    test('Case 8: Attempt accessing protected endpoint without token returns 401 Unauthorized', () async {
      print('\n[CASE 8] Attempting GET /auth/me without authorization token');
      final unauthedStorage = InMemorySecureStorageService();
      final unauthedClient = DioClient(baseUrl: baseUrl, storageService: unauthedStorage);
      final unauthedApi = AuthApi(client: unauthedClient);

      try {
        await unauthedApi.getCurrentUser();
        fail('Should have thrown ApiException on unauthenticated request');
      } on ApiException catch (e) {
        print('Status: 401 Caught ApiException');
        print('Error message: ${e.message}');
        print('Status code: ${e.statusCode}');

        expect(e.statusCode, 401);
      }
    });
  });
}
