import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/complaints/data/complaints_repository.dart';
import 'package:mobile/features/complaints/models/complaint_model.dart';
import 'package:mobile/features/complaints/presentation/citizen_complaints_screen.dart';

class FakeComplaintsRepository extends ComplaintsRepository {
  List<ComplaintSummaryModel> complaintsToReturn = [];
  int totalCount = 0;
  int totalPages = 1;
  int delayMs = 0;
  bool shouldThrow = false;
  String errorMessage = 'Failed to fetch complaints';
  int? errorOnPage;

  int? capturedPage;
  int? capturedPageSize;
  ComplaintStatus? capturedStatus;
  ComplaintCategory? capturedCategory;
  String? capturedSearch;
  String? capturedSortBy;
  String? capturedSortDirection;
  int callCount = 0;

  @override
  Future<PagedComplaintsModel> getMyComplaints({
    int page = 1,
    int pageSize = 20,
    ComplaintStatus? status,
    ComplaintCategory? category,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) async {
    callCount++;
    capturedPage = page;
    capturedPageSize = pageSize;
    capturedStatus = status;
    capturedCategory = category;
    capturedSearch = search;
    capturedSortBy = sortBy;
    capturedSortDirection = sortDirection;

    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }

    if (shouldThrow || (errorOnPage != null && errorOnPage == page)) {
      throw ApiException(message: errorMessage, statusCode: 500);
    }

    return PagedComplaintsModel(
      items: complaintsToReturn,
      page: page,
      pageSize: pageSize,
      totalCount: totalCount == 0 ? complaintsToReturn.length : totalCount,
      totalPages: totalPages,
    );
  }
}

ComplaintSummaryModel createSampleComplaint({
  required String id,
  required String subject,
  required ComplaintCategory category,
  required ComplaintStatus status,
  double? latitude,
  double? longitude,
  DateTime? createdAt,
}) {
  return ComplaintSummaryModel(
    id: id,
    citizenId: 'cit-1',
    category: category,
    subject: subject,
    status: status,
    latitude: latitude,
    longitude: longitude,
    createdAt: createdAt ?? DateTime.utc(2026, 9, 16, 10, 30),
  );
}

Widget createComplaintsTestApp({
  required ComplaintsRepository repository,
  List<RouteBase>? additionalRoutes,
  bool showAppBar = false,
}) {
  final router = GoRouter(
    initialLocation: '/citizen/complaints',
    routes: [
      GoRoute(
        path: '/citizen/complaints',
        builder: (context, state) => CitizenComplaintsScreen(
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
  group('CitizenComplaintsScreen Widget Tests', () {
    late FakeComplaintsRepository repository;

    setUp(() {
      repository = FakeComplaintsRepository();
    });

    testWidgets('renders loading state initially while fetching complaints', (tester) async {
      repository.delayMs = 200;
      await tester.pumpWidget(createComplaintsTestApp(repository: repository));
      await tester.pump();

      expect(find.text('Loading complaints...'), findsOneWidget);

      await tester.pumpAndSettle();
    });

    testWidgets('renders complaint list with cards and formatted attributes', (tester) async {
      repository.complaintsToReturn = [
        createSampleComplaint(
          id: 'c-1',
          subject: 'Missed garbage bin on Oak Lane',
          category: ComplaintCategory.missedCollection,
          status: ComplaintStatus.submitted,
          latitude: 6.9271,
          longitude: 79.8612,
        ),
        createSampleComplaint(
          id: 'c-2',
          subject: 'Delay in morning pickup',
          category: ComplaintCategory.delayedService,
          status: ComplaintStatus.inReview,
        ),
        createSampleComplaint(
          id: 'c-3',
          subject: 'Spilled litter near school',
          category: ComplaintCategory.poorService,
          status: ComplaintStatus.resolved,
        ),
      ];

      await tester.pumpWidget(createComplaintsTestApp(repository: repository));
      await tester.pumpAndSettle();

      expect(find.text('My Complaints'), findsOneWidget);
      expect(find.text('Missed garbage bin on Oak Lane'), findsOneWidget);
      expect(find.text('Delay in morning pickup'), findsOneWidget);
      expect(find.text('Spilled litter near school'), findsOneWidget);

      expect(find.text('Missed Collection'), findsOneWidget);
      expect(find.text('Delayed Service'), findsOneWidget);
      expect(find.text('Poor Service'), findsOneWidget);

      expect(find.text('Submitted'), findsOneWidget);
      expect(find.text('In Review'), findsOneWidget);
      expect(find.text('Resolved'), findsOneWidget);
    });

    testWidgets('renders empty state when no complaints exist', (tester) async {
      repository.complaintsToReturn = [];

      await tester.pumpWidget(createComplaintsTestApp(repository: repository));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('empty_complaints_state_title')), findsOneWidget);
      expect(find.text('No complaints yet'), findsOneWidget);
      expect(find.text('Submitted complaints will appear here.'), findsOneWidget);
      expect(find.byKey(const Key('empty_state_submit_complaint_button')), findsOneWidget);
    });

    testWidgets('renders error state and retries on button press', (tester) async {
      repository.shouldThrow = true;
      repository.errorMessage = 'Network connection failed';

      await tester.pumpWidget(createComplaintsTestApp(repository: repository));
      await tester.pumpAndSettle();

      expect(find.text('Unable to Load Complaints'), findsOneWidget);
      expect(find.text('Network connection failed'), findsOneWidget);

      // Fix repo and retry
      repository.shouldThrow = false;
      repository.complaintsToReturn = [
        createSampleComplaint(
          id: 'c-1',
          subject: 'Resolved complaint',
          category: ComplaintCategory.other,
          status: ComplaintStatus.resolved,
        ),
      ];

      await tester.tap(find.byKey(const Key('retry_load_complaints_button')));
      await tester.pumpAndSettle();

      expect(find.text('Resolved complaint'), findsOneWidget);
    });

    testWidgets('status filter bottom sheet triggers status query', (tester) async {
      repository.complaintsToReturn = [
        createSampleComplaint(
          id: 'c-1',
          subject: 'All complaints item',
          category: ComplaintCategory.missedCollection,
          status: ComplaintStatus.submitted,
        ),
      ];

      await tester.pumpWidget(createComplaintsTestApp(repository: repository));
      await tester.pumpAndSettle();

      // Open bottom sheet
      await tester.tap(find.byKey(const Key('complaints_status_filter_selector')));
      await tester.pumpAndSettle();

      expect(find.text('Filter by Status'), findsOneWidget);

      // Select InReview status
      await tester.tap(find.byKey(const Key('complaint_status_option_inreview')));
      await tester.pumpAndSettle();

      expect(repository.capturedStatus, ComplaintStatus.inReview);
    });

    testWidgets('search field triggers debounced search query', (tester) async {
      await tester.pumpWidget(createComplaintsTestApp(repository: repository));
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('search_complaints_input')), 'pickup delay');
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      expect(repository.capturedSearch, 'pickup delay');

      // Clear search
      await tester.tap(find.byKey(const Key('clear_complaints_search_button')));
      await tester.pumpAndSettle();

      expect(repository.capturedSearch, isNull);
    });

    testWidgets('tap complaint card navigates to complaint detail screen', (tester) async {
      String? navigatedId;

      repository.complaintsToReturn = [
        createSampleComplaint(
          id: 'c-100',
          subject: 'Test complaint card navigation',
          category: ComplaintCategory.missedCollection,
          status: ComplaintStatus.submitted,
        ),
      ];

      await tester.pumpWidget(
        createComplaintsTestApp(
          repository: repository,
          additionalRoutes: [
            GoRoute(
              path: '/citizen/complaints/:id',
              builder: (context, state) {
                navigatedId = state.pathParameters['id'];
                return Scaffold(body: Text('Detail Screen for $navigatedId'));
              },
            ),
          ],
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('complaint_card_c-100')));
      await tester.pumpAndSettle();

      expect(navigatedId, 'c-100');
      expect(find.text('Detail Screen for c-100'), findsOneWidget);
    });

    testWidgets('tap submit top action button navigates to submit complaint screen', (tester) async {
      bool navigatedToNew = false;

      await tester.pumpWidget(
        createComplaintsTestApp(
          repository: repository,
          additionalRoutes: [
            GoRoute(
              path: '/citizen/complaints/new',
              builder: (context, state) {
                navigatedToNew = true;
                return const Scaffold(body: Text('New Complaint Form'));
              },
            ),
          ],
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('submit_complaint_top_action_button')));
      await tester.pumpAndSettle();

      expect(navigatedToNew, isTrue);
      expect(find.text('New Complaint Form'), findsOneWidget);
    });
  });
}
