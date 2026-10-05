import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/operations/data/operations_repository.dart';
import 'package:mobile/features/operations/models/operational_issue_model.dart';
import 'package:mobile/features/operations/presentation/driver_operational_issues_screen.dart';

class FakeOperationsRepository extends OperationsRepository {
  List<OperationalIssueSummaryModel> issuesToReturn = [];
  int totalCount = 0;
  int totalPages = 1;
  int delayMs = 0;
  bool shouldThrow = false;
  String errorMessage = 'Failed to fetch operational issues';
  int? errorOnPage;

  int? capturedPage;
  int? capturedPageSize;
  OperationalIssueStatus? capturedStatus;
  OperationalIssueType? capturedIssueType;
  String? capturedSearch;
  String? capturedSortBy;
  String? capturedSortDirection;
  int callCount = 0;

  @override
  Future<PagedOperationalIssuesModel> getMyOperationalIssues({
    int page = 1,
    int pageSize = 20,
    OperationalIssueStatus? status,
    OperationalIssueType? issueType,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) async {
    callCount++;
    capturedPage = page;
    capturedPageSize = pageSize;
    capturedStatus = status;
    capturedIssueType = issueType;
    capturedSearch = search;
    capturedSortBy = sortBy;
    capturedSortDirection = sortDirection;

    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }

    if (shouldThrow || (errorOnPage != null && errorOnPage == page)) {
      throw ApiException(message: errorMessage, statusCode: 500);
    }

    return PagedOperationalIssuesModel(
      items: issuesToReturn,
      page: page,
      pageSize: pageSize,
      totalCount: totalCount == 0 ? issuesToReturn.length : totalCount,
      totalPages: totalPages,
    );
  }
}

OperationalIssueSummaryModel createSampleIssue({
  required String id,
  required String title,
  required OperationalIssueType issueType,
  required OperationalIssueStatus status,
  double? latitude,
  double? longitude,
  DateTime? createdAt,
}) {
  return OperationalIssueSummaryModel(
    id: id,
    driverId: 'driver-1',
    issueType: issueType,
    title: title,
    status: status,
    latitude: latitude,
    longitude: longitude,
    createdAt: createdAt ?? DateTime.utc(2026, 10, 15, 10, 30),
  );
}

Widget createIssuesTestApp({
  required OperationsRepository repository,
  List<RouteBase>? additionalRoutes,
  bool showAppBar = true,
}) {
  final router = GoRouter(
    initialLocation: '/driver/incidents',
    routes: [
      GoRoute(
        path: '/driver/incidents',
        builder: (context, state) => DriverOperationalIssuesScreen(
          repository: repository,
          showAppBar: showAppBar,
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
  group('DriverOperationalIssuesScreen Widget Tests', () {
    late FakeOperationsRepository repository;

    setUp(() {
      repository = FakeOperationsRepository();
    });

    testWidgets('shows loading indicator while initial fetch is in progress',
        (tester) async {
      repository.delayMs = 200;
      await tester.pumpWidget(createIssuesTestApp(repository: repository));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Loading operational issues...'), findsOneWidget);

      await tester.pumpAndSettle();
      expect(find.byType(CircularProgressIndicator), findsNothing);
    });

    testWidgets('renders operational issue cards with titles, types, and status chips',
        (tester) async {
      repository.issuesToReturn = [
        createSampleIssue(
          id: 'issue-1',
          title: 'Hydraulic bin lift failure',
          issueType: OperationalIssueType.equipmentProblem,
          status: OperationalIssueStatus.reported,
        ),
        createSampleIssue(
          id: 'issue-2',
          title: 'Road blocked by fallen branch',
          issueType: OperationalIssueType.roadOrAccessIssue,
          status: OperationalIssueStatus.inReview,
        ),
        createSampleIssue(
          id: 'issue-3',
          title: 'Low engine coolant warning',
          issueType: OperationalIssueType.vehicleProblem,
          status: OperationalIssueStatus.resolved,
        ),
      ];
      repository.totalCount = 3;

      await tester.pumpWidget(createIssuesTestApp(repository: repository));
      await tester.pumpAndSettle();

      expect(find.text('Operational Issues'), findsAtLeastNWidgets(1));
      expect(find.text('Hydraulic bin lift failure'), findsOneWidget);
      expect(find.text('Road blocked by fallen branch'), findsOneWidget);
      expect(find.text('Low engine coolant warning'), findsOneWidget);

      expect(find.text('Equipment Problem'), findsOneWidget);
      expect(find.text('Road / Access Issue'), findsOneWidget);
      expect(find.text('Vehicle Problem'), findsOneWidget);

      expect(find.text('Reported'), findsWidgets);
      expect(find.text('In Review'), findsWidgets);
      expect(find.text('Resolved'), findsWidgets);
    });

    testWidgets('displays empty state when driver has no issues',
        (tester) async {
      repository.issuesToReturn = [];
      repository.totalCount = 0;

      await tester.pumpWidget(createIssuesTestApp(repository: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('empty_issues_message')), findsOneWidget);
      expect(find.text('No operational issues reported yet.'), findsOneWidget);
      expect(find.byKey(const Key('empty_report_issue_button')), findsOneWidget);
    });

    testWidgets('displays error message and allows retry on API failure',
        (tester) async {
      repository.shouldThrow = true;
      repository.errorMessage = 'Network connection failed.';

      await tester.pumpWidget(createIssuesTestApp(repository: repository));
      await tester.pumpAndSettle();

      expect(find.text('Network connection failed.'), findsOneWidget);
      expect(find.byKey(const Key('retry_fetch_issues_button')), findsOneWidget);

      repository.shouldThrow = false;
      repository.issuesToReturn = [
        createSampleIssue(
          id: 'issue-1',
          title: 'Fuel cap broken',
          issueType: OperationalIssueType.vehicleProblem,
          status: OperationalIssueStatus.reported,
        ),
      ];

      await tester.tap(find.byKey(const Key('retry_fetch_issues_button')));
      await tester.pumpAndSettle();

      expect(find.text('Fuel cap broken'), findsOneWidget);
    });

    testWidgets('status filter chips trigger query with corresponding status',
        (tester) async {
      await tester.pumpWidget(createIssuesTestApp(repository: repository));
      await tester.pumpAndSettle();

      expect(repository.capturedStatus, isNull);

      await tester.tap(find.byKey(const Key('status_filter_reported')));
      await tester.pumpAndSettle();
      expect(repository.capturedStatus, equals(OperationalIssueStatus.reported));

      await tester.tap(find.byKey(const Key('status_filter_inreview')));
      await tester.pumpAndSettle();
      expect(repository.capturedStatus, equals(OperationalIssueStatus.inReview));

      await tester.tap(find.byKey(const Key('status_filter_resolved')));
      await tester.pumpAndSettle();
      expect(repository.capturedStatus, equals(OperationalIssueStatus.resolved));

      await tester.tap(find.byKey(const Key('status_filter_all')));
      await tester.pumpAndSettle();
      expect(repository.capturedStatus, isNull);
    });

    testWidgets('search input debounces and triggers search query',
        (tester) async {
      await tester.pumpWidget(createIssuesTestApp(repository: repository));
      await tester.pumpAndSettle();

      await tester.enterText(
          find.byKey(const Key('search_issues_input')), 'Transmission');
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      expect(repository.capturedSearch, equals('Transmission'));

      await tester.tap(find.byKey(const Key('clear_issues_search_button')));
      await tester.pumpAndSettle();

      expect(repository.capturedSearch, isNull);
    });

    testWidgets('tapping issue card navigates to detail route', (tester) async {
      repository.issuesToReturn = [
        createSampleIssue(
          id: 'target-issue-456',
          title: 'Flat tire near depot',
          issueType: OperationalIssueType.vehicleProblem,
          status: OperationalIssueStatus.reported,
        ),
      ];

      String? navigatedIssueId;

      final additionalRoute = GoRoute(
        path: '/driver/incidents/:id',
        builder: (context, state) {
          navigatedIssueId = state.pathParameters['id'];
          return Scaffold(body: Text('Detail Screen: $navigatedIssueId'));
        },
      );

      await tester.pumpWidget(createIssuesTestApp(
        repository: repository,
        additionalRoutes: [additionalRoute],
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('issue_card_target-issue-456')));
      await tester.pumpAndSettle();

      expect(navigatedIssueId, equals('target-issue-456'));
      expect(find.text('Detail Screen: target-issue-456'), findsOneWidget);
    });

    testWidgets('tapping Report Issue button navigates to /driver/incidents/new',
        (tester) async {
      bool newIssueScreenOpened = false;

      final additionalRoute = GoRoute(
        path: '/driver/incidents/new',
        builder: (context, state) {
          newIssueScreenOpened = true;
          return const Scaffold(body: Text('New Issue Form'));
        },
      );

      await tester.pumpWidget(createIssuesTestApp(
        repository: repository,
        additionalRoutes: [additionalRoute],
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('report_issue_top_action_button')));
      await tester.pumpAndSettle();

      expect(newIssueScreenOpened, isTrue);
      expect(find.text('New Issue Form'), findsOneWidget);
    });
  });
}
