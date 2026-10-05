import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/create_waste_report_request.dart';
import 'package:mobile/features/reporting/models/report_attachment_model.dart';
import 'package:mobile/features/reporting/models/selected_report_image.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/services/report_submission_service.dart';

class FakeReportingRepository extends ReportingRepository {
  int createCallCount = 0;
  int uploadCallCount = 0;
  final List<Map<String, dynamic>> uploadCalls = [];

  WasteReportDetailModel? reportToReturn;
  Exception? createException;

  final Map<String, ReportAttachmentModel> attachmentsToReturn = {};
  final Set<String> pathsToFail = {};
  Exception? defaultUploadException;

  @override
  Future<WasteReportDetailModel> createWasteReport(CreateWasteReportRequest request) async {
    createCallCount++;
    if (createException != null) {
      throw createException!;
    }
    return reportToReturn ??
        WasteReportDetailModel(
          id: 'rep-authed-1',
          citizenId: 'cit-1',
          citizenName: 'Citizen One',
          description: request.description,
          wasteType: request.wasteType,
          latitude: request.latitude,
          longitude: request.longitude,
          addressText: request.addressText,
          status: WasteReportStatus.submitted,
          createdAt: DateTime.parse('2026-09-16T12:00:00.000Z'),
        );
  }

  @override
  Future<ReportAttachmentModel> uploadAttachment({
    required String reportId,
    required String filePath,
    String? fileName,
    String? fileType,
  }) async {
    uploadCallCount++;
    uploadCalls.add({
      'reportId': reportId,
      'filePath': filePath,
      'fileName': fileName,
      'fileType': fileType,
    });

    if (pathsToFail.contains(filePath)) {
      throw defaultUploadException ??
          const ApiException(message: 'Upload timeout', statusCode: 408);
    }

    return attachmentsToReturn[filePath] ??
        ReportAttachmentModel(
          id: 'att-$uploadCallCount',
          wasteReportId: reportId,
          fileUrl: 'https://storage/signed/$fileName',
          fileType: fileType ?? 'image/jpeg',
          createdAt: DateTime.parse('2026-09-16T12:05:00.000Z'),
        );
  }
}

void main() {
  group('ReportSubmissionService Orchestration Tests', () {
    late FakeReportingRepository fakeRepo;
    late ReportSubmissionService service;

    const testRequest = CreateWasteReportRequest(
      description: 'Overflowing dumpster at junction',
      wasteType: WasteType.general,
      latitude: 6.9271,
      longitude: 79.8612,
      addressText: 'Junction road',
    );

    const img1 = SelectedReportImage(
      path: '/local/photo1.jpg',
      fileName: 'photo1.jpg',
      fileType: 'image/jpeg',
      sizeBytes: 1000,
    );
    const img2 = SelectedReportImage(
      path: '/local/photo2.png',
      fileName: 'photo2.png',
      fileType: 'image/png',
      sizeBytes: 2000,
    );
    const img3 = SelectedReportImage(
      path: '/local/photo3.webp',
      fileName: 'photo3.webp',
      fileType: 'image/webp',
      sizeBytes: 3000,
    );

    setUp(() {
      fakeRepo = FakeReportingRepository();
      service = ReportSubmissionService(repository: fakeRepo);
    });

    test('1. create with zero images: create called exactly once, 0 upload calls, full success', () async {
      final result = await service.submitReport(
        request: testRequest,
        images: [],
      );

      expect(fakeRepo.createCallCount, equals(1));
      expect(fakeRepo.uploadCallCount, equals(0));
      expect(result, isA<ReportSubmissionFullSuccess>());

      final success = result as ReportSubmissionFullSuccess;
      expect(success.report.id, equals('rep-authed-1'));
      expect(success.uploadedAttachments, isEmpty);
      expect(success.isFullSuccess, isTrue);
    });

    test('2. create with 3 images: create exactly once, upload 3 times with same reportId, full success', () async {
      final result = await service.submitReport(
        request: testRequest,
        images: [img1, img2, img3],
      );

      expect(fakeRepo.createCallCount, equals(1));
      expect(fakeRepo.uploadCallCount, equals(3));
      expect(fakeRepo.uploadCalls.every((c) => c['reportId'] == 'rep-authed-1'), isTrue);
      expect(fakeRepo.uploadCalls[0]['fileType'], equals('image/jpeg'));
      expect(fakeRepo.uploadCalls[1]['fileType'], equals('image/png'));
      expect(fakeRepo.uploadCalls[2]['fileType'], equals('image/webp'));

      expect(result, isA<ReportSubmissionFullSuccess>());
      final success = result as ReportSubmissionFullSuccess;
      expect(success.uploadedAttachments.length, equals(3));
      expect(success.failedImages, isEmpty);
    });

    test('3. create failure: zero attachment uploads, returns ReportCreationFailure with friendly error', () async {
      fakeRepo.createException = const ApiException(
        message: 'Unable to connect to the Smart Waste server.',
        statusCode: 503,
      );

      final result = await service.submitReport(
        request: testRequest,
        images: [img1, img2],
      );

      expect(fakeRepo.createCallCount, equals(1));
      expect(fakeRepo.uploadCallCount, equals(0));
      expect(result, isA<ReportCreationFailure>());

      final failure = result as ReportCreationFailure;
      expect(failure.userFacingMessage, contains("couldn't submit your report"));
      expect(failure.statusCode, equals(503));
    });

    test('4. attachment #2 fails: report created once, image 1 succeeds, image 2 fails, image 3 STILL attempted', () async {
      fakeRepo.pathsToFail.add(img2.path);

      final result = await service.submitReport(
        request: testRequest,
        images: [img1, img2, img3],
      );

      expect(fakeRepo.createCallCount, equals(1));
      expect(fakeRepo.uploadCallCount, equals(3)); // All 3 attempted!
      expect(result, isA<ReportSubmissionPartialSuccess>());

      final partial = result as ReportSubmissionPartialSuccess;
      expect(partial.report.id, equals('rep-authed-1'));
      expect(partial.uploadedAttachments.length, equals(2));
      expect(partial.failedImages.length, equals(1));
      expect(partial.failedImages.first.image.path, equals(img2.path));
      expect(partial.isPartialSuccess, isTrue);
      expect(partial.isFullSuccess, isFalse);
    });

    test('5. all attachments fail: report remains created, state is partial success, NOT create failure', () async {
      fakeRepo.pathsToFail.addAll([img1.path, img2.path, img3.path]);

      final result = await service.submitReport(
        request: testRequest,
        images: [img1, img2, img3],
      );

      expect(fakeRepo.createCallCount, equals(1));
      expect(fakeRepo.uploadCallCount, equals(3));
      expect(result, isA<ReportSubmissionPartialSuccess>());

      final partial = result as ReportSubmissionPartialSuccess;
      expect(partial.report.id, equals('rep-authed-1'));
      expect(partial.uploadedAttachments, isEmpty);
      expect(partial.failedImages.length, equals(3));
    });

    test('6. retry failed images: NO create API call, uploads only failed images with existing reportId', () async {
      // Initially, img2 failed
      fakeRepo.pathsToFail.add(img2.path);
      final initialResult = await service.submitReport(
        request: testRequest,
        images: [img1, img2],
      );
      expect(initialResult, isA<ReportSubmissionPartialSuccess>());
      final partial = initialResult as ReportSubmissionPartialSuccess;

      // Reset counters to track retry isolation
      fakeRepo.createCallCount = 0;
      fakeRepo.uploadCallCount = 0;
      fakeRepo.uploadCalls.clear();
      fakeRepo.pathsToFail.clear(); // img2 succeeds on retry

      final retryResult = await service.retryFailedUploads(
        existingReport: partial.report,
        failedImages: partial.failedImages.map((f) => f.image).toList(),
        previouslyUploaded: partial.uploadedAttachments,
      );

      // Verify NO create call
      expect(fakeRepo.createCallCount, equals(0));
      // Only 1 upload attempted (img2)
      expect(fakeRepo.uploadCallCount, equals(1));
      expect(fakeRepo.uploadCalls.first['reportId'], equals(partial.report.id));
      expect(fakeRepo.uploadCalls.first['filePath'], equals(img2.path));

      expect(retryResult, isA<ReportSubmissionFullSuccess>());
      final success = retryResult as ReportSubmissionFullSuccess;
      expect(success.uploadedAttachments.length, equals(2));
    });

    test('7. retry partial failure again: successful retried images added to uploaded set, remaining failures retained', () async {
      final initialReport = WasteReportDetailModel(
        id: 'rep-existing-99',
        citizenId: 'cit-1',
        citizenName: 'Citizen One',
        description: 'Test',
        wasteType: WasteType.organic,
        latitude: 6.9,
        longitude: 79.8,
        status: WasteReportStatus.submitted,
        createdAt: DateTime.parse('2026-09-16T12:00:00.000Z'),
      );

      final previouslyUploaded = [
        ReportAttachmentModel(
          id: 'att-1',
          wasteReportId: 'rep-existing-99',
          fileUrl: 'https://storage/signed/photo1.jpg',
          fileType: 'image/jpeg',
          createdAt: DateTime.parse('2026-09-16T12:00:00.000Z'),
        ),
      ];

      // img2 will succeed, img3 will fail again
      fakeRepo.pathsToFail.add(img3.path);

      final result = await service.retryFailedUploads(
        existingReport: initialReport,
        failedImages: [img2, img3],
        previouslyUploaded: previouslyUploaded,
      );

      expect(fakeRepo.createCallCount, equals(0));
      expect(fakeRepo.uploadCallCount, equals(2));
      expect(result, isA<ReportSubmissionPartialSuccess>());

      final partial = result as ReportSubmissionPartialSuccess;
      expect(partial.uploadedAttachments.length, equals(2)); // photo1 + photo2
      expect(partial.failedImages.length, equals(1)); // photo3
      expect(partial.failedImages.first.image.path, equals(img3.path));
    });
  });
}
