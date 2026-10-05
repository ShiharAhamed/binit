import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/routing/app_router.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/presentation/change_password_screen.dart';
import 'package:mobile/features/auth/presentation/login_screen.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/driver/data/driver_repository.dart';
import 'package:mobile/features/driver/models/assignment_detail_model.dart';
import 'package:mobile/features/driver/models/assignment_summary_model.dart';
import 'package:mobile/features/driver/models/assignment_task_model.dart';
import 'package:mobile/features/driver/models/collection_assignment_status.dart';
import 'package:mobile/features/driver/models/driver_availability_status.dart';
import 'package:mobile/features/driver/models/driver_self_model.dart';
import 'package:mobile/features/driver/models/paged_assignments_model.dart';
import 'package:mobile/features/driver/models/route_read_model.dart';
import 'package:mobile/features/driver/models/route_stop_model.dart';
import 'package:mobile/features/driver/models/route_stop_status.dart';
import 'package:mobile/features/driver/presentation/driver_assignment_screen.dart';
import 'package:mobile/features/driver/presentation/driver_dashboard_screen.dart';
import 'package:mobile/features/driver/presentation/driver_profile_screen.dart';
import 'package:mobile/features/driver/presentation/driver_route_screen.dart';
import 'package:mobile/features/driver/presentation/driver_tasks_screen.dart';
import 'package:mobile/features/operations/data/operations_repository.dart';
import 'package:mobile/features/operations/models/operational_issue_model.dart';
import 'package:mobile/features/operations/presentation/driver_operational_issues_screen.dart';
import 'package:mobile/shared/widgets/app_card.dart';

class MockDriverAuthNotifier extends AuthNotifier {
  final AuthState _initial;
  bool logoutCalled = false;

  MockDriverAuthNotifier(this._initial);

  @override
  AuthState build() => _initial;

  @override
  Future<void> logout() async {
    logoutCalled = true;
    state = const AuthState.unauthenticated();
  }
}

class FakeTestDriverRepository extends DriverRepository {
  DriverSelfModel? driverSelf;
  AssignmentDetailModel? currentAssignment;
  List<AssignmentSummaryModel> historyAssignments;
  bool updateAvailabilityThrows = false;
  DriverAvailabilityStatus? lastUpdatedStatus;
  int getDriverSelfCallCount = 0;
  int getCurrentAssignmentCallCount = 0;

  FakeTestDriverRepository({
    this.driverSelf,
    this.currentAssignment,
    this.historyAssignments = const [],
    this.updateAvailabilityThrows = false,
  });

  @override
  Future<DriverSelfModel> getDriverSelf(String driverId) async {
    getDriverSelfCallCount++;
    return driverSelf ??
        DriverSelfModel(
          id: driverId,
          displayName: 'Samantha Perera',
          availabilityStatus: DriverAvailabilityStatus.available,
          isOccupied: false,
        );
  }

  @override
  Future<DriverSelfModel> updateAvailability(DriverAvailabilityStatus status) async {
    if (updateAvailabilityThrows) {
      throw const ApiException(
          message: 'Server rejected availability update.', statusCode: 400);
    }
    lastUpdatedStatus = status;
    driverSelf = DriverSelfModel(
      id: driverSelf?.id ?? 'driver-789',
      displayName: driverSelf?.displayName ?? 'Samantha Perera',
      availabilityStatus: status,
      isOccupied: driverSelf?.isOccupied ?? false,
    );
    return driverSelf!;
  }

  @override
  Future<AssignmentDetailModel?> getCurrentAssignmentDetail() async {
    getCurrentAssignmentCallCount++;
    return currentAssignment;
  }

  @override
  Future<AssignmentSummaryModel?> getCurrentAssignmentSummary() async {
    return currentAssignment?.toSummary();
  }

  @override
  Future<AssignmentDetailModel> getAssignmentDetail(String assignmentId) async {
    if (currentAssignment != null && currentAssignment!.id == assignmentId) {
      return currentAssignment!;
    }
    throw const ApiException(message: 'Not found', statusCode: 404);
  }

  @override
  Future<PagedAssignmentsModel> getMyAssignments({
    CollectionAssignmentStatus? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    return PagedAssignmentsModel(
      items: historyAssignments,
      page: page,
      pageSize: pageSize,
      totalCount: historyAssignments.length,
      totalPages: 1,
    );
  }

  @override
  Future<RouteReadModel> getRoute(String routeId) async {
    throw UnimplementedError();
  }
}

class MockOperationsRepository extends OperationsRepository {
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
    return PagedOperationalIssuesModel(
      items: const [],
      page: page,
      pageSize: pageSize,
      totalCount: 0,
      totalPages: 1,
    );
  }
}

AssignmentTaskModel _createSampleTask(String id, String code) {
  return AssignmentTaskModel(
    id: id,
    taskCode: code,
    targetType: 'Bin',
    wasteBinId: 'bin-1',
    collectionReason: 'Scheduled',
    status: 'Scheduled',
    scheduledAt: DateTime.utc(2026, 9, 26, 8, 0),
    latitude: 6.9271,
    longitude: 79.8612,
  );
}

void main() {
  const driverUser = AuthUser(
    id: 'driver-789',
    fullName: 'Samantha Perera',
    email: 'samantha.p@smartwaste.lk',
    role: AppRoles.driver,
  );

  final sampleAssignedAssignment = AssignmentDetailModel(
    id: 'asn-assigned-1',
    status: CollectionAssignmentStatus.assigned,
    driverId: 'driver-789',
    driverName: 'Samantha Perera',
    vehicleId: 'veh-1',
    vehicleRegistrationNumber: 'WP CAD-5678',
    stopCount: 3,
    completedStopCount: 0,
    failedStopCount: 0,
    assignedAt: DateTime.utc(2026, 9, 26, 8, 30),
  );

  final sampleInProgressAssignment = AssignmentDetailModel(
    id: 'asn-progress-1',
    status: CollectionAssignmentStatus.inProgress,
    driverId: 'driver-789',
    driverName: 'Samantha Perera',
    vehicleId: 'veh-1',
    vehicleRegistrationNumber: 'WP CAD-5678',
    stopCount: 4,
    completedStopCount: 2,
    failedStopCount: 1,
    assignedAt: DateTime.utc(2026, 9, 26, 8, 30),
    route: RouteReadModel(
      id: 'route-1',
      collectionAssignmentId: 'asn-progress-1',
      routingMethod: 'ManualOrder',
      stops: [
        RouteStopModel(
          id: 'stop-1',
          sequence: 1,
          status: RouteStopStatus.completed,
          task: _createSampleTask('task-1', 'TSK-101'),
        ),
        RouteStopModel(
          id: 'stop-2',
          sequence: 2,
          status: RouteStopStatus.completed,
          task: _createSampleTask('task-2', 'TSK-102'),
        ),
        RouteStopModel(
          id: 'stop-3',
          sequence: 3,
          status: RouteStopStatus.failed,
          task: _createSampleTask('task-3', 'TSK-103'),
        ),
        RouteStopModel(
          id: 'stop-4',
          sequence: 4,
          status: RouteStopStatus.pending,
          task: _createSampleTask('task-4', 'TSK-104'),
        ),
      ],
    ),
  );

  Widget createDriverTestApp({
    AuthNotifier Function()? notifierOverride,
    DriverRepository? repositoryOverride,
    OperationsRepository? operationsRepositoryOverride,
  }) {
    return ProviderScope(
      overrides: [
        authProvider.overrideWith(
          notifierOverride ??
              () => MockDriverAuthNotifier(const AuthState.authenticated(driverUser)),
        ),
        driverRepositoryProvider.overrideWithValue(
          repositoryOverride ?? FakeTestDriverRepository(),
        ),
        operationsRepositoryProvider.overrideWithValue(
          operationsRepositoryOverride ?? MockOperationsRepository(),
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

  group('Driver Dashboard Rendering & Hierarchy Tests', () {
    testWidgets('renders brand, greeting with driver first name, and operational context',
        (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      // Brand title in AppBar
      expect(find.text('SmartWaste'), findsOneWidget);

      // Greeting with extracted first name
      expect(find.byKey(const Key('driver_greeting_text')), findsOneWidget);
      expect(find.text('Hello, Samantha'), findsOneWidget);
      expect(find.textContaining('Ready for today\'s operations'), findsOneWidget);

      // Top action buttons
      expect(find.byKey(const Key('driver_notification_button')), findsNothing);
      expect(find.byTooltip('Notifications'), findsNothing);
      expect(find.byKey(const Key('driver_account_button')), findsOneWidget);
    });

    testWidgets('renders duty status control and independent occupancy badge',
        (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_operational_status_card')), findsOneWidget);
      expect(find.text('Duty Status'), findsOneWidget);
      expect(find.byKey(const Key('driver_availability_available_btn')), findsOneWidget);
      expect(find.byKey(const Key('driver_availability_off_duty_btn')), findsOneWidget);
      expect(find.byKey(const Key('driver_occupancy_badge')), findsOneWidget);
      expect(find.text('Assignment: Unassigned'), findsOneWidget);
    });

    testWidgets('renders primary Current Assignment section with prominent styling',
        (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      expect(find.text('Current Assignment'), findsOneWidget);
      expect(find.text('No active assignment'), findsOneWidget);
      expect(
          find.text(
              'No active collection assignment is available right now.'),
          findsOneWidget);
      expect(find.byKey(const Key('driver_view_assignment_button')), findsOneWidget);
      expect(find.text('View Assignment'), findsOneWidget);
    });

    testWidgets('renders 4 Quick Action cards and operational empty states',
        (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      // Quick Actions section
      expect(find.text('Quick Actions'), findsOneWidget);
      final tasksCard = find.byKey(const Key('driver_quick_action_tasks'));
      expect(tasksCard, findsOneWidget);
      expect(find.descendant(of: tasksCard, matching: find.text('Collection Tasks')),
          findsOneWidget);
      expect(
          find.descendant(
              of: tasksCard, matching: find.text('View your assigned collection work.')),
          findsOneWidget);

      final routeCard = find.byKey(const Key('driver_quick_action_route'));
      expect(routeCard, findsOneWidget);
      expect(find.descendant(of: routeCard, matching: find.text('Route')), findsOneWidget);
      expect(
          find.descendant(
              of: routeCard, matching: find.text('View your collection route and stops.')),
          findsOneWidget);

      final incidentsCard = find.byKey(const Key('driver_quick_action_incidents'));
      expect(incidentsCard, findsOneWidget);
      expect(find.descendant(of: incidentsCard, matching: find.text('Report Incident')),
          findsOneWidget);
      expect(
          find.descendant(
              of: incidentsCard,
              matching: find.text('Report a problem during collection.')),
          findsOneWidget);

      final profileCard = find.byKey(const Key('driver_quick_action_profile'));
      expect(profileCard, findsOneWidget);
      expect(find.descendant(of: profileCard, matching: find.text('Profile')),
          findsOneWidget);
      expect(
          find.descendant(
              of: profileCard,
              matching: find.text('View your account and profile actions.')),
          findsOneWidget);
      expect(find.byKey(const Key('driver_quick_action_notifications')), findsNothing);

      // Today's Work empty state
      expect(find.byKey(const Key('driver_todays_work_section')), findsOneWidget);
      expect(find.text('Today\'s Work'), findsOneWidget);
      expect(find.text('No Tasks In Progress'), findsOneWidget);
      expect(find.text('Your assigned collection tasks and progress will appear here.'),
          findsOneWidget);

      // Recent Updates empty state
      expect(find.byKey(const Key('driver_recent_updates_section')), findsOneWidget);
      expect(find.text('Recent Updates'), findsOneWidget);
      expect(find.byKey(const Key('driver_recent_updates_empty')), findsOneWidget);
      expect(find.text('No recent completed work.'), findsOneWidget);
      expect(find.byKey(const Key('driver_view_history_button')), findsOneWidget);
    });

    testWidgets('renders bottom navigation with 4 Driver destinations', (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_bottom_nav')), findsOneWidget);
      expect(find.byKey(const Key('driver_bottom_nav_home')), findsOneWidget);
      expect(find.byKey(const Key('driver_bottom_nav_tasks')), findsOneWidget);
      expect(find.byKey(const Key('driver_bottom_nav_route')), findsOneWidget);
      expect(find.byKey(const Key('driver_bottom_nav_profile')), findsOneWidget);
    });

    testWidgets('shows recent terminal work and a history link without changing current work',
        (tester) async {
      final repo = FakeTestDriverRepository(
        historyAssignments: [
          AssignmentSummaryModel(
            id: 'completed-history-id',
            status: CollectionAssignmentStatus.completed,
            driverId: 'driver-789',
            driverName: 'Samantha Perera',
            vehicleId: 'veh-1',
            vehicleRegistrationNumber: 'WP CAD-5678',
            stopCount: 2,
            completedStopCount: 2,
            failedStopCount: 0,
            assignedAt: DateTime.utc(2026, 9, 26, 8, 30),
          ),
        ],
      );

      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_recent_updates_card')), findsOneWidget);
      expect(find.text('Completed'), findsOneWidget);
      expect(find.text('WP CAD-5678'), findsOneWidget);
      expect(find.textContaining('completed-history-id'), findsNothing);
      expect(find.byKey(const Key('driver_view_history_button')), findsOneWidget);
    });
  });

  group('Driver Availability & Occupancy Tests', () {
    testWidgets('displays independent occupancy when driver is occupied',
        (tester) async {
      final repo = FakeTestDriverRepository(
        driverSelf: const DriverSelfModel(
          id: 'driver-789',
          displayName: 'Samantha Perera',
          availabilityStatus: DriverAvailabilityStatus.available,
          isOccupied: true,
        ),
      );
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_occupancy_badge')), findsOneWidget);
      expect(find.text('Assignment: Occupied'), findsOneWidget);
    });

    testWidgets('displays Off Duty active state when driver is off duty',
        (tester) async {
      final repo = FakeTestDriverRepository(
        driverSelf: const DriverSelfModel(
          id: 'driver-789',
          displayName: 'Samantha Perera',
          availabilityStatus: DriverAvailabilityStatus.offDuty,
          isOccupied: false,
        ),
      );
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_availability_off_duty_btn')), findsOneWidget);
      expect(find.text('Off Duty'), findsOneWidget);
    });

    testWidgets('tapping Off Duty triggers repository availability update',
        (tester) async {
      final repo = FakeTestDriverRepository();
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      final offDutyBtn = find.byKey(const Key('driver_availability_off_duty_btn'));
      await tester.tap(offDutyBtn);
      await tester.pumpAndSettle();

      expect(repo.lastUpdatedStatus, DriverAvailabilityStatus.offDuty);
    });

    testWidgets('tapping Available when off duty updates status to available',
        (tester) async {
      final repo = FakeTestDriverRepository(
        driverSelf: const DriverSelfModel(
          id: 'driver-789',
          displayName: 'Samantha Perera',
          availabilityStatus: DriverAvailabilityStatus.offDuty,
          isOccupied: false,
        ),
      );
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      final availableBtn = find.byKey(const Key('driver_availability_available_btn'));
      await tester.tap(availableBtn);
      await tester.pumpAndSettle();

      expect(repo.lastUpdatedStatus, DriverAvailabilityStatus.available);
    });

    testWidgets('backend failure on availability update shows error SnackBar and preserves state',
        (tester) async {
      final repo = FakeTestDriverRepository(
        updateAvailabilityThrows: true,
      );
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      final offDutyBtn = find.byKey(const Key('driver_availability_off_duty_btn'));
      await tester.tap(offDutyBtn);
      await tester.pumpAndSettle();

      expect(find.byType(SnackBar), findsOneWidget);
      expect(find.text('Server rejected availability update.'), findsOneWidget);
    });
  });

  group('Driver Current Assignment States Tests', () {
    testWidgets('State A (no assignment) has disabled View Assignment button',
        (tester) async {
      final repo = FakeTestDriverRepository(currentAssignment: null);
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      expect(find.text('No active assignment'), findsOneWidget);
      final button = tester.widget<ElevatedButton>(
        find.descendant(
          of: find.byKey(const Key('driver_view_assignment_button')),
          matching: find.byType(ElevatedButton),
        ),
      );
      expect(button.onPressed, isNull);
    });

    testWidgets('State B (Assigned) shows vehicle, status badge, and enabled View Assignment',
        (tester) async {
      final repo =
          FakeTestDriverRepository(currentAssignment: sampleAssignedAssignment);
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      expect(find.text('WP CAD-5678'), findsOneWidget);
      expect(find.byKey(const Key('driver_assignment_status_badge')),
          findsAtLeastNWidgets(1));
      expect(find.text('Assigned'), findsAtLeastNWidgets(1));
      expect(find.text('3 stops scheduled · Ready to start'), findsOneWidget);

      final button = tester.widget<ElevatedButton>(
        find.descendant(
          of: find.byKey(const Key('driver_view_assignment_button')),
          matching: find.byType(ElevatedButton),
        ),
      );
      expect(button.onPressed, isNotNull);

      await tester.tap(find.byKey(const Key('driver_view_assignment_button')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverAssignmentScreen), findsOneWidget);
    });

    testWidgets('State C (InProgress) shows vehicle, status badge, and Continue Assignment CTA',
        (tester) async {
      final repo =
          FakeTestDriverRepository(currentAssignment: sampleInProgressAssignment);
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      expect(find.text('WP CAD-5678'), findsOneWidget);
      expect(find.byKey(const Key('driver_assignment_status_badge')),
          findsAtLeastNWidgets(1));
      expect(find.text('In Progress'), findsAtLeastNWidgets(1));
      expect(find.text('2 of 4 stops completed · In progress'), findsOneWidget);
      expect(find.text('Continue Assignment'), findsOneWidget);

      await tester.tap(find.byKey(const Key('driver_view_assignment_button')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverAssignmentScreen), findsOneWidget);
    });
  });

  group('Driver Today\'s Work Stop Calculation Tests', () {
    testWidgets('displays active stop counts and progress bar for InProgress assignment',
        (tester) async {
      final repo =
          FakeTestDriverRepository(currentAssignment: sampleInProgressAssignment);
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_todays_work_active_card')), findsOneWidget);
      expect(find.byKey(const Key('driver_todays_work_stop_count')), findsOneWidget);
      expect(find.text('4 Stops'), findsOneWidget);

      expect(find.byKey(const Key('driver_todays_work_breakdown')), findsOneWidget);
      expect(find.text('2 Completed · 1 Failed · 1 Pending'), findsOneWidget);
      expect(find.byKey(const Key('driver_todays_work_progress_bar')), findsOneWidget);
    });

    testWidgets('safely accounts for Skipped stops in Today\'s Work summary',
        (tester) async {
      final assignmentWithSkipped = AssignmentDetailModel(
        id: 'asn-skipped-1',
        status: CollectionAssignmentStatus.inProgress,
        driverId: 'driver-789',
        driverName: 'Samantha Perera',
        vehicleId: 'veh-1',
        vehicleRegistrationNumber: 'WP CAD-5678',
        stopCount: 5,
        completedStopCount: 2,
        failedStopCount: 1,
        assignedAt: DateTime.utc(2026, 9, 26, 8, 30),
        route: RouteReadModel(
          id: 'route-1',
          collectionAssignmentId: 'asn-skipped-1',
          routingMethod: 'ManualOrder',
          stops: [
            RouteStopModel(
              id: 'stop-1',
              sequence: 1,
              status: RouteStopStatus.completed,
              task: _createSampleTask('task-1', 'TSK-101'),
            ),
            RouteStopModel(
              id: 'stop-2',
              sequence: 2,
              status: RouteStopStatus.completed,
              task: _createSampleTask('task-2', 'TSK-102'),
            ),
            RouteStopModel(
              id: 'stop-3',
              sequence: 3,
              status: RouteStopStatus.failed,
              task: _createSampleTask('task-3', 'TSK-103'),
            ),
            RouteStopModel(
              id: 'stop-4',
              sequence: 4,
              status: RouteStopStatus.skipped,
              task: _createSampleTask('task-4', 'TSK-104'),
            ),
            RouteStopModel(
              id: 'stop-5',
              sequence: 5,
              status: RouteStopStatus.pending,
              task: _createSampleTask('task-5', 'TSK-105'),
            ),
          ],
        ),
      );

      final repo =
          FakeTestDriverRepository(currentAssignment: assignmentWithSkipped);
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      expect(find.text('5 Stops'), findsOneWidget);
      expect(
          find.text('2 Completed · 1 Failed · 1 Pending · 1 Skipped'), findsOneWidget);
    });
  });

  group('Driver Navigation & Flow Tests', () {
    testWidgets('tapping View Assignment button navigates to /driver/assignment placeholder',
        (tester) async {
      final repo =
          FakeTestDriverRepository(currentAssignment: sampleAssignedAssignment);
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      final viewAssignmentBtn = find.byKey(const Key('driver_view_assignment_button'));
      await tester.tap(viewAssignmentBtn);
      await tester.pumpAndSettle();

      // Should be on Assignment details screen
      expect(find.byType(DriverAssignmentScreen), findsOneWidget);
      expect(find.text('My Assignment'), findsAtLeastNWidgets(1));

      // Back to dashboard
      final backButton = find.byKey(const Key('driver_assignment_appbar_back_button'));
      await tester.tap(backButton);
      await tester.pumpAndSettle();

      expect(find.byType(DriverDashboardScreen), findsOneWidget);
    });

    testWidgets('tapping Quick Action cards navigates to driver destinations',
        (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      // 1. Collection Tasks
      final tasksCard = find.byKey(const Key('driver_quick_action_tasks'));
      await tester.ensureVisible(tasksCard);
      await tester.tap(tasksCard);
      await tester.pumpAndSettle();
      expect(find.byType(DriverTasksScreen), findsOneWidget);

      // Back via bottom nav home
      await tester.tap(find.byKey(const Key('driver_bottom_nav_home')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverDashboardScreen), findsOneWidget);

      // 2. Route
      final routeCard = find.byKey(const Key('driver_quick_action_route'));
      await tester.ensureVisible(routeCard);
      await tester.tap(routeCard);
      await tester.pumpAndSettle();
      expect(find.byType(DriverRouteScreen), findsOneWidget);

      // Back via bottom nav home
      await tester.tap(find.byKey(const Key('driver_bottom_nav_home')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverDashboardScreen), findsOneWidget);

      // 3. Report Incident
      final incidentsCard = find.byKey(const Key('driver_quick_action_incidents'));
      await tester.ensureVisible(incidentsCard);
      await tester.tap(incidentsCard);
      await tester.pumpAndSettle();
      expect(find.byType(DriverOperationalIssuesScreen), findsOneWidget);
      expect(find.text('Operational Issues'), findsAtLeastNWidgets(1));

      // Back via back button in AppBar
      final backButton = find.byTooltip('Back');
      await tester.tap(backButton);
      await tester.pumpAndSettle();
      expect(find.byType(DriverDashboardScreen), findsOneWidget);

      // 4. Profile
      final profileCard = find.byKey(const Key('driver_quick_action_profile'));
      await tester.ensureVisible(profileCard);
      await tester.tap(profileCard);
      await tester.pumpAndSettle();
      expect(find.byType(DriverProfileScreen), findsOneWidget);

      // Back via the existing Driver dashboard route
      await tester.tap(find.byKey(const Key('driver_profile_back_button')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverDashboardScreen), findsOneWidget);
    });

    testWidgets('tapping bottom nav items switches tabs seamlessly', (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      // Tap Tasks
      await tester.tap(find.byKey(const Key('driver_bottom_nav_tasks')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverTasksScreen), findsOneWidget);

      // Tap Route
      await tester.tap(find.byKey(const Key('driver_bottom_nav_route')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverRouteScreen), findsOneWidget);

      // Tap Profile
      await tester.tap(find.byKey(const Key('driver_bottom_nav_profile')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverProfileScreen), findsOneWidget);

      // Tap Home
      await tester.tap(find.byKey(const Key('driver_bottom_nav_home')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverDashboardScreen), findsOneWidget);
    });

    testWidgets('system back button from non-dashboard tab navigates back to dashboard',
        (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      // Tap Tasks
      await tester.tap(find.byKey(const Key('driver_bottom_nav_tasks')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverTasksScreen), findsOneWidget);

      // Trigger system back button
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();

      // Should be back on Driver Dashboard
      expect(find.byType(DriverDashboardScreen), findsOneWidget);

      // Tap Route
      await tester.tap(find.byKey(const Key('driver_bottom_nav_route')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverRouteScreen), findsOneWidget);

      // Trigger system back button
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();

      // Should be back on Driver Dashboard
      expect(find.byType(DriverDashboardScreen), findsOneWidget);

      // Tap Profile
      await tester.tap(find.byKey(const Key('driver_bottom_nav_profile')));
      await tester.pumpAndSettle();
      expect(find.byType(DriverProfileScreen), findsOneWidget);

      // Trigger system back button
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();

      // Should be back on Driver Dashboard
      expect(find.byType(DriverDashboardScreen), findsOneWidget);
    });

    testWidgets('top profile icon opens the Driver Profile screen',
        (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('driver_notification_button')), findsNothing);
      await tester.tap(find.byKey(const Key('driver_account_button')));
      await tester.pumpAndSettle();

      expect(find.byType(DriverProfileScreen), findsOneWidget);
    });
  });

  group('Driver Pull-to-Refresh Tests', () {
    testWidgets('pulling down refresh indicator triggers provider refresh',
        (tester) async {
      final repo = FakeTestDriverRepository();
      await tester.pumpWidget(createDriverTestApp(repositoryOverride: repo));
      await tester.pumpAndSettle();

      final initialSelfCalls = repo.getDriverSelfCallCount;
      final initialAssignmentCalls = repo.getCurrentAssignmentCallCount;

      // Trigger pull-to-refresh
      await tester.fling(
        find.byKey(const Key('driver_greeting_text')),
        const Offset(0.0, 300.0),
        1000.0,
      );
      await tester.pumpAndSettle();

      expect(repo.getDriverSelfCallCount, greaterThan(initialSelfCalls));
      expect(repo.getCurrentAssignmentCallCount, greaterThan(initialAssignmentCalls));
    });
  });

  group('Driver Profile Actions Tests', () {
    testWidgets('shows driver identity and only the working profile actions', (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('driver_account_button')));
      await tester.pumpAndSettle();

      expect(find.byType(DriverProfileScreen), findsOneWidget);
      expect(find.text('Samantha Perera'), findsOneWidget);
      expect(find.text('samantha.p@smartwaste.lk'), findsOneWidget);
      expect(find.text('Driver'), findsAtLeastNWidgets(1));
      expect(find.byKey(const Key('driver_profile_change_password_button')), findsOneWidget);
      expect(find.byKey(const Key('driver_profile_back_button')), findsOneWidget);
      expect(find.byKey(const Key('driver_profile_logout_button')), findsOneWidget);
      expect(find.textContaining('scheduled for implementation'), findsNothing);
      expect(find.textContaining('coming soon'), findsNothing);
      expect(find.textContaining('future update'), findsNothing);
    });

    testWidgets('Change Password opens the existing screen',
        (tester) async {
      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('driver_account_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('driver_profile_change_password_button')));
      await tester.pumpAndSettle();

      expect(find.byType(ChangePasswordScreen), findsOneWidget);
    });

    testWidgets('Logout invokes the existing notifier and navigates to Login',
        (tester) async {
      final mockNotifier =
          MockDriverAuthNotifier(const AuthState.authenticated(driverUser));

      await tester.pumpWidget(createDriverTestApp(
        notifierOverride: () => mockNotifier,
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('driver_account_button')));
      await tester.pumpAndSettle();

      final logoutButton = find.byKey(const Key('driver_profile_logout_button'));
      await tester.ensureVisible(logoutButton);
      await tester.tap(logoutButton);
      await tester.pumpAndSettle();

      expect(mockNotifier.logoutCalled, isTrue);
      expect(find.byType(LoginScreen), findsOneWidget);
    });
  });

  group('Driver Responsive Viewport Tests', () {
    testWidgets('secondary screens align content below the shared header', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();
      final headerBottom = tester.getBottomLeft(find.byType(AppBar)).dy;

      await tester.tap(find.byKey(const Key('driver_bottom_nav_tasks')));
      await tester.pumpAndSettle();
      expect(find.text('Tasks'), findsWidgets);
      expect(find.byKey(const Key('driver_account_button')), findsOneWidget);
      final tasksGap = tester.getTopLeft(find.byKey(const Key('driver_tasks_empty_state'))).dy - headerBottom;

      await tester.tap(find.byKey(const Key('driver_bottom_nav_route')));
      await tester.pumpAndSettle();
      expect(find.text('Route'), findsWidgets);
      final routeGap = tester.getTopLeft(find.byKey(const Key('driver_route_empty_state'))).dy - headerBottom;

      await tester.tap(find.byKey(const Key('driver_bottom_nav_profile')));
      await tester.pumpAndSettle();
      final profileCard = find.descendant(of: find.byType(DriverProfileScreen), matching: find.byType(AppCard));
      final profileGap = tester.getTopLeft(profileCard).dy - headerBottom;
      expect(tasksGap, inInclusiveRange(16, 48));
      expect(routeGap, inInclusiveRange(16, 48));
      expect(profileGap, inInclusiveRange(16, 48));
      expect(find.byKey(const Key('driver_bottom_nav')), findsOneWidget);
      expect(find.byKey(const Key('driver_profile_change_password_button')), findsOneWidget);
      expect(find.byKey(const Key('driver_profile_back_button')), findsOneWidget);
      expect(find.byKey(const Key('driver_profile_logout_button')), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('profile actions remain usable on a narrow 320px viewport',
        (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('driver_account_button')));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(DriverProfileScreen), findsOneWidget);
      expect(find.byKey(const Key('driver_profile_change_password_button')), findsOneWidget);
      expect(find.byKey(const Key('driver_profile_back_button')), findsOneWidget);
      expect(find.byKey(const Key('driver_profile_logout_button')), findsOneWidget);
    });

    testWidgets('renders cleanly without overflow on narrow 320px viewport',
        (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(DriverDashboardScreen), findsOneWidget);
      expect(find.byKey(const Key('driver_view_assignment_button')), findsOneWidget);
      expect(find.text('Current Assignment'), findsOneWidget);
    });

    testWidgets('renders cleanly without overflow on standard 390px viewport',
        (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(DriverDashboardScreen), findsOneWidget);
      expect(find.byKey(const Key('driver_view_assignment_button')), findsOneWidget);
    });

    testWidgets('renders cleanly without overflow on large 430px viewport',
        (tester) async {
      tester.view.physicalSize = const Size(430 * 3.0, 932 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createDriverTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(DriverDashboardScreen), findsOneWidget);
      expect(find.byKey(const Key('driver_view_assignment_button')), findsOneWidget);
    });
  });
}
