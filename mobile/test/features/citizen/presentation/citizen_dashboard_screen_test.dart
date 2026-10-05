import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/routing/app_router.dart';
import 'package:mobile/features/bins/data/public_waste_bins_repository.dart';
import 'package:mobile/features/bins/models/paged_public_waste_bins_model.dart';
import 'package:mobile/features/bins/models/public_waste_bin_detail_model.dart';
import 'package:mobile/features/bins/models/public_waste_bin_query.dart';
import 'package:mobile/features/bins/presentation/find_bins_screen.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/presentation/change_password_screen.dart';
import 'package:mobile/features/auth/presentation/login_screen.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/citizen/presentation/citizen_dashboard_screen.dart';
import 'package:mobile/features/citizen/presentation/citizen_profile_screen.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/paged_waste_reports_model.dart';
import 'package:mobile/features/reporting/models/waste_report_list_item_model.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_report_status_history_model.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/presentation/my_reports_screen.dart';
import 'package:mobile/features/reporting/presentation/report_detail_screen.dart';
import 'package:mobile/features/reporting/presentation/report_waste_screen.dart';
import 'package:mobile/features/complaints/data/complaints_repository.dart';
import 'package:mobile/features/complaints/models/complaint_model.dart';
import 'package:mobile/features/complaints/presentation/citizen_complaints_screen.dart';
import 'package:mobile/shared/widgets/app_card.dart';

class MockReportingRepository extends ReportingRepository {
  List<WasteReportListItemModel> reportsToReturn = [];
  int delayMs = 0;
  bool shouldThrow = false;
  String errorMessage = 'Failed to fetch reports';

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

    if (shouldThrow) {
      throw ApiException(message: errorMessage, statusCode: 500);
    }

    return PagedWasteReportsModel(
      items: reportsToReturn,
      page: page,
      pageSize: pageSize,
      totalCount: reportsToReturn.length,
      totalPages: 1,
    );
  }

  @override
  Future<WasteReportDetailModel> getWasteReport(String reportId) async {
    return WasteReportDetailModel(
      id: reportId,
      citizenId: 'citizen-456',
      citizenName: 'Nimali Fernando',
      description: 'Report detail for $reportId',
      wasteType: WasteType.general,
      latitude: 6.9271,
      longitude: 79.8612,
      status: WasteReportStatus.submitted,
      createdAt: DateTime.utc(2026, 9, 16, 10, 0),
    );
  }

  @override
  Future<List<WasteReportStatusHistoryModel>> getWasteReportHistory(String reportId) async {
    return [
      WasteReportStatusHistoryModel(
        id: 'hist-1',
        wasteReportId: reportId,
        fromStatus: null,
        toStatus: WasteReportStatus.submitted,
        changedAt: DateTime.utc(2026, 9, 16, 10, 0),
      ),
    ];
  }
}

WasteReportListItemModel createSampleReport({
  required String id,
  required String description,
  required WasteType wasteType,
  required WasteReportStatus status,
  String? addressText,
  DateTime? createdAt,
}) {
  return WasteReportListItemModel(
    id: id,
    description: description,
    wasteType: wasteType,
    status: status,
    addressText: addressText,
    latitude: 6.9271,
    longitude: 79.8612,
    createdAt: createdAt ?? DateTime.now(),
  );
}

class MockCitizenAuthNotifier extends AuthNotifier {
  final AuthState _initial;
  bool logoutCalled = false;

  MockCitizenAuthNotifier(this._initial);

  @override
  AuthState build() => _initial;

  @override
  Future<void> logout() async {
    logoutCalled = true;
    state = const AuthState.unauthenticated();
  }
}

class MockPublicWasteBinsRepository extends PublicWasteBinsRepository {
  @override
  Future<PagedPublicWasteBinsModel> getPublicWasteBins(PublicWasteBinQuery query) async {
    return PagedPublicWasteBinsModel(
      items: const [],
      page: query.page,
      pageSize: query.pageSize,
      totalCount: 0,
      totalPages: 1,
    );
  }

  @override
  Future<PublicWasteBinDetailModel> getPublicWasteBin(String binId) {
    throw UnimplementedError();
  }
}

class MockComplaintsRepository extends ComplaintsRepository {
  MockComplaintsRepository() : super();

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
    return PagedComplaintsModel(
      items: const [],
      page: page,
      pageSize: pageSize,
      totalCount: 0,
      totalPages: 1,
    );
  }
}

void main() {
  late MockReportingRepository mockRepo;

  setUp(() {
    mockRepo = MockReportingRepository();
  });

  const citizenUser = AuthUser(
    id: 'citizen-456',
    fullName: 'Nimali Fernando',
    email: 'nimali@smartwaste.lk',
    role: AppRoles.citizen,
  );

  Widget createCitizenTestApp({
    AuthNotifier Function()? notifierOverride,
    ReportingRepository? repositoryOverride,
    PublicWasteBinsRepository? publicBinsRepositoryOverride,
    ComplaintsRepository? complaintsRepositoryOverride,
    Size? surfaceSize,
    Key? key,
  }) {
    return ProviderScope(
      key: key,
      overrides: [
        authProvider.overrideWith(
          notifierOverride ?? () => MockCitizenAuthNotifier(const AuthState.authenticated(citizenUser)),
        ),
        reportingRepositoryProvider.overrideWithValue(repositoryOverride ?? mockRepo),
        publicWasteBinsRepositoryProvider.overrideWithValue(
          publicBinsRepositoryOverride ?? MockPublicWasteBinsRepository(),
        ),
        complaintsRepositoryProvider.overrideWithValue(
          complaintsRepositoryOverride ?? MockComplaintsRepository(),
        ),
      ],
      child: Consumer(
        builder: (context, ref, _) {
          final router = ref.watch(appRouterProvider);
          return MaterialApp.router(
            routerConfig: router,
          );
        },
      ),
    );
  }

  group('Citizen Dashboard Rendering & Hierarchy Tests', () {
    testWidgets('renders brand, greeting with first name, and context message', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      // Brand title in AppBar
      expect(find.text('SmartWaste'), findsOneWidget);

      // Greeting with extracted first name
      expect(find.byKey(const Key('citizen_greeting_text')), findsOneWidget);
      expect(find.text('Hello, Nimali'), findsOneWidget);
      expect(find.text('Help keep your community clean and healthy.'), findsOneWidget);

      // Top action buttons
      expect(find.byKey(const Key('citizen_notification_button')), findsNothing);
      expect(find.byTooltip('Notifications'), findsNothing);
      expect(find.byKey(const Key('citizen_account_button')), findsOneWidget);
    });

    testWidgets('renders primary Report Waste CTA with prominent styling', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      final reportWasteCta = find.byKey(const Key('citizen_report_waste_cta'));
      expect(reportWasteCta, findsOneWidget);
      expect(find.text('Report Waste'), findsOneWidget);
      expect(find.text('Report waste using a location, description and photo.'), findsOneWidget);
      expect(find.text('Report Issue'), findsOneWidget);
      expect(find.byKey(const Key('citizen_report_waste_illustration')), findsOneWidget);
    });

    testWidgets('renders 4 Quick Access items and Recent Activity empty state', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      // Quick Access section
      expect(find.text('Quick Access'), findsOneWidget);
      expect(find.byKey(const Key('citizen_quick_access_reports')), findsOneWidget);
      expect(find.byKey(const Key('citizen_quick_access_bins')), findsOneWidget);
      expect(find.text('Nearby Bins'), findsOneWidget);
      expect(find.text('Find waste bins near your location.'), findsOneWidget);
      expect(find.byKey(const Key('citizen_quick_access_complaints')), findsOneWidget);
      expect(find.byKey(const Key('citizen_quick_access_notifications')), findsNothing);
      expect(find.byKey(const Key('citizen_quick_access_profile')), findsOneWidget);

      // Recent Activity section
      expect(find.byKey(const Key('citizen_recent_activity_section')), findsOneWidget);
      expect(find.byKey(const Key('citizen_recent_activity_empty')), findsOneWidget);
      expect(find.text('No recent reports yet.'), findsOneWidget);
      expect(
        find.text('Submit a waste report to see activity here.'),
        findsOneWidget,
      );
    });

    testWidgets('renders bottom navigation with 4 destinations', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('citizen_bottom_nav')), findsOneWidget);
      expect(find.byKey(const Key('citizen_bottom_nav_home')), findsOneWidget);
      expect(find.byKey(const Key('citizen_bottom_nav_reports')), findsOneWidget);
      expect(find.byKey(const Key('citizen_bottom_nav_complaints')), findsOneWidget);
      expect(find.byKey(const Key('citizen_bottom_nav_profile')), findsOneWidget);
    });
  });

  group('Citizen Navigation & Flow Tests', () {
    testWidgets('tapping Report Waste CTA navigates to /citizen/report-waste screen', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      final cta = find.byKey(const Key('citizen_report_waste_cta'));
      await tester.tap(cta);
      await tester.pumpAndSettle();

      // Should be on Report Waste screen
      expect(find.byType(ReportWasteScreen), findsOneWidget);
      expect(find.text('Report Waste'), findsWidgets);

      // Back to dashboard
      final backButton = find.byKey(const Key('report_waste_back_button'));
      await tester.tap(backButton);
      await tester.pumpAndSettle();

      expect(find.byType(CitizenDashboardScreen), findsOneWidget);
    });

    testWidgets('tapping Nearby Bins card navigates to Find a Bin', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      final binsCard = find.byKey(const Key('citizen_quick_access_bins'));
      await tester.ensureVisible(binsCard);
      await tester.tap(binsCard);
      await tester.pumpAndSettle();

      expect(find.byType(FindBinsScreen), findsOneWidget);
      expect(find.text('Find a Bin'), findsOneWidget);

      // Back to dashboard
      final backButton = find.byTooltip('Back');
      await tester.tap(backButton);
      await tester.pumpAndSettle();

      expect(find.byType(CitizenDashboardScreen), findsOneWidget);
    });

    testWidgets('My Reports and Complaints quick access cards keep their routes', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      final reportsCard = find.byKey(const Key('citizen_quick_access_reports'));
      await tester.ensureVisible(reportsCard);
      await tester.tap(reportsCard);
      await tester.pumpAndSettle();
      expect(find.byType(MyReportsScreen), findsOneWidget);

      await tester.tap(find.byKey(const Key('citizen_bottom_nav_home')));
      await tester.pumpAndSettle();
      final complaintsCard = find.byKey(const Key('citizen_quick_access_complaints'));
      await tester.ensureVisible(complaintsCard);
      await tester.tap(complaintsCard);
      await tester.pumpAndSettle();
      expect(find.byType(CitizenComplaintsScreen), findsOneWidget);
    });

    testWidgets('tapping bottom nav items switches tabs seamlessly', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      // Tap My Reports
      await tester.tap(find.byKey(const Key('citizen_bottom_nav_reports')));
      await tester.pumpAndSettle();
      expect(find.byType(MyReportsScreen), findsOneWidget);

      // Tap Complaints
      await tester.tap(find.byKey(const Key('citizen_bottom_nav_complaints')));
      await tester.pumpAndSettle();
      expect(find.byType(CitizenComplaintsScreen), findsOneWidget);
      expect(find.text('My Complaints'), findsOneWidget);

      // Tap Profile
      await tester.tap(find.byKey(const Key('citizen_bottom_nav_profile')));
      await tester.pumpAndSettle();
      expect(find.byType(CitizenProfileScreen), findsOneWidget);

      // Tap Home
      await tester.tap(find.byKey(const Key('citizen_bottom_nav_home')));
      await tester.pumpAndSettle();
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);
    });

    testWidgets('system back button from non-dashboard tab navigates back to dashboard', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      // Tap My Reports
      await tester.tap(find.byKey(const Key('citizen_bottom_nav_reports')));
      await tester.pumpAndSettle();
      expect(find.byType(MyReportsScreen), findsOneWidget);

      // Trigger system back button
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();

      // Should be back on Citizen Dashboard
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);

      // Tap Complaints
      await tester.tap(find.byKey(const Key('citizen_bottom_nav_complaints')));
      await tester.pumpAndSettle();
      expect(find.byType(CitizenComplaintsScreen), findsOneWidget);
      expect(find.text('My Complaints'), findsOneWidget);

      // Trigger system back button
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();

      // Should be back on Citizen Dashboard
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);

      // Tap Profile
      await tester.tap(find.byKey(const Key('citizen_bottom_nav_profile')));
      await tester.pumpAndSettle();
      expect(find.byType(CitizenProfileScreen), findsOneWidget);

      // Trigger system back button
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();

      // Should be back on Citizen Dashboard
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);
    });

    testWidgets('Profile quick access opens the Citizen Profile screen', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      final profileCard = find.byKey(const Key('citizen_quick_access_profile'));
      await tester.ensureVisible(profileCard);
      await tester.tap(profileCard);
      await tester.pumpAndSettle();

      expect(find.byType(CitizenProfileScreen), findsOneWidget);
      await tester.tap(find.byKey(const Key('citizen_profile_back_button')));
      await tester.pumpAndSettle();
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);
    });
  });

  group('Citizen Profile Actions Tests', () {
    testWidgets('profile content stays directly below the shared header', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('citizen_account_button')));
      await tester.pumpAndSettle();

      final headerBottom = tester.getBottomLeft(find.byType(AppBar)).dy;
      final profileCard = find.descendant(
        of: find.byType(CitizenProfileScreen),
        matching: find.byType(AppCard),
      );
      final gap = tester.getTopLeft(profileCard).dy - headerBottom;
      expect(gap, inInclusiveRange(16, 48));
      expect(find.byKey(const Key('citizen_profile_change_password_button')), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('header account icon opens the finished profile with citizen details', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('citizen_account_button')));
      await tester.pumpAndSettle();

      expect(find.byType(CitizenProfileScreen), findsOneWidget);
      expect(find.text('Nimali Fernando'), findsOneWidget);
      expect(find.text('nimali@smartwaste.lk'), findsOneWidget);
      expect(find.text('Citizen'), findsAtLeastNWidgets(1));
      expect(find.byKey(const Key('citizen_profile_change_password_button')), findsOneWidget);
      expect(find.byKey(const Key('citizen_profile_back_button')), findsOneWidget);
      expect(find.byKey(const Key('citizen_profile_logout_button')), findsOneWidget);
      expect(find.textContaining('scheduled for implementation'), findsNothing);
      expect(find.textContaining('coming soon'), findsNothing);
      expect(find.textContaining('future update'), findsNothing);
    });

    testWidgets('Change Password opens the shared authenticated screen', (tester) async {
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('citizen_account_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('citizen_profile_change_password_button')));
      await tester.pumpAndSettle();

      expect(find.byType(ChangePasswordScreen), findsOneWidget);
    });

    testWidgets('Logout uses the existing notifier and navigates to Login', (tester) async {
      final mockNotifier = MockCitizenAuthNotifier(const AuthState.authenticated(citizenUser));

      await tester.pumpWidget(createCitizenTestApp(
        notifierOverride: () => mockNotifier,
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('citizen_account_button')));
      await tester.pumpAndSettle();

      final logoutButton = find.byKey(const Key('citizen_profile_logout_button'));
      await tester.ensureVisible(logoutButton);
      await tester.tap(logoutButton);
      await tester.pumpAndSettle();

      expect(mockNotifier.logoutCalled, isTrue);
      expect(find.byType(LoginScreen), findsOneWidget);
    });
  });

  group('Responsive Viewport Tests', () {
    testWidgets('finished profile actions remain usable at 320px', (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('citizen_account_button')));
      await tester.pumpAndSettle();

      expect(find.byType(CitizenProfileScreen), findsOneWidget);
      expect(find.byKey(const Key('citizen_profile_change_password_button')), findsOneWidget);
      expect(find.byKey(const Key('citizen_profile_back_button')), findsOneWidget);
      expect(find.byKey(const Key('citizen_profile_logout_button')), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('renders cleanly without overflow on narrow 320px viewport', (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);
      expect(find.byKey(const Key('citizen_report_waste_cta')), findsOneWidget);
      expect(find.text('Report Waste'), findsOneWidget);
    });

    testWidgets('renders cleanly without overflow on standard 390px viewport', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);
      expect(find.byKey(const Key('citizen_report_waste_cta')), findsOneWidget);
    });
  });

  group('Citizen Dashboard Recent Activity Tests', () {
    testWidgets('shows compact loading spinner while recent reports request is in flight', (tester) async {
      mockRepo.delayMs = 200;

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pump(); // start async fetch

      expect(find.byKey(const Key('citizen_recent_activity_loading')), findsOneWidget);
      expect(find.text('Loading recent activity...'), findsOneWidget);
      // Rest of dashboard remains visible
      expect(find.byKey(const Key('citizen_greeting_text')), findsOneWidget);
      expect(find.byKey(const Key('citizen_report_waste_cta')), findsOneWidget);

      await tester.pumpAndSettle();
      expect(find.byKey(const Key('citizen_recent_activity_loading')), findsNothing);
    });

    testWidgets('requests exactly page 1, pageSize 3, sorted by createdAt descending with authoritative scoping', (tester) async {
      mockRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-1',
          description: 'Garbage dump',
          wasteType: WasteType.general,
          status: WasteReportStatus.submitted,
        ),
      ];

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(mockRepo.callCount, 1);
      expect(mockRepo.capturedPage, 1);
      expect(mockRepo.capturedPageSize, 3);
      expect(mockRepo.capturedSortBy, 'createdAt');
      expect(mockRepo.capturedSortDirection, 'desc');
    });

    testWidgets('pull to refresh reloads recent reports and updates visible activity', (tester) async {
      mockRepo.reportsToReturn = [
        createSampleReport(id: 'rep-old', description: 'Old report', wasteType: WasteType.general, status: WasteReportStatus.submitted),
      ];
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('recent_activity_item_rep-old')), findsOneWidget);
      expect(mockRepo.callCount, 1);

      mockRepo.reportsToReturn = [
        createSampleReport(id: 'rep-new', description: 'New report', wasteType: WasteType.recyclable, status: WasteReportStatus.verified),
      ];
      await tester.fling(find.byKey(const Key('citizen_greeting_text')), const Offset(0, 300), 1000);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('citizen_dashboard_refresh_indicator')), findsOneWidget);
      expect(mockRepo.callCount, 2);
      expect(find.byKey(const Key('recent_activity_item_rep-old')), findsNothing);
      expect(find.byKey(const Key('recent_activity_item_rep-new')), findsOneWidget);
    });

    testWidgets('failed refresh keeps last loaded activity and finishes cleanly', (tester) async {
      mockRepo.reportsToReturn = [
        createSampleReport(id: 'rep-stable', description: 'Saved report', wasteType: WasteType.general, status: WasteReportStatus.submitted),
      ];
      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();
      mockRepo.shouldThrow = true;

      await tester.fling(find.byKey(const Key('citizen_greeting_text')), const Offset(0, 300), 1000);
      await tester.pumpAndSettle();

      expect(mockRepo.callCount, 2);
      expect(find.byKey(const Key('recent_activity_item_rep-stable')), findsOneWidget);
      expect(find.byKey(const Key('citizen_recent_activity_loading')), findsNothing);
      expect(find.byKey(const Key('citizen_recent_activity_error')), findsNothing);
      expect(find.text("Couldn't load recent activity."), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('renders up to 3 recent waste reports with readable waste type, status badge, and submission time', (tester) async {
      mockRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-1',
          description: 'Recyclables at bus stop',
          wasteType: WasteType.recyclable,
          status: WasteReportStatus.submitted,
          addressText: 'Temple Road, Maharagama',
          createdAt: DateTime.now(),
        ),
        createSampleReport(
          id: 'rep-2',
          description: 'Overflowing bin near park',
          wasteType: WasteType.general,
          status: WasteReportStatus.underReview,
          addressText: 'Galle Road, Colombo',
          createdAt: DateTime.now().subtract(const Duration(days: 1)),
        ),
        createSampleReport(
          id: 'rep-3',
          description: 'Food scraps pile',
          wasteType: WasteType.organic,
          status: WasteReportStatus.resolved,
          addressText: 'High Level Road, Nugegoda',
          createdAt: DateTime.now().subtract(const Duration(days: 3)),
        ),
      ];

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('citizen_recent_activity_card')), findsOneWidget);
      expect(find.byKey(const Key('recent_activity_item_rep-1')), findsOneWidget);
      expect(find.byKey(const Key('recent_activity_item_rep-2')), findsOneWidget);
      expect(find.byKey(const Key('recent_activity_item_rep-3')), findsOneWidget);

      // Waste type names
      expect(find.text('Recyclable Waste'), findsOneWidget);
      expect(find.text('General Waste'), findsOneWidget);
      expect(find.text('Organic Waste'), findsOneWidget);

      // Status labels
      expect(find.text('Submitted'), findsOneWidget);
      expect(find.text('Under Review'), findsOneWidget);
      expect(find.text('Resolved'), findsOneWidget);

      // Address texts
      expect(find.text('Temple Road, Maharagama'), findsOneWidget);
      expect(find.text('Galle Road, Colombo'), findsOneWidget);
      expect(find.text('High Level Road, Nugegoda'), findsOneWidget);

      // Submission times contain relative markers
      expect(find.textContaining('Today •'), findsOneWidget);
      expect(find.textContaining('Yesterday •'), findsOneWidget);

      // View All Reports button is present
      expect(find.byKey(const Key('citizen_view_all_reports_button')), findsOneWidget);
      expect(find.text('View All Reports'), findsOneWidget);
    });

    testWidgets('caps display at maximum 3 activities even if backend returns more', (tester) async {
      mockRepo.reportsToReturn = [
        createSampleReport(id: 'rep-1', description: 'Item 1', wasteType: WasteType.general, status: WasteReportStatus.submitted),
        createSampleReport(id: 'rep-2', description: 'Item 2', wasteType: WasteType.organic, status: WasteReportStatus.inProgress),
        createSampleReport(id: 'rep-3', description: 'Item 3', wasteType: WasteType.hazardous, status: WasteReportStatus.verified),
        createSampleReport(id: 'rep-4', description: 'Item 4', wasteType: WasteType.bulky, status: WasteReportStatus.scheduled),
      ];

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('recent_activity_item_rep-1')), findsOneWidget);
      expect(find.byKey(const Key('recent_activity_item_rep-2')), findsOneWidget);
      expect(find.byKey(const Key('recent_activity_item_rep-3')), findsOneWidget);
      expect(find.byKey(const Key('recent_activity_item_rep-4')), findsNothing);
    });

    testWidgets('renders all 8 status values with readable display names without raw enum values', (tester) async {
      for (final status in WasteReportStatus.values) {
        mockRepo.reportsToReturn = [
          createSampleReport(id: 'rep-${status.name}', description: 'Report', wasteType: WasteType.general, status: status),
        ];

        await tester.pumpWidget(createCitizenTestApp(key: ValueKey(status.name)));
        await tester.pumpAndSettle();

        expect(find.text(status.displayName), findsOneWidget);
        if (status.displayName != status.name) {
          expect(find.text(status.name), findsNothing);
        }
      }
    });

    testWidgets('empty state displays friendly message and no duplicate report CTA', (tester) async {
      mockRepo.reportsToReturn = [];

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('citizen_recent_activity_empty')), findsOneWidget);
      expect(find.text('No recent reports yet.'), findsOneWidget);
      expect(find.text('Submit a waste report to see activity here.'), findsOneWidget);
      expect(find.byKey(const Key('citizen_view_all_reports_button')), findsNothing);
    });

    testWidgets('error state displays friendly message without technical details and allows retry', (tester) async {
      mockRepo.shouldThrow = true;
      mockRepo.errorMessage = 'Network connection reset by peer (500)';

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      // Error state inside recent activity card
      expect(find.byKey(const Key('citizen_recent_activity_error')), findsOneWidget);
      expect(find.text("Couldn't load recent activity."), findsOneWidget);
      expect(find.byKey(const Key('citizen_recent_activity_retry_button')), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);

      // Technical details are not exposed
      expect(find.textContaining('Network connection reset'), findsNothing);
      expect(find.textContaining('ApiException'), findsNothing);
      expect(find.textContaining('500'), findsNothing);

      // Rest of dashboard intact
      expect(find.byKey(const Key('citizen_greeting_text')), findsOneWidget);
      expect(find.byKey(const Key('citizen_report_waste_cta')), findsOneWidget);

      // Tapping Retry succeeds after error resolves
      mockRepo.shouldThrow = false;
      mockRepo.reportsToReturn = [
        createSampleReport(id: 'rep-retry', description: 'Recovered item', wasteType: WasteType.general, status: WasteReportStatus.submitted),
      ];

      final retryBtn = find.byKey(const Key('citizen_recent_activity_retry_button'));
      await tester.ensureVisible(retryBtn);
      await tester.tap(retryBtn);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('citizen_recent_activity_error')), findsNothing);
      expect(find.byKey(const Key('recent_activity_item_rep-retry')), findsOneWidget);
    });

    testWidgets('tapping "View All Reports" navigates to /citizen/reports', (tester) async {
      mockRepo.reportsToReturn = [
        createSampleReport(id: 'rep-1', description: 'Test', wasteType: WasteType.general, status: WasteReportStatus.submitted),
      ];

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      final viewAllBtn = find.byKey(const Key('citizen_view_all_reports_button'));
      await tester.ensureVisible(viewAllBtn);
      await tester.tap(viewAllBtn);
      await tester.pumpAndSettle();

      expect(find.byType(MyReportsScreen), findsOneWidget);
    });

    testWidgets('tapping recent activity row navigates to /citizen/reports/:id', (tester) async {
      mockRepo.reportsToReturn = [
        createSampleReport(id: 'rep-1', description: 'Test', wasteType: WasteType.general, status: WasteReportStatus.submitted),
      ];

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      final row = find.byKey(const Key('recent_activity_item_rep-1'));
      await tester.ensureVisible(row);
      await tester.tap(row);
      await tester.pumpAndSettle();

      expect(find.byType(ReportDetailScreen), findsOneWidget);
    });

    testWidgets('recent activity renders cleanly on narrow 320px viewport without overflow', (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      mockRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-1',
          description: 'Long description of waste issue near main junction',
          wasteType: WasteType.recyclable,
          status: WasteReportStatus.underReview,
          addressText: 'Very long address line on Colombo-Galle main highway road Maharagama',
        ),
        createSampleReport(
          id: 'rep-2',
          description: 'Second report',
          wasteType: WasteType.general,
          status: WasteReportStatus.inProgress,
        ),
      ];

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byKey(const Key('citizen_recent_activity_card')), findsOneWidget);
      expect(find.text('Under Review'), findsOneWidget);
      expect(find.text('In Progress'), findsOneWidget);
      expect(find.text('View All Reports'), findsOneWidget);
    });

    testWidgets('recent activity renders cleanly on standard 390px viewport without overflow', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      mockRepo.reportsToReturn = [
        createSampleReport(
          id: 'rep-1',
          description: 'Description 1',
          wasteType: WasteType.hazardous,
          status: WasteReportStatus.verified,
          addressText: 'Industrial Zone, Biyagama',
        ),
      ];

      await tester.pumpWidget(createCitizenTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byKey(const Key('citizen_recent_activity_card')), findsOneWidget);
    });
  });

}
