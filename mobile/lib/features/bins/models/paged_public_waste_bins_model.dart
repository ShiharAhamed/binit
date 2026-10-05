import 'public_waste_bin_list_item_model.dart';

/// Paged envelope returned by GET /api/v1/bins/public.
class PagedPublicWasteBinsModel {
  final List<PublicWasteBinListItemModel> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  const PagedPublicWasteBinsModel({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  bool get hasMore => page < totalPages;
  bool get isEmpty => items.isEmpty;

  factory PagedPublicWasteBinsModel.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];
    if (rawItems is! List) {
      throw const FormatException("Missing or invalid required field 'items'");
    }
    final items = rawItems.map((item) {
      if (item is! Map<String, dynamic>) {
        throw const FormatException("Invalid item object in 'items' list");
      }
      return PublicWasteBinListItemModel.fromJson(item);
    }).toList(growable: false);

    return PagedPublicWasteBinsModel(
      items: items,
      page: requiredJsonInt(json, 'page'),
      pageSize: requiredJsonInt(json, 'pageSize'),
      totalCount: requiredJsonInt(json, 'totalCount'),
      totalPages: requiredJsonInt(json, 'totalPages'),
    );
  }
}
