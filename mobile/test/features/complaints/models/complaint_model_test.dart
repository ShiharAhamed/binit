import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/complaints/models/complaint_model.dart';
import 'package:mobile/features/complaints/models/create_complaint_request.dart';

void main() {
  group('ComplaintCategory Enum Tests', () {
    test('toJsonValue returns expected backend string', () {
      expect(ComplaintCategory.missedCollection.toJsonValue(), 'MissedCollection');
      expect(ComplaintCategory.delayedService.toJsonValue(), 'DelayedService');
      expect(ComplaintCategory.poorService.toJsonValue(), 'PoorService');
      expect(ComplaintCategory.unresolvedIssue.toJsonValue(), 'UnresolvedIssue');
      expect(ComplaintCategory.other.toJsonValue(), 'Other');
    });

    test('displayName returns clean user-facing label', () {
      expect(ComplaintCategory.missedCollection.displayName, 'Missed Collection');
      expect(ComplaintCategory.delayedService.displayName, 'Delayed Service');
      expect(ComplaintCategory.poorService.displayName, 'Poor Service');
      expect(ComplaintCategory.unresolvedIssue.displayName, 'Unresolved Issue');
      expect(ComplaintCategory.other.displayName, 'Other');
    });

    test('fromJsonValue parses valid string and integer representations', () {
      expect(ComplaintCategory.fromJsonValue('MissedCollection'), ComplaintCategory.missedCollection);
      expect(ComplaintCategory.fromJsonValue('missedcollection'), ComplaintCategory.missedCollection);
      expect(ComplaintCategory.fromJsonValue('DelayedService'), ComplaintCategory.delayedService);
      expect(ComplaintCategory.fromJsonValue('poorService'), ComplaintCategory.poorService);
      expect(ComplaintCategory.fromJsonValue('UnresolvedIssue'), ComplaintCategory.unresolvedIssue);
      expect(ComplaintCategory.fromJsonValue('Other'), ComplaintCategory.other);
      expect(ComplaintCategory.fromJsonValue(0), ComplaintCategory.missedCollection);
      expect(ComplaintCategory.fromJsonValue(4), ComplaintCategory.other);
    });

    test('fromJsonValue throws on invalid values', () {
      expect(() => ComplaintCategory.fromJsonValue('InvalidCategory'), throwsFormatException);
      expect(() => ComplaintCategory.fromJsonValue(99), throwsFormatException);
      expect(() => ComplaintCategory.fromJsonValue(null), throwsFormatException);
    });
  });

  group('ComplaintStatus Enum Tests', () {
    test('toJsonValue returns expected backend string', () {
      expect(ComplaintStatus.submitted.toJsonValue(), 'Submitted');
      expect(ComplaintStatus.inReview.toJsonValue(), 'InReview');
      expect(ComplaintStatus.resolved.toJsonValue(), 'Resolved');
    });

    test('displayName returns clean user-facing label', () {
      expect(ComplaintStatus.submitted.displayName, 'Submitted');
      expect(ComplaintStatus.inReview.displayName, 'In Review');
      expect(ComplaintStatus.resolved.displayName, 'Resolved');
    });

    test('fromJsonValue parses valid string and integer representations', () {
      expect(ComplaintStatus.fromJsonValue('Submitted'), ComplaintStatus.submitted);
      expect(ComplaintStatus.fromJsonValue('inreview'), ComplaintStatus.inReview);
      expect(ComplaintStatus.fromJsonValue('Resolved'), ComplaintStatus.resolved);
      expect(ComplaintStatus.fromJsonValue(0), ComplaintStatus.submitted);
      expect(ComplaintStatus.fromJsonValue(1), ComplaintStatus.inReview);
      expect(ComplaintStatus.fromJsonValue(2), ComplaintStatus.resolved);
    });

    test('fromJsonValue throws on invalid values', () {
      expect(() => ComplaintStatus.fromJsonValue('InvalidStatus'), throwsFormatException);
      expect(() => ComplaintStatus.fromJsonValue(99), throwsFormatException);
      expect(() => ComplaintStatus.fromJsonValue(null), throwsFormatException);
    });

    test('convenience getters work accurately', () {
      expect(ComplaintStatus.submitted.isSubmitted, isTrue);
      expect(ComplaintStatus.submitted.isInReview, isFalse);
      expect(ComplaintStatus.submitted.isResolved, isFalse);

      expect(ComplaintStatus.inReview.isSubmitted, isFalse);
      expect(ComplaintStatus.inReview.isInReview, isTrue);
      expect(ComplaintStatus.inReview.isResolved, isFalse);

      expect(ComplaintStatus.resolved.isSubmitted, isFalse);
      expect(ComplaintStatus.resolved.isInReview, isFalse);
      expect(ComplaintStatus.resolved.isResolved, isTrue);
    });
  });

  group('ComplaintSummaryModel Tests', () {
    test('fromJson parses full summary with coordinates', () {
      final json = {
        'id': 'comp-1',
        'citizenId': 'cit-1',
        'citizenName': 'Jane Citizen',
        'category': 'MissedCollection',
        'subject': 'Missed collection on Main St',
        'status': 'Submitted',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'createdAt': '2026-09-16T10:30:00.000Z',
        'updatedAt': '2026-09-16T11:00:00.000Z',
      };

      final model = ComplaintSummaryModel.fromJson(json);

      expect(model.id, 'comp-1');
      expect(model.citizenId, 'cit-1');
      expect(model.citizenName, 'Jane Citizen');
      expect(model.category, ComplaintCategory.missedCollection);
      expect(model.subject, 'Missed collection on Main St');
      expect(model.status, ComplaintStatus.submitted);
      expect(model.latitude, 6.9271);
      expect(model.longitude, 79.8612);
      expect(model.hasLocation, isTrue);
      expect(model.createdAt, DateTime.parse('2026-09-16T10:30:00.000Z'));
      expect(model.updatedAt, DateTime.parse('2026-09-16T11:00:00.000Z'));
    });

    test('fromJson parses summary without coordinates and nullable updatedAt', () {
      final json = {
        'id': 'comp-2',
        'citizenId': 'cit-2',
        'category': 'DelayedService',
        'subject': 'Delay in recycling route',
        'status': 'InReview',
        'latitude': null,
        'longitude': null,
        'createdAt': '2026-09-17T08:00:00.000Z',
      };

      final model = ComplaintSummaryModel.fromJson(json);

      expect(model.id, 'comp-2');
      expect(model.citizenId, 'cit-2');
      expect(model.citizenName, isNull);
      expect(model.category, ComplaintCategory.delayedService);
      expect(model.subject, 'Delay in recycling route');
      expect(model.status, ComplaintStatus.inReview);
      expect(model.latitude, isNull);
      expect(model.longitude, isNull);
      expect(model.hasLocation, isFalse);
      expect(model.updatedAt, isNull);
    });

    test('toJson serializes correctly', () {
      final model = ComplaintSummaryModel(
        id: 'comp-1',
        citizenId: 'cit-1',
        citizenName: 'Jane Citizen',
        category: ComplaintCategory.poorService,
        subject: 'Spilled bin',
        status: ComplaintStatus.resolved,
        latitude: 6.9,
        longitude: 79.8,
        createdAt: DateTime.utc(2026, 9, 16, 10, 0),
        updatedAt: DateTime.utc(2026, 9, 16, 12, 0),
      );

      final json = model.toJson();

      expect(json['id'], 'comp-1');
      expect(json['citizenId'], 'cit-1');
      expect(json['citizenName'], 'Jane Citizen');
      expect(json['category'], 'PoorService');
      expect(json['subject'], 'Spilled bin');
      expect(json['status'], 'Resolved');
      expect(json['latitude'], 6.9);
      expect(json['longitude'], 79.8);
      expect(json['createdAt'], '2026-09-16T10:00:00.000Z');
      expect(json['updatedAt'], '2026-09-16T12:00:00.000Z');
    });
  });

  group('ComplaintDetailModel Tests', () {
    test('fromJson parses resolved complaint with full fields', () {
      final json = {
        'id': 'comp-detail-1',
        'citizenId': 'cit-1',
        'citizenName': 'Jane Citizen',
        'category': 'MissedCollection',
        'subject': 'Missed bin on 2nd Ave',
        'description': 'Bin was not collected this morning as scheduled on the calendar.',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'locationDescription': 'Outside gate #4',
        'status': 'Resolved',
        'resolutionNote': 'Driver dispatched to complete unscheduled pickup.',
        'resolvedAt': '2026-09-16T14:30:00.000Z',
        'resolvedByUserId': 'user-staff-1',
        'resolvedByUserName': 'Officer Smith',
        'createdAt': '2026-09-16T09:00:00.000Z',
        'updatedAt': '2026-09-16T14:30:00.000Z',
      };

      final model = ComplaintDetailModel.fromJson(json);

      expect(model.id, 'comp-detail-1');
      expect(model.citizenId, 'cit-1');
      expect(model.citizenName, 'Jane Citizen');
      expect(model.category, ComplaintCategory.missedCollection);
      expect(model.subject, 'Missed bin on 2nd Ave');
      expect(model.description, 'Bin was not collected this morning as scheduled on the calendar.');
      expect(model.latitude, 6.9271);
      expect(model.longitude, 79.8612);
      expect(model.locationDescription, 'Outside gate #4');
      expect(model.hasLocation, isTrue);
      expect(model.status, ComplaintStatus.resolved);
      expect(model.resolutionNote, 'Driver dispatched to complete unscheduled pickup.');
      expect(model.resolvedAt, DateTime.parse('2026-09-16T14:30:00.000Z'));
      expect(model.resolvedByUserId, 'user-staff-1');
      expect(model.resolvedByUserName, 'Officer Smith');
      expect(model.createdAt, DateTime.parse('2026-09-16T09:00:00.000Z'));
      expect(model.updatedAt, DateTime.parse('2026-09-16T14:30:00.000Z'));
    });

    test('fromJson parses submitted complaint without location or resolution', () {
      final json = {
        'id': 'comp-detail-2',
        'citizenId': 'cit-2',
        'category': 'Other',
        'subject': 'General inquiry about schedule',
        'description': 'Wondering if collection day changes on public holidays.',
        'status': 'Submitted',
        'createdAt': '2026-09-17T12:00:00.000Z',
      };

      final model = ComplaintDetailModel.fromJson(json);

      expect(model.id, 'comp-detail-2');
      expect(model.citizenId, 'cit-2');
      expect(model.citizenName, isNull);
      expect(model.category, ComplaintCategory.other);
      expect(model.subject, 'General inquiry about schedule');
      expect(model.description, 'Wondering if collection day changes on public holidays.');
      expect(model.latitude, isNull);
      expect(model.longitude, isNull);
      expect(model.locationDescription, isNull);
      expect(model.hasLocation, isFalse);
      expect(model.status, ComplaintStatus.submitted);
      expect(model.resolutionNote, isNull);
      expect(model.resolvedAt, isNull);
      expect(model.resolvedByUserId, isNull);
      expect(model.resolvedByUserName, isNull);
      expect(model.updatedAt, isNull);
    });

    test('toJson serializes correctly', () {
      final model = ComplaintDetailModel(
        id: 'comp-10',
        citizenId: 'cit-10',
        category: ComplaintCategory.unresolvedIssue,
        subject: 'Previous ticket not cleared',
        description: 'Issue from last week remains unresolved.',
        status: ComplaintStatus.inReview,
        createdAt: DateTime.utc(2026, 9, 18, 10, 0),
      );

      final json = model.toJson();

      expect(json['id'], 'comp-10');
      expect(json['citizenId'], 'cit-10');
      expect(json['category'], 'UnresolvedIssue');
      expect(json['subject'], 'Previous ticket not cleared');
      expect(json['description'], 'Issue from last week remains unresolved.');
      expect(json['status'], 'InReview');
      expect(json['createdAt'], '2026-09-18T10:00:00.000Z');
      expect(json.containsKey('latitude'), isFalse);
      expect(json.containsKey('resolutionNote'), isFalse);
    });
  });

  group('PagedComplaintsModel Tests', () {
    test('fromJson parses envelope correctly', () {
      final json = {
        'items': [
          {
            'id': 'comp-1',
            'citizenId': 'cit-1',
            'category': 'MissedCollection',
            'subject': 'Missed bin',
            'status': 'Submitted',
            'createdAt': '2026-09-16T10:00:00.000Z',
          }
        ],
        'page': 1,
        'pageSize': 10,
        'totalCount': 25,
        'totalPages': 3,
      };

      final paged = PagedComplaintsModel.fromJson(json);

      expect(paged.items.length, 1);
      expect(paged.page, 1);
      expect(paged.pageSize, 10);
      expect(paged.totalCount, 25);
      expect(paged.totalPages, 3);
      expect(paged.hasMore, isTrue);
      expect(paged.isEmpty, isFalse);
      expect(paged.isNotEmpty, isTrue);
    });

    test('hasMore returns false on last page', () {
      final paged = PagedComplaintsModel(
        items: [],
        page: 3,
        pageSize: 10,
        totalCount: 25,
        totalPages: 3,
      );

      expect(paged.hasMore, isFalse);
      expect(paged.isEmpty, isTrue);
    });
  });

  group('CreateComplaintRequest Tests', () {
    test('toJson produces valid payload with all fields', () {
      const request = CreateComplaintRequest(
        category: ComplaintCategory.missedCollection,
        subject: 'Missed collection on 5th street',
        description: 'Bin was placed on the curb at 6:00 AM but was not emptied.',
        latitude: 6.9271,
        longitude: 79.8612,
        locationDescription: 'In front of house #12',
      );

      final json = request.toJson();

      expect(json['category'], 'MissedCollection');
      expect(json['subject'], 'Missed collection on 5th street');
      expect(json['description'], 'Bin was placed on the curb at 6:00 AM but was not emptied.');
      expect(json['latitude'], 6.9271);
      expect(json['longitude'], 79.8612);
      expect(json['locationDescription'], 'In front of house #12');
    });

    test('toJson omits optional location when null', () {
      const request = CreateComplaintRequest(
        category: ComplaintCategory.delayedService,
        subject: 'General delay',
        description: 'Service was delayed by 3 hours today.',
      );

      final json = request.toJson();

      expect(json['category'], 'DelayedService');
      expect(json['subject'], 'General delay');
      expect(json['description'], 'Service was delayed by 3 hours today.');
      expect(json.containsKey('latitude'), isFalse);
      expect(json.containsKey('longitude'), isFalse);
      expect(json.containsKey('locationDescription'), isFalse);
    });
  });
}
