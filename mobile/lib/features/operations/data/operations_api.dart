import 'package:dio/dio.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/dio_client.dart';
import '../models/create_operational_issue_request.dart';
import '../models/operational_issue_model.dart';

/// Data source executing authenticated HTTP requests against the SmartWaste Operational Issues API.
class OperationsApi {
  final DioClient _client;

  OperationsApi({DioClient? client}) : _client = client ?? DioClient();

  /// Reports a new operational issue from the field (POST /api/v1/operational-issues).
  Future<OperationalIssueDetailModel> createOperationalIssue(
      CreateOperationalIssueRequest request) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.operationalIssues,
        data: request.toJson(),
      );
      return OperationalIssueDetailModel.fromJson(
          response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Retrieves a paged list of the authenticated Driver's operational issues (GET /api/v1/operational-issues/mine).
  Future<PagedOperationalIssuesModel> getMyOperationalIssues({
    int page = 1,
    int pageSize = 20,
    OperationalIssueStatus? status,
    OperationalIssueType? issueType,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'page': page,
        'pageSize': pageSize,
        if (status != null) 'status': status.toJsonValue(),
        if (issueType != null) 'issueType': issueType.toJsonValue(),
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        if (sortBy != null && sortBy.trim().isNotEmpty) 'sortBy': sortBy.trim(),
        if (sortDirection != null && sortDirection.trim().isNotEmpty)
          'sortDirection': sortDirection.trim(),
      };

      final response = await _client.dio.get(
        ApiConstants.myOperationalIssues,
        queryParameters: queryParams,
      );

      return PagedOperationalIssuesModel.fromJson(
          response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Retrieves details for a specific operational issue by ID (GET /api/v1/operational-issues/{id}).
  Future<OperationalIssueDetailModel> getOperationalIssueById(String id) async {
    try {
      final response = await _client.dio.get(
        ApiConstants.operationalIssueDetail(id),
      );
      return OperationalIssueDetailModel.fromJson(
          response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }
}
