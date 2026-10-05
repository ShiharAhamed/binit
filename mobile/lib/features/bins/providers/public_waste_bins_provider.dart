import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/public_waste_bins_repository.dart';
import '../models/paged_public_waste_bins_model.dart';
import '../models/public_waste_bin_detail_model.dart';
import '../models/public_waste_bin_query.dart';

/// Future list state keyed by the explicit public-search query.
final publicWasteBinsProvider = FutureProvider.family<PagedPublicWasteBinsModel, PublicWasteBinQuery>((ref, query) {
  return ref.watch(publicWasteBinsRepositoryProvider).getPublicWasteBins(query);
});

/// Future detail state keyed by the selected public bin identifier.
final publicWasteBinDetailProvider = FutureProvider.family<PublicWasteBinDetailModel, String>((ref, binId) {
  return ref.watch(publicWasteBinsRepositoryProvider).getPublicWasteBin(binId);
});
