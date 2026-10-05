import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/auth/data/auth_repository.dart';
import 'package:mobile/features/auth/models/auth_response.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';

class MockAuthRepository extends AuthRepository {
  bool hasTokenVal = false;
  AuthUser? currentUserVal;
  AuthResponse? loginVal;
  AuthResponse? registerVal;
  Exception? throwError;
  bool logoutCalled = false;

  @override
  Future<bool> hasToken() async => hasTokenVal;

  @override
  Future<AuthUser> getCurrentUser() async {
    if (throwError != null) throw throwError!;
    return currentUserVal!;
  }

  @override
  Future<AuthResponse> login({required String email, required String password}) async {
    if (throwError != null) throw throwError!;
    return loginVal!;
  }

  @override
  Future<AuthResponse> registerCitizen({
    required String fullName,
    required String email,
    required String phoneNumber,
    required String password,
  }) async {
    if (throwError != null) throw throwError!;
    return registerVal!;
  }

  @override
  Future<void> logout() async {
    logoutCalled = true;
    hasTokenVal = false;
  }
}

void main() {
  group('AuthState State Model Tests', () {
    test('initial state is unauthenticated and idle', () {
      const state = AuthState.initial();
      expect(state.status, AuthStatus.initial);
      expect(state.user, isNull);
      expect(state.isAuthenticated, isFalse);
      expect(state.isLoading, isFalse);
      expect(state.isInitial, isTrue);
    });

    test('loading state indicates active loading', () {
      const state = AuthState.loading();
      expect(state.status, AuthStatus.loading);
      expect(state.isLoading, isTrue);
      expect(state.isAuthenticated, isFalse);
    });

    test('authenticated state holds user and indicates authenticated', () {
      const user = AuthUser(
        id: '123-uuid',
        fullName: 'Kamal Silva',
        email: 'kamal@example.com',
        role: AppRoles.citizen,
      );

      const state = AuthState.authenticated(user);
      expect(state.status, AuthStatus.authenticated);
      expect(state.user, equals(user));
      expect(state.isAuthenticated, isTrue);
      expect(state.isLoading, isFalse);
      expect(state.errorMessage, isNull);
    });

    test('unauthenticated state has no user and is not loading', () {
      const state = AuthState.unauthenticated();
      expect(state.status, AuthStatus.unauthenticated);
      expect(state.user, isNull);
      expect(state.isAuthenticated, isFalse);
      expect(state.isLoading, isFalse);
    });

    test('error state holds error message', () {
      const state = AuthState.error('Invalid credentials');
      expect(state.status, AuthStatus.error);
      expect(state.errorMessage, 'Invalid credentials');
      expect(state.isAuthenticated, isFalse);
    });
  });

  group('AuthNotifier State Transition Tests', () {
    late MockAuthRepository mockRepo;
    late ProviderContainer container;

    final testUser = const AuthUser(
      id: 'test-user-id',
      fullName: 'Chaminda Vaas',
      email: 'chaminda@binit.lk',
      role: AppRoles.citizen,
    );

    final testAuthResponse = AuthResponse(
      accessToken: 'valid-jwt',
      expiresAt: DateTime.now().add(const Duration(hours: 1)),
      user: testUser,
    );

    setUp(() {
      mockRepo = MockAuthRepository();
      container = ProviderContainer(
        overrides: [
          authRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );
    });

    tearDown(() {
      container.dispose();
    });

    test('transitions: initial -> loading -> authenticated on successful login', () async {
      mockRepo.loginVal = testAuthResponse;

      final notifier = container.read(authProvider.notifier);
      final states = <AuthState>[];
      container.listen(authProvider, (_, next) => states.add(next));

      final success = await notifier.login(email: 'chaminda@binit.lk', password: 'Password123!');

      expect(success, isTrue);
      expect(container.read(authProvider).isAuthenticated, isTrue);
      expect(container.read(authProvider).user?.fullName, 'Chaminda Vaas');
      expect(states.any((s) => s.status == AuthStatus.loading), isTrue);
      expect(states.last.status, AuthStatus.authenticated);
    });

    test('transitions: initial -> loading -> error on login failure', () async {
      mockRepo.throwError = const ApiException(message: 'Invalid email or password.', statusCode: 401);

      final notifier = container.read(authProvider.notifier);
      final states = <AuthState>[];
      container.listen(authProvider, (_, next) => states.add(next));

      final success = await notifier.login(email: 'chaminda@binit.lk', password: 'WrongPassword!');

      expect(success, isFalse);
      expect(container.read(authProvider).isAuthenticated, isFalse);
      expect(container.read(authProvider).status, AuthStatus.error);
      expect(container.read(authProvider).errorMessage, 'Invalid email or password.');
    });

    test('transitions: authenticated -> unauthenticated on logout', () async {
      mockRepo.loginVal = testAuthResponse;
      final notifier = container.read(authProvider.notifier);
      await notifier.login(email: 'chaminda@binit.lk', password: 'Password123!');
      expect(container.read(authProvider).isAuthenticated, isTrue);

      await notifier.logout();

      expect(container.read(authProvider).isAuthenticated, isFalse);
      expect(container.read(authProvider).status, AuthStatus.unauthenticated);
      expect(mockRepo.logoutCalled, isTrue);
    });

    test('session restore when valid token exists sets authenticated', () async {
      mockRepo.hasTokenVal = true;
      mockRepo.currentUserVal = testUser;

      final notifier = container.read(authProvider.notifier);
      await notifier.restoreSession();

      expect(container.read(authProvider).isAuthenticated, isTrue);
      expect(container.read(authProvider).user?.email, 'chaminda@binit.lk');
    });

    test('session restore when no token exists sets unauthenticated', () async {
      mockRepo.hasTokenVal = false;

      final notifier = container.read(authProvider.notifier);
      await notifier.restoreSession();

      expect(container.read(authProvider).isAuthenticated, isFalse);
      expect(container.read(authProvider).status, AuthStatus.unauthenticated);
    });

    test('session restore when token is expired/invalid clears token and sets unauthenticated', () async {
      mockRepo.hasTokenVal = true;
      mockRepo.throwError = const ApiException(message: 'Unauthorized', statusCode: 401);

      final notifier = container.read(authProvider.notifier);
      await notifier.restoreSession();

      expect(container.read(authProvider).isAuthenticated, isFalse);
      expect(container.read(authProvider).status, AuthStatus.unauthenticated);
      expect(mockRepo.logoutCalled, isTrue);
    });
  });
}
