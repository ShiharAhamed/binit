import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile/core/routing/app_router.dart';
import 'package:mobile/features/bins/data/public_waste_bins_repository.dart';
import 'package:mobile/features/bins/models/paged_public_waste_bins_model.dart';
import 'package:mobile/features/bins/models/public_bin_availability.dart';
import 'package:mobile/features/bins/models/public_waste_bin_detail_model.dart';
import 'package:mobile/features/bins/models/public_waste_bin_query.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/presentation/change_password_screen.dart';
import 'package:mobile/features/auth/presentation/home_screen.dart';
import 'package:mobile/features/auth/presentation/login_screen.dart';
import 'package:mobile/features/auth/presentation/register_screen.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/citizen/presentation/citizen_dashboard_screen.dart';
import 'package:mobile/features/driver/presentation/driver_dashboard_screen.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/paged_waste_reports_model.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_report_status_history_model.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/presentation/edit_report_screen.dart';
import 'package:mobile/features/reporting/presentation/manage_report_photos_screen.dart';
import 'package:mobile/features/driver/data/driver_repository.dart';
import 'package:mobile/features/driver/models/assignment_detail_model.dart';
import 'package:mobile/features/driver/models/collection_assignment_status.dart';
import 'package:mobile/features/driver/models/driver_availability_status.dart';
import 'package:mobile/features/driver/models/driver_self_model.dart';
import 'package:mobile/features/driver/models/paged_assignments_model.dart';
import 'package:mobile/features/reporting/presentation/my_reports_screen.dart';
import 'package:mobile/features/reporting/presentation/report_detail_screen.dart';

// Fake Notifier to directly test routing guards without background network calls
class TestAuthNotifier extends AuthNotifier {
  final AuthState initialState;

  TestAuthNotifier(this.initialState);

  @override
  AuthState build() => initialState;
}

class TestDriverRepository extends DriverRepository {
  @override
  Future<DriverSelfModel> getDriverSelf(String driverId) async {
    return DriverSelfModel(
      id: driverId,
      displayName: 'Authenticated Driver',
      availabilityStatus: DriverAvailabilityStatus.available,
      isOccupied: false,
    );
  }

  @override
  Future<AssignmentDetailModel?> getCurrentAssignmentDetail() async {
    return null;
  }

  @override
  Future<PagedAssignmentsModel> getMyAssignments({
    CollectionAssignmentStatus? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    return PagedAssignmentsModel(
      items: const [],
      page: page,
      pageSize: pageSize,
      totalCount: 0,
      totalPages: 1,
    );
  }
}

class TestReportingRepository extends ReportingRepository {
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
    return const PagedWasteReportsModel(
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    );
  }

  @override
  Future<WasteReportDetailModel> getWasteReport(String reportId) async {
    return WasteReportDetailModel(
      id: reportId,
      citizenId: 'test-citizen-id',
      citizenName: 'Authenticated Citizen',
      description: 'Test report',
      wasteType: WasteType.general,
      latitude: 6.9,
      longitude: 79.8,
      status: WasteReportStatus.submitted,
      createdAt: DateTime.utc(2026, 9, 16),
    );
  }

  @override
  Future<List<WasteReportStatusHistoryModel>> getWasteReportHistory(
    String reportId,
  ) async {
    return [];
  }
}

class TestPublicWasteBinsRepository extends PublicWasteBinsRepository {
  @override
  Future<PagedPublicWasteBinsModel> getPublicWasteBins(
    PublicWasteBinQuery query,
  ) async {
    return PagedPublicWasteBinsModel(
      items: const [],
      page: query.page,
      pageSize: query.pageSize,
      totalCount: 0,
      totalPages: 1,
    );
  }

  @override
  Future<PublicWasteBinDetailModel> getPublicWasteBin(String binId) async {
    return PublicWasteBinDetailModel(
      id: binId,
      binCode: 'BIN-ROUTE',
      latitude: 6.9271,
      longitude: 79.8612,
      capacityLiters: 660,
      acceptedWasteTypes: const [WasteType.general],
      publicAvailability: PublicBinAvailability.unknown,
      isCollectionScheduled: false,
    );
  }
}

void main() {
  group('AppRouter Guard Tests', () {
    const testCitizenUser = AuthUser(
      id: 'test-citizen-id',
      fullName: 'Authenticated Citizen',
      email: 'citizen@smartwaste.local',
      role: AppRoles.citizen,
    );

    const testDriverUser = AuthUser(
      id: 'test-driver-id',
      fullName: 'Authenticated Driver',
      email: 'driver@smartwaste.local',
      role: AppRoles.driver,
    );

    testWidgets(
      'unauthenticated user is redirected to LoginScreen when navigating to /home',
      (tester) async {
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(const AuthState.unauthenticated()),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                final router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();

        // Should redirect to login screen
        expect(find.byType(LoginScreen), findsOneWidget);
        expect(find.byType(HomeScreen), findsNothing);
      },
    );

    testWidgets(
      'authenticated Citizen is redirected to CitizenDashboardScreen',
      (tester) async {
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testCitizenUser),
                ),
              ),
              reportingRepositoryProvider.overrideWithValue(
                TestReportingRepository(),
              ),
              publicWasteBinsRepositoryProvider.overrideWithValue(
                TestPublicWasteBinsRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                final router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();

        // Should redirect to Citizen Dashboard
        expect(find.text('Hello, Authenticated'), findsOneWidget);
        expect(find.text('Report Waste'), findsOneWidget);
        expect(find.byType(HomeScreen), findsNothing);
        expect(find.byType(LoginScreen), findsNothing);
      },
    );

    testWidgets(
      'authenticated Citizen navigating to /login is redirected to /citizen/dashboard',
      (tester) async {
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testCitizenUser),
                ),
              ),
              reportingRepositoryProvider.overrideWithValue(
                TestReportingRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                final router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();

        expect(find.text('Hello, Authenticated'), findsOneWidget);
        expect(find.byType(LoginScreen), findsNothing);
      },
    );

    testWidgets(
      'authenticated Driver is redirected to DriverDashboardScreen and /login redirects to /driver/dashboard',
      (tester) async {
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testDriverUser),
                ),
              ),
              driverRepositoryProvider.overrideWithValue(
                TestDriverRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                final router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();

        // Driver goes to DriverDashboardScreen
        expect(find.byType(DriverDashboardScreen), findsOneWidget);
        expect(find.text('Hello, Authenticated'), findsOneWidget);
        expect(find.text('Current Assignment'), findsOneWidget);
        expect(find.byType(HomeScreen), findsNothing);
        expect(find.byType(LoginScreen), findsNothing);
      },
    );

    testWidgets(
      'authenticated Driver attempting /citizen/dashboard is blocked and redirected to /driver/dashboard',
      (tester) async {
        late GoRouter router;
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testDriverUser),
                ),
              ),
              driverRepositoryProvider.overrideWithValue(
                TestDriverRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();
        expect(find.byType(DriverDashboardScreen), findsOneWidget);

        // Attempt to navigate to citizen dashboard
        router.go('/citizen/dashboard');
        await tester.pumpAndSettle();

        // Should be redirected back to /driver/dashboard
        expect(find.byType(DriverDashboardScreen), findsOneWidget);
        expect(find.text('Report Waste'), findsNothing);

        router.go('/citizen/nearby-bins/bin-route');
        await tester.pumpAndSettle();
        expect(find.byType(DriverDashboardScreen), findsOneWidget);
      },
    );

    testWidgets('authenticated Citizen can open the public bin-detail route', (
      tester,
    ) async {
      late GoRouter router;
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(
              () => TestAuthNotifier(
                const AuthState.authenticated(testCitizenUser),
              ),
            ),
            publicWasteBinsRepositoryProvider.overrideWithValue(
              TestPublicWasteBinsRepository(),
            ),
            reportingRepositoryProvider.overrideWithValue(
              TestReportingRepository(),
            ),
          ],
          child: Consumer(
            builder: (context, ref, _) {
              router = ref.watch(appRouterProvider);
              return MaterialApp.router(routerConfig: router);
            },
          ),
        ),
      );
      await tester.pumpAndSettle();

      router.go('/citizen/nearby-bins/bin-route');
      await tester.pumpAndSettle();
      expect(find.text('Bin Details'), findsOneWidget);
      expect(find.text('BIN-ROUTE'), findsOneWidget);
    });

    testWidgets(
      'authenticated Citizen attempting /driver/dashboard is blocked and redirected to /citizen/dashboard',
      (tester) async {
        late GoRouter router;
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testCitizenUser),
                ),
              ),
              reportingRepositoryProvider.overrideWithValue(
                TestReportingRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();
        expect(find.byType(CitizenDashboardScreen), findsOneWidget);

        // Attempt to navigate to driver dashboard
        router.go('/driver/dashboard');
        await tester.pumpAndSettle();

        // Should be redirected back to /citizen/dashboard
        expect(find.byType(CitizenDashboardScreen), findsOneWidget);
        expect(find.text('Current Assignment'), findsNothing);
      },
    );

    testWidgets('unauthenticated user can access RegisterScreen', (
      tester,
    ) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(
              () => TestAuthNotifier(const AuthState.unauthenticated()),
            ),
          ],
          child: Consumer(
            builder: (context, ref, _) {
              final router = ref.watch(appRouterProvider);
              return MaterialApp.router(routerConfig: router);
            },
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.byType(LoginScreen), findsOneWidget);

      // Tap Register link
      final registerLink = find.text('Create an account');
      await tester.tap(registerLink);
      await tester.pumpAndSettle();

      expect(find.byType(RegisterScreen), findsOneWidget);
      expect(find.byType(LoginScreen), findsNothing);
    });

    testWidgets(
      'authenticated user with mustChangePassword = true is forced to ChangePasswordScreen',
      (tester) async {
        const mustChangeUser = AuthUser(
          id: 'test-user-id',
          fullName: 'New Driver',
          email: 'driver@smartwaste.local',
          role: AppRoles.driver,
          mustChangePassword: true,
        );

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(mustChangeUser),
                ),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                final router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();

        expect(find.byType(ChangePasswordScreen), findsOneWidget);
        expect(find.text('Change Temporary Password'), findsOneWidget);
        expect(
          find.text(
            'You must change your temporary password before continuing.',
          ),
          findsOneWidget,
        );
        expect(find.byType(HomeScreen), findsNothing);
      },
    );

    testWidgets('authenticated Citizen can navigate to /citizen/reports', (
      tester,
    ) async {
      late GoRouter router;
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(
              () => TestAuthNotifier(
                const AuthState.authenticated(testCitizenUser),
              ),
            ),
            reportingRepositoryProvider.overrideWithValue(
              TestReportingRepository(),
            ),
          ],
          child: Consumer(
            builder: (context, ref, _) {
              router = ref.watch(appRouterProvider);
              return MaterialApp.router(routerConfig: router);
            },
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);

      router.go('/citizen/reports');
      await tester.pumpAndSettle();

      expect(find.byType(MyReportsScreen), findsOneWidget);
    });

    testWidgets(
      'authenticated Driver attempting /citizen/reports is blocked and redirected to /driver/dashboard',
      (tester) async {
        late GoRouter router;
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testDriverUser),
                ),
              ),
              driverRepositoryProvider.overrideWithValue(
                TestDriverRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();
        expect(find.byType(DriverDashboardScreen), findsOneWidget);

        router.go('/citizen/reports');
        await tester.pumpAndSettle();

        expect(find.byType(DriverDashboardScreen), findsOneWidget);
        expect(find.byType(MyReportsScreen), findsNothing);
      },
    );

    testWidgets('authenticated Citizen can navigate to /citizen/reports/:id', (
      tester,
    ) async {
      late GoRouter router;
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authProvider.overrideWith(
              () => TestAuthNotifier(
                const AuthState.authenticated(testCitizenUser),
              ),
            ),
            reportingRepositoryProvider.overrideWithValue(
              TestReportingRepository(),
            ),
          ],
          child: Consumer(
            builder: (context, ref, _) {
              router = ref.watch(appRouterProvider);
              return MaterialApp.router(routerConfig: router);
            },
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.byType(CitizenDashboardScreen), findsOneWidget);

      router.go('/citizen/reports/rep-123');
      await tester.pumpAndSettle();

      expect(find.byType(ReportDetailScreen), findsOneWidget);
    });

    testWidgets(
      'authenticated Driver attempting /citizen/reports/:id is blocked and redirected to /driver/dashboard',
      (tester) async {
        late GoRouter router;
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testDriverUser),
                ),
              ),
              driverRepositoryProvider.overrideWithValue(
                TestDriverRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();
        expect(find.byType(DriverDashboardScreen), findsOneWidget);

        router.go('/citizen/reports/rep-123');
        await tester.pumpAndSettle();

        expect(find.byType(DriverDashboardScreen), findsOneWidget);
        expect(find.byType(ReportDetailScreen), findsNothing);
      },
    );

    testWidgets(
      'authenticated Citizen can navigate to /citizen/reports/:id/edit',
      (tester) async {
        late GoRouter router;
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testCitizenUser),
                ),
              ),
              reportingRepositoryProvider.overrideWithValue(
                TestReportingRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();
        expect(find.byType(CitizenDashboardScreen), findsOneWidget);

        router.go('/citizen/reports/rep-123/edit');
        await tester.pumpAndSettle();

        expect(find.byType(EditReportScreen), findsOneWidget);
      },
    );

    testWidgets(
      'authenticated Driver attempting /citizen/reports/:id/edit is blocked and redirected to /driver/dashboard',
      (tester) async {
        late GoRouter router;
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testDriverUser),
                ),
              ),
              driverRepositoryProvider.overrideWithValue(
                TestDriverRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();
        expect(find.byType(DriverDashboardScreen), findsOneWidget);

        router.go('/citizen/reports/rep-123/edit');
        await tester.pumpAndSettle();

        expect(find.byType(DriverDashboardScreen), findsOneWidget);
        expect(find.byType(EditReportScreen), findsNothing);
      },
    );

    testWidgets(
      'authenticated Citizen can navigate to /citizen/reports/:id/photos',
      (tester) async {
        late GoRouter router;
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testCitizenUser),
                ),
              ),
              reportingRepositoryProvider.overrideWithValue(
                TestReportingRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();
        expect(find.byType(CitizenDashboardScreen), findsOneWidget);

        router.go('/citizen/reports/rep-123/photos');
        await tester.pumpAndSettle();

        expect(find.byType(ManageReportPhotosScreen), findsOneWidget);
      },
    );

    testWidgets(
      'authenticated Driver attempting /citizen/reports/:id/photos is blocked and redirected to /driver/dashboard',
      (tester) async {
        late GoRouter router;
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              authProvider.overrideWith(
                () => TestAuthNotifier(
                  const AuthState.authenticated(testDriverUser),
                ),
              ),
              driverRepositoryProvider.overrideWithValue(
                TestDriverRepository(),
              ),
            ],
            child: Consumer(
              builder: (context, ref, _) {
                router = ref.watch(appRouterProvider);
                return MaterialApp.router(routerConfig: router);
              },
            ),
          ),
        );

        await tester.pumpAndSettle();
        expect(find.byType(DriverDashboardScreen), findsOneWidget);

        router.go('/citizen/reports/rep-123/photos');
        await tester.pumpAndSettle();

        expect(find.byType(DriverDashboardScreen), findsOneWidget);
        expect(find.byType(ManageReportPhotosScreen), findsNothing);
      },
    );
  });
}
