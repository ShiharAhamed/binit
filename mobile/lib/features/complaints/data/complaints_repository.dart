import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/complaint_model.dart';
import '../models/create_complaint_request.dart';
import 'complaints_api.dart';

/// Provider for the ComplaintsRepository instance.
final complaintsRepositoryProvider = Provider<ComplaintsRepository>((ref) {
  return ComplaintsRepository();
});

/// Repository coordinating citizen complaint data operations.
class ComplaintsRepository {
  final ComplaintsApi _api;

  ComplaintsRepository({ComplaintsApi? api}) : _api = api ?? ComplaintsApi();

  /// Submits a new citizen complaint.
  Future<ComplaintDetailModel> createComplaint(CreateComplaintRequest request) {
    return _api.createComplaint(request);
  }

  /// Retrieves a paginated list of complaints for the authenticated citizen.
  Future<PagedComplaintsModel> getMyComplaints({
    int page = 1,
    int pageSize = 20,
    ComplaintStatus? status,
    ComplaintCategory? category,
    String? search,
    String? sortBy = 'createdAt',
    String? sortDirection = 'desc',
  }) {
    return _api.getMyComplaints(
      page: page,
      pageSize: pageSize,
      status: status,
      category: category,
      search: search,
      sortBy: sortBy,
      sortDirection: sortDirection,
    );
  }

  /// Retrieves full details for a complaint by ID.
  Future<ComplaintDetailModel> getComplaintById(String id) {
    return _api.getComplaintById(id);
  }
}
