import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/core/network/dio_client.dart';
import 'package:mobile/core/storage/secure_storage_service.dart';
import 'package:mobile/features/bins/data/public_waste_bins_api.dart';
import 'package:mobile/features/bins/data/public_waste_bins_repository.dart';
import 'package:mobile/features/bins/models/paged_public_waste_bins_model.dart';
import 'package:mobile/features/bins/models/public_bin_availability.dart';
import 'package:mobile/features/bins/models/public_waste_bin_detail_model.dart';
import 'package:mobile/features/bins/models/public_waste_bin_query.dart';
import 'package:mobile/features/bins/providers/public_waste_bins_provider.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';

class _FakeSecureStorageService extends SecureStorageService {
  @override
  Future<String?> getAccessToken() async => 'citizen-test-token';
}

class _MockHttpClientAdapter implements HttpClientAdapter {
  RequestOptions? lastRequest;
  int statusCode = 200;
  String responseBody = '{}';

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    lastRequest = options;
    return ResponseBody.fromString(
      responseBody,
      statusCode,
      headers: {Headers.contentTypeHeader: [Headers.jsonContentType]},
    );
  }

  @override
  void close({bool force = false}) {}
}

Map<String, dynamic> _listItem({
  String availability = 'Unknown',
  dynamic lastObservedAt,
  dynamic distanceMeters,
}) => {
  'id': '68e0d4a4-d6a7-4a4b-8b84-2a8d89f10d11',
  'binCode': 'BIN-001',
  'latitude': 6.9271,
  'longitude': 79.8612,
  'addressText': 'Galle Face Green',
  'capacityLiters': 1100,
  'acceptedWasteTypes': ['General', 'Recyclable'],
  'publicAvailability': availability,
  'lastObservedAt': lastObservedAt,
  'distanceMeters': distanceMeters,
};

Map<String, dynamic> _detail({
  String availability = 'Usable',
  dynamic lastObservedAt,
  bool isCollectionScheduled = false,
}) {
  final detail = _listItem(availability: availability, lastObservedAt: lastObservedAt)
    ..remove('distanceMeters');
  detail['isCollectionScheduled'] = isCollectionScheduled;
  return detail;
}

class _FakePublicWasteBinsApi extends PublicWasteBinsApi {
  _FakePublicWasteBinsApi({required this.listResult, required this.detailResult});

  final PagedPublicWasteBinsModel listResult;
  final PublicWasteBinDetailModel detailResult;
  PublicWasteBinQuery? receivedQuery;
  String? receivedBinId;

  @override
  Future<PagedPublicWasteBinsModel> getPublicWasteBins(PublicWasteBinQuery query) async {
    receivedQuery = query;
    return listResult;
  }

  @override
  Future<PublicWasteBinDetailModel> getPublicWasteBin(String binId) async {
    receivedBinId = binId;
    return detailResult;
  }
}

void main() {
  group('Public waste bin models', () {
    test('parses public list pagination and optional observation fields', () {
      final page = PagedPublicWasteBinsModel.fromJson({
        'items': [
          _listItem(
            availability: 'Warning',
            lastObservedAt: '2026-09-22T04:00:00.000Z',
            distanceMeters: 123.45,
          ),
        ],
        'page': 2,
        'pageSize': 10,
        'totalCount': 15,
        'totalPages': 2,
      });

      expect(page.items.single.binCode, 'BIN-001');
      expect(page.items.single.acceptedWasteTypes, [WasteType.general, WasteType.recyclable]);
      expect(page.items.single.publicAvailability, PublicBinAvailability.warning);
      expect(page.items.single.lastObservedAt, DateTime.utc(2026, 9, 22, 4));
      expect(page.items.single.distanceMeters, 123.45);
      expect(page.hasMore, isFalse);
    });

    test('parses all backend public availability values without deriving one from observations', () {
      final values = <String, PublicBinAvailability>{
        'Usable': PublicBinAvailability.usable,
        'Warning': PublicBinAvailability.warning,
        'Full': PublicBinAvailability.full,
        'Unavailable': PublicBinAvailability.unavailable,
        'Unknown': PublicBinAvailability.unknown,
      };

      for (final entry in values.entries) {
        final page = PagedPublicWasteBinsModel.fromJson({
          'items': [_listItem(availability: entry.key, lastObservedAt: null)],
          'page': 1,
          'pageSize': 20,
          'totalCount': 1,
          'totalPages': 1,
        });
        expect(page.items.single.publicAvailability, entry.value);
      }
    });

    test('keeps a missing observation as null and honors backend Unknown availability', () {
      final item = PagedPublicWasteBinsModel.fromJson({
        'items': [_listItem(availability: 'Unknown', lastObservedAt: null, distanceMeters: null)],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'totalPages': 1,
      }).items.single;

      expect(item.lastObservedAt, isNull);
      expect(item.distanceMeters, isNull);
      expect(item.publicAvailability, PublicBinAvailability.unknown);
      expect(item.publicAvailability, isNot(PublicBinAvailability.usable));
    });

    test('parses public bin detail and collection scheduled indicator', () {
      final detail = PublicWasteBinDetailModel.fromJson(_detail(
        availability: 'Full',
        lastObservedAt: '2026-09-22T05:00:00.000Z',
        isCollectionScheduled: true,
      ));

      expect(detail.id, '68e0d4a4-d6a7-4a4b-8b84-2a8d89f10d11');
      expect(detail.publicAvailability, PublicBinAvailability.full);
      expect(detail.isCollectionScheduled, isTrue);
      expect(detail.lastObservedAt, DateTime.utc(2026, 9, 22, 5));
    });
  });

  group('PublicWasteBinQuery', () {
    test('serializes only supported public list filters and pagination', () {
      const query = PublicWasteBinQuery(
        latitude: 6.9271,
        longitude: 79.8612,
        radiusKm: 2.5,
        wasteType: WasteType.recyclable,
        page: 3,
        pageSize: 10,
      );

      expect(query.toQueryParameters(), {
        'latitude': 6.9271,
        'longitude': 79.8612,
        'radiusKm': 2.5,
        'wasteType': 'Recyclable',
        'page': 3,
        'pageSize': 10,
      });
    });

    test('rejects a radius query without both coordinates before making a request', () {
      const query = PublicWasteBinQuery(latitude: 6.9271, radiusKm: 2);
      expect(query.toQueryParameters, throwsArgumentError);
    });
  });

  group('PublicWasteBinsApi', () {
    late _MockHttpClientAdapter adapter;
    late PublicWasteBinsApi api;

    setUp(() {
      adapter = _MockHttpClientAdapter();
      final dio = Dio(BaseOptions(baseUrl: 'http://localhost:5276/api/v1'));
      dio.httpClientAdapter = adapter;
      api = PublicWasteBinsApi(
        client: DioClient(dio: dio, storageService: _FakeSecureStorageService()),
      );
    });

    test('uses the authenticated public list route with location, waste type, and pagination', () async {
      adapter.responseBody = jsonEncode({
        'items': [_listItem(availability: 'Usable')],
        'page': 2,
        'pageSize': 10,
        'totalCount': 11,
        'totalPages': 2,
      });

      final result = await api.getPublicWasteBins(const PublicWasteBinQuery(
        latitude: 6.9271,
        longitude: 79.8612,
        radiusKm: 1.5,
        wasteType: WasteType.general,
        page: 2,
        pageSize: 10,
      ));

      expect(adapter.lastRequest!.method, 'GET');
      expect(adapter.lastRequest!.path, '/bins/public');
      expect(adapter.lastRequest!.headers['Authorization'], 'Bearer citizen-test-token');
      expect(adapter.lastRequest!.queryParameters, {
        'latitude': 6.9271,
        'longitude': 79.8612,
        'radiusKm': 1.5,
        'wasteType': 'General',
        'page': 2,
        'pageSize': 10,
      });
      expect(result.totalCount, 11);
    });

    test('uses the public detail route for the requested bin ID', () async {
      adapter.responseBody = jsonEncode(_detail(isCollectionScheduled: true));

      final result = await api.getPublicWasteBin('68e0d4a4-d6a7-4a4b-8b84-2a8d89f10d11');

      expect(adapter.lastRequest!.method, 'GET');
      expect(adapter.lastRequest!.path, '/bins/public/68e0d4a4-d6a7-4a4b-8b84-2a8d89f10d11');
      expect(result.isCollectionScheduled, isTrue);
    });

    test('propagates public endpoint failures through ApiException', () async {
      adapter.statusCode = 404;
      adapter.responseBody = jsonEncode({'title': 'Not Found', 'status': 404, 'detail': 'Bin not found.'});

      expect(
        () => api.getPublicWasteBin('68e0d4a4-d6a7-4a4b-8b84-2a8d89f10d11'),
        throwsA(isA<ApiException>().having((error) => error.statusCode, 'statusCode', 404)),
      );
    });
  });

  test('Riverpod list and detail providers delegate through the repository with explicit keys', () async {
    final list = PagedPublicWasteBinsModel.fromJson({
      'items': [_listItem()],
      'page': 1,
      'pageSize': 20,
      'totalCount': 1,
      'totalPages': 1,
    });
    final detail = PublicWasteBinDetailModel.fromJson(_detail());
    final api = _FakePublicWasteBinsApi(listResult: list, detailResult: detail);
    final container = ProviderContainer(overrides: [
      publicWasteBinsRepositoryProvider.overrideWithValue(PublicWasteBinsRepository(api: api)),
    ]);
    addTearDown(container.dispose);

    const query = PublicWasteBinQuery(wasteType: WasteType.organic);
    final listResult = await container.read(publicWasteBinsProvider(query).future);
    final detailResult = await container.read(publicWasteBinDetailProvider(detail.id).future);

    expect(listResult.items.single.id, detail.id);
    expect(detailResult.binCode, 'BIN-001');
    expect(api.receivedQuery, query);
    expect(api.receivedBinId, detail.id);
  });
}
