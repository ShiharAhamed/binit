import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/paged_waste_reports_model.dart';
import 'package:mobile/features/reporting/models/report_attachment_model.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_list_item_model.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_report_status_history_model.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/presentation/my_reports_screen.dart';
import 'package:mobile/features/reporting/presentation/report_detail_screen.dart';

/// Fake repository coordinating state mutations across the end-to-end Citizen lifecycle.
class LifecycleFakeReportingRepository extends ReportingRepository {
  WasteReportDetailModel report;
  List<WasteReportStatusHistoryModel> history;
  int cancelCallCount = 0;

  LifecycleFakeReportingRepository({
    required this.report,
    required this.history,
  });

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
    // If filtering by status, check if current report matches
    final matchesStatus = status == null || report.status == status;
    final items = matchesStatus
        ? [
            WasteReportListItemModel(
              id: report.id,
              description: report.description,
              wasteType: report.wasteType,
              status: report.status,
              priority: report.priority,
              addressText: report.addressText,
              latitude: report.latitude,
              longitude: report.longitude,
              createdAt: report.createdAt,
              attachmentCount: report.attachments.length,
            ),
          ]
        : <WasteReportListItemModel>[];

    return PagedWasteReportsModel(
      items: items,
      page: 1,
      pageSize: pageSize,
      totalCount: items.length,
      totalPages: 1,
    );
  }

  @override
  Future<WasteReportDetailModel> getWasteReport(String reportId) async {
    return report;
  }

  @override
  Future<List<WasteReportStatusHistoryModel>> getWasteReportHistory(String reportId) async {
    return history;
  }

  @override
  Future<void> cancelWasteReport(String reportId) async {
    cancelCallCount++;
    final now = DateTime.utc(2026, 9, 17, 10, 0, 0);
    report = WasteReportDetailModel(
      id: report.id,
      citizenId: report.citizenId,
      citizenName: report.citizenName,
      description: report.description,
      wasteType: report.wasteType,
      latitude: report.latitude,
      longitude: report.longitude,
      addressText: report.addressText,
      status: WasteReportStatus.cancelled,
      priority: report.priority,
      attachments: report.attachments,
      createdAt: report.createdAt,
      updatedAt: now,
    );

    history = [
      ...history,
      WasteReportStatusHistoryModel(
        id: 'hist-cancel',
        wasteReportId: reportId,
        fromStatus: WasteReportStatus.submitted,
        toStatus: WasteReportStatus.cancelled,
        changedByUserId: report.citizenId,
        changedByUserName: report.citizenName,
        notes: 'Cancelled by citizen',
        changedAt: now,
      ),
    ];
  }
}

Widget createLifecycleTestApp({required LifecycleFakeReportingRepository repository}) {
  final router = GoRouter(
    initialLocation: '/citizen/reports',
    routes: [
      GoRoute(
        path: '/citizen/reports',
        builder: (context, state) => MyReportsScreen(
          repository: repository,
        ),
      ),
      GoRoute(
        path: '/citizen/reports/:id',
        builder: (context, state) => ReportDetailScreen(
          reportId: state.pathParameters['id']!,
          repository: repository,
        ),
      ),
    ],
  );

  return MaterialApp.router(
    theme: AppTheme.lightTheme,
    routerConfig: router,
  );
}

void main() {
  group('Step 9A.9.5 Citizen Reporting End-to-End Lifecycle Integration Test', () {
    testWidgets(
      'Navigates from My Reports to Report Detail, verifies Submitted action buttons, cancels report, confirms mutation buttons disappear, and verifies My Reports reflects Cancelled badge on return',
      (tester) async {
        final initialReport = WasteReportDetailModel(
          id: 'rep-e2e-001',
          citizenId: 'cit-001',
          citizenName: 'Nimal Perera',
          description: 'Large waste accumulation blocking sidewalk.',
          wasteType: WasteType.general,
          latitude: 6.9271,
          longitude: 79.8612,
          addressText: '123 Galle Road, Colombo',
          status: WasteReportStatus.submitted,
          priority: null,
          attachments: [
            ReportAttachmentModel(
              id: 'att-1',
              wasteReportId: 'rep-e2e-001',
              fileUrl: 'https://example.com/photo1.jpg',
              fileType: 'image/jpeg',
              createdAt: DateTime.utc(2026, 9, 17, 8, 30, 0),
            ),
          ],
          createdAt: DateTime.utc(2026, 9, 17, 8, 30, 0),
        );

        final initialHistory = [
          WasteReportStatusHistoryModel(
            id: 'hist-initial',
            wasteReportId: 'rep-e2e-001',
            fromStatus: null,
            toStatus: WasteReportStatus.submitted,
            changedByUserId: 'cit-001',
            changedByUserName: 'Nimal Perera',
            notes: null,
            changedAt: DateTime.utc(2026, 9, 17, 8, 30, 0),
          ),
        ];

        final fakeRepo = LifecycleFakeReportingRepository(
          report: initialReport,
          history: initialHistory,
        );

        await tester.pumpWidget(createLifecycleTestApp(repository: fakeRepo));
        await tester.pumpAndSettle();

        // 1. My Reports renders with Submitted report card
        expect(find.byKey(const Key('report_card_rep-e2e-001')), findsOneWidget);
        expect(find.text('Submitted'), findsOneWidget);
        expect(find.text('Large waste accumulation blocking sidewalk.'), findsOneWidget);

        // 2. Tap report card to navigate to Report Detail
        await tester.tap(find.byKey(const Key('report_card_rep-e2e-001')));
        await tester.pumpAndSettle();

        // 3. Verify Report Detail Screen rendered with Submitted mutation controls
        expect(find.text('Report Details'), findsOneWidget);
        expect(find.byKey(const Key('edit_report_button')), findsOneWidget);
        expect(find.byKey(const Key('edit_report_appbar_button')), findsOneWidget);
        expect(find.byKey(const Key('manage_photos_button')), findsOneWidget);

        await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
        expect(find.byKey(const Key('cancel_report_button')), findsOneWidget);

        // 4. Tap Cancel Report button to trigger confirmation dialog
        await tester.tap(find.byKey(const Key('cancel_report_button')));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('cancel_report_dialog')), findsOneWidget);
        expect(find.text('Cancel this report?'), findsOneWidget);

        // 5. Confirm cancellation in dialog
        await tester.tap(find.byKey(const Key('confirm_cancel_report_button')));
        await tester.pumpAndSettle();

        expect(fakeRepo.cancelCallCount, 1);

        // 6. Verify cancellation results on Report Detail Screen
        expect(find.byKey(const Key('cancel_report_success_snackbar')), findsOneWidget);
        expect(find.text('Report cancelled.'), findsOneWidget);
        expect(find.text('Cancelled by citizen'), findsOneWidget);

        // All mutation actions must be strictly absent
        expect(find.byKey(const Key('edit_report_button')), findsNothing);
        expect(find.byKey(const Key('edit_report_appbar_button')), findsNothing);
        expect(find.byKey(const Key('manage_photos_button')), findsNothing);
        expect(find.byKey(const Key('cancel_report_button')), findsNothing);

        // 7. Navigate back to My Reports
        await tester.tap(find.byType(BackButton));
        await tester.pumpAndSettle();

        // 8. Verify My Reports re-fetched and displays the Cancelled status badge
        expect(find.byKey(const Key('report_card_rep-e2e-001')), findsOneWidget);
        expect(find.text('Cancelled'), findsWidgets);
      },
    );
  });
}
