import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/reporting/models/update_waste_report_request.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';

void main() {
  group('UpdateWasteReportRequest Tests', () {
    test('serializes all fields when all are provided', () {
      const request = UpdateWasteReportRequest(
        description: 'Updated overflowing garbage description',
        wasteType: WasteType.recyclable,
        latitude: 6.9271,
        longitude: 79.8612,
        addressText: '45 Galle Road, Colombo',
      );

      final json = request.toJson();

      expect(json, {
        'description': 'Updated overflowing garbage description',
        'wasteType': 'Recyclable',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'addressText': '45 Galle Road, Colombo',
      });
    });

    test('omits null fields from serialized JSON', () {
      const request = UpdateWasteReportRequest(
        description: 'Only updating description here',
      );

      final json = request.toJson();

      expect(json, {
        'description': 'Only updating description here',
      });
      expect(json.containsKey('wasteType'), isFalse);
      expect(json.containsKey('latitude'), isFalse);
      expect(json.containsKey('longitude'), isFalse);
      expect(json.containsKey('addressText'), isFalse);
    });

    test('serializes empty address string as "" to support clearing address on backend', () {
      const request = UpdateWasteReportRequest(
        addressText: '',
      );

      final json = request.toJson();

      expect(json, {
        'addressText': '',
      });
    });

    test('trims text fields on serialization', () {
      const request = UpdateWasteReportRequest(
        description: '   Padded description text   ',
        addressText: '   Padded address   ',
      );

      final json = request.toJson();

      expect(json['description'], 'Padded description text');
      expect(json['addressText'], 'Padded address');
    });

    test('isEmpty returns true when all fields are null', () {
      const request = UpdateWasteReportRequest();

      expect(request.isEmpty, isTrue);
      expect(request.isNotEmpty, isFalse);
      expect(request.toJson(), isEmpty);
    });

    test('isEmpty returns false when any field is populated', () {
      expect(const UpdateWasteReportRequest(description: 'test').isEmpty, isFalse);
      expect(const UpdateWasteReportRequest(wasteType: WasteType.organic).isEmpty, isFalse);
      expect(const UpdateWasteReportRequest(latitude: 6.9).isEmpty, isFalse);
      expect(const UpdateWasteReportRequest(longitude: 79.8).isEmpty, isFalse);
      expect(const UpdateWasteReportRequest(addressText: '').isEmpty, isFalse);
      expect(const UpdateWasteReportRequest(addressText: 'loc').isNotEmpty, isTrue);
    });

    test('deserializes from JSON correctly', () {
      final json = {
        'description': 'Test description',
        'wasteType': 'Hazardous',
        'latitude': 6.91234,
        'longitude': 79.85678,
        'addressText': 'Test Address',
      };

      final request = UpdateWasteReportRequest.fromJson(json);

      expect(request.description, 'Test description');
      expect(request.wasteType, WasteType.hazardous);
      expect(request.latitude, 6.91234);
      expect(request.longitude, 79.85678);
      expect(request.addressText, 'Test Address');
    });

    test('deserializes partial JSON correctly', () {
      final json = <String, dynamic>{
        'description': 'Partial update description',
      };

      final request = UpdateWasteReportRequest.fromJson(json);

      expect(request.description, 'Partial update description');
      expect(request.wasteType, isNull);
      expect(request.latitude, isNull);
      expect(request.longitude, isNull);
      expect(request.addressText, isNull);
    });

    test('equality and hashCode work as expected', () {
      const req1 = UpdateWasteReportRequest(
        description: 'Same',
        wasteType: WasteType.general,
        latitude: 6.9,
        longitude: 79.8,
        addressText: 'Same',
      );
      const req2 = UpdateWasteReportRequest(
        description: 'Same',
        wasteType: WasteType.general,
        latitude: 6.9,
        longitude: 79.8,
        addressText: 'Same',
      );
      const req3 = UpdateWasteReportRequest(
        description: 'Different',
      );

      expect(req1, equals(req2));
      expect(req1.hashCode, equals(req2.hashCode));
      expect(req1, isNot(equals(req3)));
    });
  });
}
