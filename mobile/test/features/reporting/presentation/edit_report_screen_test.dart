import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:mobile/core/network/api_exception.dart';
import 'package:mobile/features/reporting/data/reporting_repository.dart';
import 'package:mobile/features/reporting/models/report_attachment_model.dart';
import 'package:mobile/features/reporting/models/selected_location.dart';
import 'package:mobile/features/reporting/models/update_waste_report_request.dart';
import 'package:mobile/features/reporting/models/waste_report_detail_model.dart';
import 'package:mobile/features/reporting/models/waste_report_status.dart';
import 'package:mobile/features/reporting/models/waste_type.dart';
import 'package:mobile/features/reporting/presentation/edit_report_screen.dart';
import 'package:mobile/features/reporting/services/location_service.dart';

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
  int getCurrentLocationCallCount = 0;

  FakeLocationService({
    this.serviceEnabled = true,
    this.permission = LocationPermission.whileInUse,
    this.positionToReturn,
    this.currentLocationResult,
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
    if (currentLocationResult != null) {
      return currentLocationResult!;
    }
    return LocationSuccess(
      positionToReturn ?? const SelectedLocation(latitude: 6.9271, longitude: 79.8612),
    );
  }

  @override
  Future<bool> openAppSettings() async => true;

  @override
  Future<bool> openLocationSettings() async => true;
}

class MockReportingRepository extends ReportingRepository {
  WasteReportDetailModel? reportToReturn;
  WasteReportDetailModel? updateResponseToReturn;
  UpdateWasteReportRequest? lastUpdateRequest;
  String? lastUpdateReportId;

  bool shouldThrowGet = false;
  String getErrorMessage = 'Error loading report';

  bool shouldThrowUpdate = false;
  int updateStatusCode = 500;
  String updateErrorMessage = 'Error updating report';

  int getReportCallCount = 0;
  int updateCallCount = 0;

  @override
  Future<WasteReportDetailModel> getWasteReport(String reportId) async {
    getReportCallCount++;
    if (shouldThrowGet) {
      throw ApiException(message: getErrorMessage, statusCode: 500);
    }
    if (reportToReturn != null) {
      return reportToReturn!;
    }
    throw const ApiException(message: 'Report not found', statusCode: 404);
  }

  @override
  Future<WasteReportDetailModel> updateWasteReport({
    required String reportId,
    required UpdateWasteReportRequest request,
  }) async {
    updateCallCount++;
    lastUpdateReportId = reportId;
    lastUpdateRequest = request;

    if (shouldThrowUpdate) {
      throw ApiException(message: updateErrorMessage, statusCode: updateStatusCode);
    }

    if (updateResponseToReturn != null) {
      return updateResponseToReturn!;
    }

    final orig = reportToReturn;
    return WasteReportDetailModel(
      id: reportId,
      citizenId: orig?.citizenId ?? 'cit-1',
      citizenName: orig?.citizenName ?? 'Jane Citizen',
      description: request.description ?? orig?.description ?? 'Default Description',
      wasteType: request.wasteType ?? orig?.wasteType ?? WasteType.general,
      latitude: request.latitude ?? orig?.latitude ?? 6.9271,
      longitude: request.longitude ?? orig?.longitude ?? 79.8612,
      addressText: request.addressText != null
          ? (request.addressText!.isEmpty ? null : request.addressText)
          : orig?.addressText,
      status: orig?.status ?? WasteReportStatus.submitted,
      attachments: orig?.attachments ?? const [],
      createdAt: orig?.createdAt ?? DateTime.utc(2026, 9, 16, 12, 0, 0),
      updatedAt: DateTime.utc(2026, 9, 17, 10, 0, 0),
    );
  }
}

void main() {
  late MockReportingRepository mockRepo;
  late FakeLocationService fakeLocation;
  late FakeTestTileProvider fakeTileProvider;

  setUp(() {
    mockRepo = MockReportingRepository();
    fakeLocation = FakeLocationService();
    fakeTileProvider = FakeTestTileProvider();
  });

  WasteReportDetailModel createSampleReport({
    String id = 'rep-1',
    String description = 'Existing overflowing trash pile on pavement',
    WasteType wasteType = WasteType.general,
    double latitude = 6.9271,
    double longitude = 79.8612,
    String? addressText = 'Galle Road, Colombo 03',
    WasteReportStatus status = WasteReportStatus.submitted,
    List<ReportAttachmentModel> attachments = const [],
  }) {
    return WasteReportDetailModel(
      id: id,
      citizenId: 'cit-1',
      citizenName: 'Jane Citizen',
      description: description,
      wasteType: wasteType,
      latitude: latitude,
      longitude: longitude,
      addressText: addressText,
      status: status,
      attachments: attachments,
      createdAt: DateTime.utc(2026, 9, 16, 8, 30, 0),
    );
  }

  WasteReportDetailModel? lastReturnedResult;

  Widget createTestWidget({
    String reportId = 'rep-1',
    WasteReportDetailModel? initialReport,
  }) {
    return MaterialApp(
      home: Scaffold(
        body: Builder(
          builder: (context) => Center(
            child: ElevatedButton(
              key: const Key('open_edit_screen_btn'),
              onPressed: () async {
                lastReturnedResult = await Navigator.of(context).push<WasteReportDetailModel>(
                  MaterialPageRoute(
                    builder: (_) => EditReportScreen(
                      reportId: reportId,
                      initialReport: initialReport,
                      repository: mockRepo,
                      locationService: fakeLocation,
                      tileProvider: fakeTileProvider,
                    ),
                  ),
                );
              },
              child: const Text('Open Edit'),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> pumpEditScreen(
    WidgetTester tester, {
    String reportId = 'rep-1',
    WasteReportDetailModel? initialReport,
  }) async {
    lastReturnedResult = null;
    await tester.pumpWidget(createTestWidget(reportId: reportId, initialReport: initialReport));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('open_edit_screen_btn')));
    await tester.pumpAndSettle();
  }

  Future<void> tapSave(WidgetTester tester) async {
    final finder = find.byKey(const Key('save_changes_button'));
    await tester.ensureVisible(finder);
    await tester.pumpAndSettle();
    await tester.tap(finder);
    await tester.pumpAndSettle();
  }

  Future<void> tapCancel(WidgetTester tester) async {
    final finder = find.byKey(const Key('cancel_button'));
    await tester.ensureVisible(finder);
    await tester.pumpAndSettle();
    await tester.tap(finder);
    await tester.pumpAndSettle();
  }

  TextFormField getDescField(WidgetTester tester) {
    return tester.widget<TextFormField>(
      find.descendant(
        of: find.byKey(const Key('edit_description_field')),
        matching: find.byType(TextFormField),
      ),
    );
  }

  TextFormField getAddressField(WidgetTester tester) {
    return tester.widget<TextFormField>(
      find.descendant(
        of: find.byKey(const Key('edit_address_field')),
        matching: find.byType(TextFormField),
      ),
    );
  }

  group('EditReportScreen Initialization & Prefill Tests', () {
    testWidgets('prefills form immediately from initialReport', (tester) async {
      final sample = createSampleReport();

      await pumpEditScreen(tester, initialReport: sample);

      // Description
      expect(find.byKey(const Key('edit_description_field')), findsWidgets);
      final descField = getDescField(tester);
      expect(descField.controller?.text, sample.description);

      // Address
      expect(find.byKey(const Key('edit_address_field')), findsWidgets);
      final addressField = getAddressField(tester);
      expect(addressField.controller?.text, sample.addressText);

      // WasteType chip selected
      final generalChip = tester.widget<ChoiceChip>(
        find.byKey(const Key('waste_type_chip_general')),
      );
      expect(generalChip.selected, isTrue);

      // Location coordinates displayed
      expect(find.byKey(const Key('selected_coordinates_text')), findsOneWidget);
      expect(find.textContaining('6.92710, 79.86120'), findsOneWidget);

      // Verify no API get was called because initialReport was passed
      expect(mockRepo.getReportCallCount, 0);
    });

    testWidgets('fetches report dynamically when initialReport is null', (tester) async {
      final sample = createSampleReport(
        id: 'rep-fetch-1',
        description: 'Dynamically loaded description text',
        wasteType: WasteType.recyclable,
      );
      mockRepo.reportToReturn = sample;

      await pumpEditScreen(tester, reportId: 'rep-fetch-1', initialReport: null);

      expect(mockRepo.getReportCallCount, 1);
      expect(find.byKey(const Key('edit_report_loading')), findsNothing);

      final descField = getDescField(tester);
      expect(descField.controller?.text, 'Dynamically loaded description text');

      final recyclableChip = tester.widget<ChoiceChip>(
        find.byKey(const Key('waste_type_chip_recyclable')),
      );
      expect(recyclableChip.selected, isTrue);
    });

    testWidgets('displays error state when initial fetch fails and allows retry', (tester) async {
      mockRepo.shouldThrowGet = true;
      mockRepo.getErrorMessage = 'Failed to load report from server.';

      await pumpEditScreen(tester, reportId: 'rep-err-1', initialReport: null);

      expect(find.byKey(const Key('edit_report_error_text')), findsOneWidget);
      expect(find.text('Failed to load report from server.'), findsOneWidget);
      expect(find.byKey(const Key('edit_report_retry_button')), findsOneWidget);

      // Now fix mock and tap retry
      mockRepo.shouldThrowGet = false;
      mockRepo.reportToReturn = createSampleReport();

      await tester.tap(find.byKey(const Key('edit_report_retry_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('edit_report_error_text')), findsNothing);
      expect(find.byKey(const Key('edit_description_field')), findsWidgets);
    });

    testWidgets('blocks editing and shows error if fetched report is not in Submitted status', (tester) async {
      mockRepo.reportToReturn = createSampleReport(
        status: WasteReportStatus.underReview,
      );

      await pumpEditScreen(tester, reportId: 'rep-reviewed', initialReport: null);

      expect(find.byKey(const Key('edit_report_error_text')), findsOneWidget);
      expect(find.textContaining('Only submitted reports can be edited'), findsOneWidget);
    });
  });

  group('EditReportScreen Validation Tests', () {
    testWidgets('shows validation error when description has fewer than 10 characters', (tester) async {
      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(find.byKey(const Key('edit_description_field')), 'Too short');
      await tester.pump();

      await tapSave(tester);

      expect(find.text('Please provide at least 10 characters.'), findsOneWidget);
      expect(mockRepo.updateCallCount, 0);
    });

    testWidgets('shows validation error when description is completely cleared', (tester) async {
      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(find.byKey(const Key('edit_description_field')), '');
      await tester.pump();

      await tapSave(tester);

      expect(find.text('Please provide a description.'), findsOneWidget);
      expect(mockRepo.updateCallCount, 0);
    });

    testWidgets('shows validation error when address exceeds 500 characters', (tester) async {
      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      final overlyLongAddress = 'A' * 501;
      await tester.enterText(find.byKey(const Key('edit_address_field')), overlyLongAddress);
      await tester.pump();

      await tapSave(tester);

      expect(find.text('Address cannot exceed 500 characters.'), findsOneWidget);
      expect(mockRepo.updateCallCount, 0);
    });

    testWidgets('shows validation error when location is cleared and not re-selected', (tester) async {
      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      // Clear location
      await tester.tap(find.byKey(const Key('clear_location_button')));
      await tester.pumpAndSettle();

      expect(find.text('No location selected'), findsOneWidget);

      await tapSave(tester);

      expect(find.byKey(const Key('location_error_text')), findsOneWidget);
      expect(find.text('Please select a location for the report.'), findsOneWidget);
      expect(mockRepo.updateCallCount, 0);
    });
  });

  group('EditReportScreen Location Updates', () {
    testWidgets('updates coordinates when Use Current Location is tapped', (tester) async {
      final sample = createSampleReport(latitude: 6.9000, longitude: 79.8000);
      fakeLocation.positionToReturn = const SelectedLocation(latitude: 6.9500, longitude: 79.8900);

      await pumpEditScreen(tester, initialReport: sample);

      // Clear existing location
      await tester.ensureVisible(find.byKey(const Key('clear_location_button')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('clear_location_button')));
      await tester.pumpAndSettle();

      // Tap Use Current Location
      final currentLocBtn = find.byKey(const Key('use_current_location_button'));
      await tester.ensureVisible(currentLocBtn);
      await tester.pumpAndSettle();
      await tester.tap(currentLocBtn);
      await tester.pumpAndSettle();

      expect(fakeLocation.getCurrentLocationCallCount, 1);
      expect(find.textContaining('6.95000, 79.89000'), findsOneWidget);
    });
  });

  group('EditReportScreen Change Detection & Submission Tests', () {
    testWidgets('shows "No changes to save." when user taps Save without modifying anything', (tester) async {
      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      await tapSave(tester);

      expect(find.byKey(const Key('no_changes_snackbar')), findsOneWidget);
      expect(find.text('No changes to save.'), findsOneWidget);
      expect(mockRepo.updateCallCount, 0);
    });

    testWidgets('sends PATCH with only modified description', (tester) async {
      final sample = createSampleReport(description: 'Original description here');
      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(
        find.byKey(const Key('edit_description_field')),
        'Updated description text that is long enough',
      );
      await tester.pump();

      await tapSave(tester);

      expect(mockRepo.updateCallCount, 1);
      expect(mockRepo.lastUpdateRequest, isNotNull);
      expect(mockRepo.lastUpdateRequest!.description, 'Updated description text that is long enough');
      expect(mockRepo.lastUpdateRequest!.wasteType, isNull);
      expect(mockRepo.lastUpdateRequest!.latitude, isNull);
      expect(mockRepo.lastUpdateRequest!.longitude, isNull);
      expect(mockRepo.lastUpdateRequest!.addressText, isNull);
    });

    testWidgets('sends PATCH with only modified waste type', (tester) async {
      final sample = createSampleReport(wasteType: WasteType.general);
      await pumpEditScreen(tester, initialReport: sample);

      await tester.tap(find.byKey(const Key('waste_type_chip_organic')));
      await tester.pump();

      await tapSave(tester);

      expect(mockRepo.updateCallCount, 1);
      expect(mockRepo.lastUpdateRequest!.wasteType, WasteType.organic);
      expect(mockRepo.lastUpdateRequest!.description, isNull);
    });

    testWidgets('sends empty string addressText when address is cleared to nullify on backend', (tester) async {
      final sample = createSampleReport(addressText: '123 Old Address Road');
      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(find.byKey(const Key('edit_address_field')), '');
      await tester.pump();

      await tapSave(tester);

      expect(mockRepo.updateCallCount, 1);
      expect(mockRepo.lastUpdateRequest!.addressText, '');
    });

    testWidgets('sends updated address text when address is modified', (tester) async {
      final sample = createSampleReport(addressText: '123 Old Address Road');
      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(find.byKey(const Key('edit_address_field')), '456 New Street');
      await tester.pump();

      await tapSave(tester);

      expect(mockRepo.updateCallCount, 1);
      expect(mockRepo.lastUpdateRequest!.addressText, '456 New Street');
    });

    testWidgets('successful save displays success snackbar and pops with updated model', (tester) async {
      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(
        find.byKey(const Key('edit_description_field')),
        'Updated description after successful edit',
      );
      await tester.pump();

      await tapSave(tester);

      expect(lastReturnedResult, isNotNull);
      expect(lastReturnedResult!.description, 'Updated description after successful edit');
      expect(find.byType(EditReportScreen), findsNothing);
    });

    testWidgets('handles 409 Conflict when officer changed report status during edit', (tester) async {
      final sample = createSampleReport();
      mockRepo.shouldThrowUpdate = true;
      mockRepo.updateStatusCode = 409;
      mockRepo.updateErrorMessage = 'Status has changed.';

      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(
        find.byKey(const Key('edit_description_field')),
        'Attempting to edit report that just got reviewed',
      );
      await tester.pump();

      await tapSave(tester);

      expect(find.byKey(const Key('edit_report_conflict_snackbar')), findsOneWidget);
      expect(
        find.text('This report can no longer be edited because its status has changed.'),
        findsOneWidget,
      );
      expect(find.byType(EditReportScreen), findsNothing);
    });

    testWidgets('shows error snackbar on general failure and retains user inputs', (tester) async {
      final sample = createSampleReport();
      mockRepo.shouldThrowUpdate = true;
      mockRepo.updateStatusCode = 500;
      mockRepo.updateErrorMessage = 'Internal server error';

      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(
        find.byKey(const Key('edit_description_field')),
        'My detailed changes that should not be lost',
      );
      await tester.pump();

      await tapSave(tester);

      expect(find.byKey(const Key('edit_report_error_snackbar')), findsOneWidget);
      expect(find.text('Internal server error'), findsOneWidget);

      // Verify form text is preserved
      final descField = getDescField(tester);
      expect(descField.controller?.text, 'My detailed changes that should not be lost');
      expect(find.byType(EditReportScreen), findsOneWidget);
    });

    testWidgets('cancel button pops without making any network call', (tester) async {
      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      await tester.enterText(
        find.byKey(const Key('edit_description_field')),
        'Some changes that will be discarded',
      );
      await tester.pump();

      await tapCancel(tester);

      expect(mockRepo.updateCallCount, 0);
      expect(find.byType(EditReportScreen), findsNothing);
    });
  });

  group('EditReportScreen Responsiveness Tests', () {
    testWidgets('renders cleanly without overflow on narrow 320x640 viewport', (tester) async {
      tester.view.physicalSize = const Size(320, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      expect(tester.takeException(), isNull);
      final saveBtn = find.byKey(const Key('save_changes_button'));
      await tester.ensureVisible(saveBtn);
      expect(saveBtn, findsOneWidget);
    });

    testWidgets('renders cleanly without overflow on standard 390x844 viewport', (tester) async {
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final sample = createSampleReport();
      await pumpEditScreen(tester, initialReport: sample);

      expect(tester.takeException(), isNull);
      final saveBtn = find.byKey(const Key('save_changes_button'));
      await tester.ensureVisible(saveBtn);
      expect(saveBtn, findsOneWidget);
    });
  });
}
