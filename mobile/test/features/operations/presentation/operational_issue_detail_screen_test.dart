import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/operations/data/operations_repository.dart';
import 'package:mobile/features/operations/models/operational_issue_model.dart';
import 'package:mobile/features/operations/presentation/operational_issue_detail_screen.dart';

class FakeOperationsRepository extends OperationsRepository {
  OperationalIssueDetailModel? issueToReturn;
  bool shouldThrow = false;
  String errorMessage = 'Failed to load operational issue';
  int fetchCallCount = 0;
  int delayMs = 0;

  @override
  Future<OperationalIssueDetailModel> getOperationalIssueById(String id) async {
    fetchCallCount++;
    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }

    if (shouldThrow) {
      throw ApiException(message: errorMessage, statusCode: 500);
    }

    if (issueToReturn != null) {
      return issueToReturn!;
    }

    throw const ApiException(message: 'Not found', statusCode: 404);
  }
}

Widget createOperationalIssueDetailTestApp({
  required OperationsRepository repository,
  required String issueId,
}) {
  return MaterialApp(
    theme: AppTheme.lightTheme,
    home: OperationalIssueDetailScreen(
      issueId: issueId,
      repository: repository,
    ),
  );
}

void main() {
  group('OperationalIssueDetailScreen Widget Tests', () {
    late FakeOperationsRepository repository;

    setUp(() {
      repository = FakeOperationsRepository();
    });

    testWidgets('renders loading state initially while fetching',
        (tester) async {
      repository.delayMs = 200;
      await tester.pumpWidget(
        createOperationalIssueDetailTestApp(
          repository: repository,
          issueId: 'issue-1',
        ),
      );
      await tester.pump();

      expect(find.text('Loading operational issue details...'), findsOneWidget);

      await tester.pumpAndSettle();
    });

    testWidgets('renders error state on API failure and retries on press',
        (tester) async {
      repository.shouldThrow = true;
      repository.errorMessage = 'Network timeout';

      await tester.pumpWidget(
        createOperationalIssueDetailTestApp(
          repository: repository,
          issueId: 'issue-1',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Network timeout'), findsOneWidget);
      expect(
          find.byKey(const Key('retry_fetch_issue_detail_button')), findsOneWidget);

      // Fix repo and retry
      repository.shouldThrow = false;
      repository.issueToReturn = OperationalIssueDetailModel(
        id: 'issue-1',
        driverId: 'driver-1',
        issueType: OperationalIssueType.vehicleProblem,
        title: 'Transmission fluid leaking',
        description: 'Oil pool observed under truck chassis.',
        status: OperationalIssueStatus.reported,
        createdAt: DateTime.utc(2026, 10, 15, 8, 0),
      );

      await tester
          .tap(find.byKey(const Key('retry_fetch_issue_detail_button')));
      await tester.pumpAndSettle();

      expect(find.text('Transmission fluid leaking'), findsOneWidget);
      expect(find.text('Oil pool observed under truck chassis.'), findsOneWidget);
    });

    testWidgets('renders full issue details with map and location description',
        (tester) async {
      repository.issueToReturn = OperationalIssueDetailModel(
        id: 'issue-100',
        driverId: 'driver-1',
        driverName: 'Samantha Perera',
        issueType: OperationalIssueType.roadOrAccessIssue,
        title: 'Fallen power cable blocking narrow lane',
        description: 'High voltage wire down across lane. Vehicle cannot pass safely.',
        latitude: 6.9271,
        longitude: 79.8612,
        locationDescription: 'Opposite public library gate',
        status: OperationalIssueStatus.inReview,
        createdAt: DateTime.utc(2026, 10, 15, 9, 30),
      );

      await tester.pumpWidget(
        createOperationalIssueDetailTestApp(
          repository: repository,
          issueId: 'issue-100',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Fallen power cable blocking narrow lane'), findsOneWidget);
      expect(find.text('Road / Access Issue'), findsWidgets);
      expect(find.text('In Review'), findsWidgets);
      expect(find.textContaining('High voltage wire down across lane'),
          findsOneWidget);
      expect(find.textContaining('Opposite public library gate'), findsOneWidget);
      expect(find.byKey(const Key('detail_issue_coordinates')), findsOneWidget);
      expect(find.textContaining('6.92710'), findsOneWidget);

      // InReview shows resolution pending banner
      expect(find.byKey(const Key('detail_resolution_pending_title')),
          findsOneWidget);
      expect(find.text('Staff is actively reviewing this issue.'),
          findsOneWidget);

      // Verify Driver has no mutation controls
      expect(find.text('Resolve'), findsNothing);
      expect(find.text('Start Review'), findsNothing);
      expect(find.text('Delete'), findsNothing);
      expect(find.text('Edit'), findsNothing);
    });

    testWidgets('renders no-location message when issue has no coordinates',
        (tester) async {
      repository.issueToReturn = OperationalIssueDetailModel(
        id: 'issue-200',
        driverId: 'driver-1',
        issueType: OperationalIssueType.operationalDelay,
        title: 'Traffic delay on highway',
        description: 'Heavy congestion due to national event.',
        status: OperationalIssueStatus.reported,
        createdAt: DateTime.utc(2026, 10, 15, 11, 0),
      );

      await tester.pumpWidget(
        createOperationalIssueDetailTestApp(
          repository: repository,
          issueId: 'issue-200',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('detail_no_location_message')), findsOneWidget);
      expect(find.text('No map coordinates were provided for this issue.'),
          findsOneWidget);
      expect(find.byKey(const Key('detail_issue_coordinates')), findsNothing);
    });

    testWidgets('renders resolution details when status is Resolved',
        (tester) async {
      repository.issueToReturn = OperationalIssueDetailModel(
        id: 'issue-300',
        driverId: 'driver-1',
        issueType: OperationalIssueType.equipmentProblem,
        title: 'Weigh scale battery depleted',
        description: 'Portable scale turned off during verification.',
        status: OperationalIssueStatus.resolved,
        resolutionNote: 'Replacement battery pack provided at depot counter.',
        resolvedAt: DateTime.utc(2026, 10, 15, 14, 0),
        resolvedByUserName: 'Officer Fernando',
        createdAt: DateTime.utc(2026, 10, 15, 8, 30),
      );

      await tester.pumpWidget(
        createOperationalIssueDetailTestApp(
          repository: repository,
          issueId: 'issue-300',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Resolved'), findsWidgets);
      expect(find.text('Resolution Details'), findsOneWidget);
      expect(find.text('Replacement battery pack provided at depot counter.'),
          findsOneWidget);
      expect(find.textContaining('Officer Fernando'), findsOneWidget);
      expect(find.byKey(const Key('detail_resolution_pending_title')),
          findsNothing);
    });
  });
}
