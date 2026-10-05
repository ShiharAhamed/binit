import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/report_attachment_model.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_priority.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_report_status_history_model.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/presentation/report_detail_screen.dart';

class MockReportingRepository extends ReportingRepository {
  WasteReportDetailModel? reportToReturn;
  List<WasteReportStatusHistoryModel>? historyToReturn;

  bool shouldThrowReport = false;
  String reportErrorMessage = 'Network error loading report';

  bool shouldThrowHistory = false;
  String historyErrorMessage = 'Network error loading history';

  int getReportCallCount = 0;
  int getHistoryCallCount = 0;

  @override
  Future<WasteReportDetailModel> getWasteReport(String reportId) async {
    getReportCallCount++;
    if (shouldThrowReport) {
      throw ApiException(message: reportErrorMessage, statusCode: 500);
    }
    if (reportToReturn != null) {
      return reportToReturn!;
    }
    throw const ApiException(message: 'Report not found', statusCode: 404);
  }

  @override
  Future<List<WasteReportStatusHistoryModel>> getWasteReportHistory(String reportId) async {
    getHistoryCallCount++;
    if (shouldThrowHistory) {
      throw ApiException(message: historyErrorMessage, statusCode: 500);
    }
    return historyToReturn ?? [];
  }

  bool shouldThrowCancel = false;
  ApiException? cancelApiException;
  int cancelCallCount = 0;
  String? lastCancelledReportId;

  @override
  Future<void> cancelWasteReport(String reportId) async {
    cancelCallCount++;
    lastCancelledReportId = reportId;
    if (shouldThrowCancel) {
      throw cancelApiException ?? const ApiException(message: "Couldn't cancel report", statusCode: 500);
    }
  }
}

void main() {
  late MockReportingRepository mockRepo;

  setUp(() {
    mockRepo = MockReportingRepository();
  });

  WasteReportDetailModel createSampleReport({
    String id = 'rep-101',
    String? reportReference,
    String citizenId = 'cit-1',
    String citizenName = 'Kamal Perera',
    String description = 'Large pile of mixed waste blocking public pavement.',
    WasteType wasteType = WasteType.general,
    double latitude = 6.9271,
    double longitude = 79.8612,
    String? addressText = 'Galle Road, Colombo 03',
    WasteReportStatus status = WasteReportStatus.submitted,
    WasteReportPriority? priority = WasteReportPriority.high,
    List<ReportAttachmentModel> attachments = const [],
    DateTime? createdAt,
  }) {
    return WasteReportDetailModel(
      id: id,
      reportReference: reportReference,
      citizenId: citizenId,
      citizenName: citizenName,
      description: description,
      wasteType: wasteType,
      latitude: latitude,
      longitude: longitude,
      addressText: addressText,
      status: status,
      priority: priority,
      attachments: attachments,
      createdAt: createdAt ?? DateTime.utc(2026, 9, 16, 8, 30, 0),
    );
  }

  Widget createTestWidget({required String reportId}) {
    return MaterialApp(
      home: ReportDetailScreen(
        reportId: reportId,
        repository: mockRepo,
      ),
    );
  }

  group('ReportDetailScreen Tests', () {
    testWidgets('renders loading state initially while report details load', (tester) async {
      mockRepo.reportToReturn = createSampleReport();
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));

      // Before settling, loading indicator is visible
      expect(find.byKey(const Key('report_detail_loading')), findsOneWidget);
      expect(find.text('Loading report details...'), findsOneWidget);

      await tester.pumpAndSettle();

      expect(find.byKey(const Key('report_detail_loading')), findsNothing);
      expect(find.byKey(const Key('report_detail_summary_card')), findsOneWidget);
    });

    testWidgets('renders full report details, metadata, description, and coordinates', (tester) async {
      mockRepo.reportToReturn = createSampleReport(
        description: 'Large pile of construction and general waste blocking sidewalk.',
        wasteType: WasteType.bulky,
        status: WasteReportStatus.underReview,
        priority: WasteReportPriority.urgent,
        addressText: '42 Marine Drive, Colombo',
        latitude: 6.90123,
        longitude: 79.85432,
      );
      mockRepo.historyToReturn = [
        WasteReportStatusHistoryModel(
          id: 'hist-1',
          wasteReportId: 'rep-101',
          fromStatus: null,
          toStatus: WasteReportStatus.submitted,
          changedAt: DateTime.utc(2026, 9, 16, 8, 30, 0),
        ),
      ];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      // Summary Card
      expect(find.text('Bulky Waste'), findsOneWidget);
      expect(find.text('Under Review'), findsOneWidget);
      expect(find.text('Priority: Urgent'), findsOneWidget);
      expect(find.textContaining('Submitted on 16 Sep 2026'), findsOneWidget);

      // Description Card
      expect(find.text('Large pile of construction and general waste blocking sidewalk.'), findsOneWidget);

      // Location Card
      expect(find.text('42 Marine Drive, Colombo'), findsOneWidget);
      expect(find.textContaining('6.90123, 79.85432'), findsOneWidget);
    });

    testWidgets('renders zero-photo empty state when no attachments exist', (tester) async {
      mockRepo.reportToReturn = createSampleReport(attachments: const []);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('report_no_photos')), findsOneWidget);
      expect(find.text('No photo evidence attached.'), findsOneWidget);
      expect(find.text('0 / 3'), findsOneWidget);
    });

    testWidgets('renders photo thumbnails when attachments exist and opens modal on tap', (tester) async {
      final attachments = [
        ReportAttachmentModel(
          id: 'att-1',
          wasteReportId: 'rep-101',
          fileUrl: 'https://storage.example.com/photo1.jpg',
          fileType: 'image/jpeg',
          createdAt: DateTime.utc(2026, 9, 16, 8, 31, 0),
        ),
        ReportAttachmentModel(
          id: 'att-2',
          wasteReportId: 'rep-101',
          fileUrl: 'https://storage.example.com/photo2.jpg',
          fileType: 'image/png',
          createdAt: DateTime.utc(2026, 9, 16, 8, 32, 0),
        ),
      ];

      mockRepo.reportToReturn = createSampleReport(attachments: attachments);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.text('2 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_thumbnail_att-1')), findsOneWidget);
      expect(find.byKey(const Key('photo_thumbnail_att-2')), findsOneWidget);

      // Tap thumbnail opens photo preview dialog
      await tester.ensureVisible(find.byKey(const Key('photo_thumbnail_att-1')));
      await tester.tap(find.byKey(const Key('photo_thumbnail_att-1')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('photo_preview_dialog')), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_close_button')), findsOneWidget);

      // Tap close button dismisses dialog
      await tester.tap(find.byKey(const Key('photo_preview_close_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('photo_preview_dialog')), findsNothing);
    });

    testWidgets('status history timeline formats initial Submitted and transitions properly with notes', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.rejected);
      mockRepo.historyToReturn = [
        WasteReportStatusHistoryModel(
          id: 'h-1',
          wasteReportId: 'rep-101',
          fromStatus: null,
          toStatus: WasteReportStatus.submitted,
          changedAt: DateTime.utc(2026, 9, 16, 8, 0, 0),
        ),
        WasteReportStatusHistoryModel(
          id: 'h-2',
          wasteReportId: 'rep-101',
          fromStatus: WasteReportStatus.submitted,
          toStatus: WasteReportStatus.underReview,
          changedAt: DateTime.utc(2026, 9, 16, 8, 30, 0),
        ),
        WasteReportStatusHistoryModel(
          id: 'h-3',
          wasteReportId: 'rep-101',
          fromStatus: WasteReportStatus.underReview,
          toStatus: WasteReportStatus.rejected,
          notes: 'Location falls under private commercial property boundary.',
          changedAt: DateTime.utc(2026, 9, 16, 9, 0, 0),
        ),
      ];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      // Initial transition formatted cleanly as 'Submitted', not 'null -> Submitted'
      expect(find.text('Submitted'), findsWidgets);
      expect(find.text('Submitted → Under Review'), findsOneWidget);
      expect(find.text('Under Review → Rejected'), findsOneWidget);

      // Rejection notes callout
      expect(find.byKey(const Key('history_notes_h-3')), findsOneWidget);
      expect(find.text('Location falls under private commercial property boundary.'), findsOneWidget);
    });

    testWidgets('resilient partial loading: report succeeds, history fails, shows inline retry', (tester) async {
      mockRepo.reportToReturn = createSampleReport();
      mockRepo.shouldThrowHistory = true;
      mockRepo.historyErrorMessage = 'Unable to reach history service';

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      // Report detail cards are displayed
      expect(find.byKey(const Key('report_detail_summary_card')), findsOneWidget);
      expect(find.byKey(const Key('report_detail_description_card')), findsOneWidget);

      // History error card is displayed inline
      expect(find.byKey(const Key('history_error_card')), findsOneWidget);
      expect(find.text("Couldn't load status history."), findsOneWidget);
      expect(find.byKey(const Key('history_retry_button')), findsOneWidget);

      // Tap retry after history service recovers
      mockRepo.shouldThrowHistory = false;
      mockRepo.historyToReturn = [
        WasteReportStatusHistoryModel(
          id: 'h-rec',
          wasteReportId: 'rep-101',
          fromStatus: null,
          toStatus: WasteReportStatus.submitted,
          changedAt: DateTime.utc(2026, 9, 16, 8, 0, 0),
        ),
      ];

      final retryFinder = find.byKey(const Key('history_retry_button'));
      await tester.ensureVisible(retryFinder);
      await tester.tap(retryFinder);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('history_error_card')), findsNothing);
      expect(find.text('Submitted'), findsWidgets);
    });

    testWidgets('full screen error state when report details fail, with recovery on retry', (tester) async {
      mockRepo.shouldThrowReport = true;
      mockRepo.reportErrorMessage = 'Server internal error';

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.text('Unable to Load Report'), findsOneWidget);
      expect(find.text('Server internal error'), findsOneWidget);
      expect(find.byKey(const Key('report_detail_retry_button')), findsOneWidget);

      // Recover and tap retry
      mockRepo.shouldThrowReport = false;
      mockRepo.reportToReturn = createSampleReport();
      mockRepo.historyToReturn = [];

      await tester.tap(find.byKey(const Key('report_detail_retry_button')));
      await tester.pumpAndSettle();

      expect(find.text('Unable to Load Report'), findsNothing);
      expect(find.byKey(const Key('report_detail_summary_card')), findsOneWidget);
    });

    testWidgets('pull-to-refresh triggers reloading of both report and history', (tester) async {
      mockRepo.reportToReturn = createSampleReport();
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(mockRepo.getReportCallCount, 1);
      expect(mockRepo.getHistoryCallCount, 1);

      // Pull to refresh
      await tester.fling(find.byType(SingleChildScrollView), const Offset(0, 300), 1000);
      await tester.pumpAndSettle();

      expect(mockRepo.getReportCallCount, 2);
      expect(mockRepo.getHistoryCallCount, 2);
    });

    for (final nextStatus in [WasteReportStatus.underReview, WasteReportStatus.verified, WasteReportStatus.rejected]) {
      testWidgets('pull-to-refresh shows ${nextStatus.displayName} from WasteReport without workflow state', (tester) async {
        const id = 'c17add4f-79f3-4a0a-a9e0-150fde7d7827';
        mockRepo.reportToReturn = createSampleReport(id: id, reportReference: 'C17ADD4F', status: nextStatus == WasteReportStatus.underReview ? WasteReportStatus.submitted : WasteReportStatus.underReview);
        mockRepo.historyToReturn = [];
        await tester.pumpWidget(createTestWidget(reportId: id));
        await tester.pumpAndSettle();
        expect(find.text('Report C17ADD4F'), findsOneWidget);

        mockRepo.reportToReturn = createSampleReport(id: id, reportReference: 'C17ADD4F', status: nextStatus);
        await tester.fling(find.byType(SingleChildScrollView), const Offset(0, 300), 1000);
        await tester.pumpAndSettle();

        expect(find.descendant(of: find.byKey(const Key('report_detail_summary_card')), matching: find.text(nextStatus.displayName)), findsOneWidget);
        expect(mockRepo.getReportCallCount, 2);
      });
    }

    testWidgets('failed refresh keeps the last authoritative status and offers retry', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.underReview);
      mockRepo.historyToReturn = [];
      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      mockRepo.shouldThrowReport = true;
      await tester.fling(find.byType(SingleChildScrollView), const Offset(0, 300), 1000);
      await tester.pumpAndSettle();
      expect(find.text('Under Review'), findsOneWidget);
      expect(find.text("Couldn't refresh this report. Showing the last loaded status."), findsOneWidget);

      mockRepo.shouldThrowReport = false;
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.verified);
      await tester.tap(find.byKey(const Key('report_detail_refresh_retry_button')));
      await tester.pumpAndSettle();
      expect(find.text('Verified'), findsOneWidget);
      expect(find.text("Couldn't refresh this report. Showing the last loaded status."), findsNothing);
    });

    testWidgets('responsive layout on narrow 320px viewport without overflow', (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      mockRepo.reportToReturn = createSampleReport(
        description: 'A very long description that spans multiple lines on narrow screens without overflowing.',
        addressText: 'A very detailed road name, near intersection 4B, Sector 2, Municipal Zone',
      );
      mockRepo.historyToReturn = [
        WasteReportStatusHistoryModel(
          id: 'h-1',
          wasteReportId: 'rep-101',
          fromStatus: null,
          toStatus: WasteReportStatus.submitted,
          changedAt: DateTime.utc(2026, 9, 16, 8, 0, 0),
        ),
      ];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });

    testWidgets('responsive layout on standard 390px viewport without overflow', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      mockRepo.reportToReturn = createSampleReport();
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });

    testWidgets('displays Edit Report button when report status is Submitted', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('edit_report_button')), findsOneWidget);
      expect(find.byKey(const Key('edit_report_appbar_button')), findsOneWidget);
    });

    testWidgets('does not display Edit Report button when status is UnderReview or Verified', (tester) async {
      // UnderReview
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.underReview);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('edit_report_button')), findsNothing);
      expect(find.byKey(const Key('edit_report_appbar_button')), findsNothing);

      // Verified
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.verified);
      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('edit_report_button')), findsNothing);
      expect(find.byKey(const Key('edit_report_appbar_button')), findsNothing);
    });

    testWidgets('does not display Edit Report button when status is Rejected or Resolved', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.rejected);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('edit_report_button')), findsNothing);
      expect(find.byKey(const Key('edit_report_appbar_button')), findsNothing);

      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.resolved);
      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('edit_report_button')), findsNothing);
      expect(find.byKey(const Key('edit_report_appbar_button')), findsNothing);
    });

    testWidgets('displays Manage Photos button when report status is Submitted', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('manage_photos_button')), findsOneWidget);
    });

    testWidgets('does not display Manage Photos button when status is not Submitted', (tester) async {
      for (final status in [
        WasteReportStatus.underReview,
        WasteReportStatus.verified,
        WasteReportStatus.rejected,
        WasteReportStatus.resolved,
      ]) {
        mockRepo.reportToReturn = createSampleReport(status: status);
        mockRepo.historyToReturn = [];

        await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('manage_photos_button')),
          findsNothing,
          reason: 'Manage Photos button should not appear for status $status',
        );
      }
    });

    testWidgets('displays Cancel Report button when report status is Submitted', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('cancel_report_button')), findsOneWidget);
    });

    testWidgets('does not display Cancel Report button when status is not Submitted', (tester) async {
      for (final status in [
        WasteReportStatus.underReview,
        WasteReportStatus.verified,
        WasteReportStatus.rejected,
        WasteReportStatus.scheduled,
        WasteReportStatus.inProgress,
        WasteReportStatus.resolved,
        WasteReportStatus.cancelled,
      ]) {
        mockRepo.reportToReturn = createSampleReport(status: status);
        mockRepo.historyToReturn = [];

        await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('cancel_report_button')),
          findsNothing,
          reason: 'Cancel Report button should not appear for status $status',
        );
      }
    });

    testWidgets('tapping Cancel Report button opens confirmation dialog with Keep Report and Cancel Report buttons', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
      await tester.tap(find.byKey(const Key('cancel_report_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('cancel_report_dialog')), findsOneWidget);
      expect(find.text('Cancel this report?'), findsOneWidget);
      expect(find.text("This report will be marked as cancelled. You won't be able to edit it or add photos afterward."), findsOneWidget);
      expect(find.byKey(const Key('keep_report_button')), findsOneWidget);
      expect(find.byKey(const Key('confirm_cancel_report_button')), findsOneWidget);
    });

    testWidgets('tapping Keep Report dismisses dialog without calling cancel API', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
      await tester.tap(find.byKey(const Key('cancel_report_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('keep_report_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('cancel_report_dialog')), findsNothing);
      expect(mockRepo.cancelCallCount, 0);
    });

    testWidgets('confirming Cancel Report calls cancelWasteReport, shows success snackbar, and reloads data to Cancelled state', (tester) async {
      final initialReport = createSampleReport(id: 'rep-cancel-test', status: WasteReportStatus.submitted);
      final cancelledReport = createSampleReport(id: 'rep-cancel-test', status: WasteReportStatus.cancelled);

      final initialHistory = [
        WasteReportStatusHistoryModel(
          id: 'hist-1',
          wasteReportId: 'rep-cancel-test',
          fromStatus: null,
          toStatus: WasteReportStatus.submitted,
          changedByUserId: 'cit-1',
          changedByUserName: 'Kamal Perera',
          notes: null,
          changedAt: DateTime.utc(2026, 9, 16, 8, 30, 0),
        ),
      ];

      final updatedHistory = [
        ...initialHistory,
        WasteReportStatusHistoryModel(
          id: 'hist-2',
          wasteReportId: 'rep-cancel-test',
          fromStatus: WasteReportStatus.submitted,
          toStatus: WasteReportStatus.cancelled,
          changedByUserId: 'cit-1',
          changedByUserName: 'Kamal Perera',
          notes: 'Cancelled by citizen',
          changedAt: DateTime.utc(2026, 9, 16, 9, 0, 0),
        ),
      ];

      mockRepo.reportToReturn = initialReport;
      mockRepo.historyToReturn = initialHistory;

      await tester.pumpWidget(createTestWidget(reportId: 'rep-cancel-test'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('cancel_report_button')), findsOneWidget);
      expect(find.byKey(const Key('edit_report_button')), findsOneWidget);
      expect(find.byKey(const Key('manage_photos_button')), findsOneWidget);

      await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
      await tester.tap(find.byKey(const Key('cancel_report_button')));
      await tester.pumpAndSettle();

      // Configure repo to return cancelled data on subsequent get
      mockRepo.reportToReturn = cancelledReport;
      mockRepo.historyToReturn = updatedHistory;

      await tester.tap(find.byKey(const Key('confirm_cancel_report_button')));
      await tester.pumpAndSettle();

      expect(mockRepo.cancelCallCount, 1);
      expect(mockRepo.lastCancelledReportId, 'rep-cancel-test');

      // Success SnackBar shown
      expect(find.byKey(const Key('cancel_report_success_snackbar')), findsOneWidget);
      expect(find.text('Report cancelled.'), findsOneWidget);

      // Mutative controls now hidden
      expect(find.byKey(const Key('cancel_report_button')), findsNothing);
      expect(find.byKey(const Key('edit_report_button')), findsNothing);
      expect(find.byKey(const Key('manage_photos_button')), findsNothing);
      expect(find.byKey(const Key('edit_report_appbar_button')), findsNothing);

      // Status badge shows Cancelled
      expect(find.text('Cancelled'), findsWidgets);

      // Status history shows "Cancelled by citizen"
      expect(find.text('Cancelled by citizen'), findsOneWidget);
    });

    testWidgets('handles 409 Conflict: shows conflict snackbar and re-fetches authoritative data', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];
      mockRepo.shouldThrowCancel = true;
      mockRepo.cancelApiException = const ApiException(
        message: 'Waste report cannot be cancelled. Current status does not allow cancellation.',
        statusCode: 409,
      );

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
      await tester.tap(find.byKey(const Key('cancel_report_button')));
      await tester.pumpAndSettle();

      // Simulate that when it refetches, it's now UnderReview
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.underReview);

      await tester.tap(find.byKey(const Key('confirm_cancel_report_button')));
      await tester.pumpAndSettle();

      expect(mockRepo.cancelCallCount, 1);
      expect(find.byKey(const Key('cancel_report_conflict_snackbar')), findsOneWidget);
      expect(find.text('This report can no longer be cancelled because its status has changed.'), findsOneWidget);

      // Button is now gone because status is UnderReview
      expect(find.byKey(const Key('cancel_report_button')), findsNothing);
    });

    testWidgets('handles 403 Forbidden: shows permission error snackbar', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];
      mockRepo.shouldThrowCancel = true;
      mockRepo.cancelApiException = const ApiException(
        message: 'You can only cancel your own waste reports.',
        statusCode: 403,
      );

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
      await tester.tap(find.byKey(const Key('cancel_report_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_cancel_report_button')));
      await tester.pumpAndSettle();

      expect(mockRepo.cancelCallCount, 1);
      expect(find.byKey(const Key('cancel_report_error_snackbar')), findsOneWidget);
      expect(find.text('You can only cancel your own waste reports.'), findsOneWidget);
    });

    testWidgets('handles 404 Not Found: shows not found error snackbar', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];
      mockRepo.shouldThrowCancel = true;
      mockRepo.cancelApiException = const ApiException(
        message: 'Waste report was not found.',
        statusCode: 404,
      );

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
      await tester.tap(find.byKey(const Key('cancel_report_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_cancel_report_button')));
      await tester.pumpAndSettle();

      expect(mockRepo.cancelCallCount, 1);
      expect(find.byKey(const Key('cancel_report_error_snackbar')), findsOneWidget);
      expect(find.text('Waste report was not found.'), findsOneWidget);
    });

    testWidgets('handles 500 error: shows generic error snackbar', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];
      mockRepo.shouldThrowCancel = true;
      mockRepo.cancelApiException = const ApiException(
        message: 'Internal server error.',
        statusCode: 500,
      );

      await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
      await tester.tap(find.byKey(const Key('cancel_report_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_cancel_report_button')));
      await tester.pumpAndSettle();

      expect(mockRepo.cancelCallCount, 1);
      expect(find.byKey(const Key('cancel_report_error_snackbar')), findsOneWidget);
    });

    testWidgets('renders cleanly without overflow on narrow 320px and 390px viewports', (tester) async {
      mockRepo.reportToReturn = createSampleReport(status: WasteReportStatus.submitted);
      mockRepo.historyToReturn = [];

      for (final width in [320.0, 390.0]) {
        tester.view.physicalSize = Size(width, 800);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(() {
          tester.view.resetPhysicalSize();
          tester.view.resetDevicePixelRatio();
        });

        await tester.pumpWidget(createTestWidget(reportId: 'rep-101'));
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);

        await tester.scrollUntilVisible(find.byKey(const Key('cancel_report_button')), 200);
        expect(find.byKey(const Key('cancel_report_button')), findsOneWidget);

        await tester.tap(find.byKey(const Key('cancel_report_button')));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('cancel_report_dialog')), findsOneWidget);
        expect(tester.takeException(), isNull);

        // Dismiss dialog
        await tester.tap(find.byKey(const Key('keep_report_button')));
        await tester.pumpAndSettle();
      }
    });
  });
}
