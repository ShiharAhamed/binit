import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:mobile/core/theme/app_theme.dart';
import 'package:mobile/features/reporting/models/create_waste_report_request.dart';
import 'package:mobile/features/reporting/models/report_attachment_model.dart';
import 'package:mobile/features/reporting/models/selected_location.dart';
import 'package:mobile/features/reporting/models/selected_report_image.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/presentation/map_location_picker_screen.dart';
import 'package:mobile/features/reporting/presentation/report_waste_screen.dart';
import 'package:mobile/features/reporting/services/image_picker_service.dart';
import 'package:mobile/features/reporting/services/location_service.dart';
import 'package:mobile/features/reporting/services/report_submission_service.dart';

final Uint8List _kTransparentImage = Uint8List.fromList(const [
  0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
  0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
  0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
  0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
  0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
  0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
]);

class FakeTestTileProvider extends TileProvider {
  @override
  ImageProvider getImage(TileCoordinates coordinates, TileLayer options) {
    return MemoryImage(_kTransparentImage);
  }
}

class FakeLocationService implements LocationService {
  bool serviceEnabled;
  LocationPermission permission;
  SelectedLocation? positionToReturn;
  LocationResult? currentLocationResult;
  bool appSettingsOpened = false;
  bool locationSettingsOpened = false;
  int getCurrentLocationCallCount = 0;
  Duration delay;

  FakeLocationService({
    this.serviceEnabled = true,
    this.permission = LocationPermission.whileInUse,
    this.positionToReturn,
    this.currentLocationResult,
    this.delay = Duration.zero,
  });

  @override
  Future<bool> isLocationServiceEnabled() async => serviceEnabled;

  @override
  Future<LocationPermission> checkPermission() async => permission;

  @override
  Future<LocationPermission> requestPermission() async => permission;

  @override
  Future<SelectedLocation> getCurrentPosition() async =>
      positionToReturn ?? const SelectedLocation(latitude: 6.9271, longitude: 79.8612);

  @override
  Future<LocationResult> getCurrentLocation() async {
    getCurrentLocationCallCount++;
    if (delay > Duration.zero) {
      await Future.delayed(delay);
    }
    if (currentLocationResult != null) {
      return currentLocationResult!;
    }
    if (!serviceEnabled) {
      return const LocationFailure(
        reason: LocationFailureReason.serviceDisabled,
        message: 'Location services are turned off. Please enable location on your device.',
      );
    }
    if (permission == LocationPermission.denied) {
      return const LocationFailure(
        reason: LocationFailureReason.permissionDenied,
        message: 'Location permission is needed to use your current location.',
      );
    }
    if (permission == LocationPermission.deniedForever) {
      return const LocationFailure(
        reason: LocationFailureReason.permissionDeniedForever,
        message: 'Location permission is permanently denied. Please enable it in app settings.',
      );
    }
    return LocationSuccess(
      positionToReturn ?? const SelectedLocation(latitude: 6.9271, longitude: 79.8612),
    );
  }

  @override
  Future<bool> openAppSettings() async {
    appSettingsOpened = true;
    return true;
  }

  @override
  Future<bool> openLocationSettings() async {
    locationSettingsOpened = true;
    return true;
  }
}

class FakeImagePickerService implements ImagePickerService {
  SelectedReportImage? imageToReturnFromCamera;
  List<SelectedReportImage> imagesToReturnFromGallery = [];
  List<SelectedReportImage> lostImagesToReturn = [];
  bool throwCameraException = false;
  bool throwGalleryException = false;
  String cameraErrorMessage = 'Camera is not available or permission was denied.';
  String galleryErrorMessage = 'Unable to access photo gallery.';
  int takePhotoCallCount = 0;
  int pickFromGalleryCallCount = 0;
  int retrieveLostImagesCallCount = 0;
  Duration delay = Duration.zero;

  @override
  Future<SelectedReportImage?> takePhoto() async {
    takePhotoCallCount++;
    if (delay > Duration.zero) await Future.delayed(delay);
    if (throwCameraException) throw ImagePickerException(cameraErrorMessage);
    return imageToReturnFromCamera;
  }

  @override
  Future<List<SelectedReportImage>> pickFromGallery({required int maxImages}) async {
    pickFromGalleryCallCount++;
    if (delay > Duration.zero) await Future.delayed(delay);
    if (throwGalleryException) throw ImagePickerException(galleryErrorMessage);
    return imagesToReturnFromGallery.take(maxImages).toList();
  }

  @override
  Future<List<SelectedReportImage>> retrieveLostImages() async {
    retrieveLostImagesCallCount++;
    return lostImagesToReturn;
  }
}

class FakeReportSubmissionService extends ReportSubmissionService {
  ReportSubmissionResult? resultToReturn;
  ReportSubmissionResult? retryResultToReturn;
  CreateWasteReportRequest? capturedRequest;
  List<SelectedReportImage>? capturedImages;
  WasteReportDetailModel? capturedRetryReport;
  List<SelectedReportImage>? capturedRetryImages;
  List<ReportAttachmentModel>? capturedPreviouslyUploaded;
  int submitReportCallCount = 0;
  int retryFailedUploadsCallCount = 0;
  Duration delay = Duration.zero;

  FakeReportSubmissionService({
    this.resultToReturn,
    this.retryResultToReturn,
    this.delay = Duration.zero,
  });

  @override
  Future<ReportSubmissionResult> submitReport({
    required CreateWasteReportRequest request,
    List<SelectedReportImage> images = const [],
  }) async {
    submitReportCallCount++;
    capturedRequest = request;
    capturedImages = images;
    if (delay > Duration.zero) {
      await Future.delayed(delay);
    }
    return resultToReturn ??
        ReportSubmissionFullSuccess(
          report: WasteReportDetailModel(
            id: 'c17add4f-79f3-4a0a-a9e0-150fde7d7827',
            citizenId: 'citizen-1',
            citizenName: 'Test Citizen',
            wasteType: request.wasteType,
            status: WasteReportStatus.submitted,
            description: request.description,
            latitude: request.latitude,
            longitude: request.longitude,
            addressText: request.addressText,
            createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
          ),
          uploadedAttachments: images
              .map((img) => ReportAttachmentModel(
                    id: 'att-${img.fileName}',
                    wasteReportId: 'c17add4f-79f3-4a0a-a9e0-150fde7d7827',
                    fileUrl: 'https://example.com/${img.fileName}',
                    fileType: img.fileType,
                    createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
                  ))
              .toList(),
        );
  }

  @override
  Future<ReportSubmissionResult> retryFailedUploads({
    required WasteReportDetailModel existingReport,
    required List<SelectedReportImage> failedImages,
    List<ReportAttachmentModel> previouslyUploaded = const [],
  }) async {
    retryFailedUploadsCallCount++;
    capturedRetryReport = existingReport;
    capturedRetryImages = failedImages;
    capturedPreviouslyUploaded = previouslyUploaded;
    if (delay > Duration.zero) {
      await Future.delayed(delay);
    }
    return retryResultToReturn ??
        ReportSubmissionFullSuccess(
          report: existingReport,
          uploadedAttachments: [
            ...previouslyUploaded,
            ...failedImages.map((img) => ReportAttachmentModel(
                  id: 'att-${img.fileName}',
                  wasteReportId: existingReport.id,
                  fileUrl: 'https://example.com/${img.fileName}',
                  fileType: img.fileType,
                  createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
                )),
          ],
        );
  }
}

Widget createReportWasteTestApp({
  WasteType? initialWasteType,
  String? initialDescription,
  double? initialLatitude,
  double? initialLongitude,
  String? initialAddressText,
  List<SelectedReportImage>? initialImages,
  LocationService? locationService,
  ImagePickerService? imagePickerService,
  ReportSubmissionService? submissionService,
  TileProvider? tileProvider,
  Widget Function(BuildContext context, SelectedReportImage image)? imagePreviewBuilder,
}) {
  return MaterialApp(
    theme: AppTheme.lightTheme,
    home: ReportWasteScreen(
      initialWasteType: initialWasteType,
      initialDescription: initialDescription,
      initialLatitude: initialLatitude,
      initialLongitude: initialLongitude,
      initialAddressText: initialAddressText,
      initialImages: initialImages,
      locationService: locationService,
      imagePickerService: imagePickerService,
      submissionService: submissionService ?? FakeReportSubmissionService(),
      tileProvider: tileProvider ?? FakeTestTileProvider(),
      imagePreviewBuilder: imagePreviewBuilder ??
          (context, img) => Container(
                key: Key('mock_preview_${img.fileName}'),
                color: Colors.grey.shade300,
                alignment: Alignment.center,
                child: Text(img.fileName, style: const TextStyle(fontSize: 10)),
              ),
    ),
  );
}

void main() {
  group('ReportWasteScreen Rendering Tests', () {
    testWidgets('renders AppBar, intro, sections, and submit button', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp());
      await tester.pumpAndSettle();

      // AppBar
      expect(find.text('Report Waste'), findsOneWidget);
      expect(find.byKey(const Key('report_waste_back_button')), findsOneWidget);

      // Intro
      expect(find.text('Report a Waste Issue'), findsOneWidget);

      // Waste Type section
      expect(find.text('Waste Type'), findsOneWidget);
      for (final type in WasteType.values) {
        expect(find.text(type.displayName), findsOneWidget);
        expect(find.byKey(Key('waste_type_chip_${type.value.toLowerCase()}')), findsOneWidget);
      }

      // Description section
      expect(find.text('Description'), findsOneWidget);
      expect(find.byKey(const Key('report_waste_description_field')), findsAtLeastNWidgets(1));
      expect(find.byKey(const Key('description_character_count')), findsOneWidget);
      expect(find.text('0 / 1000'), findsOneWidget);

      // Location section
      expect(find.text('Location'), findsOneWidget);
      expect(find.byKey(const Key('location_status_text')), findsOneWidget);
      expect(find.text('No location selected'), findsOneWidget);
      expect(find.byKey(const Key('use_current_location_button')), findsOneWidget);
      expect(find.byKey(const Key('choose_on_map_button')), findsOneWidget);

      // Scroll to reveal remaining sections
      await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -300));
      await tester.pumpAndSettle();

      // Optional Address section
      expect(find.text('Address / Landmark (optional)'), findsOneWidget);
      expect(find.byKey(const Key('report_waste_address_field')), findsAtLeastNWidgets(1));

      // Photo Evidence section
      expect(find.text('Photo Evidence'), findsOneWidget);
      expect(
        find.text('Add up to 3 photos to help officers identify the issue.'),
        findsOneWidget,
      );
      expect(find.byKey(const Key('add_photo_button')), findsOneWidget);

      // Submit Button
      expect(find.byKey(const Key('submit_report_button')), findsOneWidget);
      expect(find.text('Submit Report'), findsOneWidget);
    });
  });

  group('Waste Type Selection Tests', () {
    testWidgets('allows selecting exactly one waste type and toggles selection', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp());
      await tester.pumpAndSettle();

      final generalChip = find.byKey(const Key('waste_type_chip_general'));
      final organicChip = find.byKey(const Key('waste_type_chip_organic'));

      // Select General
      await tester.tap(generalChip);
      await tester.pumpAndSettle();

      ChoiceChip chipWidget = tester.widget<ChoiceChip>(generalChip);
      expect(chipWidget.selected, isTrue);

      // Switch to Organic
      await tester.tap(organicChip);
      await tester.pumpAndSettle();

      chipWidget = tester.widget<ChoiceChip>(generalChip);
      expect(chipWidget.selected, isFalse);

      ChoiceChip organicWidget = tester.widget<ChoiceChip>(organicChip);
      expect(organicWidget.selected, isTrue);
    });
  });

  group('Description Field Validation Tests', () {
    testWidgets('updates live character counter as user types', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp());
      await tester.pumpAndSettle();

      final descField = find.byKey(const Key('report_waste_description_field'));
      await tester.enterText(descField, 'Overflowing bin');
      await tester.pumpAndSettle();

      expect(find.text('15 / 1000'), findsOneWidget);
    });

    testWidgets('validates description min length 10 and max length 1000', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.general,
        initialLatitude: 6.9271,
        initialLongitude: 79.8612,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);

      // 1. Empty description
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();
      expect(find.text('Please provide a description.'), findsOneWidget);

      // 2. Less than 10 characters
      final descField = find.byKey(const Key('report_waste_description_field'));
      await tester.enterText(descField, 'Short 123'); // 9 characters
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();
      expect(find.text('Please provide at least 10 characters.'), findsOneWidget);

      // 3. Greater than 1000 characters
      final longText = 'A' * 1001;
      await tester.enterText(descField, longText);
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();
      expect(find.text('Description cannot exceed 1000 characters.'), findsOneWidget);

      // 4. Exactly 10 characters (valid)
      await tester.enterText(descField, '1234567890');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();
      expect(find.text('Please provide a description.'), findsNothing);
      expect(find.text('Please provide at least 10 characters.'), findsNothing);
      expect(find.text('Description cannot exceed 1000 characters.'), findsNothing);
    });
  });

  group('Missing Required Fields Validation Tests', () {
    testWidgets('shows error when submitting without selecting waste type', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialDescription: 'Valid description text here',
        initialLatitude: 6.9271,
        initialLongitude: 79.8612,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('waste_type_error_text')), findsOneWidget);
      expect(find.text('Please select a waste type.'), findsOneWidget);
    });

    testWidgets('shows error when submitting without selecting location', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.recyclable,
        initialDescription: 'Valid description text here',
        initialLatitude: null,
        initialLongitude: null,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('location_error_text')), findsOneWidget);
      expect(find.text('Please select a location for the report.'), findsOneWidget);
    });
  });

  group('Location Section & State Tests', () {
    testWidgets('tapping Use Current Location triggers location service and updates selected coordinates', (tester) async {
      final fakeLocationService = FakeLocationService(
        positionToReturn: const SelectedLocation(latitude: 6.9150, longitude: 79.8650),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        locationService: fakeLocationService,
      ));
      await tester.pumpAndSettle();

      expect(find.text('No location selected'), findsOneWidget);

      final currentLocBtn = find.byKey(const Key('use_current_location_button'));
      await tester.ensureVisible(currentLocBtn);
      await tester.tap(currentLocBtn);
      await tester.pumpAndSettle();

      expect(fakeLocationService.getCurrentLocationCallCount, equals(1));
      expect(find.text('Location Selected ✓'), findsOneWidget);
      expect(find.byKey(const Key('selected_coordinates_text')), findsOneWidget);
      expect(find.text('6.91500, 79.86500'), findsOneWidget);
    });

    testWidgets('shows loading state on Use Current Location button while locating', (tester) async {
      final fakeLocationService = FakeLocationService(
        delay: const Duration(milliseconds: 500),
        positionToReturn: const SelectedLocation(latitude: 6.9150, longitude: 79.8650),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        locationService: fakeLocationService,
      ));
      await tester.pumpAndSettle();

      final currentLocBtn = find.byKey(const Key('use_current_location_button'));
      await tester.ensureVisible(currentLocBtn);
      await tester.tap(currentLocBtn);
      await tester.pump(const Duration(milliseconds: 100));

      // Loading spinner active inside the button
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      expect(find.text('Location Selected ✓'), findsOneWidget);
    });

    testWidgets('location failure (services disabled) shows citizen-friendly message and leaves Choose on Map available', (tester) async {
      final fakeLocationService = FakeLocationService(serviceEnabled: false);

      await tester.pumpWidget(createReportWasteTestApp(
        locationService: fakeLocationService,
      ));
      await tester.pumpAndSettle();

      final currentLocBtn = find.byKey(const Key('use_current_location_button'));
      await tester.ensureVisible(currentLocBtn);
      await tester.tap(currentLocBtn);
      await tester.pumpAndSettle();

      expect(find.textContaining('Location services are turned off'), findsOneWidget);
      expect(find.text('Settings'), findsOneWidget);
      expect(find.text('No location selected'), findsOneWidget);
      expect(find.byKey(const Key('choose_on_map_button')), findsOneWidget);
    });

    testWidgets('location failure (permission denied) shows friendly message and preserves form data', (tester) async {
      final fakeLocationService = FakeLocationService(
        permission: LocationPermission.denied,
      );

      await tester.pumpWidget(createReportWasteTestApp(
        initialDescription: 'Important dumping issue here',
        initialWasteType: WasteType.bulky,
        locationService: fakeLocationService,
      ));
      await tester.pumpAndSettle();

      final currentLocBtn = find.byKey(const Key('use_current_location_button'));
      await tester.ensureVisible(currentLocBtn);
      await tester.tap(currentLocBtn);
      await tester.pumpAndSettle();

      expect(find.textContaining('Location permission is needed'), findsOneWidget);
      // Form fields are preserved
      expect(find.text('Important dumping issue here'), findsOneWidget);
      final chip = tester.widget<ChoiceChip>(find.byKey(const Key('waste_type_chip_bulky')));
      expect(chip.selected, isTrue);
      // No fake coordinates injected
      expect(find.text('No location selected'), findsOneWidget);
    });

    testWidgets('location failure (timeout) shows friendly error message and does not set fake coordinates', (tester) async {
      final fakeLocationService = FakeLocationService(
        currentLocationResult: const LocationFailure(
          reason: LocationFailureReason.timeout,
          message: 'Location request timed out. Please try again or choose on map.',
        ),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        locationService: fakeLocationService,
      ));
      await tester.pumpAndSettle();

      final currentLocBtn = find.byKey(const Key('use_current_location_button'));
      await tester.ensureVisible(currentLocBtn);
      await tester.tap(currentLocBtn);
      await tester.pumpAndSettle();

      expect(find.textContaining('Location request timed out'), findsOneWidget);
      expect(find.text('No location selected'), findsOneWidget);
    });

    testWidgets('renders Location Selected state with 5-decimal precision and allows change', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialLatitude: 6.9271,
        initialLongitude: 79.8612,
      ));
      await tester.pumpAndSettle();

      expect(find.text('Location Selected ✓'), findsOneWidget);
      expect(find.byKey(const Key('selected_coordinates_text')), findsOneWidget);
      expect(find.text('6.92710, 79.86120'), findsOneWidget);

      // Change button clears location
      final changeBtn = find.byKey(const Key('clear_location_button'));
      await tester.ensureVisible(changeBtn);
      await tester.tap(changeBtn);
      await tester.pumpAndSettle();

      expect(find.text('No location selected'), findsOneWidget);
      expect(find.byKey(const Key('use_current_location_button')), findsOneWidget);
      expect(find.byKey(const Key('choose_on_map_button')), findsOneWidget);
    });

    testWidgets('changing location clears coordinates without clearing description, waste type, or address text', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.hazardous,
        initialDescription: 'Chemical drum leaking into grass',
        initialAddressText: 'Plot 4, Industrial Zone',
        initialLatitude: 6.9271,
        initialLongitude: 79.8612,
      ));
      await tester.pumpAndSettle();

      expect(find.text('Location Selected ✓'), findsOneWidget);

      // Tap Change
      final changeBtn = find.byKey(const Key('clear_location_button'));
      await tester.ensureVisible(changeBtn);
      await tester.tap(changeBtn);
      await tester.pumpAndSettle();

      // Location is cleared to allow re-selection
      expect(find.text('No location selected'), findsOneWidget);

      // All other fields remain preserved
      expect(find.text('Chemical drum leaking into grass'), findsOneWidget);
      expect(find.text('Plot 4, Industrial Zone'), findsOneWidget);
      final chip = tester.widget<ChoiceChip>(find.byKey(const Key('waste_type_chip_hazardous')));
      expect(chip.selected, isTrue);
    });

    testWidgets('tapping Choose on Map navigates to MapLocationPickerScreen and returns selected coordinates', (tester) async {
      final fakeLocationService = FakeLocationService();

      await tester.pumpWidget(createReportWasteTestApp(
        locationService: fakeLocationService,
      ));
      await tester.pumpAndSettle();

      final mapBtn = find.byKey(const Key('choose_on_map_button'));
      await tester.ensureVisible(mapBtn);
      await tester.tap(mapBtn);
      await tester.pumpAndSettle();

      // Screen is pushed
      expect(find.byType(MapLocationPickerScreen), findsOneWidget);
      expect(find.text('Select Waste Location'), findsOneWidget);

      // Tap on the map to select a point
      final mapFinder = find.byType(FlutterMap);
      await tester.tap(mapFinder);
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      // Confirm
      final confirmBtn = find.byKey(const Key('use_this_location_button'));
      await tester.tap(confirmBtn);
      await tester.pumpAndSettle();

      // Returned to ReportWasteScreen with location selected!
      expect(find.byType(ReportWasteScreen), findsOneWidget);
      expect(find.text('Location Selected ✓'), findsOneWidget);
      expect(find.byKey(const Key('selected_coordinates_text')), findsOneWidget);
    });
  });

  group('Address Field Tests', () {
    testWidgets('address is optional and validates max 500 characters', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.organic,
        initialDescription: 'Valid organic waste dump',
        initialLatitude: 6.9000,
        initialLongitude: 79.8500,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      final addressField = find.byKey(const Key('report_waste_address_field'));

      // 1. Exceeding 500 characters shows error
      await tester.ensureVisible(addressField.first);
      await tester.enterText(addressField, 'B' * 501);
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();
      expect(find.text('Address cannot exceed 500 characters.'), findsOneWidget);

      // 2. Valid address under 500 characters passes and submits
      await tester.enterText(addressField, 'Near Market Gate 2');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();
      expect(find.text('Address cannot exceed 500 characters.'), findsNothing);
      expect(find.byKey(const Key('submission_success_dialog')), findsOneWidget);
    });
  });

  group('Photo Evidence Selection Tests', () {
    const sampleImg1 = SelectedReportImage(
      path: '/mock/evidence_1.jpg',
      fileName: 'evidence_1.jpg',
      fileType: 'image/jpeg',
      sizeBytes: 1024 * 100,
    );
    const sampleImg2 = SelectedReportImage(
      path: '/mock/evidence_2.png',
      fileName: 'evidence_2.png',
      fileType: 'image/png',
      sizeBytes: 1024 * 200,
    );
    const sampleImg3 = SelectedReportImage(
      path: '/mock/evidence_3.webp',
      fileName: 'evidence_3.webp',
      fileType: 'image/webp',
      sizeBytes: 1024 * 150,
    );

    testWidgets('renders photo guidance and counter showing 0 / 3 initially', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp());
      await tester.pumpAndSettle();

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      expect(find.text('Add up to 3 photos to help officers identify the issue.'), findsOneWidget);
      expect(find.text('Attach photos to help officers locate and assess the waste.'), findsOneWidget);
      expect(find.byKey(const Key('photo_evidence_counter')), findsOneWidget);
      expect(find.text('0 / 3'), findsOneWidget);
    });

    testWidgets('tapping Add Photo opens modal bottom sheet and cancels cleanly', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp());
      await tester.pumpAndSettle();

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      await tester.tap(addPhotoBtn);
      await tester.pumpAndSettle();

      // Bottom sheet content
      expect(find.descendant(of: find.byType(BottomSheet), matching: find.text('Add Photo')), findsOneWidget);
      expect(find.byKey(const Key('take_photo_option')), findsOneWidget);
      expect(find.byKey(const Key('choose_gallery_option')), findsOneWidget);
      expect(find.byKey(const Key('cancel_photo_picker_button')), findsOneWidget);

      // Cancel dismisses sheet
      await tester.tap(find.byKey(const Key('cancel_photo_picker_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('take_photo_option')), findsNothing);
      expect(find.text('0 / 3'), findsOneWidget);
    });

    testWidgets('Take Photo captures image, adds preview tile, and increments counter', (tester) async {
      final fakePicker = FakeImagePickerService();
      fakePicker.imageToReturnFromCamera = sampleImg1;

      await tester.pumpWidget(createReportWasteTestApp(imagePickerService: fakePicker));
      await tester.pumpAndSettle();

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      await tester.tap(addPhotoBtn);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(fakePicker.takePhotoCallCount, equals(1));
      expect(find.text('1 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsOneWidget);
      expect(find.text('evidence_1.jpg'), findsOneWidget);
    });

    testWidgets('Take Photo cancellation preserves form without adding photo or error', (tester) async {
      final fakePicker = FakeImagePickerService();
      fakePicker.imageToReturnFromCamera = null;

      await tester.pumpWidget(createReportWasteTestApp(imagePickerService: fakePicker));
      await tester.pumpAndSettle();

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      await tester.tap(addPhotoBtn);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(fakePicker.takePhotoCallCount, equals(1));
      expect(find.text('0 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsNothing);
    });

    testWidgets('Choose from Gallery picks multiple images and respects remaining slots', (tester) async {
      final fakePicker = FakeImagePickerService();
      fakePicker.imagesToReturnFromGallery = [sampleImg1, sampleImg2];

      await tester.pumpWidget(createReportWasteTestApp(imagePickerService: fakePicker));
      await tester.pumpAndSettle();

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      await tester.tap(addPhotoBtn);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('choose_gallery_option')));
      await tester.pumpAndSettle();

      expect(fakePicker.pickFromGalleryCallCount, equals(1));
      expect(find.text('2 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_1')), findsOneWidget);
    });

    testWidgets('Max 3 photos hides Add Photo button and blocks 4th photo', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialImages: [sampleImg1, sampleImg2, sampleImg3],
      ));
      await tester.pumpAndSettle();

      expect(find.text('3 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_1')), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_2')), findsOneWidget);
      expect(find.byKey(const Key('add_photo_button')), findsNothing);
    });

    testWidgets('Removing a photo updates local state, decrements counter, and restores Add Photo button', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialImages: [sampleImg1, sampleImg2, sampleImg3],
      ));
      await tester.pumpAndSettle();

      expect(find.text('3 / 3'), findsOneWidget);
      expect(find.byKey(const Key('add_photo_button')), findsNothing);

      // Remove photo at index 1
      final removeBtn = find.byKey(const Key('remove_photo_button_1'));
      await tester.ensureVisible(removeBtn);
      await tester.tap(removeBtn);
      await tester.pumpAndSettle();

      expect(find.text('2 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_1')), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_2')), findsNothing);
      expect(find.byKey(const Key('add_photo_button')), findsOneWidget);
    });

    testWidgets('Duplicate photo is rejected with friendly notice and not added', (tester) async {
      final fakePicker = FakeImagePickerService();
      fakePicker.imageToReturnFromCamera = sampleImg1;

      await tester.pumpWidget(createReportWasteTestApp(
        initialImages: [sampleImg1],
        imagePickerService: fakePicker,
      ));
      await tester.pumpAndSettle();

      expect(find.text('1 / 3'), findsOneWidget);

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      await tester.tap(addPhotoBtn);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(find.text('That photo has already been added.'), findsOneWidget);
      expect(find.text('1 / 3'), findsOneWidget);
    });

    testWidgets('Picker service error shows friendly SnackBar', (tester) async {
      final fakePicker = FakeImagePickerService();
      fakePicker.throwCameraException = true;
      fakePicker.cameraErrorMessage = 'Camera is not available or permission was denied.';

      await tester.pumpWidget(createReportWasteTestApp(imagePickerService: fakePicker));
      await tester.pumpAndSettle();

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      await tester.tap(addPhotoBtn);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(find.text('Camera is not available or permission was denied.'), findsOneWidget);
      expect(find.text('0 / 3'), findsOneWidget);
    });

    testWidgets('Oversized photo error shows friendly SnackBar', (tester) async {
      final fakePicker = FakeImagePickerService();
      fakePicker.throwGalleryException = true;
      fakePicker.galleryErrorMessage = 'Each photo must be 5 MB or smaller.';

      await tester.pumpWidget(createReportWasteTestApp(imagePickerService: fakePicker));
      await tester.pumpAndSettle();

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      await tester.tap(addPhotoBtn);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('choose_gallery_option')));
      await tester.pumpAndSettle();

      expect(find.text('Each photo must be 5 MB or smaller.'), findsOneWidget);
    });

    testWidgets('Unsupported format error shows friendly SnackBar', (tester) async {
      final fakePicker = FakeImagePickerService();
      fakePicker.throwCameraException = true;
      fakePicker.cameraErrorMessage = 'Only JPEG, PNG, or WebP photos are supported.';

      await tester.pumpWidget(createReportWasteTestApp(imagePickerService: fakePicker));
      await tester.pumpAndSettle();

      final addPhotoBtn = find.byKey(const Key('add_photo_button'));
      await tester.ensureVisible(addPhotoBtn);
      await tester.tap(addPhotoBtn);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('take_photo_option')));
      await tester.pumpAndSettle();

      expect(find.text('Only JPEG, PNG, or WebP photos are supported.'), findsOneWidget);
    });

    testWidgets('Selected photos survive GPS current location updates', (tester) async {
      final fakeLocation = FakeLocationService(
        positionToReturn: const SelectedLocation(latitude: 6.9200, longitude: 79.8500),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        initialImages: [sampleImg1],
        locationService: fakeLocation,
      ));
      await tester.pumpAndSettle();

      expect(find.text('1 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsOneWidget);

      final gpsBtn = find.byKey(const Key('use_current_location_button'));
      await tester.ensureVisible(gpsBtn);
      await tester.tap(gpsBtn);
      await tester.pumpAndSettle();

      expect(find.text('6.92000, 79.85000'), findsOneWidget);
      // Photos remain intact
      expect(find.text('1 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsOneWidget);
    });

    testWidgets('Selected photos survive map location picker updates', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialImages: [sampleImg1],
      ));
      await tester.pumpAndSettle();

      expect(find.text('1 / 3'), findsOneWidget);

      final mapBtn = find.byKey(const Key('choose_on_map_button'));
      await tester.ensureVisible(mapBtn);
      await tester.tap(mapBtn);
      await tester.pumpAndSettle();

      expect(find.byType(MapLocationPickerScreen), findsOneWidget);

      // Tap back without changing
      await tester.tap(find.byKey(const Key('map_picker_back_button')));
      await tester.pumpAndSettle();

      expect(find.byType(ReportWasteScreen), findsOneWidget);
      expect(find.text('1 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsOneWidget);
    });

    testWidgets('Lost data recovery on screen initialization populates photos', (tester) async {
      final fakePicker = FakeImagePickerService();
      fakePicker.lostImagesToReturn = [sampleImg1];

      await tester.pumpWidget(createReportWasteTestApp(
        imagePickerService: fakePicker,
      ));
      await tester.pumpAndSettle();

      expect(fakePicker.retrieveLostImagesCallCount, equals(1));
      expect(find.text('1 / 3'), findsOneWidget);
      expect(find.byKey(const Key('photo_preview_tile_0')), findsOneWidget);
    });

    testWidgets('Photo evidence is optional for valid report submission (0 photos)', (tester) async {
      final fakeService = FakeReportSubmissionService();

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.general,
        initialDescription: 'Overflowing dumpster behind public market',
        initialLatitude: 6.9100,
        initialLongitude: 79.8600,
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('submission_success_dialog')), findsOneWidget);
      expect(find.text('No photos attached.'), findsOneWidget);
    });

    testWidgets('Photo evidence submission with selected photos (2 photos)', (tester) async {
      final fakeService = FakeReportSubmissionService();
      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.general,
        initialDescription: 'Overflowing dumpster behind public market',
        initialLatitude: 6.9100,
        initialLongitude: 79.8600,
        initialImages: [sampleImg1, sampleImg2],
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('submission_success_dialog')), findsOneWidget);
      expect(find.text('2 photos attached.'), findsOneWidget);
    });
  });

  group('Step 9A.8.5 Real Citizen Report Submission & Invariants', () {
    const sampleImg1 = SelectedReportImage(
      path: '/mock/evidence_1.jpg',
      fileName: 'evidence_1.jpg',
      fileType: 'image/jpeg',
      sizeBytes: 1024 * 100,
    );
    const sampleImg2 = SelectedReportImage(
      path: '/mock/evidence_2.png',
      fileName: 'evidence_2.png',
      fileType: 'image/png',
      sizeBytes: 1024 * 200,
    );
    const sampleImg3 = SelectedReportImage(
      path: '/mock/evidence_3.webp',
      fileName: 'evidence_3.webp',
      fileType: 'image/webp',
      sizeBytes: 1024 * 150,
    );

    testWidgets('full success with 0 photos displays success dialog and reference ID', (tester) async {
      final fakeService = FakeReportSubmissionService();

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.general,
        initialDescription: 'Overflowing dumpster behind market area',
        initialLatitude: 6.9200,
        initialLongitude: 79.8600,
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(fakeService.submitReportCallCount, equals(1));
      expect(fakeService.capturedRequest?.wasteType, equals(WasteType.general));
      expect(fakeService.capturedRequest?.description, equals('Overflowing dumpster behind market area'));
      expect(fakeService.capturedImages, isEmpty);

      expect(find.byKey(const Key('submission_success_dialog')), findsOneWidget);
      expect(find.text('Report Submitted'), findsOneWidget);
      expect(find.text('No photos attached.'), findsOneWidget);
      expect(find.byKey(const Key('submission_success_reference_text')), findsOneWidget);
      expect(find.text('Report C17ADD4F'), findsOneWidget);
    });

    testWidgets('submission success displays the backend report reference', (tester) async {
      final fakeService = FakeReportSubmissionService(
        resultToReturn: ReportSubmissionFullSuccess(
          report: WasteReportDetailModel(
            id: 'c17add4f-79f3-4a0a-a9e0-150fde7d7827',
            reportReference: 'A1B2C3D4',
            citizenId: 'citizen-1',
            citizenName: 'Test Citizen',
            wasteType: WasteType.general,
            status: WasteReportStatus.submitted,
            description: 'Overflowing dumpster behind market area',
            latitude: 6.9200,
            longitude: 79.8600,
            createdAt: DateTime.utc(2026, 9, 16, 12),
          ),
          uploadedAttachments: const [],
        ),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.general,
        initialDescription: 'Overflowing dumpster behind market area',
        initialLatitude: 6.9200,
        initialLongitude: 79.8600,
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(fakeService.submitReportCallCount, 1);
      expect(find.text('Report A1B2C3D4'), findsOneWidget);
      expect(find.text('Report C17ADD4F'), findsNothing);
    });

    testWidgets('full success with 3 photos uploads all photos and displays dialog', (tester) async {
      final fakeService = FakeReportSubmissionService();

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.recyclable,
        initialDescription: 'Piles of recyclable plastic discarded in public park',
        initialLatitude: 6.9150,
        initialLongitude: 79.8550,
        initialImages: [sampleImg1, sampleImg2, sampleImg3],
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(fakeService.submitReportCallCount, equals(1));
      expect(fakeService.capturedImages?.length, equals(3));
      expect(find.byKey(const Key('submission_success_dialog')), findsOneWidget);
      expect(find.text('3 photos attached.'), findsOneWidget);
    });

    testWidgets('double-submit prevention: submit button disabled during submission and called once', (tester) async {
      final fakeService = FakeReportSubmissionService(delay: const Duration(milliseconds: 200));

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.organic,
        initialDescription: 'Organic food waste dumped in open drainage canal',
        initialLatitude: 6.9300,
        initialLongitude: 79.8700,
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);

      // First tap begins submission
      await tester.tap(submitBtn);
      await tester.pump(const Duration(milliseconds: 50));

      // Second tap while still in progress
      await tester.tap(submitBtn);
      await tester.pump(const Duration(milliseconds: 50));

      // Settle the delayed completion
      await tester.pumpAndSettle();

      expect(fakeService.submitReportCallCount, equals(1));
      expect(find.byKey(const Key('submission_success_dialog')), findsOneWidget);
    });

    testWidgets('report creation failure shows error SnackBar and keeps form editable', (tester) async {
      final fakeService = FakeReportSubmissionService(
        resultToReturn: const ReportCreationFailure(
          userFacingMessage: "We couldn't submit your report. Please check your connection and try again.",
        ),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.hazardous,
        initialDescription: 'Chemical drum leaking into soil behind factory',
        initialLatitude: 6.9400,
        initialLongitude: 79.8800,
        initialAddressText: 'Factory lane 4',
        initialImages: [sampleImg1],
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(fakeService.submitReportCallCount, equals(1));
      expect(find.byKey(const Key('submission_error_snackbar')), findsOneWidget);
      expect(find.text("We couldn't submit your report. Please check your connection and try again."), findsOneWidget);

      // Form remains intact and not locked
      final state = tester.state<ReportWasteScreenState>(find.byType(ReportWasteScreen));
      expect(state.createdReport, isNull);
      expect(state.isSubmitting, isFalse);

      // Button is still enabled for retry
      expect(find.byKey(const Key('submit_report_button')), findsOneWidget);
    });

    testWidgets('partial photo failure displays recovery card and locks form inputs', (tester) async {
      final fakeReport = WasteReportDetailModel(
        id: 'report-abc-12345678',
        citizenId: 'citizen-1',
        citizenName: 'Jane Citizen',
        wasteType: WasteType.general,
        status: WasteReportStatus.submitted,
        description: 'Overflowing dumpsters with broken glass and debris',
        latitude: 6.9200,
        longitude: 79.8600,
        createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
      );
      final uploadedAtt1 = ReportAttachmentModel(
        id: 'att-1',
        wasteReportId: 'report-abc-12345678',
        fileUrl: 'https://example.com/evidence_1.jpg',
        fileType: 'image/jpeg',
        createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
      );
      final failedAtt2 = const FailedReportImage(
        image: sampleImg2,
        userFacingMessage: 'Image payload exceeded 10 MB limit.',
      );

      final fakeService = FakeReportSubmissionService(
        resultToReturn: ReportSubmissionPartialSuccess(
          report: fakeReport,
          uploadedAttachments: [uploadedAtt1],
          failedImages: [failedAtt2],
        ),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.general,
        initialDescription: 'Overflowing dumpsters with broken glass and debris',
        initialLatitude: 6.9200,
        initialLongitude: 79.8600,
        initialImages: [sampleImg1, sampleImg2],
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      // Report created authoritatively
      final state = tester.state<ReportWasteScreenState>(find.byType(ReportWasteScreen));
      expect(state.createdReport, isNotNull);
      expect(state.createdReport?.id, equals('report-abc-12345678'));
      expect(state.failedImages.length, equals(1));
      expect(state.uploadedAttachments.length, equals(1));

      // Partial success recovery UI is shown
      expect(find.byKey(const Key('partial_success_card')), findsOneWidget);
      expect(find.byKey(const Key('partial_success_header')), findsOneWidget);
      expect(find.text('Report Submitted'), findsOneWidget);
      expect(find.text('1 of 2 photos uploaded successfully.'), findsOneWidget);
      expect(find.textContaining('Image payload exceeded 10 MB limit.'), findsOneWidget);

      // Recovery buttons are present
      expect(find.byKey(const Key('retry_failed_photos_button')), findsOneWidget);
      expect(find.byKey(const Key('finish_partial_submission_button')), findsOneWidget);

      // Form fields are locked
      expect(find.byKey(const Key('clear_location_button')), findsNothing);
      expect(find.byKey(const Key('add_photo_button')), findsNothing);
      expect(find.byKey(const Key('remove_photo_button_0')), findsNothing);
      expect(find.byKey(const Key('remove_photo_button_1')), findsNothing);
    });

    testWidgets('retry failed photos calls retryFailedUploads and transitions to full success', (tester) async {
      final fakeReport = WasteReportDetailModel(
        id: 'report-abc-12345678',
        citizenId: 'citizen-1',
        citizenName: 'Jane Citizen',
        wasteType: WasteType.general,
        status: WasteReportStatus.submitted,
        description: 'Overflowing dumpsters with broken glass and debris',
        latitude: 6.9200,
        longitude: 79.8600,
        createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
      );
      final uploadedAtt1 = ReportAttachmentModel(
        id: 'att-1',
        wasteReportId: 'report-abc-12345678',
        fileUrl: 'https://example.com/evidence_1.jpg',
        fileType: 'image/jpeg',
        createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
      );
      final uploadedAtt2 = ReportAttachmentModel(
        id: 'att-2',
        wasteReportId: 'report-abc-12345678',
        fileUrl: 'https://example.com/evidence_2.png',
        fileType: 'image/png',
        createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
      );
      final failedAtt2 = const FailedReportImage(
        image: sampleImg2,
        userFacingMessage: 'Temporary timeout.',
      );

      final fakeService = FakeReportSubmissionService(
        resultToReturn: ReportSubmissionPartialSuccess(
          report: fakeReport,
          uploadedAttachments: [uploadedAtt1],
          failedImages: [failedAtt2],
        ),
        retryResultToReturn: ReportSubmissionFullSuccess(
          report: fakeReport,
          uploadedAttachments: [uploadedAtt1, uploadedAtt2],
        ),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.general,
        initialDescription: 'Overflowing dumpsters with broken glass and debris',
        initialLatitude: 6.9200,
        initialLongitude: 79.8600,
        initialImages: [sampleImg1, sampleImg2],
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      // Submit report initially
      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(fakeService.submitReportCallCount, equals(1));
      expect(find.byKey(const Key('retry_failed_photos_button')), findsOneWidget);

      // Tap Retry Failed Photos
      final retryBtn = find.byKey(const Key('retry_failed_photos_button'));
      await tester.ensureVisible(retryBtn);
      await tester.tap(retryBtn);
      await tester.pumpAndSettle();

      // Verify retry called with ONLY the failed photo and did NOT call submitReport again
      expect(fakeService.submitReportCallCount, equals(1));
      expect(fakeService.retryFailedUploadsCallCount, equals(1));
      expect(fakeService.capturedRetryImages?.length, equals(1));
      expect(fakeService.capturedRetryImages?.first.fileName, equals('evidence_2.png'));

      // Transitions to full success dialog
      expect(find.byKey(const Key('submission_success_dialog')), findsOneWidget);
      expect(find.text('2 photos attached.'), findsOneWidget);
    });

    testWidgets('finish button on partial failure returns without deleting created report', (tester) async {
      final fakeReport = WasteReportDetailModel(
        id: 'report-xyz-99887766',
        citizenId: 'citizen-1',
        citizenName: 'Jane Citizen',
        wasteType: WasteType.bulky,
        status: WasteReportStatus.submitted,
        description: 'Concrete slabs left on pedestrian walkway blocking footpath',
        latitude: 6.9250,
        longitude: 79.8650,
        createdAt: DateTime.utc(2026, 9, 16, 12, 0, 0),
      );

      final fakeService = FakeReportSubmissionService(
        resultToReturn: ReportSubmissionPartialSuccess(
          report: fakeReport,
          uploadedAttachments: const [],
          failedImages: const [
            FailedReportImage(image: sampleImg1, userFacingMessage: 'Upload error'),
          ],
        ),
      );

      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.bulky,
        initialDescription: 'Concrete slabs left on pedestrian walkway blocking footpath',
        initialLatitude: 6.9250,
        initialLongitude: 79.8650,
        initialImages: [sampleImg1],
        submissionService: fakeService,
      ));
      await tester.pumpAndSettle();

      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('finish_partial_submission_button')), findsOneWidget);

      // Verify state preserves created report
      final state = tester.state<ReportWasteScreenState>(find.byType(ReportWasteScreen));
      expect(state.createdReport?.id, equals('report-xyz-99887766'));

      // Tap finish
      final finishBtn = find.byKey(const Key('finish_partial_submission_button'));
      await tester.ensureVisible(finishBtn);
      await tester.tap(finishBtn);
      await tester.pumpAndSettle();

      // In single MaterialApp home without nav stack, pop is called safely without exception
      expect(tester.takeException(), isNull);
    });
  });

  group('No Internal Development Step Labels In UI', () {
    testWidgets('does not expose internal development step labels to Citizens', (tester) async {
      await tester.pumpWidget(createReportWasteTestApp(
        initialWasteType: WasteType.hazardous,
        initialDescription: 'Hazardous chemical spill',
        initialLatitude: 6.9200,
        initialLongitude: 79.8600,
      ));
      await tester.pumpAndSettle();

      expect(find.textContaining('Step 9A.8.3'), findsNothing);
      expect(find.textContaining('Step 9A.8.4'), findsNothing);
      expect(find.textContaining('Step 9A.8.5'), findsNothing);

      // Trigger submit to inspect validated state
      final submitBtn = find.byKey(const Key('submit_report_button'));
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(find.textContaining('Step 9A.8.3'), findsNothing);
      expect(find.textContaining('Step 9A.8.4'), findsNothing);
      expect(find.textContaining('Step 9A.8.5'), findsNothing);
    });
  });

  group('Responsive Viewport Tests', () {
    testWidgets('renders cleanly without overflow and displays full location action labels on narrow 320px viewport', (tester) async {
      tester.view.physicalSize = const Size(320 * 3.0, 640 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createReportWasteTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(ReportWasteScreen), findsOneWidget);
      expect(find.text('Report Waste'), findsOneWidget);

      // Verify location buttons are present with full labels
      final useCurrentBtn = find.byKey(const Key('use_current_location_button'));
      final chooseMapBtn = find.byKey(const Key('choose_on_map_button'));
      expect(useCurrentBtn, findsOneWidget);
      expect(chooseMapBtn, findsOneWidget);
      expect(find.text('Use Current Location'), findsOneWidget);
      expect(find.text('Choose on Map'), findsOneWidget);

      // Verify buttons are stacked vertically on narrow 320px viewport
      final currentBottom = tester.getBottomLeft(useCurrentBtn).dy;
      final mapTop = tester.getTopLeft(chooseMapBtn).dy;
      expect(mapTop, greaterThanOrEqualTo(currentBottom));

      // Verify both buttons stretch to occupy the full card width
      final useCurrentWidth = tester.getSize(useCurrentBtn).width;
      final chooseMapWidth = tester.getSize(chooseMapBtn).width;
      expect(useCurrentWidth, equals(chooseMapWidth));
      expect(useCurrentWidth, greaterThan(200.0));
    });

    testWidgets('renders cleanly without overflow and displays full location action labels on standard 390px viewport', (tester) async {
      tester.view.physicalSize = const Size(390 * 3.0, 844 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createReportWasteTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(ReportWasteScreen), findsOneWidget);

      // Verify location buttons are present with full labels
      final useCurrentBtn = find.byKey(const Key('use_current_location_button'));
      final chooseMapBtn = find.byKey(const Key('choose_on_map_button'));
      expect(useCurrentBtn, findsOneWidget);
      expect(chooseMapBtn, findsOneWidget);
      expect(find.text('Use Current Location'), findsOneWidget);
      expect(find.text('Choose on Map'), findsOneWidget);

      // Verify buttons are stacked vertically on standard 390px viewport
      final currentBottom = tester.getBottomLeft(useCurrentBtn).dy;
      final mapTop = tester.getTopLeft(chooseMapBtn).dy;
      expect(mapTop, greaterThanOrEqualTo(currentBottom));

      // Verify full width utilization on standard phone screen
      final useCurrentWidth = tester.getSize(useCurrentBtn).width;
      final chooseMapWidth = tester.getSize(chooseMapBtn).width;
      expect(useCurrentWidth, equals(chooseMapWidth));
      expect(useCurrentWidth, greaterThan(280.0));
    });

    testWidgets('renders location buttons horizontally side-by-side on wide viewports without truncation', (tester) async {
      tester.view.physicalSize = const Size(600 * 3.0, 800 * 3.0);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(createReportWasteTestApp());
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);

      final useCurrentBtn = find.byKey(const Key('use_current_location_button'));
      final chooseMapBtn = find.byKey(const Key('choose_on_map_button'));
      expect(useCurrentBtn, findsOneWidget);
      expect(chooseMapBtn, findsOneWidget);
      expect(find.text('Use Current Location'), findsOneWidget);
      expect(find.text('Choose on Map'), findsOneWidget);

      // On wide viewport (constraints.maxWidth >= 420), buttons are in Row (aligned top)
      final currentTop = tester.getTopLeft(useCurrentBtn).dy;
      final mapTop = tester.getTopLeft(chooseMapBtn).dy;
      expect(currentTop, equals(mapTop));

      // Horizontally side-by-side
      final currentRight = tester.getTopRight(useCurrentBtn).dx;
      final mapLeft = tester.getTopLeft(chooseMapBtn).dx;
      expect(mapLeft, greaterThan(currentRight));
    });
  });
}
