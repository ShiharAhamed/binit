import 'package:dio/dio.dart';

import '../../../core/constants/api_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/dio_client.dart';
import '../models/assignment_detail_model.dart';
import '../models/collection_assignment_status.dart';
import '../models/driver_availability_status.dart';
import '../models/driver_self_model.dart';
import '../models/paged_assignments_model.dart';
import '../models/route_read_model.dart';

/// HTTP API client executing authenticated Driver operations against the SmartWaste backend.
class DriverApi {
  final DioClient _client;

  DriverApi({DioClient? client}) : _client = client ?? DioClient();

  /// Retrieves the authenticated driver's self-profile and duty availability.
  /// (GET /api/v1/drivers/{driverId})
  Future<DriverSelfModel> getDriverSelf(String driverId) async {
    try {
      final response = await _client.dio.get(
        ApiConstants.driverProfile(driverId),
      );
      return DriverSelfModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Updates the authenticated driver's duty availability status.
  /// (PATCH /api/v1/drivers/me/availability)
  Future<DriverSelfModel> updateAvailability(
    DriverAvailabilityStatus status,
  ) async {
    try {
      final response = await _client.dio.patch(
        ApiConstants.driverAvailability,
        data: {'availabilityStatus': status.toJsonValue()},
      );
      return DriverSelfModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Retrieves a paginated list of assignments owned by the authenticated driver.
  /// (GET /api/v1/assignments/mine)
  ///
  /// Page size is strictly clamped between 1 and 50 per backend contract.
  Future<PagedAssignmentsModel> getMyAssignments({
    CollectionAssignmentStatus? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    final effectivePage = page < 1 ? 1 : page;
    final effectivePageSize = pageSize.clamp(1, 50);

    final queryParameters = <String, dynamic>{
      'page': effectivePage,
      'pageSize': effectivePageSize,
      if (status != null) 'status': status.toJsonValue(),
    };

    try {
      final response = await _client.dio.get(
        ApiConstants.myAssignments,
        queryParameters: queryParameters,
      );
      return PagedAssignmentsModel.fromJson(
        response.data as Map<String, dynamic>,
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Retrieves full details of a specific collection assignment.
  /// (GET /api/v1/assignments/{id})
  Future<AssignmentDetailModel> getAssignmentDetail(String assignmentId) async {
    try {
      final response = await _client.dio.get(
        ApiConstants.assignmentDetail(assignmentId),
      );
      return AssignmentDetailModel.fromJson(
        response.data as Map<String, dynamic>,
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Retrieves the ordered route and stop sequence for a collection route.
  /// (GET /api/v1/routes/{id})
  Future<RouteReadModel> getRoute(String routeId) async {
    try {
      final response = await _client.dio.get(ApiConstants.routeDetail(routeId));
      return RouteReadModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Starts an assignment owned by the authenticated Driver.
  /// (POST /api/v1/assignments/{id}/start)
  Future<AssignmentDetailModel> startAssignment(String assignmentId) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.assignmentStart(assignmentId),
      );
      return AssignmentDetailModel.fromJson(
        response.data as Map<String, dynamic>,
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Records a completed outcome for a pending route stop.
  /// (POST /api/v1/assignments/{assignmentId}/stops/{stopId}/complete)
  Future<AssignmentDetailModel> completeStop(
    String assignmentId,
    String stopId,
  ) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.assignmentStopComplete(assignmentId, stopId),
      );
      return AssignmentDetailModel.fromJson(
        response.data as Map<String, dynamic>,
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Records a failed outcome and the Driver's required failure reason.
  /// (POST /api/v1/assignments/{assignmentId}/stops/{stopId}/fail)
  Future<AssignmentDetailModel> failStop(
    String assignmentId,
    String stopId,
    String reason,
  ) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.assignmentStopFail(assignmentId, stopId),
        data: {'reason': reason},
      );
      return AssignmentDetailModel.fromJson(
        response.data as Map<String, dynamic>,
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }

  /// Finalizes a fully processed assignment owned by the authenticated Driver.
  /// (POST /api/v1/assignments/{id}/finalize)
  Future<AssignmentDetailModel> finalizeAssignment(String assignmentId) async {
    try {
      final response = await _client.dio.post(
        ApiConstants.assignmentFinalize(assignmentId),
      );
      return AssignmentDetailModel.fromJson(
        response.data as Map<String, dynamic>,
      );
    } on DioException catch (e) {
      throw ApiException.fromDioError(e);
    }
  }
}
