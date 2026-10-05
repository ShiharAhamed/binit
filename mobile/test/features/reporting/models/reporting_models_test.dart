import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/reporting/models/create_waste_report_request.dart';
import 'package:mobile/features/reporting/models/report_attachment_model.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_priority.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';

void main() {
  group('WasteType Enum Tests', () {
    test('serializes to string values matching ASP.NET enum exactly', () {
      expect(WasteType.general.toJsonValue(), 'General');
      expect(WasteType.organic.toJsonValue(), 'Organic');
      expect(WasteType.recyclable.toJsonValue(), 'Recyclable');
      expect(WasteType.hazardous.toJsonValue(), 'Hazardous');
      expect(WasteType.bulky.toJsonValue(), 'Bulky');
      expect(WasteType.other.toJsonValue(), 'Other');
    });

    test('deserializes from case-insensitive string values', () {
      expect(WasteType.fromJsonValue('General'), WasteType.general);
      expect(WasteType.fromJsonValue('general'), WasteType.general);
      expect(WasteType.fromJsonValue('ORGANIC'), WasteType.organic);
      expect(WasteType.fromJsonValue('Recyclable'), WasteType.recyclable);
      expect(WasteType.fromJsonValue('hazardous'), WasteType.hazardous);
      expect(WasteType.fromJsonValue('Bulky'), WasteType.bulky);
      expect(WasteType.fromJsonValue('other'), WasteType.other);
    });

    test('throws FormatException on unknown or invalid WasteType value', () {
      expect(() => WasteType.fromJsonValue('unknown_category'), throwsA(isA<FormatException>()));
      expect(() => WasteType.fromJsonValue(''), throwsA(isA<FormatException>()));
      expect(() => WasteType.fromJsonValue('   '), throwsA(isA<FormatException>()));
    });

    test('displayName provides friendly category labels', () {
      expect(WasteType.general.displayName, 'General Waste');
      expect(WasteType.organic.displayName, 'Organic Waste');
      expect(WasteType.recyclable.displayName, 'Recyclable Waste');
      expect(WasteType.hazardous.displayName, 'Hazardous Waste');
      expect(WasteType.bulky.displayName, 'Bulky Waste');
      expect(WasteType.other.displayName, 'Other Waste');
    });
  });

  group('WasteReportStatus Enum Tests', () {
    test('uses citizen-facing labels for every authoritative report status', () {
      expect(WasteReportStatus.values.map((status) => status.displayName).toList(), [
        'Submitted', 'Under Review', 'Verified', 'Rejected',
        'Scheduled', 'In Progress', 'Resolved', 'Cancelled',
      ]);
    });
    test('serializes to string values matching ASP.NET enum exactly', () {
      expect(WasteReportStatus.submitted.toJsonValue(), 'Submitted');
      expect(WasteReportStatus.underReview.toJsonValue(), 'UnderReview');
      expect(WasteReportStatus.verified.toJsonValue(), 'Verified');
      expect(WasteReportStatus.rejected.toJsonValue(), 'Rejected');
      expect(WasteReportStatus.scheduled.toJsonValue(), 'Scheduled');
      expect(WasteReportStatus.inProgress.toJsonValue(), 'InProgress');
      expect(WasteReportStatus.resolved.toJsonValue(), 'Resolved');
      expect(WasteReportStatus.cancelled.toJsonValue(), 'Cancelled');
    });

    test('deserializes from string values correctly', () {
      expect(WasteReportStatus.fromJsonValue('Submitted'), WasteReportStatus.submitted);
      expect(WasteReportStatus.fromJsonValue('submitted'), WasteReportStatus.submitted);
      expect(WasteReportStatus.fromJsonValue('UnderReview'), WasteReportStatus.underReview);
      expect(WasteReportStatus.fromJsonValue('verified'), WasteReportStatus.verified);
      expect(WasteReportStatus.fromJsonValue('Rejected'), WasteReportStatus.rejected);
      expect(WasteReportStatus.fromJsonValue('Scheduled'), WasteReportStatus.scheduled);
      expect(WasteReportStatus.fromJsonValue('InProgress'), WasteReportStatus.inProgress);
      expect(WasteReportStatus.fromJsonValue('Resolved'), WasteReportStatus.resolved);
      expect(WasteReportStatus.fromJsonValue('Cancelled'), WasteReportStatus.cancelled);
    });

    test('throws FormatException on unknown or invalid WasteReportStatus value', () {
      expect(() => WasteReportStatus.fromJsonValue('invalid'), throwsA(isA<FormatException>()));
      expect(() => WasteReportStatus.fromJsonValue(''), throwsA(isA<FormatException>()));
      expect(() => WasteReportStatus.fromJsonValue('   '), throwsA(isA<FormatException>()));
    });

    test('helper properties reflect status accurately', () {
      expect(WasteReportStatus.submitted.isSubmitted, isTrue);
      expect(WasteReportStatus.submitted.isUnderReview, isFalse);
      expect(WasteReportStatus.underReview.isUnderReview, isTrue);
      expect(WasteReportStatus.verified.isVerified, isTrue);
      expect(WasteReportStatus.rejected.isRejected, isTrue);
      expect(WasteReportStatus.scheduled.isScheduled, isTrue);
      expect(WasteReportStatus.inProgress.isInProgress, isTrue);
      expect(WasteReportStatus.resolved.isResolved, isTrue);
      expect(WasteReportStatus.cancelled.isCancelled, isTrue);
    });
  });

  group('WasteReportPriority Enum Tests', () {
    test('serializes to string values matching ASP.NET enum exactly', () {
      expect(WasteReportPriority.low.toJsonValue(), 'Low');
      expect(WasteReportPriority.medium.toJsonValue(), 'Medium');
      expect(WasteReportPriority.high.toJsonValue(), 'High');
      expect(WasteReportPriority.urgent.toJsonValue(), 'Urgent');
    });

    test('handles nullable priority parsing correctly', () {
      expect(WasteReportPriority.fromJsonValue(null), isNull);
      expect(WasteReportPriority.fromJsonValue(''), isNull);
      expect(WasteReportPriority.fromJsonValue('   '), isNull);
      expect(WasteReportPriority.fromJsonValue('Low'), WasteReportPriority.low);
      expect(WasteReportPriority.fromJsonValue('medium'), WasteReportPriority.medium);
      expect(WasteReportPriority.fromJsonValue('HIGH'), WasteReportPriority.high);
      expect(WasteReportPriority.fromJsonValue('Urgent'), WasteReportPriority.urgent);
    });

    test('throws FormatException on unknown non-null WasteReportPriority value', () {
      expect(() => WasteReportPriority.fromJsonValue('invalid_priority'), throwsA(isA<FormatException>()));
      expect(() => WasteReportPriority.fromJsonValue('Critical'), throwsA(isA<FormatException>()));
    });
  });

  group('CreateWasteReportRequest Model Tests', () {
    test('serializes to JSON matching ASP.NET contract with string enums and no forbidden fields', () {
      const request = CreateWasteReportRequest(
        description: 'Large pile of rubbish blocking sidewalk',
        wasteType: WasteType.general,
        latitude: 6.9271,
        longitude: 79.8612,
        addressText: '123 Galle Road, Colombo',
      );

      final json = request.toJson();

      expect(json['description'], 'Large pile of rubbish blocking sidewalk');
      expect(json['wasteType'], 'General'); // String, NOT integer 0
      expect(json['latitude'], 6.9271);
      expect(json['longitude'], 79.8612);
      expect(json['addressText'], '123 Galle Road, Colombo');

      // Crucial: Client must not submit authoritative fields
      expect(json.containsKey('citizenId'), isFalse);
      expect(json.containsKey('status'), isFalse);
      expect(json.containsKey('priority'), isFalse);
      expect(json.containsKey('verifiedByUserId'), isFalse);
      expect(json.containsKey('verifiedAt'), isFalse);
    });

    test('omits addressText when null or empty', () {
      const reqNullAddress = CreateWasteReportRequest(
        description: 'Trash pile',
        wasteType: WasteType.organic,
        latitude: 6.9000,
        longitude: 79.8500,
        addressText: null,
      );
      expect(reqNullAddress.toJson().containsKey('addressText'), isFalse);

      const reqEmptyAddress = CreateWasteReportRequest(
        description: 'Trash pile',
        wasteType: WasteType.recyclable,
        latitude: 6.9000,
        longitude: 79.8500,
        addressText: '   ',
      );
      expect(reqEmptyAddress.toJson().containsKey('addressText'), isFalse);
    });

    test('deserializes from JSON correctly', () {
      final json = {
        'description': 'Plastic bottles on beach',
        'wasteType': 'Recyclable',
        'latitude': 6.9100,
        'longitude': 79.8600,
        'addressText': 'Marine Drive',
      };

      final req = CreateWasteReportRequest.fromJson(json);

      expect(req.description, 'Plastic bottles on beach');
      expect(req.wasteType, WasteType.recyclable);
      expect(req.latitude, 6.9100);
      expect(req.longitude, 79.8600);
      expect(req.addressText, 'Marine Drive');
    });
  });

  group('ReportAttachmentModel Tests', () {
    test('deserializes accurately without exposing internal StorageKey', () {
      final json = {
        'id': 'att-456',
        'wasteReportId': 'rep-123',
        'fileUrl': 'https://supabase.local/storage/v1/object/sign/waste-report-attachments/rep-123/att-456.jpg?token=secret',
        'fileType': 'image/jpeg',
        'createdAt': '2026-09-16T10:30:00.000Z',
      };

      final attachment = ReportAttachmentModel.fromJson(json);

      expect(attachment.id, 'att-456');
      expect(attachment.wasteReportId, 'rep-123');
      expect(attachment.fileUrl, contains('https://supabase.local'));
      expect(attachment.fileType, 'image/jpeg');
      expect(attachment.createdAt, DateTime.parse('2026-09-16T10:30:00.000Z').toUtc());
    });

    test('serializes to JSON correctly', () {
      final created = DateTime.parse('2026-09-16T10:30:00.000Z');
      final attachment = ReportAttachmentModel(
        id: 'att-1',
        wasteReportId: 'rep-1',
        fileUrl: 'https://cdn.example.com/photo.jpg',
        fileType: 'image/png',
        createdAt: created,
      );

      final json = attachment.toJson();

      expect(json['id'], 'att-1');
      expect(json['wasteReportId'], 'rep-1');
      expect(json['fileUrl'], 'https://cdn.example.com/photo.jpg');
      expect(json['fileType'], 'image/png');
      expect(json['createdAt'], '2026-09-16T10:30:00.000Z');
      expect(json.containsKey('storageKey'), isFalse);
    });
    test('throws FormatException when required fields are missing or invalid', () {
      final validJson = {
        'id': 'att-456',
        'wasteReportId': 'rep-123',
        'fileUrl': 'https://supabase.local/sign/photo.jpg',
        'fileType': 'image/jpeg',
        'createdAt': '2026-09-16T10:30:00.000Z',
      };

      // Missing id
      expect(
        () => ReportAttachmentModel.fromJson({...validJson}..remove('id')),
        throwsA(isA<FormatException>()),
      );
      // Empty id
      expect(
        () => ReportAttachmentModel.fromJson({...validJson, 'id': ''}),
        throwsA(isA<FormatException>()),
      );
      // Missing wasteReportId
      expect(
        () => ReportAttachmentModel.fromJson({...validJson}..remove('wasteReportId')),
        throwsA(isA<FormatException>()),
      );
      // Missing fileUrl
      expect(
        () => ReportAttachmentModel.fromJson({...validJson}..remove('fileUrl')),
        throwsA(isA<FormatException>()),
      );
      // Missing fileType
      expect(
        () => ReportAttachmentModel.fromJson({...validJson}..remove('fileType')),
        throwsA(isA<FormatException>()),
      );
      // Missing createdAt
      expect(
        () => ReportAttachmentModel.fromJson({...validJson}..remove('createdAt')),
        throwsA(isA<FormatException>()),
      );
      // Invalid createdAt
      expect(
        () => ReportAttachmentModel.fromJson({...validJson, 'createdAt': 'not-a-date'}),
        throwsA(isA<FormatException>()),
      );
    });
  });

  group('WasteReportDetailModel Tests', () {
    test('deserializes complete report detail with attachments and null priority', () {
      final json = {
        'id': 'rep-777',
        'citizenId': 'cit-888',
        'citizenName': 'Kasun Perera',
        'description': 'Overflowing bin near school',
        'wasteType': 'General',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'addressText': 'School Road, Colombo 03',
        'status': 'Submitted',
        'priority': null,
        'verifiedByUserId': null,
        'verifiedByUserName': null,
        'verifiedAt': null,
        'attachments': [
          {
            'id': 'att-101',
            'wasteReportId': 'rep-777',
            'fileUrl': 'https://storage.supabase.co/sign/photo1.jpg?token=xyz',
            'fileType': 'image/jpeg',
            'createdAt': '2026-09-16T11:00:00.000Z',
          }
        ],
        'createdAt': '2026-09-16T10:55:00.000Z',
        'updatedAt': null,
      };

      final report = WasteReportDetailModel.fromJson(json);

      expect(report.id, 'rep-777');
      expect(report.citizenId, 'cit-888');
      expect(report.citizenName, 'Kasun Perera');
      expect(report.description, 'Overflowing bin near school');
      expect(report.wasteType, WasteType.general);
      expect(report.latitude, 6.9271);
      expect(report.longitude, 79.8612);
      expect(report.addressText, 'School Road, Colombo 03');
      expect(report.status, WasteReportStatus.submitted);
      expect(report.priority, isNull);
      expect(report.verifiedByUserId, isNull);
      expect(report.verifiedByUserName, isNull);
      expect(report.verifiedAt, isNull);
      expect(report.attachments.length, 1);
      expect(report.attachments.first.id, 'att-101');
      expect(report.createdAt, DateTime.parse('2026-09-16T10:55:00.000Z').toUtc());
      expect(report.updatedAt, isNull);
      expect(report.reportReference, isNull);
    });

    test('reads an optional reportReference on a normal successful create response', () {
      final report = WasteReportDetailModel.fromJson({
        'id': 'c17add4f-79f3-4a0a-a9e0-150fde7d7827',
        'reportReference': 'C17ADD4F',
        'citizenId': 'citizen-1',
        'citizenName': 'Citizen',
        'description': 'Waste by the road',
        'wasteType': 'General',
        'latitude': 6.9,
        'longitude': 79.8,
        'status': 'Submitted',
        'attachments': <dynamic>[],
        'createdAt': '2026-10-03T08:00:00Z',
      });
      expect(report.reportReference, 'C17ADD4F');
      expect(report.id, 'c17add4f-79f3-4a0a-a9e0-150fde7d7827');
      expect(report.toJson()['reportReference'], 'C17ADD4F');
    });

    test('deserializes scheduled report with non-null priority and verification info', () {
      final json = {
        'id': 'rep-999',
        'citizenId': 'cit-888',
        'citizenName': 'Kasun Perera',
        'description': 'Hazardous hospital waste dumped',
        'wasteType': 'Hazardous',
        'latitude': 6.9300,
        'longitude': 79.8700,
        'addressText': null,
        'status': 'Scheduled',
        'priority': 'Urgent',
        'verifiedByUserId': 'officer-333',
        'verifiedByUserName': 'Officer Silva',
        'verifiedAt': '2026-09-16T12:00:00.000Z',
        'attachments': <dynamic>[],
        'createdAt': '2026-09-16T11:00:00.000Z',
        'updatedAt': '2026-09-16T12:00:00.000Z',
      };

      final report = WasteReportDetailModel.fromJson(json);

      expect(report.id, 'rep-999');
      expect(report.wasteType, WasteType.hazardous);
      expect(report.status, WasteReportStatus.scheduled);
      expect(report.priority, WasteReportPriority.urgent);
      expect(report.verifiedByUserId, 'officer-333');
      expect(report.verifiedByUserName, 'Officer Silva');
      expect(report.verifiedAt, DateTime.parse('2026-09-16T12:00:00.000Z').toUtc());
      expect(report.attachments, isEmpty);
      expect(report.updatedAt, DateTime.parse('2026-09-16T12:00:00.000Z').toUtc());
    });

    test('throws FormatException when required fields are missing or invalid in WasteReportDetailModel', () {
      final validJson = {
        'id': 'rep-1',
        'citizenId': 'cit-1',
        'citizenName': 'Citizen One',
        'description': 'Valid description',
        'wasteType': 'Organic',
        'latitude': 6.9,
        'longitude': 79.8,
        'status': 'Submitted',
        'createdAt': '2026-09-16T10:00:00.000Z',
      };

      // Missing id
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('id')),
        throwsA(isA<FormatException>()),
      );
      // Missing citizenId
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('citizenId')),
        throwsA(isA<FormatException>()),
      );
      // Missing citizenName
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('citizenName')),
        throwsA(isA<FormatException>()),
      );
      // Missing description
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('description')),
        throwsA(isA<FormatException>()),
      );
      // Missing wasteType
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('wasteType')),
        throwsA(isA<FormatException>()),
      );
      // Missing latitude
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('latitude')),
        throwsA(isA<FormatException>()),
      );
      // Missing longitude
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('longitude')),
        throwsA(isA<FormatException>()),
      );
      // Missing status
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('status')),
        throwsA(isA<FormatException>()),
      );
      // Missing createdAt
      expect(
        () => WasteReportDetailModel.fromJson({...validJson}..remove('createdAt')),
        throwsA(isA<FormatException>()),
      );
      // Invalid createdAt
      expect(
        () => WasteReportDetailModel.fromJson({...validJson, 'createdAt': 'invalid-date'}),
        throwsA(isA<FormatException>()),
      );
    });
  });
}
