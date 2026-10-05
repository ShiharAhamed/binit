import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_report_status_history_model.dart';

void main() {
  group('WasteReportStatusHistoryModel Tests', () {
    final validUtc = DateTime.utc(2026, 9, 16, 10, 30, 0);

    test('parses initial submission history record with null fromStatus and null notes', () {
      final json = {
        'id': 'hist-001',
        'wasteReportId': 'rep-001',
        'fromStatus': null,
        'toStatus': 'Submitted',
        'changedByUserId': 'user-123',
        'changedByUserName': 'Jane Citizen',
        'notes': null,
        'changedAt': '2026-09-16T10:30:00.000Z',
      };

      final model = WasteReportStatusHistoryModel.fromJson(json);

      expect(model.id, 'hist-001');
      expect(model.wasteReportId, 'rep-001');
      expect(model.fromStatus, isNull);
      expect(model.toStatus, WasteReportStatus.submitted);
      expect(model.changedByUserId, 'user-123');
      expect(model.changedByUserName, 'Jane Citizen');
      expect(model.notes, isNull);
      expect(model.changedAt, validUtc);
    });

    test('parses subsequent transition record with populated fromStatus and notes', () {
      final json = {
        'id': 'hist-002',
        'wasteReportId': 'rep-001',
        'fromStatus': 'Submitted',
        'toStatus': 'UnderReview',
        'changedByUserId': 'officer-456',
        'changedByUserName': 'Officer John',
        'notes': 'Starting site inspection',
        'changedAt': '2026-09-16T11:00:00.000Z',
      };

      final model = WasteReportStatusHistoryModel.fromJson(json);

      expect(model.id, 'hist-002');
      expect(model.wasteReportId, 'rep-001');
      expect(model.fromStatus, WasteReportStatus.submitted);
      expect(model.toStatus, WasteReportStatus.underReview);
      expect(model.changedByUserId, 'officer-456');
      expect(model.changedByUserName, 'Officer John');
      expect(model.notes, 'Starting site inspection');
      expect(model.changedAt, DateTime.utc(2026, 9, 16, 11, 0, 0));
    });

    test('parses rejection transition with rejection reason in notes', () {
      final json = {
        'id': 'hist-003',
        'wasteReportId': 'rep-001',
        'fromStatus': 'UnderReview',
        'toStatus': 'Rejected',
        'changedByUserId': 'officer-456',
        'changedByUserName': 'Officer John',
        'notes': 'Duplicate report of incident #842. Already handled.',
        'changedAt': '2026-09-16T11:30:00.000Z',
      };

      final model = WasteReportStatusHistoryModel.fromJson(json);

      expect(model.fromStatus, WasteReportStatus.underReview);
      expect(model.toStatus, WasteReportStatus.rejected);
      expect(model.notes, 'Duplicate report of incident #842. Already handled.');
    });

    test('serializes to JSON correctly', () {
      final model = WasteReportStatusHistoryModel(
        id: 'hist-001',
        wasteReportId: 'rep-001',
        fromStatus: WasteReportStatus.submitted,
        toStatus: WasteReportStatus.verified,
        changedByUserId: 'user-1',
        changedByUserName: 'Officer Dave',
        notes: 'Verified on-site',
        changedAt: validUtc,
      );

      final json = model.toJson();

      expect(json['id'], 'hist-001');
      expect(json['wasteReportId'], 'rep-001');
      expect(json['fromStatus'], 'Submitted');
      expect(json['toStatus'], 'Verified');
      expect(json['changedByUserId'], 'user-1');
      expect(json['changedByUserName'], 'Officer Dave');
      expect(json['notes'], 'Verified on-site');
      expect(json['changedAt'], '2026-09-16T10:30:00.000Z');
    });

    test('serializes with null fromStatus and notes correctly', () {
      final model = WasteReportStatusHistoryModel(
        id: 'hist-001',
        wasteReportId: 'rep-001',
        toStatus: WasteReportStatus.submitted,
        changedAt: validUtc,
      );

      final json = model.toJson();

      expect(json.containsKey('fromStatus'), isFalse);
      expect(json.containsKey('notes'), isFalse);
      expect(json['toStatus'], 'Submitted');
    });

    test('throws FormatException on missing or empty required fields', () {
      expect(
        () => WasteReportStatusHistoryModel.fromJson({
          'id': '',
          'wasteReportId': 'rep-1',
          'toStatus': 'Submitted',
          'changedAt': '2026-09-16T10:30:00.000Z',
        }),
        throwsA(isA<FormatException>()),
      );

      expect(
        () => WasteReportStatusHistoryModel.fromJson({
          'id': 'hist-1',
          'wasteReportId': '',
          'toStatus': 'Submitted',
          'changedAt': '2026-09-16T10:30:00.000Z',
        }),
        throwsA(isA<FormatException>()),
      );

      expect(
        () => WasteReportStatusHistoryModel.fromJson({
          'id': 'hist-1',
          'wasteReportId': 'rep-1',
          'toStatus': null,
          'changedAt': '2026-09-16T10:30:00.000Z',
        }),
        throwsA(isA<FormatException>()),
      );

      expect(
        () => WasteReportStatusHistoryModel.fromJson({
          'id': 'hist-1',
          'wasteReportId': 'rep-1',
          'toStatus': 'Submitted',
          'changedAt': 'invalid-date',
        }),
        throwsA(isA<FormatException>()),
      );
    });

    test('equality and hashCode verify identical values', () {
      final a = WasteReportStatusHistoryModel(
        id: 'hist-1',
        wasteReportId: 'rep-1',
        fromStatus: null,
        toStatus: WasteReportStatus.submitted,
        changedAt: validUtc,
      );

      final b = WasteReportStatusHistoryModel(
        id: 'hist-1',
        wasteReportId: 'rep-1',
        fromStatus: null,
        toStatus: WasteReportStatus.submitted,
        changedAt: validUtc,
      );

      final c = WasteReportStatusHistoryModel(
        id: 'hist-2',
        wasteReportId: 'rep-1',
        fromStatus: null,
        toStatus: WasteReportStatus.submitted,
        changedAt: validUtc,
      );

      expect(a, equals(b));
      expect(a.hashCode, equals(b.hashCode));
      expect(a, isNot(equals(c)));
    });
  });
}
