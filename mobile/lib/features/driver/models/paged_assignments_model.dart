import 'assignment_summary_model.dart';
import 'driver_json_helpers.dart';

/// Paged envelope returned by GET /api/v1/assignments/mine.
class PagedAssignmentsModel {
  final List<AssignmentSummaryModel> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  const PagedAssignmentsModel({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  bool get hasMore => page < totalPages;
  bool get isEmpty => items.isEmpty;

  factory PagedAssignmentsModel.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];
    if (rawItems is! List) {
      throw const FormatException("Missing or invalid required field 'items'");
    }
    final items = rawItems.map((item) {
      if (item is! Map<String, dynamic>) {
        throw const FormatException("Invalid item object in 'items' list");
      }
      return AssignmentSummaryModel.fromJson(item);
    }).toList(growable: false);

    return PagedAssignmentsModel(
      items: items,
      page: requiredJsonInt(json, 'page'),
      pageSize: requiredJsonInt(json, 'pageSize'),
      totalCount: requiredJsonInt(json, 'totalCount'),
      totalPages: requiredJsonInt(json, 'totalPages'),
    );
  }

  Map<String, dynamic> toJson() => {
    'items': items.map((e) => e.toJson()).toList(),
    'page': page,
    'pageSize': pageSize,
    'totalCount': totalCount,
    'totalPages': totalPages,
  };
}
