import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/operations/models/create_operational_issue_request.dart';
import 'package:mobile/features/operations/models/operational_issue_model.dart';

void main() {
  group('OperationalIssueType Enum Tests', () {
    test('serializes and deserializes all 6 issue types correctly', () {
      final types = [
        OperationalIssueType.vehicleProblem,
        OperationalIssueType.roadOrAccessIssue,
        OperationalIssueType.equipmentProblem,
        OperationalIssueType.safetyConcern,
        OperationalIssueType.operationalDelay,
        OperationalIssueType.other,
      ];

      final expectedStrings = [
        'VehicleProblem',
        'RoadOrAccessIssue',
        'EquipmentProblem',
        'SafetyConcern',
        'OperationalDelay',
        'Other',
      ];

      for (int i = 0; i < types.length; i++) {
        expect(types[i].toJsonValue(), equals(expectedStrings[i]));
        expect(OperationalIssueType.fromJsonValue(expectedStrings[i]), equals(types[i]));
        expect(OperationalIssueType.fromJsonValue(expectedStrings[i].toLowerCase()), equals(types[i]));
        expect(OperationalIssueType.fromJsonValue(i), equals(types[i]));
      }
    });

    test('throws FormatException for invalid type value or type', () {
      expect(() => OperationalIssueType.fromJsonValue('NonExistentType'), throwsFormatException);
      expect(() => OperationalIssueType.fromJsonValue(999), throwsFormatException);
      expect(() => OperationalIssueType.fromJsonValue(true), throwsFormatException);
    });
  });

  group('OperationalIssueStatus Enum Tests', () {
    test('serializes and deserializes all 3 statuses correctly', () {
      final statuses = [
        OperationalIssueStatus.reported,
        OperationalIssueStatus.inReview,
        OperationalIssueStatus.resolved,
      ];

      final expectedStrings = [
        'Reported',
        'InReview',
        'Resolved',
      ];

      for (int i = 0; i < statuses.length; i++) {
        expect(statuses[i].toJsonValue(), equals(expectedStrings[i]));
        expect(OperationalIssueStatus.fromJsonValue(expectedStrings[i]), equals(statuses[i]));
        expect(OperationalIssueStatus.fromJsonValue(expectedStrings[i].toLowerCase()), equals(statuses[i]));
        expect(OperationalIssueStatus.fromJsonValue(i), equals(statuses[i]));
      }

      expect(OperationalIssueStatus.reported.isReported, isTrue);
      expect(OperationalIssueStatus.reported.isInReview, isFalse);
      expect(OperationalIssueStatus.reported.isResolved, isFalse);

      expect(OperationalIssueStatus.inReview.isReported, isFalse);
      expect(OperationalIssueStatus.inReview.isInReview, isTrue);
      expect(OperationalIssueStatus.inReview.isResolved, isFalse);

      expect(OperationalIssueStatus.resolved.isReported, isFalse);
      expect(OperationalIssueStatus.resolved.isInReview, isFalse);
      expect(OperationalIssueStatus.resolved.isResolved, isTrue);
    });

    test('throws FormatException for invalid status value', () {
      expect(() => OperationalIssueStatus.fromJsonValue('InvalidStatus'), throwsFormatException);
      expect(() => OperationalIssueStatus.fromJsonValue(-1), throwsFormatException);
      expect(() => OperationalIssueStatus.fromJsonValue(42), throwsFormatException);
      expect(() => OperationalIssueStatus.fromJsonValue(null), throwsFormatException);
    });
  });

  group('OperationalIssueSummaryModel Tests', () {
    test('parses json with location and optional fields correctly', () {
      final json = {
        'id': 'issue-101',
        'driverId': 'driver-202',
        'driverName': 'Samantha Perera',
        'issueType': 'VehicleProblem',
        'title': 'Engine overheating during route 4',
        'status': 'Reported',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'createdAt': '2026-10-15T08:30:00.000Z',
        'updatedAt': '2026-10-15T09:00:00.000Z',
      };

      final model = OperationalIssueSummaryModel.fromJson(json);

      expect(model.id, equals('issue-101'));
      expect(model.driverId, equals('driver-202'));
      expect(model.driverName, equals('Samantha Perera'));
      expect(model.issueType, equals(OperationalIssueType.vehicleProblem));
      expect(model.title, equals('Engine overheating during route 4'));
      expect(model.status, equals(OperationalIssueStatus.reported));
      expect(model.latitude, equals(6.9271));
      expect(model.longitude, equals(79.8612));
      expect(model.hasLocation, isTrue);
      expect(model.createdAt, equals(DateTime.parse('2026-10-15T08:30:00.000Z')));
      expect(model.updatedAt, equals(DateTime.parse('2026-10-15T09:00:00.000Z')));

      final serialized = model.toJson();
      expect(serialized['id'], equals('issue-101'));
      expect(serialized['issueType'], equals('VehicleProblem'));
      expect(serialized['status'], equals('Reported'));
      expect(serialized['latitude'], equals(6.9271));
      expect(serialized['longitude'], equals(79.8612));
    });

    test('parses json without location and without updatedAt correctly', () {
      final json = {
        'id': 'issue-102',
        'driverId': 'driver-202',
        'issueType': 'OperationalDelay',
        'title': 'Traffic congestion at central junction',
        'status': 'InReview',
        'createdAt': '2026-10-15T10:00:00.000Z',
      };

      final model = OperationalIssueSummaryModel.fromJson(json);

      expect(model.id, equals('issue-102'));
      expect(model.driverName, isNull);
      expect(model.latitude, isNull);
      expect(model.longitude, isNull);
      expect(model.hasLocation, isFalse);
      expect(model.updatedAt, isNull);
    });

    test('throws FormatException if required fields are missing', () {
      expect(() => OperationalIssueSummaryModel.fromJson({}), throwsFormatException);
      expect(() => OperationalIssueSummaryModel.fromJson({'id': '1'}), throwsFormatException);
    });
  });

  group('OperationalIssueDetailModel Tests', () {
    test('parses fully resolved operational issue with all fields', () {
      final json = {
        'id': 'detail-001',
        'driverId': 'driver-111',
        'driverName': 'Samantha Perera',
        'issueType': 'SafetyConcern',
        'title': 'Exposed electrical wires near bin cluster',
        'description': 'Live wire hanging low over the bin access path. Could cause electric shock.',
        'latitude': 6.9319,
        'longitude': 79.8478,
        'locationDescription': 'Near Temple Road junction behind the bus stop',
        'status': 'Resolved',
        'resolutionNote': 'Depot dispatched technician and CEB isolated the wire safely.',
        'resolvedAt': '2026-10-15T12:00:00.000Z',
        'resolvedByUserId': 'staff-999',
        'resolvedByUserName': 'Officer Bandara',
        'createdAt': '2026-10-15T09:00:00.000Z',
        'updatedAt': '2026-10-15T12:05:00.000Z',
      };

      final detail = OperationalIssueDetailModel.fromJson(json);

      expect(detail.id, equals('detail-001'));
      expect(detail.driverId, equals('driver-111'));
      expect(detail.driverName, equals('Samantha Perera'));
      expect(detail.issueType, equals(OperationalIssueType.safetyConcern));
      expect(detail.title, equals('Exposed electrical wires near bin cluster'));
      expect(detail.description, contains('Live wire hanging'));
      expect(detail.latitude, equals(6.9319));
      expect(detail.longitude, equals(79.8478));
      expect(detail.locationDescription, equals('Near Temple Road junction behind the bus stop'));
      expect(detail.status, equals(OperationalIssueStatus.resolved));
      expect(detail.resolutionNote, contains('Depot dispatched'));
      expect(detail.resolvedAt, isNotNull);
      expect(detail.resolvedByUserId, equals('staff-999'));
      expect(detail.resolvedByUserName, equals('Officer Bandara'));
      expect(detail.hasLocation, isTrue);

      final serialized = detail.toJson();
      expect(serialized['status'], equals('Resolved'));
      expect(serialized['resolutionNote'], isNotNull);
      expect(serialized['resolvedByUserName'], equals('Officer Bandara'));
    });

    test('parses unresolved issue with nullable fields correctly', () {
      final json = {
        'id': 'detail-002',
        'driverId': 'driver-111',
        'issueType': 'RoadOrAccessIssue',
        'title': 'Road blocked by construction crane',
        'description': 'Access road completely blocked by mobile crane. Cannot reach bins 12 to 15.',
        'status': 'Reported',
        'createdAt': '2026-10-15T07:15:00.000Z',
      };

      final detail = OperationalIssueDetailModel.fromJson(json);

      expect(detail.latitude, isNull);
      expect(detail.longitude, isNull);
      expect(detail.locationDescription, isNull);
      expect(detail.resolutionNote, isNull);
      expect(detail.resolvedAt, isNull);
      expect(detail.resolvedByUserId, isNull);
      expect(detail.resolvedByUserName, isNull);
      expect(detail.hasLocation, isFalse);
    });
  });

  group('PagedOperationalIssuesModel Tests', () {
    test('parses paged result envelope and handles pagination properties', () {
      final json = {
        'items': [
          {
            'id': 'item-1',
            'driverId': 'driver-1',
            'issueType': 'Other',
            'title': 'Special waste disposal needed',
            'status': 'Reported',
            'createdAt': '2026-10-15T08:00:00.000Z',
          }
        ],
        'page': 1,
        'pageSize': 10,
        'totalCount': 25,
        'totalPages': 3,
      };

      final paged = PagedOperationalIssuesModel.fromJson(json);

      expect(paged.items.length, equals(1));
      expect(paged.page, equals(1));
      expect(paged.pageSize, equals(10));
      expect(paged.totalCount, equals(25));
      expect(paged.totalPages, equals(3));
      expect(paged.hasMore, isTrue);
      expect(paged.isEmpty, isFalse);
      expect(paged.isNotEmpty, isTrue);
    });
  });

  group('CreateOperationalIssueRequest Tests', () {
    test('serializes to JSON omitting server-controlled fields', () {
      const request = CreateOperationalIssueRequest(
        issueType: OperationalIssueType.equipmentProblem,
        title: 'Hydraulic lift lever jammed',
        description: 'Rear bin lift mechanism failed to engage when attempting to empty 240L bin.',
        latitude: 6.9271,
        longitude: 79.8612,
        locationDescription: 'Depot maintenance bay 3',
      );

      final json = request.toJson();

      expect(json['issueType'], equals('EquipmentProblem'));
      expect(json['title'], equals('Hydraulic lift lever jammed'));
      expect(json['description'], contains('Rear bin lift mechanism'));
      expect(json['latitude'], equals(6.9271));
      expect(json['longitude'], equals(79.8612));
      expect(json['locationDescription'], equals('Depot maintenance bay 3'));

      // Ensure server-controlled fields are strictly absent
      expect(json.containsKey('driverId'), isFalse);
      expect(json.containsKey('status'), isFalse);
      expect(json.containsKey('resolvedAt'), isFalse);
      expect(json.containsKey('resolutionNote'), isFalse);
      expect(json.containsKey('assignmentId'), isFalse);
      expect(json.containsKey('vehicleId'), isFalse);
      expect(json.containsKey('routeStopId'), isFalse);
    });

    test('serializes to JSON without location correctly', () {
      const request = CreateOperationalIssueRequest(
        issueType: OperationalIssueType.operationalDelay,
        title: 'Heavy rain delay in sector B',
        description: 'Severe weather causing 45-minute delay on collection schedule.',
      );

      final json = request.toJson();

      expect(json['issueType'], equals('OperationalDelay'));
      expect(json.containsKey('latitude'), isFalse);
      expect(json.containsKey('longitude'), isFalse);
      expect(json.containsKey('locationDescription'), isFalse);
    });
  });
}
