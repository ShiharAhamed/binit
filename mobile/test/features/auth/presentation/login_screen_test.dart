import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/auth/presentation/login_screen.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';

void main() {
  Widget createTestWidget() {
    return const ProviderScope(
      child: MaterialApp(
        home: LoginScreen(),
      ),
    );
  }

  group('LoginScreen Validation Tests', () {
    testWidgets('rejects submission with empty email and empty password', (tester) async {
      await tester.pumpWidget(createTestWidget());

      // Tap Sign In button without entering any details
      final submitButton = find.byKey(const Key('login_submit_button'));
      expect(submitButton, findsOneWidget);
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      // Should show validation error messages
      expect(find.text('Email is required'), findsOneWidget);
      expect(find.text('Password is required'), findsOneWidget);
    });

    testWidgets('rejects invalid email formats', (tester) async {
      await tester.pumpWidget(createTestWidget());

      final emailField = find.byKey(const Key('login_email_field'));
      final passwordField = find.byKey(const Key('login_password_field'));
      final submitButton = find.byKey(const Key('login_submit_button'));

      await tester.enterText(emailField, 'not-an-email');
      await tester.enterText(passwordField, 'Password123!');
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email address'), findsOneWidget);
      expect(find.text('Password is required'), findsNothing);
    });

    testWidgets('rejects empty password when email is valid', (tester) async {
      await tester.pumpWidget(createTestWidget());

      final emailField = find.byKey(const Key('login_email_field'));
      final submitButton = find.byKey(const Key('login_submit_button'));

      await tester.enterText(emailField, 'valid@example.com');
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(find.text('Email is required'), findsNothing);
      expect(find.text('Enter a valid email address'), findsNothing);
      expect(find.text('Password is required'), findsOneWidget);
    });
  });

  group('LoginScreen Platform Role Restriction Error Rendering', () {
    testWidgets('displays exact web application message when WasteOfficer attempts login', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(
              () => _StaticErrorAuthNotifier(
                const AuthState.error('This account is for the SmartWaste web application.'),
              ),
            ),
          ],
          child: const MaterialApp(
            home: LoginScreen(),
          ),
        ),
      );

      expect(
        find.text('This account is for the SmartWaste web application.'),
        findsOneWidget,
      );
      expect(
        find.text('Access denied. Your account lacks required permissions.'),
        findsNothing,
      );
    });

    testWidgets('displays exact web application message when MunicipalManager attempts login', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(
              () => _StaticErrorAuthNotifier(
                const AuthState.error('This account is for the SmartWaste web application.'),
              ),
            ),
          ],
          child: const MaterialApp(
            home: LoginScreen(),
          ),
        ),
      );

      expect(
        find.text('This account is for the SmartWaste web application.'),
        findsOneWidget,
      );
    });

    testWidgets('displays generic access denied message for unrelated 403 errors', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(
              () => _StaticErrorAuthNotifier(
                const AuthState.error('Access denied. Your account lacks required permissions.'),
              ),
            ),
          ],
          child: const MaterialApp(
            home: LoginScreen(),
          ),
        ),
      );

      expect(
        find.text('Access denied. Your account lacks required permissions.'),
        findsOneWidget,
      );
      expect(
        find.text('This account is for the SmartWaste web application.'),
        findsNothing,
      );
    });
  });

  group('LoginScreen Registration and Access Guidance Tests', () {
    testWidgets('renders Citizen registration action and Driver informational note', (tester) async {
      await tester.pumpWidget(createTestWidget());

      // Verifies "Don't have an account?" header is not rendered
      expect(find.text("Don't have an account?"), findsNothing);

      // Citizen registration action
      expect(find.text('New citizen? '), findsOneWidget);
      expect(find.text('Create an account'), findsOneWidget);
      expect(find.byKey(const Key('login_register_button')), findsOneWidget);

      // Driver informational message
      expect(
        find.text(
          'Drivers receive accounts from the municipal administration. Contact your Municipal Manager if you need access.',
        ),
        findsOneWidget,
      );

      // Confirms no Driver registration action exists
      expect(find.textContaining('Register as Driver'), findsNothing);
      expect(find.textContaining('Driver registration'), findsNothing);
      expect(find.textContaining('Create Driver Account'), findsNothing);
    });
  });
}

class _StaticErrorAuthNotifier extends AuthNotifier {
  final AuthState _state;
  _StaticErrorAuthNotifier(this._state);

  @override
  AuthState build() => _state;
}
