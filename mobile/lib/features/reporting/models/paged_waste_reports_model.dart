import 'waste_report_list_item_model.dart';

/// Paged envelope for waste report lists.
/// Conforms strictly to ASP.NET Core `PagedResult<WasteReportSummaryDto>` response.
class PagedWasteReportsModel {
  final List<WasteReportListItemModel> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  const PagedWasteReportsModel({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  bool get hasMore => page < totalPages;
  bool get isEmpty => items.isEmpty;
  bool get isNotEmpty => items.isNotEmpty;

  factory PagedWasteReportsModel.fromJson(Map<String, dynamic> json) {
    final itemsRaw = json['items'];
    if (itemsRaw == null || itemsRaw is! List) {
      throw const FormatException("Missing or invalid required field 'items'");
    }

    final items = itemsRaw.map((item) {
      if (item is! Map<String, dynamic>) {
        throw const FormatException("Invalid item object in 'items' list");
      }
      return WasteReportListItemModel.fromJson(item);
    }).toList();

    final pageRaw = json['page'];
    if (pageRaw == null || pageRaw is! num) {
      throw const FormatException("Missing or invalid required field 'page'");
    }
    final page = pageRaw.toInt();

    final pageSizeRaw = json['pageSize'];
    if (pageSizeRaw == null || pageSizeRaw is! num) {
      throw const FormatException("Missing or invalid required field 'pageSize'");
    }
    final pageSize = pageSizeRaw.toInt();

    final totalCountRaw = json['totalCount'];
    if (totalCountRaw == null || totalCountRaw is! num) {
      throw const FormatException("Missing or invalid required field 'totalCount'");
    }
    final totalCount = totalCountRaw.toInt();

    final totalPagesRaw = json['totalPages'];
    if (totalPagesRaw == null || totalPagesRaw is! num) {
      throw const FormatException("Missing or invalid required field 'totalPages'");
    }
    final totalPages = totalPagesRaw.toInt();

    return PagedWasteReportsModel(
      items: items,
      page: page,
      pageSize: pageSize,
      totalCount: totalCount,
      totalPages: totalPages,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'items': items.map((item) => item.toJson()).toList(),
      'page': page,
      'pageSize': pageSize,
      'totalCount': totalCount,
      'totalPages': totalPages,
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is PagedWasteReportsModel &&
          runtimeType == other.runtimeType &&
          page == other.page &&
          pageSize == other.pageSize &&
          totalCount == other.totalCount &&
          totalPages == other.totalPages;

  @override
  int get hashCode =>
      page.hashCode ^
      pageSize.hashCode ^
      totalCount.hashCode ^
      totalPages.hashCode;
}
