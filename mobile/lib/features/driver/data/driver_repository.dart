import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/assignment_detail_model.dart';
import '../models/assignment_summary_model.dart';
import '../models/collection_assignment_status.dart';
import '../models/driver_availability_status.dart';
import '../models/driver_self_model.dart';
import '../models/paged_assignments_model.dart';
import '../models/route_read_model.dart';
import 'driver_api.dart';

/// Provider exposing the singleton DriverRepository to the application.
final driverRepositoryProvider = Provider<DriverRepository>((ref) {
  return DriverRepository();
});

/// Repository boundary mediating Driver operational data and assignments.
class DriverRepository {
  final DriverApi _api;

  DriverRepository({DriverApi? api}) : _api = api ?? DriverApi();

  /// Retrieves the driver's own profile and availability.
  Future<DriverSelfModel> getDriverSelf(String driverId) {
    return _api.getDriverSelf(driverId);
  }

  /// Updates the driver's duty availability status (Available or OffDuty).
  Future<DriverSelfModel> updateAvailability(DriverAvailabilityStatus status) {
    return _api.updateAvailability(status);
  }

  /// Retrieves a paginated list of assignments belonging to the authenticated driver.
  Future<PagedAssignmentsModel> getMyAssignments({
    CollectionAssignmentStatus? status,
    int page = 1,
    int pageSize = 20,
  }) {
    return _api.getMyAssignments(
      status: status,
      page: page,
      pageSize: pageSize,
    );
  }

  /// Retrieves the full operational detail of a specific collection assignment.
  Future<AssignmentDetailModel> getAssignmentDetail(String assignmentId) {
    return _api.getAssignmentDetail(assignmentId);
  }

  /// Retrieves the ordered route and stop sequence for a route.
  Future<RouteReadModel> getRoute(String routeId) {
    return _api.getRoute(routeId);
  }

  /// Starts a collection assignment owned by the authenticated Driver.
  Future<AssignmentDetailModel> startAssignment(String assignmentId) {
    return _api.startAssignment(assignmentId);
  }

  /// Completes one pending route stop on the authenticated Driver's assignment.
  Future<AssignmentDetailModel> completeStop(
    String assignmentId,
    String stopId,
  ) {
    return _api.completeStop(assignmentId, stopId);
  }

  /// Fails one pending route stop with the required Driver-provided reason.
  Future<AssignmentDetailModel> failStop(
    String assignmentId,
    String stopId,
    String reason,
  ) {
    return _api.failStop(assignmentId, stopId, reason);
  }

  /// Finalizes an assignment after every route stop has a terminal outcome.
  Future<AssignmentDetailModel> finalizeAssignment(String assignmentId) {
    return _api.finalizeAssignment(assignmentId);
  }

  /// Resolves the driver's current active assignment summary, if one exists.
  ///
  /// The backend guarantees at most one unfinished assignment per driver at any time.
  /// This method queries status-filtered endpoints with minimal payload (pageSize: 1):
  /// 1. Queries for an active [CollectionAssignmentStatus.inProgress] run.
  /// 2. If none, falls back to check for an upcoming [CollectionAssignmentStatus.assigned] run.
  /// 3. If neither exists, returns null (driver has no active assignment).
  Future<AssignmentSummaryModel?> getCurrentAssignmentSummary() async {
    final inProgressResult = await _api.getMyAssignments(
      status: CollectionAssignmentStatus.inProgress,
      page: 1,
      pageSize: 1,
    );
    if (inProgressResult.items.isNotEmpty) {
      return inProgressResult.items.first;
    }

    final assignedResult = await _api.getMyAssignments(
      status: CollectionAssignmentStatus.assigned,
      page: 1,
      pageSize: 1,
    );
    if (assignedResult.items.isNotEmpty) {
      return assignedResult.items.first;
    }

    return null;
  }

  /// Resolves the full [AssignmentDetailModel] of the driver's active assignment,
  /// or returns null if no active assignment is currently underway or assigned.
  Future<AssignmentDetailModel?> getCurrentAssignmentDetail() async {
    final summary = await getCurrentAssignmentSummary();
    if (summary == null) return null;
    return getAssignmentDetail(summary.id);
  }
}
