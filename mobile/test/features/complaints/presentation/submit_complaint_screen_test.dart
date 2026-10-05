import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/complaints/data/complaints_repository.dart';
import 'package:mobile/features/complaints/models/complaint_model.dart';
import 'package:mobile/features/complaints/models/create_complaint_request.dart';
import 'package:mobile/features/complaints/presentation/submit_complaint_screen.dart';
import 'package:mobile/features/reporting/models/selected_location.dart';
import 'package:mobile/features/reporting/services/location_service.dart';

class FakeComplaintsRepository extends ComplaintsRepository {
  CreateComplaintRequest? capturedRequest;
  int createCallCount = 0;
  bool shouldThrow = false;
  String errorMessage = 'Creation failed';
  int delayMs = 0;

  @override
  Future<ComplaintDetailModel> createComplaint(CreateComplaintRequest request) async {
    createCallCount++;
    capturedRequest = request;

    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }

    if (shouldThrow) {
      throw ApiException(message: errorMessage, statusCode: 400);
    }

    return ComplaintDetailModel(
      id: 'new-comp-123',
      citizenId: 'cit-1',
      category: request.category,
      subject: request.subject,
      description: request.description,
      latitude: request.latitude,
      longitude: request.longitude,
      locationDescription: request.locationDescription,
      status: ComplaintStatus.submitted,
      createdAt: DateTime.utc(2026, 9, 16, 12, 0),
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
  Future<LocationPermission> checkPermission() async => LocationPermission.always;

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
  Future<LocationPermission> requestPermission() async => LocationPermission.always;
}

Widget createSubmitComplaintTestApp({
  required ComplaintsRepository repository,
  required LocationService locationService,
  List<RouteBase>? additionalRoutes,
}) {
  final router = GoRouter(
    initialLocation: '/citizen/complaints/new',
    routes: [
      GoRoute(
        path: '/citizen/complaints/new',
        builder: (context, state) => SubmitComplaintScreen(
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
  group('SubmitComplaintScreen Widget Tests', () {
    late FakeComplaintsRepository repository;
    late FakeLocationService locationService;

    setUp(() {
      repository = FakeComplaintsRepository();
      locationService = FakeLocationService();
    });

    testWidgets('renders all expected form fields and sections', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      await tester.pumpWidget(
        createSubmitComplaintTestApp(
          repository: repository,
          locationService: locationService,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Submit Complaint'), findsNWidgets(2)); // AppBar title and Submit button
      expect(find.text('Report a Service Concern'), findsOneWidget);
      expect(find.byKey(const Key('complaint_category_dropdown')), findsOneWidget);
      expect(find.byKey(const Key('complaint_subject_field')), findsWidgets);
      expect(find.byKey(const Key('complaint_description_field')), findsWidgets);
      expect(find.text('Location (Optional)'), findsOneWidget);
      expect(find.byKey(const Key('use_current_location_button')), findsOneWidget);
      expect(find.byKey(const Key('complaint_location_description_field')), findsWidgets);
      expect(find.byKey(const Key('submit_complaint_button')), findsOneWidget);
    });

    testWidgets('validates required category, subject, and description', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      await tester.pumpWidget(
        createSubmitComplaintTestApp(
          repository: repository,
          locationService: locationService,
        ),
      );
      await tester.pumpAndSettle();

      // Tap submit with empty fields
      await tester.tap(find.byKey(const Key('submit_complaint_button')));
      await tester.pumpAndSettle();

      expect(find.text('Please select a complaint category.'), findsOneWidget);
      expect(find.text('Please enter a subject.'), findsOneWidget);
      expect(find.text('Please enter a description.'), findsOneWidget);
      expect(repository.createCallCount, 0);

      // Enter subject too short (< 5 chars)
      await tester.enterText(find.byKey(const Key('complaint_subject_field')), 'Fix');
      // Enter description too short (< 10 chars)
      await tester.enterText(find.byKey(const Key('complaint_description_field')), 'Too short');
      await tester.tap(find.byKey(const Key('submit_complaint_button')));
      await tester.pumpAndSettle();

      expect(find.text('Subject must be at least 5 characters.'), findsOneWidget);
      expect(find.text('Description must be at least 10 characters.'), findsOneWidget);
      expect(repository.createCallCount, 0);
    });

    testWidgets('submission without location succeeds (location is strictly optional)', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      await tester.pumpWidget(
        createSubmitComplaintTestApp(
          repository: repository,
          locationService: locationService,
          additionalRoutes: [
            GoRoute(
              path: '/citizen/complaints/:id',
              builder: (context, state) => Scaffold(
                body: Text('Complaint Detail Screen ${state.pathParameters['id']}'),
              ),
            ),
          ],
        ),
      );
      await tester.pumpAndSettle();

      // Select Category
      await tester.tap(find.byKey(const Key('complaint_category_dropdown')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('category_option_missedcollection')).last);
      await tester.pumpAndSettle();

      // Enter Subject
      await tester.enterText(find.byKey(const Key('complaint_subject_field')), 'Missed morning collection');
      // Enter Description
      await tester.enterText(
        find.byKey(const Key('complaint_description_field')),
        'The truck did not empty our standard bin today on 5th avenue.',
      );

      // Submit without touching location
      await tester.tap(find.byKey(const Key('submit_complaint_button')));
      await tester.pumpAndSettle();

      expect(repository.createCallCount, 1);
      expect(repository.capturedRequest?.category, ComplaintCategory.missedCollection);
      expect(repository.capturedRequest?.subject, 'Missed morning collection');
      expect(repository.capturedRequest?.description, 'The truck did not empty our standard bin today on 5th avenue.');
      expect(repository.capturedRequest?.latitude, isNull);
      expect(repository.capturedRequest?.longitude, isNull);

      // Confirm navigation
      expect(find.text('Complaint Detail Screen new-comp-123'), findsOneWidget);
    });

    testWidgets('use current location and clear location works accurately', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      await tester.pumpWidget(
        createSubmitComplaintTestApp(
          repository: repository,
          locationService: locationService,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('No location selected'), findsOneWidget);

      // Tap GPS button
      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(find.text('Location Selected ✓'), findsOneWidget);
      expect(find.byKey(const Key('complaint_selected_coordinates_text')), findsOneWidget);
      expect(find.byKey(const Key('clear_location_button')), findsOneWidget);

      // Clear location
      await tester.tap(find.byKey(const Key('clear_location_button')));
      await tester.pumpAndSettle();

      expect(find.text('No location selected'), findsOneWidget);
    });

    testWidgets('permission denied shows error message and does NOT block form submission', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      locationService.resultToReturn = const LocationFailure(
        reason: LocationFailureReason.permissionDenied,
        message: 'Location permission was denied.',
      );

      await tester.pumpWidget(
        createSubmitComplaintTestApp(
          repository: repository,
          locationService: locationService,
          additionalRoutes: [
            GoRoute(
              path: '/citizen/complaints/:id',
              builder: (context, state) => const Scaffold(body: Text('Detail Screen')),
            ),
          ],
        ),
      );
      await tester.pumpAndSettle();

      // Tap GPS -> fails gracefully
      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(find.text('Location permission was denied.'), findsOneWidget);
      expect(find.text('No location selected'), findsOneWidget);

      // Fill required fields and submit
      await tester.tap(find.byKey(const Key('complaint_category_dropdown')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('category_option_delayedservice')).last);
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('complaint_subject_field')), 'Delayed garbage truck');
      await tester.enterText(
        find.byKey(const Key('complaint_description_field')),
        'The truck arrived four hours after the normal schedule.',
      );

      await tester.tap(find.byKey(const Key('submit_complaint_button')));
      await tester.pumpAndSettle();

      expect(repository.createCallCount, 1);
      expect(find.text('Detail Screen'), findsOneWidget);
    });

    testWidgets('handles submission error and preserves entered inputs', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      repository.shouldThrow = true;
      repository.errorMessage = 'Server error processing complaint';

      await tester.pumpWidget(
        createSubmitComplaintTestApp(
          repository: repository,
          locationService: locationService,
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('complaint_category_dropdown')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('category_option_poorservice')).last);
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('complaint_subject_field')), 'Litter spilled during pickup');
      await tester.enterText(
        find.byKey(const Key('complaint_description_field')),
        'Some plastics fell onto the roadway during collection.',
      );

      await tester.tap(find.byKey(const Key('submit_complaint_button')));
      await tester.pumpAndSettle();

      expect(find.text('Server error processing complaint'), findsWidgets);
      // Ensure form values are preserved
      expect(find.text('Litter spilled during pickup'), findsOneWidget);
      expect(find.text('Some plastics fell onto the roadway during collection.'), findsOneWidget);
    });
  });
}
