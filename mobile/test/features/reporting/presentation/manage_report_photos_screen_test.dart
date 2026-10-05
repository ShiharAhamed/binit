import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/report_attachment_model.dart';
import 'package:mobile/features/reporting/models/selected_report_image.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/presentation/manage_report_photos_screen.dart';
import 'package:mobile/features/reporting/services/image_picker_service.dart';

class FakeReportingRepository extends ReportingRepository {
  WasteReportDetailModel? reportToReturn;
  bool shouldThrowGetReport = false;
  String getReportErrorMessage = 'Network error';

  ReportAttachmentModel? attachmentToReturnOnUpload;
  bool shouldThrowUpload = false;
  int uploadStatusCode = 500;
  String uploadErrorMessage = 'Upload failed';

  bool shouldThrowDelete = false;
  int deleteStatusCode = 500;
  String deleteErrorMessage = 'Delete failed';

  int getReportCallCount = 0;
  int uploadCallCount = 0;
  int deleteCallCount = 0;
  int _uploadCounter = 0;
  Duration getReportDelay = Duration.zero;

  final List<String> deletedAttachmentIds = [];

  @override
  Future<WasteReportDetailModel> getWasteReport(String reportId) async {
    getReportCallCount++;
    if (getReportDelay > Duration.zero) {
      await Future.delayed(getReportDelay);
    }
    if (shouldThrowGetReport) {
      throw ApiException(message: getReportErrorMessage, statusCode: 500);
    }
    if (reportToReturn != null) {
      return reportToReturn!;
    }
    throw const ApiException(message: 'Report not found', statusCode: 404);
  }

  @override
  Future<ReportAttachmentModel> uploadAttachment({
    required String reportId,
    required String filePath,
    String? fileName,
    String? fileType,
  }) async {
    uploadCallCount++;
    if (shouldThrowUpload) {
      throw ApiException(message: uploadErrorMessage, statusCode: uploadStatusCode);
    }
    _uploadCounter++;
    return attachmentToReturnOnUpload ??
        ReportAttachmentModel(
          id: 'att-new-$_uploadCounter',
          wasteReportId: reportId,
          fileType: fileType ?? 'image/jpeg',
          fileUrl: 'https://storage.smartwaste.local/evidence.jpg',
          createdAt: DateTime.now(),
        );
  }

  @override
  Future<void> deleteAttachment({
    required String reportId,
    required String attachmentId,
  }) async {
    deleteCallCount++;
    if (shouldThrowDelete) {
      throw ApiException(message: deleteErrorMessage, statusCode: deleteStatusCode);
    }
    deletedAttachmentIds.add(attachmentId);
  }
}

class FakeImagePickerService implements ImagePickerService {
  SelectedReportImage? imageToReturnFromCamera;
  List<SelectedReportImage> imagesToReturnFromGallery = [];
  bool throwCameraException = false;
  bool throwGalleryException = false;
  String cameraErrorMessage = 'Camera is not available.';
  String galleryErrorMessage = 'Unable to access gallery.';

  int takePhotoCallCount = 0;
  int pickFromGalleryCallCount = 0;

  @override
  Future<SelectedReportImage?> takePhoto() async {
    takePhotoCallCount++;
    if (throwCameraException) throw ImagePickerException(cameraErrorMessage);
    return imageToReturnFromCamera;
  }

  @override
  Future<List<SelectedReportImage>> pickFromGallery({required int maxImages}) async {
    pickFromGalleryCallCount++;
    if (throwGalleryException) throw ImagePickerException(galleryErrorMessage);
    return imagesToReturnFromGallery.take(maxImages).toList();
  }

  @override
  Future<List<SelectedReportImage>> retrieveLostImages() async {
    return const [];
  }
}

void main() {
  late FakeReportingRepository fakeRepo;
  late FakeImagePickerService fakePicker;

  setUp(() {
    fakeRepo = FakeReportingRepository();
    fakePicker = FakeImagePickerService();
  });

  WasteReportDetailModel createSampleReport({
    String id = 'rep-101',
    WasteReportStatus status = WasteReportStatus.submitted,
    List<ReportAttachmentModel> attachments = const [],
  }) {
    return WasteReportDetailModel(
      id: id,
      citizenId: 'cit-1',
      citizenName: 'Kamal Perera',
      description: 'Illegal waste dump',
      wasteType: WasteType.general,
      latitude: 6.9271,
      longitude: 79.8612,
      status: status,
      attachments: attachments,
      createdAt: DateTime.utc(2026, 9, 16, 8, 30, 0),
    );
  }

  ReportAttachmentModel createSampleAttachment({
    required String id,
    String wasteReportId = 'rep-101',
    String fileType = 'image/jpeg',
    String fileUrl = 'https://storage.smartwaste.local/photo.jpg',
  }) {
    return ReportAttachmentModel(
      id: id,
      wasteReportId: wasteReportId,
      fileType: fileType,
      fileUrl: fileUrl,
      createdAt: DateTime.utc(2026, 9, 16, 8, 35, 0),
    );
  }

  SelectedReportImage createSampleSelectedImage({
    String path = '/tmp/camera_photo.jpg',
    String fileName = 'camera_photo.jpg',
    String fileType = 'image/jpeg',
    int sizeBytes = 1024 * 150,
  }) {
    return SelectedReportImage(
      path: path,
      fileName: fileName,
      fileType: fileType,
      sizeBytes: sizeBytes,
    );
  }

  Widget createTestWidget({
    String reportId = 'rep-101',
    WasteReportDetailModel? initialReport,
    void Function(dynamic result)? onPopped,
  }) {
    return MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () async {
                final res = await Navigator.of(context).push<bool>(
                  MaterialPageRoute(
                    builder: (_) => ManageReportPhotosScreen(
                      reportId: reportId,
                      initialReport: initialReport,
                      repository: fakeRepo,
                      imagePickerService: fakePicker,
                    ),
                  ),
                );
                onPopped?.call(res);
              },
              child: const Text('Open Manage Photos'),
            ),
          ),
        ),
      ),
    );
  }

  group('ManageReportPhotosScreen Tests', () {
    testWidgets('prefill from initialReport displays count and photos without API call', (tester) async {
      final att1 = createSampleAttachment(id: 'att-1');
      final att2 = createSampleAttachment(id: 'att-2');
      final report = createSampleReport(attachments: [att1, att2]);

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      expect(fakeRepo.getReportCallCount, 0);
      expect(find.text('2 / 3 photos'), findsOneWidget);
      expect(find.byKey(const Key('attachment_tile_att-1')), findsOneWidget);
      expect(find.byKey(const Key('attachment_tile_att-2')), findsOneWidget);
      expect(find.byKey(const Key('add_photo_button')), findsOneWidget);
    });

    testWidgets('fetches report from repository when initialReport is null', (tester) async {
      final att1 = createSampleAttachment(id: 'att-99');
      fakeRepo.reportToReturn = createSampleReport(attachments: [att1]);

      await tester.pumpWidget(MaterialApp(
        home: ManageReportPhotosScreen(
          reportId: 'rep-101',
          initialReport: null,
          repository: fakeRepo,
          imagePickerService: fakePicker,
        ),
      ));

      // Loading state on first frame
      expect(find.byKey(const Key('manage_photos_loading')), findsOneWidget);

      await tester.pumpAndSettle();

      expect(fakeRepo.getReportCallCount, 1);
      expect(find.text('1 / 3 photos'), findsOneWidget);
      expect(find.byKey(const Key('attachment_tile_att-99')), findsOneWidget);
    });

    testWidgets('shows error view when fetching report fails, retries on tap', (tester) async {
      fakeRepo.shouldThrowGetReport = true;

      await tester.pumpWidget(createTestWidget(initialReport: null));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('manage_photos_error_text')), findsOneWidget);
      expect(find.byKey(const Key('manage_photos_retry_button')), findsOneWidget);

      // Now fix error and retry
      fakeRepo.shouldThrowGetReport = false;
      fakeRepo.reportToReturn = createSampleReport();

      await tester.tap(find.byKey(const Key('manage_photos_retry_button')));
      await tester.pumpAndSettle();

      expect(fakeRepo.getReportCallCount, 2);
      expect(find.byKey(const Key('manage_photos_card')), findsOneWidget);
      expect(find.text('0 / 3 photos'), findsOneWidget);
    });

    testWidgets('empty state is shown when report has 0 photos', (tester) async {
      final report = createSampleReport(attachments: []);

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      expect(find.text('0 / 3 photos'), findsOneWidget);
      expect(find.byKey(const Key('manage_photos_empty_state')), findsOneWidget);
      expect(find.text('No photos attached.'), findsOneWidget);
      expect(find.byKey(const Key('add_photo_button')), findsOneWidget);
    });

    testWidgets('locks screen when report status is not Submitted (e.g. UnderReview)', (tester) async {
      final att1 = createSampleAttachment(id: 'att-1');
      final report = createSampleReport(
        status: WasteReportStatus.underReview,
        attachments: [att1],
      );

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      // Banner is shown
      expect(find.byKey(const Key('status_locked_banner')), findsOneWidget);
      // Add Photo CTA is hidden
      expect(find.byKey(const Key('add_photo_button')), findsNothing);
      // Delete button on photo is hidden
      expect(find.byKey(const Key('remove_attachment_button_att-1')), findsNothing);
    });

    testWidgets('shows maximum reached notice when photos count is 3', (tester) async {
      final att1 = createSampleAttachment(id: 'att-1');
      final att2 = createSampleAttachment(id: 'att-2');
      final att3 = createSampleAttachment(id: 'att-3');
      final report = createSampleReport(attachments: [att1, att2, att3]);

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      expect(find.text('3 / 3 photos'), findsOneWidget);
      expect(find.byKey(const Key('max_photos_reached_notice')), findsOneWidget);
      expect(find.byKey(const Key('add_photo_button')), findsNothing);
    });

    testWidgets('Add Photo button opens bottom sheet with options and cancel dismisses it', (tester) async {
      final report = createSampleReport(attachments: []);

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_photo_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('add_photo_bottom_sheet')), findsOneWidget);
      expect(find.byKey(const Key('take_photo_option')), findsOneWidget);
      expect(find.byKey(const Key('choose_gallery_option')), findsOneWidget);

      await tester.tap(find.byKey(const Key('cancel_photo_picker_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('add_photo_bottom_sheet')), findsNothing);
    });

    testWidgets('taking photo uploads successfully, adds photo tile, and shows snackbar', (tester) async {
      final report = createSampleReport(attachments: []);
      fakePicker.imageToReturnFromCamera = createSampleSelectedImage();
      fakeRepo.attachmentToReturnOnUpload = createSampleAttachment(id: 'att-new-1');

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_photo_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(fakePicker.takePhotoCallCount, 1);
      expect(fakeRepo.uploadCallCount, 1);
      expect(find.text('Photo added successfully.'), findsOneWidget);
      expect(find.byKey(const Key('attachment_tile_att-new-1')), findsOneWidget);
      expect(find.text('1 / 3 photos'), findsOneWidget);
    });

    testWidgets('camera cancel returns null and does not upload', (tester) async {
      final report = createSampleReport(attachments: []);
      fakePicker.imageToReturnFromCamera = null;

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_photo_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(fakePicker.takePhotoCallCount, 1);
      expect(fakeRepo.uploadCallCount, 0);
    });

    testWidgets('selecting gallery photos uploads sequentially and displays success snackbar', (tester) async {
      final report = createSampleReport(attachments: []);
      final img1 = createSampleSelectedImage(fileName: 'p1.jpg', path: '/p1.jpg');
      final img2 = createSampleSelectedImage(fileName: 'p2.jpg', path: '/p2.jpg');
      fakePicker.imagesToReturnFromGallery = [img1, img2];

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_photo_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('choose_gallery_option')));
      await tester.pumpAndSettle();

      expect(fakePicker.pickFromGalleryCallCount, 1);
      expect(fakeRepo.uploadCallCount, 2);
      expect(find.text('2 photos uploaded successfully.'), findsOneWidget);
      expect(find.text('2 / 3 photos'), findsOneWidget);
    });

    testWidgets('upload 409 Conflict locks mutation controls and displays conflict snackbar', (tester) async {
      final report = createSampleReport(attachments: []);
      fakePicker.imageToReturnFromCamera = createSampleSelectedImage();
      fakeRepo.shouldThrowUpload = true;
      fakeRepo.uploadStatusCode = 409;

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_photo_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('photo_status_conflict_snackbar')), findsOneWidget);
      // Status lock banner should appear
      expect(find.byKey(const Key('status_locked_banner')), findsOneWidget);
      // Add Photo CTA should disappear
      expect(find.byKey(const Key('add_photo_button')), findsNothing);
    });

    testWidgets('upload 403 Forbidden displays access denied message', (tester) async {
      final report = createSampleReport(attachments: []);
      fakePicker.imageToReturnFromCamera = createSampleSelectedImage();
      fakeRepo.shouldThrowUpload = true;
      fakeRepo.uploadStatusCode = 403;

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_photo_button')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(find.text("You don't have access to change this report."), findsOneWidget);
    });

    testWidgets('delete photo shows confirmation dialog, Cancel dismisses without deleting', (tester) async {
      final att1 = createSampleAttachment(id: 'att-1');
      final report = createSampleReport(attachments: [att1]);

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('remove_attachment_button_att-1')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('delete_attachment_dialog')), findsOneWidget);
      expect(find.text('Remove this photo?'), findsOneWidget);

      await tester.tap(find.byKey(const Key('cancel_delete_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('delete_attachment_dialog')), findsNothing);
      expect(fakeRepo.deleteCallCount, 0);
      expect(find.byKey(const Key('attachment_tile_att-1')), findsOneWidget);
    });

    testWidgets('delete photo confirmation calls deleteAttachment, removes tile, and shows snackbar', (tester) async {
      final att1 = createSampleAttachment(id: 'att-1');
      final report = createSampleReport(attachments: [att1]);

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('remove_attachment_button_att-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_delete_button')));
      await tester.pumpAndSettle();

      expect(fakeRepo.deleteCallCount, 1);
      expect(fakeRepo.deletedAttachmentIds, contains('att-1'));
      expect(find.text('Photo removed successfully.'), findsOneWidget);
      expect(find.byKey(const Key('attachment_tile_att-1')), findsNothing);
      expect(find.text('0 / 3 photos'), findsOneWidget);
    });

    testWidgets('delete photo 409 Conflict locks mutation controls and displays conflict snackbar', (tester) async {
      final att1 = createSampleAttachment(id: 'att-1');
      final report = createSampleReport(attachments: [att1]);
      fakeRepo.shouldThrowDelete = true;
      fakeRepo.deleteStatusCode = 409;

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('remove_attachment_button_att-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_delete_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('photo_status_conflict_snackbar')), findsOneWidget);
      expect(find.byKey(const Key('status_locked_banner')), findsOneWidget);
      // Delete button is now gone
      expect(find.byKey(const Key('remove_attachment_button_att-1')), findsNothing);
    });

    testWidgets('tapping photo thumbnail opens fullscreen preview dialog and close dismisses it', (tester) async {
      final att1 = createSampleAttachment(id: 'att-1');
      final report = createSampleReport(attachments: [att1]);

      await tester.pumpWidget(createTestWidget(initialReport: report));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('photo_preview_gesture_att-1')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('photo_preview_dialog')), findsOneWidget);

      await tester.tap(find.byKey(const Key('photo_preview_close_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('photo_preview_dialog')), findsNothing);
    });

    testWidgets('pops with false if user exits without mutations', (tester) async {
      dynamic poppedResult;
      final report = createSampleReport(attachments: []);

      await tester.pumpWidget(createTestWidget(
        initialReport: report,
        onPopped: (res) => poppedResult = res,
      ));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('manage_photos_back_button')));
      await tester.pumpAndSettle();

      expect(poppedResult, false);
    });

    testWidgets('pops with true after photo is added via Done button', (tester) async {
      dynamic poppedResult;
      final report = createSampleReport(attachments: []);
      fakePicker.imageToReturnFromCamera = createSampleSelectedImage();

      await tester.pumpWidget(createTestWidget(
        initialReport: report,
        onPopped: (res) => poppedResult = res,
      ));
      await tester.tap(find.text('Open Manage Photos'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_photo_button')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('done_button')));
      await tester.tap(find.byKey(const Key('done_button')));
      await tester.pumpAndSettle();

      expect(poppedResult, true);
    });

    testWidgets('responsive layout on narrow 320px viewport without overflow', (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final att1 = createSampleAttachment(id: 'att-1');
      final report = createSampleReport(attachments: [att1]);

      await tester.pumpWidget(MaterialApp(
        home: ManageReportPhotosScreen(
          reportId: 'rep-101',
          initialReport: report,
          repository: fakeRepo,
          imagePickerService: fakePicker,
        ),
      ));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });

    testWidgets('responsive layout on standard 390px viewport without overflow', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final att1 = createSampleAttachment(id: 'att-1');
      final att2 = createSampleAttachment(id: 'att-2');
      final report = createSampleReport(attachments: [att1, att2]);

      await tester.pumpWidget(MaterialApp(
        home: ManageReportPhotosScreen(
          reportId: 'rep-101',
          initialReport: report,
          repository: fakeRepo,
          imagePickerService: fakePicker,
        ),
      ));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });
}
