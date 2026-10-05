import 'package:dio/dio.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/dio_client.dart';
import '../models/create_waste_report_request.dart';
import '../models/paged_waste_reports_model.dart';
import '../models/report_attachment_model.dart';
import '../models/update_waste_report_request.dart';
import '../models/waste_report_detail_model.dart';
import '../models/waste_report_status.dart';
import '../models/waste_report_status_history_model.dart';
import '../models/waste_type.dart';

/// Data source executing authenticated HTTP requests against SmartWaste ASP.NET Core Reporting API.
/// Communication is strictly with ASP.NET Core; Flutter never communicates with Supabase directly.
class ReportingApi {
  final DioClient _client;

  ReportingApi({DioClient? client}) : _client = client ?? DioClient();

  /// Submits a new waste report (POST /api/v1/waste-reports).
  /// Required fields: description, wasteType, latitude, longitude.
  /// Optional: addressText.
  Future<WasteReportDetailModel> createWasteReport(CreateWasteReportRequest request) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.wasteReports,
        data: request.toJson(),
      );
      return WasteReportDetailModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Uploads a photographic attachment from a local file path (POST /api/v1/waste-reports/{id}/attachments).
  /// Multipart form field name: "file".
  /// ASP.NET authoritatively validates magic bytes, size (<= 5 MB), quota (<= 3), and uploads to Supabase.
  Future<ReportAttachmentModel> uploadAttachment({
    required String reportId,
    required String filePath,
    String? fileName,
    String? fileType,
  }) async {
    try {
      final multipartFile = await MultipartFile.fromFile(
        filePath,
        filename: fileName,
        contentType: fileType != null ? DioMediaType.parse(fileType) : null,
      );
      final formData = FormData.fromMap({
        'file': multipartFile,
      });

      final response = await _client.dio.post(
        ApiConstants.wasteReportAttachments(reportId),
        data: formData,
      );
      return ReportAttachmentModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Uploads a photographic attachment from raw in-memory bytes.
  /// Multipart form field name: "file".
  Future<ReportAttachmentModel> uploadAttachmentBytes({
    required String reportId,
    required List<int> bytes,
    required String fileName,
    String? fileType,
  }) async {
    try {
      final multipartFile = MultipartFile.fromBytes(
        bytes,
        filename: fileName,
        contentType: fileType != null ? DioMediaType.parse(fileType) : null,
      );
      final formData = FormData.fromMap({
        'file': multipartFile,
      });

      final response = await _client.dio.post(
        ApiConstants.wasteReportAttachments(reportId),
        data: formData,
      );
      return ReportAttachmentModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Fetches full details of a specific waste report (GET /api/v1/waste-reports/{id}).
  Future<WasteReportDetailModel> getWasteReport(String reportId) async {
    try {
      final response = await _client.dio.get(
        ApiConstants.wasteReportDetail(reportId),
      );
      return WasteReportDetailModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Updates core fields of an existing submitted waste report (PATCH /api/v1/waste-reports/{id}).
  /// Permitted only when status is Submitted.
  Future<WasteReportDetailModel> updateWasteReport({
    required String reportId,
    required UpdateWasteReportRequest request,
  }) async {
    try {
      final response = await _client.dio.patch(
        ApiConstants.wasteReportDetail(reportId),
        data: request.toJson(),
      );
      return WasteReportDetailModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Fetches chronological status transition audit trail for a waste report (GET /api/v1/waste-reports/{id}/history).
  Future<List<WasteReportStatusHistoryModel>> getWasteReportHistory(String reportId) async {
    try {
      final response = await _client.dio.get(
        ApiConstants.wasteReportHistory(reportId),
      );
      final list = response.data as List<dynamic>;
      return list
          .map((item) => WasteReportStatusHistoryModel.fromJson(item as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Removes an attachment from a submitted waste report (DELETE /api/v1/waste-reports/{id}/attachments/{attachmentId}).
  Future<void> deleteAttachment({
    required String reportId,
    required String attachmentId,
  }) async {
    try {
      await _client.dio.delete(
        ApiConstants.wasteReportAttachment(reportId, attachmentId),
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Cancels a submitted waste report (DELETE /api/v1/waste-reports/{id}).
  /// Permitted only for the citizen owner while status is Submitted.
  Future<void> cancelWasteReport(String reportId) async {
    try {
      await _client.dio.delete(
        ApiConstants.wasteReportDetail(reportId),
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Retrieves a paged list of waste reports (GET /api/v1/waste-reports).
  /// For Citizens, results are authoritatively filtered to reports they created.
  Future<PagedWasteReportsModel> getWasteReports({
    int page = 1,
    int pageSize = 20,
    WasteReportStatus? status,
    WasteType? wasteType,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'page': page,
        'pageSize': pageSize,
        if (status != null) 'status': status.toJsonValue(),
        if (wasteType != null) 'wasteType': wasteType.toJsonValue(),
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        if (sortBy != null && sortBy.trim().isNotEmpty) 'sortBy': sortBy.trim(),
        if (sortDirection != null && sortDirection.trim().isNotEmpty) 'sortDirection': sortDirection.trim(),
      };

      final response = await _client.dio.get(
        ApiConstants.wasteReports,
        queryParameters: queryParams,
      );

      return PagedWasteReportsModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }
}
