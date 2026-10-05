import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/operations_repository.dart';
import '../models/operational_issue_model.dart';

export '../data/operations_repository.dart' show operationsRepositoryProvider;

/// FutureProvider to fetch operational issue detail by ID.
final operationalIssueDetailFutureProvider =
    FutureProvider.family<OperationalIssueDetailModel, String>((ref, id) async {
  final repository = ref.watch(operationsRepositoryProvider);
  return repository.getOperationalIssueById(id);
});
