import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/presentation/home_screen.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';

class MockAuthNotifier extends AuthNotifier {
  final AuthState _initial;
  bool logoutCalled = false;

  MockAuthNotifier(this._initial);

  @override
  AuthState build() => _initial;

  @override
  Future<void> logout() async {
    logoutCalled = true;
    state = const AuthState.unauthenticated();
  }
}

void main() {
  group('HomeScreen Widget Tests', () {
    const testUser = AuthUser(
      id: 'citizen-id-123',
      fullName: 'Nimali Fernando',
      email: 'nimali@binit.lk',
      phoneNumber: '+94771234567',
      role: AppRoles.citizen,
    );

    testWidgets('renders user name, email, role, and welcome message', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(() => MockAuthNotifier(const AuthState.authenticated(testUser))),
          ],
          child: const MaterialApp(
            home: HomeScreen(),
          ),
        ),
      );

      expect(find.text('Smart Waste Management System'), findsAtLeastNWidgets(1));
      expect(find.text('Welcome, Nimali Fernando'), findsOneWidget);
      expect(find.text('Citizen'), findsOneWidget);
      expect(find.text('Email: nimali@binit.lk'), findsOneWidget);
      expect(find.textContaining('Mobile Authentication Foundation Ready'), findsOneWidget);
      expect(find.byKey(const Key('home_logout_button')), findsOneWidget);
    });

    testWidgets('triggers logout when logout button is tapped', (tester) async {
      final mockNotifier = MockAuthNotifier(const AuthState.authenticated(testUser));

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(() => mockNotifier),
          ],
          child: const MaterialApp(
            home: HomeScreen(),
          ),
        ),
      );

      final logoutBtn = find.byKey(const Key('home_logout_button'));
      await tester.ensureVisible(logoutBtn);
      await tester.tap(logoutBtn);
      await tester.pumpAndSettle();

      expect(mockNotifier.logoutCalled, isTrue);
    });
  });
}
