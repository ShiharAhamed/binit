import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/shared/widgets/app_alert.dart';
import 'package:mobile/shared/widgets/app_button.dart';
import 'package:mobile/shared/widgets/app_card.dart';
import 'package:mobile/shared/widgets/app_loading_indicator.dart';
import 'package:mobile/shared/widgets/app_text_field.dart';

void main() {
  Widget buildTestApp(Widget child) {
    return MaterialApp(
      theme: AppTheme.lightTheme,
      home: Scaffold(
        body: Center(child: child),
      ),
    );
  }

  group('AppButton Widget Tests', () {
    testWidgets('renders label and triggers onPressed when tapped', (tester) async {
      bool tapped = false;
      await tester.pumpWidget(
        buildTestApp(
          AppButton(
            onPressed: () => tapped = true,
            label: 'Submit Action',
          ),
        ),
      );

      expect(find.text('Submit Action'), findsOneWidget);
      await tester.tap(find.text('Submit Action'));
      expect(tapped, isTrue);
    });

    testWidgets('displays loading spinner and prevents taps when isLoading is true', (tester) async {
      bool tapped = false;
      await tester.pumpWidget(
        buildTestApp(
          AppButton(
            onPressed: () => tapped = true,
            isLoading: true,
            label: 'Submit Action',
          ),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Submit Action'), findsNothing);

      await tester.tap(find.byType(CircularProgressIndicator));
      expect(tapped, isFalse);
    });
  });

  group('AppTextField Widget Tests', () {
    testWidgets('renders label, hint and accepts user input', (tester) async {
      final controller = TextEditingController();
      await tester.pumpWidget(
        buildTestApp(
          AppTextField(
            controller: controller,
            label: 'Username',
            hint: 'Enter your username',
          ),
        ),
      );

      expect(find.text('Username'), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'john_doe');
      expect(controller.text, 'john_doe');
    });
  });

  group('AppAlert Widget Tests', () {
    testWidgets('renders semantic alerts with message and icons', (tester) async {
      await tester.pumpWidget(
        buildTestApp(
          const Column(
            children: [
              AppAlert.error(message: 'Fatal error occurred'),
              AppAlert.warning(message: 'Please take caution'),
              AppAlert.success(message: 'Action completed successfully'),
              AppAlert.info(message: 'Informational note'),
            ],
          ),
        ),
      );

      expect(find.text('Fatal error occurred'), findsOneWidget);
      expect(find.text('Please take caution'), findsOneWidget);
      expect(find.text('Action completed successfully'), findsOneWidget);
      expect(find.text('Informational note'), findsOneWidget);

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
      expect(find.byIcon(Icons.warning_amber_rounded), findsOneWidget);
      expect(find.byIcon(Icons.check_circle_outline), findsOneWidget);
      expect(find.byIcon(Icons.info_outline), findsOneWidget);
    });
  });

  group('AppCard Widget Tests', () {
    testWidgets('renders child content inside styled container', (tester) async {
      bool tapped = false;
      await tester.pumpWidget(
        buildTestApp(
          AppCard(
            onTap: () => tapped = true,
            child: const Text('Card Content Inner'),
          ),
        ),
      );

      expect(find.text('Card Content Inner'), findsOneWidget);
      await tester.tap(find.text('Card Content Inner'));
      expect(tapped, isTrue);
    });
  });

  group('AppLoadingIndicator Widget Tests', () {
    testWidgets('renders spinner and optional message', (tester) async {
      await tester.pumpWidget(
        buildTestApp(
          const AppLoadingIndicator(message: 'Loading test data...'),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Loading test data...'), findsOneWidget);
    });
  });
}
