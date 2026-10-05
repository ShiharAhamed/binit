import 'package:dio/dio.dart';

import '../../../core/constants/api_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/dio_client.dart';
import '../models/paged_public_waste_bins_model.dart';
import '../models/public_waste_bin_detail_model.dart';
import '../models/public_waste_bin_query.dart';

/// Authenticated data source for the citizen public bin-discovery endpoints.
class PublicWasteBinsApi {
  final DioClient _client;

  PublicWasteBinsApi({DioClient? client}) : _client = client ?? DioClient();

  Future<PagedPublicWasteBinsModel> getPublicWasteBins(PublicWasteBinQuery query) async {
    try {
      final response = await _client.dio.get(
        ApiConstants.publicWasteBins,
        queryParameters: query.toQueryParameters(),
      );
      return PagedPublicWasteBinsModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (error) {
      throw ApiException.fromDioError(error);
    }
  }

  Future<PublicWasteBinDetailModel> getPublicWasteBin(String binId) async {
    try {
      final response = await _client.dio.get(ApiConstants.publicWasteBinDetail(binId));
      return PublicWasteBinDetailModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (error) {
      throw ApiException.fromDioError(error);
    }
  }
}
