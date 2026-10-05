import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/network/api_exception.dart';
import '../data/auth_repository.dart';
import '../models/auth_user.dart';

/// Distinct authentication statuses for the application lifecycle.
enum AuthStatus {
  initial,
  loading,
  authenticated,
  unauthenticated,
  error,
}

/// Immutable state holder for authentication.
class AuthState {
  final AuthStatus status;
  final AuthUser? user;
  final String? errorMessage;

  const AuthState({
    required this.status,
    this.user,
    this.errorMessage,
  });

  const AuthState.initial()
      : status = AuthStatus.initial,
        user = null,
        errorMessage = null;

  const AuthState.loading()
      : status = AuthStatus.loading,
        user = null,
        errorMessage = null;

  const AuthState.authenticated(this.user)
      : status = AuthStatus.authenticated,
        errorMessage = null;

  const AuthState.unauthenticated()
      : status = AuthStatus.unauthenticated,
        user = null,
        errorMessage = null;

  const AuthState.error(String message)
      : status = AuthStatus.error,
        user = null,
        errorMessage = message;

  bool get isAuthenticated => status == AuthStatus.authenticated && user != null;
  bool get isLoading => status == AuthStatus.loading;
  bool get isInitial => status == AuthStatus.initial;
  bool get mustChangePassword => user?.mustChangePassword ?? false;
}

/// Provider for the singleton AuthRepository.
final authRepositoryProvider = Provider<AuthRepository>((ref) {
  return AuthRepository();
});

/// StateNotifier/Notifier controlling user session, login, and registration.
class AuthNotifier extends Notifier<AuthState> {
  AuthRepository get _repository => ref.read(authRepositoryProvider);

  @override
  AuthState build() {
    return const AuthState.initial();
  }

  /// Restores session using stored token, verifying against GET /auth/me.
  Future<void> restoreSession() async {
    state = const AuthState.loading();
    try {
      final hasToken = await _repository.hasToken();
      if (!hasToken) {
        state = const AuthState.unauthenticated();
        return;
      }

      final user = await _repository.getCurrentUser();
      state = AuthState.authenticated(user);
    } catch (_) {
      // Clear token if invalid or expired
      await _repository.logout();
      state = const AuthState.unauthenticated();
    }
  }

  /// Authenticates with email and password.
  Future<bool> login({
    required String email,
    required String password,
  }) async {
    state = const AuthState.loading();
    try {
      final response = await _repository.login(email: email, password: password);
      state = AuthState.authenticated(response.user);
      return true;
    } on ApiException catch (e) {
      state = AuthState.error(e.message);
      return false;
    } catch (e) {
      state = const AuthState.error('An unexpected error occurred during login.');
      return false;
    }
  }

  /// Registers a new citizen.
  Future<bool> register({
    required String fullName,
    required String email,
    required String phoneNumber,
    required String password,
  }) async {
    state = const AuthState.loading();
    try {
      await _repository.registerCitizen(
        fullName: fullName,
        email: email,
        phoneNumber: phoneNumber,
        password: password,
      );
      state = const AuthState.unauthenticated();
      return true;
    } on ApiException catch (e) {
      state = AuthState.error(e.message);
      return false;
    } catch (e) {
      state = const AuthState.error('An unexpected error occurred during registration.');
      return false;
    }
  }

  /// Changes user password, wipes token, and transitions state to unauthenticated.
  Future<bool> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    state = const AuthState.loading();
    try {
      await _repository.changePassword(
        currentPassword: currentPassword,
        newPassword: newPassword,
      );
      state = const AuthState.unauthenticated();
      return true;
    } on ApiException catch (e) {
      state = AuthState.error(e.message);
      return false;
    } catch (_) {
      state = const AuthState.error('Failed to change password. Please try again.');
      return false;
    }
  }

  /// Logs out user and wipes credentials.
  Future<void> logout() async {
    await _repository.logout();
    state = const AuthState.unauthenticated();
  }
}

/// Main auth provider exposed to UI and routing guards.
final authProvider = NotifierProvider<AuthNotifier, AuthState>(() {
  return AuthNotifier();
});
