import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/operations/data/operations_repository.dart';
import 'package:mobile/features/operations/models/create_operational_issue_request.dart';
import 'package:mobile/features/operations/models/operational_issue_model.dart';
import 'package:mobile/features/operations/presentation/report_operational_issue_screen.dart';
import 'package:mobile/features/reporting/models/selected_location.dart';
import 'package:mobile/features/reporting/services/location_service.dart';

class FakeOperationsRepository extends OperationsRepository {
  CreateOperationalIssueRequest? capturedRequest;
  int createCallCount = 0;
  bool shouldThrow = false;
  String errorMessage = 'Creation failed';
  int delayMs = 0;

  @override
  Future<OperationalIssueDetailModel> createOperationalIssue(
      CreateOperationalIssueRequest request) async {
    createCallCount++;
    capturedRequest = request;

    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }

    if (shouldThrow) {
      throw ApiException(message: errorMessage, statusCode: 400);
    }

    return OperationalIssueDetailModel(
      id: 'new-issue-123',
      driverId: 'driver-1',
      issueType: request.issueType,
      title: request.title,
      description: request.description,
      latitude: request.latitude,
      longitude: request.longitude,
      locationDescription: request.locationDescription,
      status: OperationalIssueStatus.reported,
      createdAt: DateTime.utc(2026, 10, 15, 12, 0),
    );
  }
}

class FakeLocationService implements LocationService {
  LocationResult resultToReturn;

  FakeLocationService({
    this.resultToReturn = const LocationSuccess(
      SelectedLocation(latitude: 6.9271, longitude: 79.8612),
    ),
  });

  @override
  Future<LocationPermission> checkPermission() async =>
      LocationPermission.always;

  @override
  Future<LocationResult> getCurrentLocation() async => resultToReturn;

  @override
  Future<SelectedLocation> getCurrentPosition() async =>
      const SelectedLocation(latitude: 6.9271, longitude: 79.8612);

  @override
  Future<bool> isLocationServiceEnabled() async => true;

  @override
  Future<bool> openAppSettings() async => true;

  @override
  Future<bool> openLocationSettings() async => true;

  @override
  Future<LocationPermission> requestPermission() async =>
      LocationPermission.always;
}

Widget createReportIssueTestApp({
  required OperationsRepository repository,
  required LocationService locationService,
  List<RouteBase>? additionalRoutes,
}) {
  final router = GoRouter(
    initialLocation: '/driver/incidents/new',
    routes: [
      GoRoute(
        path: '/driver/incidents/new',
        builder: (context, state) => ReportOperationalIssueScreen(
          repository: repository,
          locationService: locationService,
        ),
      ),
      ...?additionalRoutes,
    ],
  );

  return MaterialApp.router(
    theme: AppTheme.lightTheme,
    routerConfig: router,
  );
}

void main() {
  group('ReportOperationalIssueScreen Widget Tests', () {
    late FakeOperationsRepository repository;
    late FakeLocationService locationService;

    setUp(() {
      repository = FakeOperationsRepository();
      locationService = FakeLocationService();
    });

    testWidgets('shows validation errors when submitting empty form',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      await tester.pumpWidget(createReportIssueTestApp(
        repository: repository,
        locationService: locationService,
      ));
      await tester.pumpAndSettle();

      await tester
          .tap(find.byKey(const Key('submit_operational_issue_button')));
      await tester.pumpAndSettle();

      expect(find.text('Please select an issue type.'), findsOneWidget);
      expect(find.text('Please enter a title.'), findsOneWidget);
      expect(find.text('Please enter a description.'), findsOneWidget);
      expect(repository.createCallCount, equals(0));
    });

    testWidgets('validates minimum and maximum lengths for title and description',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      await tester.pumpWidget(createReportIssueTestApp(
        repository: repository,
        locationService: locationService,
      ));
      await tester.pumpAndSettle();

      // Enter short title (<5 chars) and short description (<10 chars)
      await tester.enterText(
          find.byKey(const Key('issue_title_input')).first, 'Stop');
      await tester.enterText(
          find.byKey(const Key('issue_description_input')).first, 'Too short');

      await tester
          .tap(find.byKey(const Key('submit_operational_issue_button')));
      await tester.pumpAndSettle();

      expect(
          find.text('Title must be at least 5 characters.'), findsOneWidget);
      expect(find.text('Description must be at least 10 characters.'),
          findsOneWidget);
      expect(repository.createCallCount, equals(0));
    });

    testWidgets('submission without location succeeds (location is strictly optional)',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      String? createdId;

      final additionalRoute = GoRoute(
        path: '/driver/incidents/:id',
        builder: (context, state) {
          createdId = state.pathParameters['id'];
          return Scaffold(body: Text('Issue Created: $createdId'));
        },
      );

      await tester.pumpWidget(createReportIssueTestApp(
        repository: repository,
        locationService: locationService,
        additionalRoutes: [additionalRoute],
      ));
      await tester.pumpAndSettle();

      // 1. Select Issue Type
      await tester.tap(find.byKey(const Key('issue_type_dropdown')));
      await tester.pumpAndSettle();
      await tester.tap(
        find.byKey(const Key('type_option_equipmentproblem')).last,
        warnIfMissed: false,
      );
      await tester.pumpAndSettle();

      // 2. Enter Title and Description
      await tester.enterText(
          find.byKey(const Key('issue_title_input')).first,
          'Hydraulic compactor pressure loss');
      await tester.enterText(
          find.byKey(const Key('issue_description_input')).first,
          'Pressure gauge dropped below normal operating threshold during route.');

      // 3. Submit
      await tester
          .tap(find.byKey(const Key('submit_operational_issue_button')));
      await tester.pumpAndSettle();

      expect(repository.createCallCount, equals(1));
      expect(repository.capturedRequest?.issueType,
          equals(OperationalIssueType.equipmentProblem));
      expect(repository.capturedRequest?.title,
          equals('Hydraulic compactor pressure loss'));
      expect(repository.capturedRequest?.latitude, isNull);
      expect(repository.capturedRequest?.longitude, isNull);

      expect(createdId, equals('new-issue-123'));
      expect(find.text('Issue Created: new-issue-123'), findsOneWidget);
    });

    testWidgets('use current location populates coordinates and clear location removes them',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      await tester.pumpWidget(createReportIssueTestApp(
        repository: repository,
        locationService: locationService,
      ));
      await tester.pumpAndSettle();

      // Tap Use Current Location
      await tester
          .tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('selected_coordinates_text')), findsOneWidget);
      expect(find.textContaining('6.92710'), findsOneWidget);

      // Tap Clear Location
      await tester.tap(find.byKey(const Key('clear_location_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('selected_coordinates_text')), findsNothing);
    });

    testWidgets('permission denial shows message and leaves form fully usable',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      locationService.resultToReturn = const LocationFailure(
        reason: LocationFailureReason.permissionDenied,
        message: 'Location permission is needed to use your current location.',
      );

      await tester.pumpWidget(createReportIssueTestApp(
        repository: repository,
        locationService: locationService,
      ));
      await tester.pumpAndSettle();

      await tester
          .tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(find.textContaining('Location permission is needed'), findsOneWidget);
      expect(find.byKey(const Key('submit_operational_issue_button')),
          findsOneWidget);
    });

    testWidgets('prevents double-submit while submission request is in-flight',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      repository.delayMs = 300;

      await tester.pumpWidget(createReportIssueTestApp(
        repository: repository,
        locationService: locationService,
      ));
      await tester.pumpAndSettle();

      // Select Issue Type
      await tester.tap(find.byKey(const Key('issue_type_dropdown')));
      await tester.pumpAndSettle();
      await tester.tap(
        find.byKey(const Key('type_option_vehicleproblem')).last,
        warnIfMissed: false,
      );
      await tester.pumpAndSettle();

      await tester.enterText(
          find.byKey(const Key('issue_title_input')).first,
          'Flat tire on vehicle 08');
      await tester.enterText(
          find.byKey(const Key('issue_description_input')).first,
          'Rear left tire punctured near commercial complex.');

      // Tap Submit twice rapidly
      await tester
          .tap(find.byKey(const Key('submit_operational_issue_button')));
      await tester.pump(const Duration(milliseconds: 50));
      await tester
          .tap(find.byKey(const Key('submit_operational_issue_button')));
      await tester.pumpAndSettle();

      expect(repository.createCallCount, equals(1));
    });

    testWidgets('handles submission error and preserves entered inputs',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      repository.shouldThrow = true;
      repository.errorMessage = 'Server error during submission.';

      await tester.pumpWidget(createReportIssueTestApp(
        repository: repository,
        locationService: locationService,
      ));
      await tester.pumpAndSettle();

      // Select Issue Type
      await tester.tap(find.byKey(const Key('issue_type_dropdown')));
      await tester.pumpAndSettle();
      await tester.tap(
        find.byKey(const Key('type_option_other')).last,
        warnIfMissed: false,
      );
      await tester.pumpAndSettle();

      await tester.enterText(
          find.byKey(const Key('issue_title_input')).first,
          'Special container required');
      await tester.enterText(
          find.byKey(const Key('issue_description_input')).first,
          'Hazardous paint drums found next to domestic waste bin.');

      await tester
          .tap(find.byKey(const Key('submit_operational_issue_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('submission_error_text')), findsOneWidget);
      expect(find.text('Server error during submission.'), findsOneWidget);

      // Form values preserved
      expect(find.text('Special container required'), findsOneWidget);
      expect(
          find.text(
              'Hazardous paint drums found next to domestic waste bin.'),
          findsOneWidget);
    });
  });
}
