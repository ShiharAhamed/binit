import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/complaints_repository.dart';
import '../models/complaint_model.dart';

export '../data/complaints_repository.dart' show complaintsRepositoryProvider;

/// FutureProvider to fetch complaint detail by ID.
final complaintDetailFutureProvider =
    FutureProvider.family<ComplaintDetailModel, String>((ref, id) async {
  final repository = ref.watch(complaintsRepositoryProvider);
  return repository.getComplaintById(id);
});
