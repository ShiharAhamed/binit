import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/driver_repository.dart';
import '../models/assignment_detail_model.dart';
import '../models/assignment_summary_model.dart';
import '../models/collection_assignment_status.dart';
import '../models/paged_assignments_model.dart';

/// The small recent-results query includes room for the single allowed active
/// assignment while still returning up to three terminal assignments.
const driverRecentAssignmentsPageSize = 4;
const driverHistoryPageSize = 20;

/// Server-side page request for a driver's assignment history.
///
/// A null status intentionally asks the existing API for all of the driver's
/// assignments. Screens then omit unfinished records, preserving the API's
/// authoritative ordering without pretending there is a separate history API.
class DriverAssignmentHistoryQuery {
  final CollectionAssignmentStatus? status;
  final int page;
  final int pageSize;

  const DriverAssignmentHistoryQuery({
    this.status,
    required this.page,
    this.pageSize = driverHistoryPageSize,
  });

  @override
  bool operator ==(Object other) =>
      other is DriverAssignmentHistoryQuery &&
      other.status == status &&
      other.page == page &&
      other.pageSize == pageSize;

  @override
  int get hashCode => Object.hash(status, page, pageSize);
}

/// Recent terminal results for the dashboard. This is deliberately independent
/// of the current-assignment provider so finishing work is reflected without a
/// restart and a recent-query failure cannot affect active execution UI.
final driverRecentAssignmentsProvider =
    FutureProvider<List<AssignmentSummaryModel>>((ref) async {
  final response = await ref
      .watch(driverRepositoryProvider)
      .getMyAssignments(page: 1, pageSize: driverRecentAssignmentsPageSize);

  return terminalAssignments(response.items).take(3).toList(growable: false);
});

/// A server-paginated page used by the read-only assignment-history screen.
final driverAssignmentHistoryPageProvider = FutureProvider.family<
    PagedAssignmentsModel, DriverAssignmentHistoryQuery>((ref, query) {
  return ref.watch(driverRepositoryProvider).getMyAssignments(
        status: query.status,
        page: query.page,
        pageSize: query.pageSize,
      );
});

/// Full read-only detail for one assignment owned by the authenticated Driver.
final driverHistoryDetailProvider =
    FutureProvider.family<AssignmentDetailModel, String>((ref, assignmentId) {
  return ref.watch(driverRepositoryProvider).getAssignmentDetail(assignmentId);
});

/// Removes active assignments from an already-authorized `mine` response.
Iterable<AssignmentSummaryModel> terminalAssignments(
  Iterable<AssignmentSummaryModel> assignments,
) =>
    assignments.where((assignment) => assignment.status.isTerminal);
