import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/create_operational_issue_request.dart';
import '../models/operational_issue_model.dart';
import 'operations_api.dart';

/// Provider for the OperationsRepository instance.
final operationsRepositoryProvider = Provider<OperationsRepository>((ref) {
  return OperationsRepository();
});

/// Repository coordinating driver operational issues data operations.
class OperationsRepository {
  final OperationsApi _api;

  OperationsRepository({OperationsApi? api}) : _api = api ?? OperationsApi();

  /// Reports a new operational issue from the field.
  Future<OperationalIssueDetailModel> createOperationalIssue(
      CreateOperationalIssueRequest request) {
    return _api.createOperationalIssue(request);
  }

  /// Retrieves a paginated list of operational issues reported by the authenticated driver.
  Future<PagedOperationalIssuesModel> getMyOperationalIssues({
    int page = 1,
    int pageSize = 20,
    OperationalIssueStatus? status,
    OperationalIssueType? issueType,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) {
    return _api.getMyOperationalIssues(
      page: page,
      pageSize: pageSize,
      status: status,
      issueType: issueType,
      search: search,
      sortBy: sortBy,
      sortDirection: sortDirection,
    );
  }

  /// Retrieves full details for an operational issue by ID.
  Future<OperationalIssueDetailModel> getOperationalIssueById(String id) {
    return _api.getOperationalIssueById(id);
  }
}
