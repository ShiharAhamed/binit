import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/presentation/change_password_screen.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';

class TestAuthNotifier extends AuthNotifier {
  final AuthState initialState;

  TestAuthNotifier(this.initialState);

  @override
  AuthState build() => initialState;
}

void main() {
  const normalUser = AuthUser(
    id: 'user-1',
    fullName: 'Driver Saman',
    email: 'saman@smartwaste.lk',
    role: AppRoles.driver,
    mustChangePassword: false,
  );

  const forcedUser = AuthUser(
    id: 'user-2',
    fullName: 'New Driver',
    email: 'newdriver@smartwaste.lk',
    role: AppRoles.driver,
    mustChangePassword: true,
  );

  Widget createTestWidget({required AuthUser user}) {
    return ProviderScope(
      overrides: [
        authProvider.overrideWith(() => TestAuthNotifier(AuthState.authenticated(user))),
      ],
      child: const MaterialApp(
        home: ChangePasswordScreen(),
      ),
    );
  }

  group('ChangePasswordScreen Validation and Rendering Tests', () {
    testWidgets('renders voluntary mode for normal user', (tester) async {
      await tester.pumpWidget(createTestWidget(user: normalUser));

      expect(find.text('Change Password'), findsWidgets);
      expect(find.text('Current Password'), findsOneWidget);
      expect(find.text('New Password'), findsOneWidget);
      expect(find.text('Confirm New Password'), findsOneWidget);
      expect(find.text('Sign out'), findsNothing);
      expect(find.text('You must change your temporary password before continuing.'), findsNothing);
    });

    testWidgets('renders forced mode with info banner and Sign out button for temporary password user', (tester) async {
      await tester.pumpWidget(createTestWidget(user: forcedUser));

      expect(find.text('Change Temporary Password'), findsOneWidget);
      expect(find.text('Current Temporary Password'), findsOneWidget);
      expect(find.text('Sign out'), findsOneWidget);
      expect(find.text('You must change your temporary password before continuing.'), findsOneWidget);
    });

    testWidgets('validates required fields on empty submit', (tester) async {
      await tester.pumpWidget(createTestWidget(user: normalUser));

      final saveButton = find.byKey(const Key('save_password_button'));
      await tester.tap(saveButton);
      await tester.pumpAndSettle();

      expect(find.text('Current password is required'), findsOneWidget);
      expect(find.text('New password is required'), findsOneWidget);
    });

    testWidgets('validates new password minimum 8 characters', (tester) async {
      await tester.pumpWidget(createTestWidget(user: normalUser));

      await tester.enterText(find.byKey(const Key('current_password_input')), 'OldPass123!');
      await tester.enterText(find.byKey(const Key('new_password_input')), 'short');
      await tester.enterText(find.byKey(const Key('confirm_new_password_input')), 'short');

      final saveButton = find.byKey(const Key('save_password_button'));
      await tester.tap(saveButton);
      await tester.pumpAndSettle();

      expect(find.text('Password must be at least 8 characters'), findsOneWidget);
    });

    testWidgets('validates password mismatch between new and confirm passwords', (tester) async {
      await tester.pumpWidget(createTestWidget(user: normalUser));

      await tester.enterText(find.byKey(const Key('current_password_input')), 'OldPass123!');
      await tester.enterText(find.byKey(const Key('new_password_input')), 'NewPassword123!');
      await tester.enterText(find.byKey(const Key('confirm_new_password_input')), 'DifferentPassword123!');

      final saveButton = find.byKey(const Key('save_password_button'));
      await tester.tap(saveButton);
      await tester.pumpAndSettle();

      expect(find.text('Passwords do not match'), findsOneWidget);
    });
  });
}
