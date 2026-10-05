import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/complaints/data/complaints_repository.dart';
import 'package:mobile/features/complaints/models/complaint_model.dart';
import 'package:mobile/features/complaints/presentation/complaint_detail_screen.dart';

class FakeComplaintsRepository extends ComplaintsRepository {
  ComplaintDetailModel? complaintToReturn;
  bool shouldThrow = false;
  String errorMessage = 'Failed to load complaint';
  int fetchCallCount = 0;
  int delayMs = 0;

  @override
  Future<ComplaintDetailModel> getComplaintById(String id) async {
    fetchCallCount++;
    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }

    if (shouldThrow) {
      throw ApiException(message: errorMessage, statusCode: 500);
    }

    if (complaintToReturn != null) {
      return complaintToReturn!;
    }

    throw ApiException(message: 'Not found', statusCode: 404);
  }
}

Widget createComplaintDetailTestApp({
  required ComplaintsRepository repository,
  required String complaintId,
}) {
  return MaterialApp(
    theme: AppTheme.lightTheme,
    home: ComplaintDetailScreen(
      complaintId: complaintId,
      repository: repository,
    ),
  );
}

void main() {
  group('ComplaintDetailScreen Widget Tests', () {
    late FakeComplaintsRepository repository;

    setUp(() {
      repository = FakeComplaintsRepository();
    });

    testWidgets('renders loading state initially while fetching', (tester) async {
      repository.delayMs = 200;
      await tester.pumpWidget(
        createComplaintDetailTestApp(
          repository: repository,
          complaintId: 'c-1',
        ),
      );
      await tester.pump();

      expect(find.text('Loading complaint details...'), findsOneWidget);

      await tester.pumpAndSettle();
    });

    testWidgets('renders error state on API failure and retries on press', (tester) async {
      repository.shouldThrow = true;
      repository.errorMessage = 'Network timeout';

      await tester.pumpWidget(
        createComplaintDetailTestApp(
          repository: repository,
          complaintId: 'c-1',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Unable to Load Complaint'), findsOneWidget);
      expect(find.text('Network timeout'), findsOneWidget);

      // Fix repo and retry
      repository.shouldThrow = false;
      repository.complaintToReturn = ComplaintDetailModel(
        id: 'c-1',
        citizenId: 'cit-1',
        category: ComplaintCategory.missedCollection,
        subject: 'Missed bin on 3rd Lane',
        description: 'Trash was not picked up as scheduled.',
        status: ComplaintStatus.submitted,
        createdAt: DateTime.utc(2026, 9, 16, 8, 0),
      );

      await tester.tap(find.byKey(const Key('retry_load_complaint_detail_button')));
      await tester.pumpAndSettle();

      expect(find.text('Missed bin on 3rd Lane'), findsOneWidget);
    });

    testWidgets('renders full detail with read-only map and resolution for Resolved complaint', (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      repository.complaintToReturn = ComplaintDetailModel(
        id: 'c-10',
        citizenId: 'cit-1',
        citizenName: 'Citizen Jane',
        category: ComplaintCategory.missedCollection,
        subject: 'Missed collection on Pine Street',
        description: 'The green waste bin was placed at the front curb by 6:30 AM but was skipped by the morning crew.',
        latitude: 6.9271,
        longitude: 79.8612,
        locationDescription: 'In front of green gate #14',
        status: ComplaintStatus.resolved,
        resolutionNote: 'A replacement collection team was dispatched and the bin has been emptied.',
        resolvedAt: DateTime.utc(2026, 9, 16, 14, 0),
        resolvedByUserName: 'Officer Michael',
        createdAt: DateTime.utc(2026, 9, 16, 7, 30),
      );

      await tester.pumpWidget(
        createComplaintDetailTestApp(
          repository: repository,
          complaintId: 'c-10',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Complaint Details'), findsOneWidget);
      expect(find.text('Missed collection on Pine Street'), findsOneWidget);
      expect(find.text('Missed Collection'), findsOneWidget);
      expect(find.text('Resolved'), findsOneWidget);

      // Description
      expect(find.text('The green waste bin was placed at the front curb by 6:30 AM but was skipped by the morning crew.'), findsOneWidget);

      // Location & Map
      expect(find.text('In front of green gate #14'), findsOneWidget);
      expect(find.byKey(const Key('complaint_detail_coordinates_text')), findsOneWidget);
      expect(find.byKey(const Key('complaint_detail_marker')), findsOneWidget);

      // Resolution section
      expect(find.text('A replacement collection team was dispatched and the bin has been emptied.'), findsOneWidget);
      expect(find.text('Resolved by Officer Michael'), findsOneWidget);
      expect(find.byKey(const Key('complaint_resolved_at_text')), findsOneWidget);
    });

    testWidgets('renders no-location message when complaint has no coordinates', (tester) async {
      repository.complaintToReturn = ComplaintDetailModel(
        id: 'c-20',
        citizenId: 'cit-1',
        category: ComplaintCategory.other,
        subject: 'General inquiry',
        description: 'Asking about holiday schedule.',
        status: ComplaintStatus.submitted,
        createdAt: DateTime.utc(2026, 9, 17, 9, 0),
      );

      await tester.pumpWidget(
        createComplaintDetailTestApp(
          repository: repository,
          complaintId: 'c-20',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('complaint_detail_no_location_text')), findsOneWidget);
      expect(find.text('No map location was provided for this complaint.'), findsOneWidget);
      expect(find.byKey(const Key('complaint_detail_marker')), findsNothing);

      // Resolution pending notice
      expect(find.text('This complaint is currently awaiting staff review and resolution.'), findsOneWidget);
    });

    testWidgets('citizen detail view has no staff mutation controls or edit buttons', (tester) async {
      repository.complaintToReturn = ComplaintDetailModel(
        id: 'c-30',
        citizenId: 'cit-1',
        category: ComplaintCategory.delayedService,
        subject: 'Delayed truck',
        description: 'Truck is running very late today.',
        status: ComplaintStatus.inReview,
        createdAt: DateTime.utc(2026, 9, 18, 10, 0),
      );

      await tester.pumpWidget(
        createComplaintDetailTestApp(
          repository: repository,
          complaintId: 'c-30',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Start Review'), findsNothing);
      expect(find.text('Resolve'), findsNothing);
      expect(find.text('Edit Complaint'), findsNothing);
      expect(find.text('Delete Complaint'), findsNothing);
    });
  });
}
