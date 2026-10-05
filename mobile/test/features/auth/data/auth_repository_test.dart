import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';
import 'package:mobile/features/auth/data/auth_api.dart';
import 'package:mobile/features/auth/data/auth_repository.dart';
import 'package:mobile/features/auth/models/auth_response.dart';
import 'package:mobile/features/auth/models/auth_user.dart';

class FakeAuthApi extends AuthApi {
  AuthResponse? registerResult;
  AuthResponse? loginResult;
  AuthUser? currentUserResult;
  bool shouldThrow = false;

  @override
  Future<AuthResponse> registerCitizen({
    required String fullName,
    required String email,
    required String phoneNumber,
    required String password,
  }) async {
    if (shouldThrow) throw Exception('Registration failed');
    return registerResult!;
  }

  ApiException? throwApiException;

  @override
  Future<AuthResponse> login({
    required String email,
    required String password,
  }) async {
    if (throwApiException != null) throw throwApiException!;
    if (shouldThrow) throw Exception('Login failed');
    return loginResult!;
  }

  @override
  Future<AuthUser> getCurrentUser() async {
    if (shouldThrow) throw Exception('Unauthorized');
    return currentUserResult!;
  }

  bool changePasswordCalled = false;
  String? lastCurrentPassword;
  String? lastNewPassword;

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    if (shouldThrow) throw Exception('Change password failed');
    changePasswordCalled = true;
    lastCurrentPassword = currentPassword;
    lastNewPassword = newPassword;
  }
}

class FakeSecureStorageService extends SecureStorageService {
  String? _token;

  @override
  Future<void> saveAccessToken(String token) async {
    _token = token;
  }

  @override
  Future<String?> getAccessToken() async => _token;

  @override
  Future<void> deleteAccessToken() async {
    _token = null;
  }

  @override
  Future<bool> hasAccessToken() async => _token != null && _token!.isNotEmpty;
}

void main() {
  group('AuthRepository Tests', () {
    late FakeAuthApi fakeApi;
    late FakeSecureStorageService fakeStorage;
    late AuthRepository repository;

    final dummyUser = const AuthUser(
      id: 'test-user-id',
      fullName: 'Sunil Perera',
      email: 'sunil@binit.lk',
      phoneNumber: '+94771234567',
      role: AppRoles.citizen,
    );

    final dummyAuthResponse = AuthResponse(
      accessToken: 'valid-test-jwt-token',
      expiresAt: DateTime.now().add(const Duration(hours: 1)),
      user: dummyUser,
    );

    setUp(() {
      fakeApi = FakeAuthApi();
      fakeStorage = FakeSecureStorageService();
      repository = AuthRepository(api: fakeApi, storage: fakeStorage);
    });

    test('registerCitizen returns AuthResponse from API', () async {
      fakeApi.registerResult = dummyAuthResponse;

      final res = await repository.registerCitizen(
        fullName: 'Sunil Perera',
        email: 'sunil@binit.lk',
        phoneNumber: '+94771234567',
        password: 'Password123!',
      );

      expect(res.accessToken, 'valid-test-jwt-token');
      expect(res.user.fullName, 'Sunil Perera');
    });

    test('login saves access token in secure storage and returns AuthResponse', () async {
      fakeApi.loginResult = dummyAuthResponse;

      final res = await repository.login(
        email: 'sunil@binit.lk',
        password: 'Password123!',
      );

      expect(res.accessToken, 'valid-test-jwt-token');
      expect(await fakeStorage.getAccessToken(), 'valid-test-jwt-token');
      expect(await repository.hasToken(), isTrue);
    });

    test('getCurrentUser retrieves user profile from API', () async {
      fakeApi.currentUserResult = dummyUser;

      final user = await repository.getCurrentUser();

      expect(user.id, 'test-user-id');
      expect(user.fullName, 'Sunil Perera');
      expect(user.isCitizen, isTrue);
    });

    test('logout deletes token from secure storage', () async {
      await fakeStorage.saveAccessToken('existing-token');
      expect(await repository.hasToken(), isTrue);

      await repository.logout();

      expect(await repository.hasToken(), isFalse);
      expect(await fakeStorage.getAccessToken(), isNull);
    });

    test('login with WasteOfficer credentials rejected by API with 403 unsupported_client_role', () async {
      fakeApi.throwApiException = const ApiException(
        message: 'This account is for the SmartWaste web application.',
        statusCode: 403,
        errorCode: 'unsupported_client_role',
      );

      expect(
        () => repository.login(email: 'officer@smartwaste.lk', password: 'Password123!'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'This account is for the SmartWaste web application.',
          ),
        ),
      );

      expect(await fakeStorage.getAccessToken(), isNull);
      expect(await repository.hasToken(), isFalse);
    });

    test('login with MunicipalManager credentials rejected by API with 403 unsupported_client_role', () async {
      fakeApi.throwApiException = const ApiException(
        message: 'This account is for the SmartWaste web application.',
        statusCode: 403,
        errorCode: 'unsupported_client_role',
      );

      expect(
        () => repository.login(email: 'manager@smartwaste.lk', password: 'Password123!'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'This account is for the SmartWaste web application.',
          ),
        ),
      );

      expect(await fakeStorage.getAccessToken(), isNull);
      expect(await repository.hasToken(), isFalse);
    });

    test('login rejects web-only WasteOfficer role defensively if API returned 200 and does not save token', () async {
      fakeApi.loginResult = AuthResponse(
        accessToken: 'officer-token',
        expiresAt: DateTime.now().add(const Duration(hours: 1)),
        user: const AuthUser(
          id: 'officer-1',
          fullName: 'Officer Kamal',
          email: 'kamal@smartwaste.lk',
          role: AppRoles.wasteOfficer,
        ),
      );

      expect(
        () => repository.login(email: 'kamal@smartwaste.lk', password: 'Password123!'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'This account is for the SmartWaste web application.',
          ),
        ),
      );

      expect(await fakeStorage.getAccessToken(), isNull);
      expect(await repository.hasToken(), isFalse);
    });

    test('login rejects web-only MunicipalManager role defensively if API returned 200 and does not save token', () async {
      fakeApi.loginResult = AuthResponse(
        accessToken: 'manager-token',
        expiresAt: DateTime.now().add(const Duration(hours: 1)),
        user: const AuthUser(
          id: 'manager-1',
          fullName: 'Manager Anura',
          email: 'anura@smartwaste.lk',
          role: AppRoles.municipalManager,
        ),
      );

      expect(
        () => repository.login(email: 'anura@smartwaste.lk', password: 'Password123!'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'This account is for the SmartWaste web application.',
          ),
        ),
      );

      expect(await fakeStorage.getAccessToken(), isNull);
      expect(await repository.hasToken(), isFalse);
    });

    test('login with unrelated generic 403 error throws generic access-denied message and does not save token', () async {
      fakeApi.throwApiException = const ApiException(
        message: 'Access denied. Your account lacks required permissions.',
        statusCode: 403,
      );

      expect(
        () => repository.login(email: 'blocked@smartwaste.lk', password: 'Password123!'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'Access denied. Your account lacks required permissions.',
          ),
        ),
      );

      expect(await fakeStorage.getAccessToken(), isNull);
      expect(await repository.hasToken(), isFalse);
    });

    test('login allows Citizen role and saves token', () async {
      fakeApi.loginResult = dummyAuthResponse;

      final res = await repository.login(email: 'sunil@binit.lk', password: 'Password123!');
      expect(res.user.isCitizen, isTrue);
      expect(await fakeStorage.getAccessToken(), 'valid-test-jwt-token');
      expect(await repository.hasToken(), isTrue);
    });

    test('login allows Driver role and saves token', () async {
      fakeApi.loginResult = AuthResponse(
        accessToken: 'driver-token',
        expiresAt: DateTime.now().add(const Duration(hours: 1)),
        user: const AuthUser(
          id: 'driver-1',
          fullName: 'Driver Saman',
          email: 'saman@smartwaste.lk',
          role: AppRoles.driver,
        ),
      );

      final res = await repository.login(email: 'saman@smartwaste.lk', password: 'Password123!');
      expect(res.user.isDriver, isTrue);
      expect(await fakeStorage.getAccessToken(), 'driver-token');
      expect(await repository.hasToken(), isTrue);
    });

    test('changePassword calls API and wipes access token from storage', () async {
      await fakeStorage.saveAccessToken('active-token');

      await repository.changePassword(
        currentPassword: 'OldPassword123!',
        newPassword: 'NewPassword123!',
      );

      expect(fakeApi.changePasswordCalled, isTrue);
      expect(fakeApi.lastCurrentPassword, 'OldPassword123!');
      expect(fakeApi.lastNewPassword, 'NewPassword123!');
      expect(await fakeStorage.getAccessToken(), isNull);
    });
  });
}
