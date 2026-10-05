import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/create_waste_report_request.dart';
import '../models/paged_waste_reports_model.dart';
import '../models/report_attachment_model.dart';
import '../models/update_waste_report_request.dart';
import '../models/waste_report_detail_model.dart';
import '../models/waste_report_status.dart';
import '../models/waste_report_status_history_model.dart';
import '../models/waste_type.dart';
import 'reporting_api.dart';

/// Provider for the default ReportingRepository instance.
final reportingRepositoryProvider = Provider<ReportingRepository>((ref) {
  return ReportingRepository();
});

/// Repository coordinating reporting use cases.
/// Exposes high-level reporting contracts for view models and Riverpod providers.
class ReportingRepository {
  final ReportingApi _api;

  ReportingRepository({ReportingApi? api}) : _api = api ?? ReportingApi();

  /// Submits a new waste report to the backend.
  Future<WasteReportDetailModel> createWasteReport(CreateWasteReportRequest request) {
    return _api.createWasteReport(request);
  }

  /// Uploads an attachment to an existing report from a local file path.
  Future<ReportAttachmentModel> uploadAttachment({
    required String reportId,
    required String filePath,
    String? fileName,
    String? fileType,
  }) {
    return _api.uploadAttachment(
      reportId: reportId,
      filePath: filePath,
      fileName: fileName,
      fileType: fileType,
    );
  }

  /// Uploads an attachment to an existing report from in-memory bytes.
  Future<ReportAttachmentModel> uploadAttachmentBytes({
    required String reportId,
    required List<int> bytes,
    required String fileName,
    String? fileType,
  }) {
    return _api.uploadAttachmentBytes(
      reportId: reportId,
      bytes: bytes,
      fileName: fileName,
      fileType: fileType,
    );
  }

  /// Fetches details for an existing waste report.
  Future<WasteReportDetailModel> getWasteReport(String reportId) {
    return _api.getWasteReport(reportId);
  }

  /// Updates core fields of an existing submitted waste report.
  Future<WasteReportDetailModel> updateWasteReport({
    required String reportId,
    required UpdateWasteReportRequest request,
  }) {
    return _api.updateWasteReport(
      reportId: reportId,
      request: request,
    );
  }

  /// Fetches chronological status transition history for an existing waste report.
  Future<List<WasteReportStatusHistoryModel>> getWasteReportHistory(String reportId) {
    return _api.getWasteReportHistory(reportId);
  }

  /// Deletes an attachment from a submitted waste report.
  Future<void> deleteAttachment({
    required String reportId,
    required String attachmentId,
  }) {
    return _api.deleteAttachment(
      reportId: reportId,
      attachmentId: attachmentId,
    );
  }

  /// Cancels a submitted waste report.
  Future<void> cancelWasteReport(String reportId) {
    return _api.cancelWasteReport(reportId);
  }

  /// Fetches a paged list of waste reports with optional filtering, search, and sorting.
  Future<PagedWasteReportsModel> getWasteReports({
    int page = 1,
    int pageSize = 20,
    WasteReportStatus? status,
    WasteType? wasteType,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) {
    return _api.getWasteReports(
      page: page,
      pageSize: pageSize,
      status: status,
      wasteType: wasteType,
      search: search,
      sortBy: sortBy,
      sortDirection: sortDirection,
    );
  }
}
