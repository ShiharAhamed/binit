import 'package:dio/dio.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/dio_client.dart';
import '../models/complaint_model.dart';
import '../models/create_complaint_request.dart';

/// Data source executing authenticated HTTP requests against the SmartWaste Complaints API.
class ComplaintsApi {
  final DioClient _client;

  ComplaintsApi({DioClient? client}) : _client = client ?? DioClient();

  /// Submits a new citizen complaint (POST /api/v1/complaints).
  Future<ComplaintDetailModel> createComplaint(CreateComplaintRequest request) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.complaints,
        data: request.toJson(),
      );
      return ComplaintDetailModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Retrieves a paged list of complaints (GET /api/v1/complaints).
  /// For Citizens, the backend authoritatively filters results to their own complaints.
  Future<PagedComplaintsModel> getMyComplaints({
    int page = 1,
    int pageSize = 20,
    ComplaintStatus? status,
    ComplaintCategory? category,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'page': page,
        'pageSize': pageSize,
        if (status != null) 'status': status.toJsonValue(),
        if (category != null) 'category': category.toJsonValue(),
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        if (sortBy != null && sortBy.trim().isNotEmpty) 'sortBy': sortBy.trim(),
        if (sortDirection != null && sortDirection.trim().isNotEmpty)
          'sortDirection': sortDirection.trim(),
      };

      final response = await _client.dio.get(
        ApiConstants.complaints,
        queryParameters: queryParams,
      );

      return PagedComplaintsModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Retrieves details for a specific complaint by ID (GET /api/v1/complaints/{id}).
  Future<ComplaintDetailModel> getComplaintById(String id) async {
    try {
      final response = await _client.dio.get(
        ApiConstants.complaintDetail(id),
      );
      return ComplaintDetailModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }
}
