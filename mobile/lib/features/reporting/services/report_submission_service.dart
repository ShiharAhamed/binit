import '../../../core/network/api_exception.dart';
import '../data/reporting_repository.dart';
import '../models/create_waste_report_request.dart';
import '../models/report_attachment_model.dart';
import '../models/selected_report_image.dart';
import '../models/waste_report_detail_model.dart';

/// Represents a failure to upload a specific photographic attachment.
class FailedReportImage {
  final SelectedReportImage image;
  final String userFacingMessage;

  const FailedReportImage({
    required this.image,
    required this.userFacingMessage,
  });

  @override
  String toString() => 'FailedReportImage(file: ${image.fileName}, reason: $userFacingMessage)';
}

/// Sealed result hierarchy capturing the outcome of a waste report submission attempt.
sealed class ReportSubmissionResult {
  const ReportSubmissionResult();
}

/// WasteReport creation failed at the ASP.NET backend. No report exists.
class ReportCreationFailure extends ReportSubmissionResult {
  final String userFacingMessage;
  final int? statusCode;

  const ReportCreationFailure({
    required this.userFacingMessage,
    this.statusCode,
  });
}

/// Authoritative WasteReport exists in the database.
sealed class ReportCreatedResult extends ReportSubmissionResult {
  final WasteReportDetailModel report;
  final List<ReportAttachmentModel> uploadedAttachments;
  final List<FailedReportImage> failedImages;

  const ReportCreatedResult({
    required this.report,
    this.uploadedAttachments = const [],
    this.failedImages = const [],
  });

  /// Total count of images attempted for this report.
  int get totalImagesCount => uploadedAttachments.length + failedImages.length;

  /// Whether all photos succeeded (or 0 were selected).
  bool get isFullSuccess => failedImages.isEmpty;

  /// Whether some or all selected photos failed upload.
  bool get isPartialSuccess => failedImages.isNotEmpty;
}

/// Full success: Authoritative WasteReport created and all photos (0–3) uploaded.
class ReportSubmissionFullSuccess extends ReportCreatedResult {
  const ReportSubmissionFullSuccess({
    required super.report,
    super.uploadedAttachments,
  });
}

/// Partial success: Authoritative WasteReport created, but one or more photos failed upload.
class ReportSubmissionPartialSuccess extends ReportCreatedResult {
  const ReportSubmissionPartialSuccess({
    required super.report,
    super.uploadedAttachments,
    required super.failedImages,
  });
}

/// Orchestration service coordinating the authoritative two-phase waste report submission:
/// 1. Create WasteReport via ASP.NET Core (EXACTLY ONCE).
/// 2. Sequentially upload selected photos against the authoritative reportId.
/// 3. Track partial attachment failures and coordinate retry against the existing reportId.
class ReportSubmissionService {
  final ReportingRepository _repository;

  ReportSubmissionService({ReportingRepository? repository})
      : _repository = repository ?? ReportingRepository();

  /// Submits a new waste report and sequentially uploads any selected photos.
  Future<ReportSubmissionResult> submitReport({
    required CreateWasteReportRequest request,
    List<SelectedReportImage> images = const [],
  }) async {
    // Phase 1: Create WasteReport (EXACTLY ONCE)
    late final WasteReportDetailModel createdReport;
    try {
      createdReport = await _repository.createWasteReport(request);
    } catch (e) {
      return ReportCreationFailure(
        userFacingMessage: _mapReportCreationError(e),
        statusCode: e is ApiException ? e.statusCode : null,
      );
    }

    // Phase 2: If no photos, report creation is immediately full success.
    if (images.isEmpty) {
      return ReportSubmissionFullSuccess(
        report: createdReport,
        uploadedAttachments: const [],
      );
    }

    // Phase 3: Sequentially upload selected photos against authoritative reportId.
    final uploaded = <ReportAttachmentModel>[];
    final failed = <FailedReportImage>[];

    for (final image in images) {
      try {
        final attachment = await _repository.uploadAttachment(
          reportId: createdReport.id,
          filePath: image.path,
          fileName: image.fileName,
          fileType: image.fileType,
        );
        uploaded.add(attachment);
      } on ApiException catch (e) {
        failed.add(FailedReportImage(
          image: image,
          userFacingMessage: e.message,
        ));
      } catch (_) {
        failed.add(FailedReportImage(
          image: image,
          userFacingMessage: "Failed to upload photo '${image.fileName}'.",
        ));
      }
    }

    if (failed.isEmpty) {
      return ReportSubmissionFullSuccess(
        report: createdReport,
        uploadedAttachments: uploaded,
      );
    } else {
      return ReportSubmissionPartialSuccess(
        report: createdReport,
        uploadedAttachments: uploaded,
        failedImages: failed,
      );
    }
  }

  /// Retries upload of failed photos against the EXISTING reportId.
  /// Never calls createWasteReport.
  Future<ReportSubmissionResult> retryFailedUploads({
    required WasteReportDetailModel existingReport,
    required List<SelectedReportImage> failedImages,
    List<ReportAttachmentModel> previouslyUploaded = const [],
  }) async {
    final newlyUploaded = List<ReportAttachmentModel>.from(previouslyUploaded);
    final stillFailed = <FailedReportImage>[];

    for (final image in failedImages) {
      try {
        final attachment = await _repository.uploadAttachment(
          reportId: existingReport.id,
          filePath: image.path,
          fileName: image.fileName,
          fileType: image.fileType,
        );
        newlyUploaded.add(attachment);
      } on ApiException catch (e) {
        stillFailed.add(FailedReportImage(
          image: image,
          userFacingMessage: e.message,
        ));
      } catch (_) {
        stillFailed.add(FailedReportImage(
          image: image,
          userFacingMessage: "Failed to upload photo '${image.fileName}'.",
        ));
      }
    }

    if (stillFailed.isEmpty) {
      return ReportSubmissionFullSuccess(
        report: existingReport,
        uploadedAttachments: newlyUploaded,
      );
    } else {
      return ReportSubmissionPartialSuccess(
        report: existingReport,
        uploadedAttachments: newlyUploaded,
        failedImages: stillFailed,
      );
    }
  }

  String _mapReportCreationError(dynamic error) {
    if (error is ApiException) {
      if (error.statusCode == 400) {
        return error.message.isNotEmpty
            ? error.message
            : 'Please check your report details and try again.';
      }
      if (error.statusCode == 401) {
        return 'Authentication required. Please log in again.';
      }
      if (error.statusCode == 403) {
        return error.message.isNotEmpty
            ? error.message
            : 'You do not have permission to submit waste reports.';
      }
      if (error.statusCode == 408 ||
          error.message.contains('connect') ||
          error.message.contains('network') ||
          error.message.contains('timed out')) {
        return "We couldn't submit your report. Please check your connection and try again.";
      }
      if (error.statusCode != null && error.statusCode! >= 500) {
        return "We couldn't submit your report right now. Please try again.";
      }
      return error.message;
    }
    return "We couldn't submit your report right now. Please try again.";
  }
}
