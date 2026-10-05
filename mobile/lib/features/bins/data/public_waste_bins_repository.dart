import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/paged_public_waste_bins_model.dart';
import '../models/public_waste_bin_detail_model.dart';
import '../models/public_waste_bin_query.dart';
import 'public_waste_bins_api.dart';

final publicWasteBinsRepositoryProvider = Provider<PublicWasteBinsRepository>((ref) {
  return PublicWasteBinsRepository();
});

/// Repository boundary for citizen bin discovery data.
class PublicWasteBinsRepository {
  final PublicWasteBinsApi _api;

  PublicWasteBinsRepository({PublicWasteBinsApi? api}) : _api = api ?? PublicWasteBinsApi();

  Future<PagedPublicWasteBinsModel> getPublicWasteBins(PublicWasteBinQuery query) {
    return _api.getPublicWasteBins(query);
  }

  Future<PublicWasteBinDetailModel> getPublicWasteBin(String binId) {
    return _api.getPublicWasteBin(binId);
  }
}
