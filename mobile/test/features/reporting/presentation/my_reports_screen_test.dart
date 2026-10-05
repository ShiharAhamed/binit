import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/paged_waste_reports_model.dart';
import 'package:mobile/features/reporting/models/waste_report_list_item_model.dart';
import 'package:mobile/features/reporting/models/waste_report_priority.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/presentation/my_reports_screen.dart';

class FakeReportingRepository extends ReportingRepository {
  List<WasteReportListItemModel> reportsToReturn = [];
  int totalCount = 0;
  int totalPages = 1;
  int delayMs = 0;
  bool shouldThrow = false;
  String errorMessage = 'Failed to fetch reports';
  int? errorOnPage;

  int? capturedPage;
  int? capturedPageSize;
  WasteReportStatus? capturedStatus;
  WasteType? capturedWasteType;
  String? capturedSearch;
  String? capturedSortBy;
  String? capturedSortDirection;
  int callCount = 0;

  @override
  Future<PagedWasteReportsModel> getWasteReports({
    int page = 1,
    int pageSize = 20,
    WasteReportStatus? status,
    WasteType? wasteType,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) async {
    callCount++;
    capturedPage = page;
    capturedPageSize = pageSize;
    capturedStatus = status;
    capturedWasteType = wasteType;
    capturedSearch = search;
    capturedSortBy = sortBy;
    capturedSortDirection = sortDirection;

    if (delayMs > 0) {
      await Future<void>.delayed(Duration(milliseconds: delayMs));
    }

    if (shouldThrow || (errorOnPage != null && errorOnPage == page)) {
      throw ApiException(message: errorMessage, statusCode: 500);
    }

    return PagedWasteReportsModel(
      items: reportsToReturn,
      page: page,
      pageSize: pageSize,
      totalCount: totalCount == 0 ? reportsToReturn.length : totalCount,
      totalPages: totalPages,
    );
  }
}

WasteReportListItemModel createSampleReport({
  required String id,
  String? reportReference,
  required String description,
  required WasteType wasteType,
  required WasteReportStatus status,
  WasteReportPriority? priority,
  String? addressText,
  double latitude = 6.9271,
  double longitude = 79.8612,
  DateTime? createdAt,
  int attachmentCount = 0,
}) {
  return WasteReportListItemModel(
    id: id,
    reportReference: reportReference,
    description: description,
    wasteType: wasteType,
    status: status,
    priority: priority,
    addressText: addressText,
    latitude: latitude,
    longitude: longitude,
    createdAt: createdAt ?? DateTime.utc(2026, 9, 16, 10, 30),
    attachmentCount: attachmentCount,
  );
}

Widget createMyReportsTestApp({
  required ReportingRepository repository,
  List<RouteBase>? additionalRoutes,
  bool showAppBar = true,
}) {
  final router = GoRouter(
    initialLocation: '/citizen/reports',
    routes: [
      GoRoute(
        path: '/citizen/reports',
        builder: (context, state) => MyReportsScreen(
          repository: repository,
          showAppBar: showAppBar,
        ),
      ),
      GoRoute(
        path: '/citizen/report-waste',
        builder: (context, state) => const Scaffold(
          body: Center(child: Text('Report Waste Screen Mock')),
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
  group('MyReportsScreen Widget Tests', () {
    late FakeReportingRepository fakeRepo;

    setUp(() {
      fakeRepo = FakeReportingRepository();
    });

    testWidgets('displays loading spinner while initial request is in progress', (tester) async {
      fakeRepo.delayMs = 1000;
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-1',
          description: 'Delayed report loading test',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pump(); // Start build

      expect(find.text('Loading reports...'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      await tester.pump(const Duration(milliseconds: 1050));
      await tester.pumpAndSettle();

      expect(find.text('Loading reports...'), findsNothing);
      expect(find.text('Delayed report loading test'), findsOneWidget);
    });

    testWidgets('renders report list with waste type, status, description, address, and date', (tester) async {
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-1',
          description: 'Large garbage pile overflowing near school gate',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
          addressText: 'Temple Road, Maharagama',
          createdAt: DateTime.utc(2026, 9, 16, 14, 30),
        ),
        createSampleReport(
          id: 'rep-2',
          description: 'Plastic containers and bags dumped on canal bank',
          wasteType: WasteType.recyclable,
          status: WasteReportStatus.underReview,
          addressText: null, // Coordinates fallback
          latitude: 6.9271,
          longitude: 79.8612,
          createdAt: DateTime.utc(2026, 9, 16, 16, 45),
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(find.text('My Reports'), findsOneWidget);
      expect(find.byKey(const Key('report_card_rep-1')), findsOneWidget);
      expect(find.byKey(const Key('report_card_rep-2')), findsOneWidget);

      // Report 1 content
      expect(find.descendant(of: find.byKey(const Key('report_card_rep-1')), matching: find.text('General Waste')), findsOneWidget);
      expect(find.descendant(of: find.byKey(const Key('report_card_rep-1')), matching: find.text('Submitted')), findsOneWidget);
      expect(find.text('Large garbage pile overflowing near school gate'), findsOneWidget);
      expect(find.text('Temple Road, Maharagama'), findsOneWidget);

      // Report 2 content
      expect(find.descendant(of: find.byKey(const Key('report_card_rep-2')), matching: find.text('Recyclable Waste')), findsOneWidget);
      expect(find.descendant(of: find.byKey(const Key('report_card_rep-2')), matching: find.text('Under Review')), findsOneWidget);
      expect(find.text('Plastic containers and bags dumped on canal bank'), findsOneWidget);
      expect(find.text('6.9271, 79.8612'), findsOneWidget);
    });

    testWidgets('shows friendly references and authoritative statuses for the citizen review journey', (tester) async {
      final ids = [
        'a17add4f-79f3-4a0a-a9e0-150fde7d7827',
        'b17add4f-79f3-4a0a-a9e0-150fde7d7827',
        'c17add4f-79f3-4a0a-a9e0-150fde7d7827',
        'd17add4f-79f3-4a0a-a9e0-150fde7d7827',
      ];
      final statuses = [WasteReportStatus.submitted, WasteReportStatus.underReview, WasteReportStatus.verified, WasteReportStatus.rejected];
      fakeRepo.reportsToReturn = List.generate(ids.length, (index) => createSampleReport(
        id: ids[index], reportReference: ids[index].substring(0, 8).toUpperCase(),
        description: 'Reported waste item ${index + 1}', wasteType: WasteType.general, status: statuses[index],
      ));

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      for (final report in fakeRepo.reportsToReturn) {
        final card = find.byKey(Key('report_card_${report.id}'));
        await tester.scrollUntilVisible(card, 100, scrollable: find.descendant(of: find.byKey(const Key('my_reports_list_view')), matching: find.byType(Scrollable)));
        expect(find.descendant(of: card, matching: find.text('Report ${report.reportReference}')), findsOneWidget);
        expect(find.descendant(of: card, matching: find.text(report.status.displayName)), findsOneWidget);
        expect(find.text(report.id), findsNothing);
      }
    });

    testWidgets('renders all 8 WasteReport statuses with correct human-readable display names', (tester) async {
      fakeRepo.reportsToReturn = [
        createSampleReport(id: 'r1', description: 'Rep 1', wasteType: WasteType.general, status: WasteReportStatus.submitted),
        createSampleReport(id: 'r2', description: 'Rep 2', wasteType: WasteType.general, status: WasteReportStatus.underReview),
        createSampleReport(id: 'r3', description: 'Rep 3', wasteType: WasteType.general, status: WasteReportStatus.verified),
        createSampleReport(id: 'r4', description: 'Rep 4', wasteType: WasteType.general, status: WasteReportStatus.scheduled),
        createSampleReport(id: 'r5', description: 'Rep 5', wasteType: WasteType.general, status: WasteReportStatus.inProgress),
        createSampleReport(id: 'r6', description: 'Rep 6', wasteType: WasteType.general, status: WasteReportStatus.resolved),
        createSampleReport(id: 'r7', description: 'Rep 7', wasteType: WasteType.general, status: WasteReportStatus.rejected),
        createSampleReport(id: 'r8', description: 'Rep 8', wasteType: WasteType.general, status: WasteReportStatus.cancelled),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      for (final report in fakeRepo.reportsToReturn) {
        final card = find.byKey(Key('report_card_${report.id}'));
        await tester.scrollUntilVisible(
          card,
          100,
          scrollable: find.descendant(
            of: find.byKey(const Key('my_reports_list_view')),
            matching: find.byType(Scrollable),
          ),
        );
        expect(find.descendant(of: card, matching: find.text(report.status.displayName)), findsOneWidget);
      }
    });

    testWidgets('genuine empty state (0 reports, no filter) displays "No reports yet" and navigates to Report Waste', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('empty_reports_state_title')), findsOneWidget);
      expect(find.text('No reports yet'), findsOneWidget);
      expect(find.text("You haven't submitted any waste reports."), findsOneWidget);

      final reportWasteCta = find.byKey(const Key('empty_state_report_waste_cta'));
      expect(reportWasteCta, findsOneWidget);

      await tester.tap(reportWasteCta);
      await tester.pumpAndSettle();

      expect(find.text('Report Waste Screen Mock'), findsOneWidget);
    });

    testWidgets('filtered empty state displays "No matching reports" and Clear Filters button', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      // Enter search query
      final searchField = find.byKey(const Key('search_reports_input'));
      await tester.enterText(searchField, 'nonexistent waste query');
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('filtered_empty_state_title')), findsOneWidget);
      expect(find.text('No matching reports'), findsOneWidget);
      expect(find.text('Try changing your search or filter.'), findsOneWidget);
      expect(find.byKey(const Key('empty_state_report_waste_cta')), findsNothing);

      // Tap Clear Filters button
      final clearFiltersBtn = find.byKey(const Key('clear_filters_button'));
      expect(clearFiltersBtn, findsOneWidget);

      await tester.tap(clearFiltersBtn);
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedSearch, isNull);
    });

    testWidgets('initial error state displays friendly error and Retry button', (tester) async {
      fakeRepo.shouldThrow = true;
      fakeRepo.errorMessage = 'Network connection failed.';

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(find.text('Unable to Load Reports'), findsOneWidget);
      expect(find.text('Network connection failed.'), findsOneWidget);

      final retryBtn = find.byKey(const Key('retry_load_reports_button'));
      expect(retryBtn, findsOneWidget);

      // Clear error and retry
      fakeRepo.shouldThrow = false;
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-recovered',
          description: 'Recovered after retry',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
        ),
      ];

      await tester.tap(retryBtn);
      await tester.pumpAndSettle();

      expect(find.text('Recovered after retry'), findsOneWidget);
      expect(find.text('Unable to Load Reports'), findsNothing);
    });

    testWidgets('pull to refresh triggers page 1 reload preserving active filters', (tester) async {
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-1',
          description: 'Initial report',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(find.text('Initial report'), findsOneWidget);
      expect(fakeRepo.callCount, 1);

      // Select filter
      await tester.tap(find.byKey(const Key('status_filter_selector')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('status_option_verified')));
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedStatus, WasteReportStatus.verified);
      expect(fakeRepo.callCount, 2);

      // Trigger pull to refresh
      await tester.fling(find.byType(ListView), const Offset(0, 300), 1000);
      await tester.pump();
      await tester.pump(const Duration(seconds: 1));
      await tester.pumpAndSettle();

      expect(fakeRepo.callCount, 3);
      expect(fakeRepo.capturedPage, 1);
      expect(fakeRepo.capturedStatus, WasteReportStatus.verified);
    });

    testWidgets('search input debounces and calls backend with search term', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      final searchInput = find.byKey(const Key('search_reports_input'));
      await tester.enterText(searchInput, 'market');

      // Before debounce fires
      await tester.pump(const Duration(milliseconds: 200));
      expect(fakeRepo.capturedSearch, isNull);

      // After debounce fires
      await tester.pump(const Duration(milliseconds: 300));
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedSearch, 'market');
      expect(fakeRepo.capturedPage, 1);
    });

    testWidgets('clear search button resets search and reloads page 1 preserving status filter', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      // Select status filter
      await tester.tap(find.byKey(const Key('status_filter_selector')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('status_option_underreview')));
      await tester.pumpAndSettle();

      // Enter search
      final searchInput = find.byKey(const Key('search_reports_input'));
      await tester.enterText(searchInput, 'dumpster');
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedSearch, 'dumpster');
      expect(fakeRepo.capturedStatus, WasteReportStatus.underReview);

      // Clear search
      final clearBtn = find.byKey(const Key('clear_search_button'));
      expect(clearBtn, findsOneWidget);

      await tester.tap(clearBtn);
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedSearch, isNull);
      expect(fakeRepo.capturedStatus, WasteReportStatus.underReview);
    });

    testWidgets('pagination: Load More button appends next page items without duplicating', (tester) async {
      fakeRepo.totalCount = 4;
      fakeRepo.totalPages = 2;
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-p1-1',
          description: 'Page 1 item 1',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(find.text('Page 1 item 1'), findsOneWidget);
      expect(fakeRepo.capturedPage, 1);

      // Prepare page 2 data
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-p2-1',
          description: 'Page 2 item 1',
          wasteType: WasteType.organic,
          status: WasteReportStatus.verified,
        ),
      ];

      final loadMoreBtn = find.byKey(const Key('load_more_button'));
      expect(loadMoreBtn, findsOneWidget);
      await tester.tap(loadMoreBtn);
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedPage, 2);
      expect(find.text('Page 1 item 1'), findsOneWidget);
      expect(find.text('Page 2 item 1'), findsOneWidget);
    });

    testWidgets('pagination error retains existing items and shows retry footer', (tester) async {
      fakeRepo.totalCount = 30;
      fakeRepo.totalPages = 2;
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-existing-1',
          description: 'Existing report that must remain visible',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(find.text('Existing report that must remain visible'), findsOneWidget);

      // Cause page 2 to fail
      fakeRepo.errorOnPage = 2;
      fakeRepo.errorMessage = 'Connection lost during page 2';

      // Tap Load More
      final loadMoreBtn = find.byKey(const Key('load_more_button'));
      expect(loadMoreBtn, findsOneWidget);
      await tester.tap(loadMoreBtn);
      await tester.pumpAndSettle();

      // Existing item should still be visible
      expect(find.text('Existing report that must remain visible'), findsOneWidget);

      // Retry banner should be shown
      expect(find.text('Connection lost during page 2'), findsOneWidget);
      final retryLoadMoreBtn = find.byKey(const Key('retry_load_more_button'));
      expect(retryLoadMoreBtn, findsOneWidget);

      // Fix error and retry
      fakeRepo.errorOnPage = null;
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-page2-item',
          description: 'Loaded page 2 item after retry',
          wasteType: WasteType.organic,
          status: WasteReportStatus.verified,
        ),
      ];

      await tester.tap(retryLoadMoreBtn);
      await tester.pumpAndSettle();

      expect(find.text('Existing report that must remain visible'), findsOneWidget);
      expect(find.text('Loaded page 2 item after retry'), findsOneWidget);
    });

    testWidgets('authoritative ownership: no citizenId parameter is sent in query', (tester) async {
      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      // Confirm call was made with standard pagination parameters only
      expect(fakeRepo.capturedPage, 1);
      expect(fakeRepo.capturedPageSize, 20);
      expect(fakeRepo.capturedSortBy, 'createdAt');
      expect(fakeRepo.capturedSortDirection, 'desc');
    });

    testWidgets('renders cleanly on narrow 320px viewport without overflow', (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-narrow',
          description: 'Overflowing plastic and food waste on narrow street pavement',
          wasteType: WasteType.general,
          status: WasteReportStatus.underReview,
          addressText: '123 Very Long Road Name Near Junction, Colombo',
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(MyReportsScreen), findsOneWidget);
      expect(find.text('My Reports'), findsOneWidget);
      expect(find.text('Under Review'), findsOneWidget);
    });

    testWidgets('renders cleanly on standard 390px viewport without overflow', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-std',
          description: 'Standard viewport waste report display verification',
          wasteType: WasteType.organic,
          status: WasteReportStatus.inProgress,
          addressText: 'Main Street',
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(MyReportsScreen), findsOneWidget);
      expect(find.text('In Progress'), findsOneWidget);
    });

    testWidgets('"My Reports" title appears only once as page title and duplicate body heading is removed', (tester) async {
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-1',
          description: 'Single title test',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      // "My Reports" should only appear once (in AppBar)
      expect(find.text('My Reports'), findsOneWidget);
      // Subtitle should appear
      expect(find.text('Track your submitted waste reports'), findsOneWidget);
    });

    testWidgets('when showAppBar is false (in shell), inner AppBar is omitted and duplicate title is not rendered', (tester) async {
      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo, showAppBar: false));
      await tester.pumpAndSettle();

      // "My Reports" should not be rendered inside MyReportsScreen when showAppBar is false
      expect(find.text('My Reports'), findsNothing);
      // Subtitle is still rendered
      expect(find.text('Track your submitted waste reports'), findsOneWidget);
    });

    testWidgets('status selector is visible and horizontal status chips no longer exist', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      // Compact status selector is visible
      expect(find.byKey(const Key('status_filter_selector')), findsOneWidget);
      expect(find.text('Status: '), findsOneWidget);
      expect(find.text('All Reports'), findsOneWidget);

      // Horizontal chip bar keys do not exist
      expect(find.byKey(const Key('status_filter_all')), findsNothing);
      expect(find.byKey(const Key('status_filter_submitted')), findsNothing);
      expect(find.byKey(const Key('status_filter_verified')), findsNothing);
    });

    testWidgets('tapping status selector opens bottom sheet with all 9 options', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      // Tap selector
      await tester.tap(find.byKey(const Key('status_filter_selector')));
      await tester.pumpAndSettle();

      // Bottom sheet header
      expect(find.text('Filter by Status'), findsOneWidget);
      expect(find.text('Select a report status to filter your list'), findsOneWidget);

      // All 9 status options are displayed
      expect(find.byKey(const Key('status_option_all')), findsOneWidget);
      expect(find.byKey(const Key('status_option_submitted')), findsOneWidget);
      expect(find.byKey(const Key('status_option_underreview')), findsOneWidget);
      expect(find.byKey(const Key('status_option_verified')), findsOneWidget);
      expect(find.byKey(const Key('status_option_rejected')), findsOneWidget);
      expect(find.byKey(const Key('status_option_scheduled')), findsOneWidget);
      expect(find.byKey(const Key('status_option_inprogress')), findsOneWidget);
      expect(find.byKey(const Key('status_option_resolved')), findsOneWidget);
      expect(find.byKey(const Key('status_option_cancelled')), findsOneWidget);
    });

    testWidgets('selecting "Submitted" triggers Submitted backend filter and closes bottom sheet', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedStatus, isNull);

      // Open selector and select Submitted
      await tester.tap(find.byKey(const Key('status_filter_selector')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('status_option_submitted')));
      await tester.pumpAndSettle();

      // Bottom sheet is dismissed
      expect(find.byKey(const Key('status_option_submitted')), findsNothing);

      // Backend called with Submitted filter
      expect(fakeRepo.capturedStatus, WasteReportStatus.submitted);
      expect(fakeRepo.capturedPage, 1);

      // Selector reflects selected filter
      expect(find.descendant(of: find.byKey(const Key('status_filter_selector')), matching: find.text('Submitted')), findsOneWidget);
    });

    testWidgets('selecting another status works and selecting "All Reports" clears filter', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      // Select Verified
      await tester.tap(find.byKey(const Key('status_filter_selector')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('status_option_verified')));
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedStatus, WasteReportStatus.verified);

      // Now select All Reports
      await tester.tap(find.byKey(const Key('status_filter_selector')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('status_option_all')));
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedStatus, isNull);
      expect(fakeRepo.capturedPage, 1);
    });

    testWidgets('search text remains preserved when changing status filter', (tester) async {
      fakeRepo.reportsToReturn = [];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      // Type search
      final searchField = find.byKey(const Key('search_reports_input'));
      await tester.enterText(searchField, 'industrial park');
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      expect(fakeRepo.capturedSearch, 'industrial park');

      // Change status filter
      await tester.tap(find.byKey(const Key('status_filter_selector')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('status_option_scheduled')));
      await tester.pumpAndSettle();

      // Search remains preserved and backend receives both
      expect(fakeRepo.capturedSearch, 'industrial park');
      expect(fakeRepo.capturedStatus, WasteReportStatus.scheduled);
      expect(find.text('industrial park'), findsOneWidget);
    });

    testWidgets('renders photo count badge on cards when attachments exist', (tester) async {
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-with-photos',
          description: 'Report with 3 photos attached',
          wasteType: WasteType.hazardous,
          status: WasteReportStatus.submitted,
          attachmentCount: 3,
        ),
        createSampleReport(
          id: 'rep-single-photo',
          description: 'Report with 1 photo attached',
          wasteType: WasteType.general,
          status: WasteReportStatus.verified,
          attachmentCount: 1,
        ),
        createSampleReport(
          id: 'rep-no-photos',
          description: 'Report with no photos',
          wasteType: WasteType.bulky,
          status: WasteReportStatus.resolved,
          attachmentCount: 0,
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(repository: fakeRepo));
      await tester.pumpAndSettle();

      expect(find.text('3 photos'), findsOneWidget);
      expect(find.text('1 photo'), findsOneWidget);
      expect(find.text('0 photos'), findsNothing);
    });

    testWidgets('tapping report card navigates to /citizen/reports/:id', (tester) async {
      fakeRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-detail-test',
          description: 'Report to open in detail view',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
        ),
      ];

      await tester.pumpWidget(createMyReportsTestApp(
        repository: fakeRepo,
        additionalRoutes: [
          GoRoute(
            path: '/citizen/reports/:id',
            builder: (context, state) => Scaffold(
              body: Center(child: Text('Detail Screen Mock for ${state.pathParameters['id']}')),
            ),
          ),
        ],
      ));
      await tester.pumpAndSettle();

      final cardFinder = find.byKey(const Key('report_card_rep-detail-test'));
      expect(cardFinder, findsOneWidget);

      await tester.tap(cardFinder);
      await tester.pumpAndSettle();

      expect(find.text('Detail Screen Mock for rep-detail-test'), findsOneWidget);
    });
  });
}
