import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/reporting/models/paged_waste_reports_model.dart';
import 'package:mobile/features/reporting/models/waste_report_list_item_model.dart';
import 'package:mobile/features/reporting/models/waste_report_priority.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';

void main() {
  group('WasteReportListItemModel Tests', () {
    final validJson = {
      'id': '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      'description': 'Large garbage heap overflowing near bus stand',
      'wasteType': 'General',
      'status': 'Submitted',
      'priority': 'High',
      'addressText': 'Main Street, Pettah',
      'latitude': 6.9271,
      'longitude': 79.8612,
      'citizenId': '1fa85f64-5717-4562-b3fc-2c963f66afa1',
      'citizenName': 'Kamal Perera',
      'createdAt': '2026-09-15T08:30:00.000Z',
      'updatedAt': '2026-09-15T09:00:00.000Z',
    };

    test('deserializes valid complete JSON matching ASP.NET WasteReportSummaryDto', () {
      final model = WasteReportListItemModel.fromJson(validJson);

      expect(model.id, '3fa85f64-5717-4562-b3fc-2c963f66afa6');
      expect(model.description, 'Large garbage heap overflowing near bus stand');
      expect(model.wasteType, WasteType.general);
      expect(model.status, WasteReportStatus.submitted);
      expect(model.priority, WasteReportPriority.high);
      expect(model.addressText, 'Main Street, Pettah');
      expect(model.latitude, 6.9271);
      expect(model.longitude, 79.8612);
      expect(model.citizenId, '1fa85f64-5717-4562-b3fc-2c963f66afa1');
      expect(model.citizenName, 'Kamal Perera');
      expect(model.createdAt, DateTime.parse('2026-09-15T08:30:00.000Z').toUtc());
      expect(model.updatedAt, DateTime.parse('2026-09-15T09:00:00.000Z').toUtc());
      expect(model.reportReference, isNull);
    });

    test('reads optional backend reportReference without replacing the authoritative ID', () {
      final model = WasteReportListItemModel.fromJson({...validJson, 'reportReference': '3FA85F64'});
      expect(model.reportReference, '3FA85F64');
      expect(model.id, validJson['id']);
      expect(model.toJson()['reportReference'], '3FA85F64');
    });

    test('deserializes with optional and nullable fields as null (Citizen scope response)', () {
      final citizenScopeJson = {
        'id': '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        'description': 'Recyclables gathered for collection',
        'wasteType': 'Recyclable',
        'status': 'UnderReview',
        'priority': null,
        'addressText': null,
        'latitude': 6.9271,
        'longitude': 79.8612,
        'citizenId': null,
        'citizenName': null,
        'createdAt': '2026-09-16T10:00:00.000Z',
        'updatedAt': null,
      };

      final model = WasteReportListItemModel.fromJson(citizenScopeJson);

      expect(model.priority, isNull);
      expect(model.addressText, isNull);
      expect(model.citizenId, isNull);
      expect(model.citizenName, isNull);
      expect(model.updatedAt, isNull);
      expect(model.status, WasteReportStatus.underReview);
      expect(model.wasteType, WasteType.recyclable);
      expect(model.attachmentCount, 0);
    });

    test('deserializes attachmentCount from json or attachments list', () {
      final jsonWithCount = Map<String, dynamic>.from(validJson)..['attachmentCount'] = 3;
      final modelWithCount = WasteReportListItemModel.fromJson(jsonWithCount);
      expect(modelWithCount.attachmentCount, 3);

      final jsonWithList = Map<String, dynamic>.from(validJson)..['attachments'] = ['a', 'b'];
      final modelWithList = WasteReportListItemModel.fromJson(jsonWithList);
      expect(modelWithList.attachmentCount, 2);

      final modelDefault = WasteReportListItemModel.fromJson(validJson);
      expect(modelDefault.attachmentCount, 0);
    });

    test('serializes to JSON correctly', () {
      final model = WasteReportListItemModel.fromJson(validJson);
      final json = model.toJson();

      expect(json['id'], '3fa85f64-5717-4562-b3fc-2c963f66afa6');
      expect(json['description'], 'Large garbage heap overflowing near bus stand');
      expect(json['wasteType'], 'General');
      expect(json['status'], 'Submitted');
      expect(json['priority'], 'High');
      expect(json['addressText'], 'Main Street, Pettah');
      expect(json['latitude'], 6.9271);
      expect(json['longitude'], 79.8612);
    });

    test('throws FormatException on missing or empty required id', () {
      expect(
        () => WasteReportListItemModel.fromJson({...validJson, 'id': ''}),
        throwsA(isA<FormatException>()),
      );
      expect(
        () => WasteReportListItemModel.fromJson({...validJson}..remove('id')),
        throwsA(isA<FormatException>()),
      );
    });

    test('throws FormatException on missing description', () {
      expect(
        () => WasteReportListItemModel.fromJson({...validJson, 'description': ''}),
        throwsA(isA<FormatException>()),
      );
      expect(
        () => WasteReportListItemModel.fromJson({...validJson}..remove('description')),
        throwsA(isA<FormatException>()),
      );
    });

    test('throws FormatException on missing or invalid wasteType', () {
      expect(
        () => WasteReportListItemModel.fromJson({...validJson}..remove('wasteType')),
        throwsA(isA<FormatException>()),
      );
      expect(
        () => WasteReportListItemModel.fromJson({...validJson, 'wasteType': 'UnknownCategory'}),
        throwsA(isA<FormatException>()),
      );
    });

    test('throws FormatException on missing or invalid status', () {
      expect(
        () => WasteReportListItemModel.fromJson({...validJson}..remove('status')),
        throwsA(isA<FormatException>()),
      );
      expect(
        () => WasteReportListItemModel.fromJson({...validJson, 'status': 'UnknownStatus'}),
        throwsA(isA<FormatException>()),
      );
    });

    test('throws FormatException on invalid coordinate types', () {
      expect(
        () => WasteReportListItemModel.fromJson({...validJson, 'latitude': 'invalid'}),
        throwsA(isA<FormatException>()),
      );
      expect(
        () => WasteReportListItemModel.fromJson({...validJson, 'longitude': null}),
        throwsA(isA<FormatException>()),
      );
    });

    test('throws FormatException on invalid createdAt', () {
      expect(
        () => WasteReportListItemModel.fromJson({...validJson, 'createdAt': 'not-a-date'}),
        throwsA(isA<FormatException>()),
      );
    });

    test('value equality holds for identical properties', () {
      final model1 = WasteReportListItemModel.fromJson(validJson);
      final model2 = WasteReportListItemModel.fromJson(validJson);

      expect(model1, equals(model2));
      expect(model1.hashCode, equals(model2.hashCode));
    });
  });

  group('PagedWasteReportsModel Tests', () {
    final validPagedJson = {
      'items': [
        {
          'id': '3fa85f64-5717-4562-b3fc-2c963f66afa6',
          'description': 'Organic waste pile',
          'wasteType': 'Organic',
          'status': 'Submitted',
          'priority': null,
          'addressText': 'Market Lane',
          'latitude': 6.9271,
          'longitude': 79.8612,
          'citizenId': null,
          'citizenName': null,
          'createdAt': '2026-09-16T08:00:00.000Z',
          'updatedAt': null,
        },
      ],
      'page': 1,
      'pageSize': 20,
      'totalCount': 45,
      'totalPages': 3,
    };

    test('deserializes valid paged response and calculates hasMore correctly', () {
      final paged = PagedWasteReportsModel.fromJson(validPagedJson);

      expect(paged.items.length, 1);
      expect(paged.items.first.description, 'Organic waste pile');
      expect(paged.page, 1);
      expect(paged.pageSize, 20);
      expect(paged.totalCount, 45);
      expect(paged.totalPages, 3);
      expect(paged.hasMore, isTrue);
      expect(paged.isEmpty, isFalse);
      expect(paged.isNotEmpty, isTrue);
    });

    test('hasMore is false on last page', () {
      final lastPageJson = {
        ...validPagedJson,
        'page': 3,
        'totalPages': 3,
      };

      final paged = PagedWasteReportsModel.fromJson(lastPageJson);
      expect(paged.hasMore, isFalse);
    });

    test('handles empty items list correctly', () {
      final emptyJson = {
        'items': <dynamic>[],
        'page': 1,
        'pageSize': 20,
        'totalCount': 0,
        'totalPages': 0,
      };

      final paged = PagedWasteReportsModel.fromJson(emptyJson);
      expect(paged.items, isEmpty);
      expect(paged.isEmpty, isTrue);
      expect(paged.isNotEmpty, isFalse);
      expect(paged.hasMore, isFalse);
    });

    test('throws FormatException on malformed items', () {
      expect(
        () => PagedWasteReportsModel.fromJson({...validPagedJson, 'items': 'not-a-list'}),
        throwsA(isA<FormatException>()),
      );
      expect(
        () => PagedWasteReportsModel.fromJson({
          ...validPagedJson,
          'items': ['not-a-map'],
        }),
        throwsA(isA<FormatException>()),
      );
    });

    test('throws FormatException on missing pagination metadata', () {
      expect(
        () => PagedWasteReportsModel.fromJson({...validPagedJson}..remove('page')),
        throwsA(isA<FormatException>()),
      );
      expect(
        () => PagedWasteReportsModel.fromJson({...validPagedJson}..remove('totalCount')),
        throwsA(isA<FormatException>()),
      );
    });

    test('serializes to JSON correctly', () {
      final paged = PagedWasteReportsModel.fromJson(validPagedJson);
      final json = paged.toJson();

      expect(json['page'], 1);
      expect(json['pageSize'], 20);
      expect(json['totalCount'], 45);
      expect(json['totalPages'], 3);
      expect((json['items'] as List).length, 1);
    });
  });
}
