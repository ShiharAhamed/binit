import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../models/create_waste_report_request.dart';
import '../models/report_attachment_model.dart';
import '../models/selected_location.dart';
import '../models/selected_report_image.dart';
import '../models/waste_report_detail_model.dart';
import '../models/report_reference.dart';
import '../models/waste_type.dart';
import '../services/image_picker_service.dart';
import '../services/location_service.dart';
import '../services/report_submission_service.dart';
import 'map_location_picker_screen.dart';

/// Screen allowing citizens to create a waste report.
/// Features interactive waste category selection, description,
/// GPS current location or interactive map picker, photo evidence selection,
/// and authoritative ASP.NET Core submission with partial-failure recovery.
class ReportWasteScreen extends StatefulWidget {
  final WasteType? initialWasteType;
  final String? initialDescription;
  final double? initialLatitude;
  final double? initialLongitude;
  final String? initialAddressText;
  final List<SelectedReportImage>? initialImages;
  final LocationService? locationService;
  final ImagePickerService? imagePickerService;
  final ReportSubmissionService? submissionService;
  final TileProvider? tileProvider;
  final Widget Function(BuildContext context, SelectedReportImage image)? imagePreviewBuilder;

  const ReportWasteScreen({
    super.key,
    this.initialWasteType,
    this.initialDescription,
    this.initialLatitude,
    this.initialLongitude,
    this.initialAddressText,
    this.initialImages,
    this.locationService,
    this.imagePickerService,
    this.submissionService,
    this.tileProvider,
    this.imagePreviewBuilder,
  });

  @override
  State<ReportWasteScreen> createState() => ReportWasteScreenState();
}

class ReportWasteScreenState extends State<ReportWasteScreen> {
  final _formKey = GlobalKey<FormState>();

  late WasteType? _selectedWasteType;
  late final TextEditingController _descriptionController;
  late final TextEditingController _addressController;
  late double? _selectedLatitude;
  late double? _selectedLongitude;
  late final LocationService _locationService;
  late final ImagePickerService _imagePickerService;
  late final ReportSubmissionService _submissionService;
  late final List<SelectedReportImage> _selectedImages;

  bool _isLocatingCurrent = false;
  bool _isPickingImage = false;
  bool _isSubmitting = false;
  bool _isRetryingPhotos = false;
  String? _submittingStatusMessage;

  WasteReportDetailModel? _createdReport;
  List<FailedReportImage> _failedImages = [];
  List<ReportAttachmentModel> _uploadedAttachments = [];
  bool _isSuccess = false;

  String? _wasteTypeError;
  String? _descriptionError;
  String? _locationError;
  String? _addressError;

  @override
  void initState() {
    super.initState();
    _selectedWasteType = widget.initialWasteType;
    _descriptionController = TextEditingController(text: widget.initialDescription ?? '');
    _addressController = TextEditingController(text: widget.initialAddressText ?? '');
    _selectedLatitude = widget.initialLatitude;
    _selectedLongitude = widget.initialLongitude;
    _locationService = widget.locationService ?? const GeolocatorLocationService();
    _imagePickerService = widget.imagePickerService ?? DefaultImagePickerService();
    _submissionService = widget.submissionService ?? ReportSubmissionService();
    _selectedImages = List<SelectedReportImage>.from(widget.initialImages ?? []);

    _descriptionController.addListener(() {
      if (mounted) setState(() {});
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _recoverLostImages();
    });
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  /// Validates all form inputs locally against SmartWaste business rules.
  /// Returns true if all fields are valid; false otherwise.
  bool _validateForm() {
    bool isValid = true;

    // 1. Waste Type validation (Required)
    if (_selectedWasteType == null) {
      _wasteTypeError = 'Please select a waste type.';
      isValid = false;
    } else {
      _wasteTypeError = null;
    }

    // 2. Description validation (Required, 10 - 1000 characters)
    final desc = _descriptionController.text.trim();
    if (desc.isEmpty) {
      _descriptionError = 'Please provide a description.';
      isValid = false;
    } else if (desc.length < 10) {
      _descriptionError = 'Please provide at least 10 characters.';
      isValid = false;
    } else if (desc.length > 1000) {
      _descriptionError = 'Description cannot exceed 1000 characters.';
      isValid = false;
    } else {
      _descriptionError = null;
    }

    // 3. Location validation (Required coordinates, -90..90 lat, -180..180 lon)
    if (_selectedLatitude == null || _selectedLongitude == null) {
      _locationError = 'Please select a location for the report.';
      isValid = false;
    } else if (_selectedLatitude! < -90.0 || _selectedLatitude! > 90.0) {
      _locationError = 'Latitude must be between -90 and 90 degrees.';
      isValid = false;
    } else if (_selectedLongitude! < -180.0 || _selectedLongitude! > 180.0) {
      _locationError = 'Longitude must be between -180 and 180 degrees.';
      isValid = false;
    } else {
      _locationError = null;
    }

    // 4. Address validation (Optional, max 500 characters)
    final address = _addressController.text.trim();
    if (address.length > 500) {
      _addressError = 'Address cannot exceed 500 characters.';
      isValid = false;
    } else {
      _addressError = null;
    }

    setState(() {});
    return isValid;
  }

  /// Obtains current GPS coordinates using [LocationService].
  Future<void> _onUseCurrentLocation() async {
    if (_isLocatingCurrent) return;

    setState(() {
      _isLocatingCurrent = true;
    });

    try {
      final result = await _locationService.getCurrentLocation();
      if (!mounted) return;

      switch (result) {
        case LocationSuccess(:final location):
          setState(() {
            _selectedLatitude = location.latitude;
            _selectedLongitude = location.longitude;
            _locationError = null;
          });
        case LocationFailure(:final reason, :final message):
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(message),
              duration: const Duration(seconds: 4),
              action: reason == LocationFailureReason.serviceDisabled
                  ? SnackBarAction(
                      label: 'Settings',
                      textColor: AppColors.primaryLight,
                      onPressed: () => _locationService.openLocationSettings(),
                    )
                  : reason == LocationFailureReason.permissionDeniedForever
                      ? SnackBarAction(
                          label: 'Settings',
                          textColor: AppColors.primaryLight,
                          onPressed: () => _locationService.openAppSettings(),
                        )
                      : null,
            ),
          );
      }
    } finally {
      if (mounted) {
        setState(() {
          _isLocatingCurrent = false;
        });
      }
    }
  }

  /// Opens the interactive map picker screen to let citizen select a location.
  Future<void> _onChooseLocationOnMap() async {
    final result = await Navigator.of(context).push<SelectedLocation>(
      MaterialPageRoute(
        builder: (context) => MapLocationPickerScreen(
          initialLatitude: _selectedLatitude,
          initialLongitude: _selectedLongitude,
          locationService: _locationService,
          tileProvider: widget.tileProvider,
        ),
      ),
    );

    if (result != null && mounted) {
      setState(() {
        _selectedLatitude = result.latitude;
        _selectedLongitude = result.longitude;
        _locationError = null;
      });
    }
  }

  /// Recovers any lost images from Android Activity termination.
  Future<void> _recoverLostImages() async {
    try {
      final recovered = await _imagePickerService.retrieveLostImages();
      if (!mounted || recovered.isEmpty) return;

      setState(() {
        for (final img in recovered) {
          if (_selectedImages.length >= 3) break;
          if (!_selectedImages.any((existing) => existing.path == img.path)) {
            _selectedImages.add(img);
          }
        }
      });
    } catch (_) {
      // Graceful degradation on recovery failure; no crash
    }
  }

  /// Opens the Add Photo bottom sheet offering Camera or Gallery selection.
  void _onAddPhotoPressed() {
    if (_selectedImages.length >= 3) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('You can add up to 3 photos.'),
          duration: Duration(seconds: 2),
        ),
      );
      return;
    }

    showModalBottomSheet<void>(
      context: context,
      backgroundColor: AppColors.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (sheetContext) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.sm,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Center(
                  child: Container(
                    width: 36,
                    height: 4,
                    margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                    decoration: BoxDecoration(
                      color: AppColors.border,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs),
                  child: Text(
                    'Add Photo',
                    style: Theme.of(sheetContext).textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.bold,
                          color: AppColors.textPrimary,
                        ),
                  ),
                ),
                const SizedBox(height: AppSpacing.xs),
                ListTile(
                  key: const Key('take_photo_option'),
                  leading: const Icon(Icons.camera_alt_outlined, color: AppColors.primary),
                  title: const Text('Take Photo'),
                  subtitle: const Text('Use camera to capture evidence'),
                  onTap: () {
                    Navigator.of(sheetContext).pop();
                    _onTakePhoto();
                  },
                ),
                ListTile(
                  key: const Key('choose_gallery_option'),
                  leading: const Icon(Icons.photo_library_outlined, color: AppColors.primary),
                  title: const Text('Choose from Gallery'),
                  subtitle: Text('Select up to ${3 - _selectedImages.length} photos'),
                  onTap: () {
                    Navigator.of(sheetContext).pop();
                    _onPickFromGallery();
                  },
                ),
                const SizedBox(height: AppSpacing.xs),
                AppButton.text(
                  key: const Key('cancel_photo_picker_button'),
                  label: 'Cancel',
                  onPressed: () => Navigator.of(sheetContext).pop(),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  /// Handles camera photo capture.
  Future<void> _onTakePhoto() async {
    if (_isPickingImage) return;

    if (_selectedImages.length >= 3) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('You can add up to 3 photos.'),
          duration: Duration(seconds: 2),
        ),
      );
      return;
    }

    setState(() {
      _isPickingImage = true;
    });

    try {
      final image = await _imagePickerService.takePhoto();
      if (!mounted || image == null) return;

      if (_selectedImages.any((img) => img.path == image.path)) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('That photo has already been added.'),
            duration: Duration(seconds: 3),
          ),
        );
        return;
      }

      if (_selectedImages.length >= 3) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('You can add up to 3 photos.'),
            duration: Duration(seconds: 2),
          ),
        );
        return;
      }

      setState(() {
        _selectedImages.add(image);
      });
    } on ImagePickerException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.message),
          duration: const Duration(seconds: 3),
        ),
      );
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Failed to capture photo. Please try again.'),
          duration: Duration(seconds: 3),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _isPickingImage = false;
        });
      }
    }
  }

  /// Handles gallery photo selection.
  Future<void> _onPickFromGallery() async {
    if (_isPickingImage) return;

    final remaining = 3 - _selectedImages.length;
    if (remaining <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('You can add up to 3 photos.'),
          duration: Duration(seconds: 2),
        ),
      );
      return;
    }

    setState(() {
      _isPickingImage = true;
    });

    try {
      final images = await _imagePickerService.pickFromGallery(maxImages: remaining);
      if (!mounted || images.isEmpty) return;

      bool hasDuplicate = false;
      bool reachedLimit = false;

      setState(() {
        for (final img in images) {
          if (_selectedImages.length >= 3) {
            reachedLimit = true;
            break;
          }
          if (_selectedImages.any((existing) => existing.path == img.path)) {
            hasDuplicate = true;
            continue;
          }
          _selectedImages.add(img);
        }
      });

      if (hasDuplicate) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('That photo has already been added.'),
            duration: Duration(seconds: 3),
          ),
        );
      } else if (reachedLimit) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('You can add up to 3 photos.'),
            duration: Duration(seconds: 2),
          ),
        );
      }
    } on ImagePickerException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.message),
          duration: const Duration(seconds: 3),
        ),
      );
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Failed to select photos. Please try again.'),
          duration: Duration(seconds: 3),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _isPickingImage = false;
        });
      }
    }
  }

  /// Removes a selected photo at [index] from local form state.
  void _removePhoto(int index) {
    if (index >= 0 && index < _selectedImages.length) {
      setState(() {
        _selectedImages.removeAt(index);
      });
    }
  }

  Widget _buildPhotoPreviewTile(BuildContext context, SelectedReportImage image, int index) {
    final isReportCreated = _createdReport != null;
    final isLocked = isReportCreated || _isSubmitting;
    final isFailed = _failedImages.any((f) => f.image.path == image.path);
    final isUploaded = isReportCreated && !isFailed;

    return Container(
      key: Key('photo_preview_tile_$index'),
      width: 88,
      height: 88,
      decoration: BoxDecoration(
        color: AppColors.surfaceSubtle,
        borderRadius: AppSpacing.roundedSm,
        border: Border.all(
          color: isFailed
              ? AppColors.error
              : isUploaded
                  ? AppColors.primary
                  : AppColors.border,
          width: (isFailed || isUploaded) ? 1.5 : 1.0,
        ),
      ),
      child: Stack(
        fit: StackFit.expand,
        children: [
          ClipRRect(
            borderRadius: AppSpacing.roundedSm,
            child: widget.imagePreviewBuilder != null
                ? widget.imagePreviewBuilder!(context, image)
                : Image.file(
                    File(image.path),
                    fit: BoxFit.cover,
                    errorBuilder: (context, error, stackTrace) {
                      return const Center(
                        child: Icon(
                          Icons.broken_image_outlined,
                          color: AppColors.textMuted,
                          size: 28,
                        ),
                      );
                    },
                  ),
          ),
          if (!isLocked)
            Positioned(
              top: 2,
              right: 2,
              child: Material(
                color: Colors.black54,
                shape: const CircleBorder(),
                child: InkWell(
                  key: Key('remove_photo_button_$index'),
                  customBorder: const CircleBorder(),
                  onTap: () => _removePhoto(index),
                  child: const Padding(
                    padding: EdgeInsets.all(4),
                    child: Icon(
                      Icons.close,
                      size: 16,
                      color: Colors.white,
                    ),
                  ),
                ),
              ),
            ),
          if (isReportCreated)
            Positioned(
              bottom: 2,
              right: 2,
              child: Container(
                decoration: const BoxDecoration(
                  color: Colors.white,
                  shape: BoxShape.circle,
                ),
                padding: const EdgeInsets.all(2),
                child: Icon(
                  isFailed
                      ? Icons.error
                      : isUploaded
                          ? Icons.check_circle
                          : Icons.hourglass_empty,
                  size: 16,
                  color: isFailed ? AppColors.error : AppColors.primary,
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildAddPhotoTile(BuildContext context) {
    return InkWell(
      key: const Key('add_photo_button'),
      onTap: _isPickingImage ? null : _onAddPhotoPressed,
      borderRadius: AppSpacing.roundedSm,
      child: Container(
        width: 88,
        height: 88,
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: AppSpacing.roundedSm,
          border: Border.all(
            color: AppColors.border,
            style: BorderStyle.solid,
          ),
        ),
        child: _isPickingImage
            ? const Center(
                child: SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              )
            : const Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    Icons.add_photo_alternate_outlined,
                    color: AppColors.primary,
                    size: 24,
                  ),
                  SizedBox(height: 4),
                  Text(
                    'Add Photo',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: AppColors.primary,
                    ),
                  ),
                ],
              ),
      ),
    );
  }

  /// Exposes selected images list for testing and Step 9A.8.5 submission.
  List<SelectedReportImage> get selectedImages => List.unmodifiable(_selectedImages);

  /// Exposes created report details for testing and state verification.
  WasteReportDetailModel? get createdReport => _createdReport;

  /// Exposes failed photo uploads for testing and retry flows.
  List<FailedReportImage> get failedImages => List.unmodifiable(_failedImages);

  /// Exposes successfully uploaded attachments for testing.
  List<ReportAttachmentModel> get uploadedAttachments => List.unmodifiable(_uploadedAttachments);

  /// Exposes submission status for testing.
  bool get isSubmitting => _isSubmitting;

  /// Exposes retry status for testing.
  bool get isRetryingPhotos => _isRetryingPhotos;

  /// Exposes success completion status for testing.
  bool get isSuccess => _isSuccess;

  /// Submits the waste report to the authoritative ASP.NET Core backend.
  /// If photos are attached, uploads them sequentially.
  /// Handles creation failure, full success, and partial failure.
  Future<void> _onSubmitReport() async {
    if (_isSubmitting || _isRetryingPhotos || _createdReport != null) return;

    final valid = _validateForm();
    if (!valid) return;

    setState(() {
      _isSubmitting = true;
      _submittingStatusMessage = 'Submitting report...';
    });

    final request = CreateWasteReportRequest(
      wasteType: _selectedWasteType!,
      description: _descriptionController.text.trim(),
      latitude: _selectedLatitude!,
      longitude: _selectedLongitude!,
      addressText: _addressController.text.trim().isEmpty ? null : _addressController.text.trim(),
    );

    final result = await _submissionService.submitReport(
      request: request,
      images: _selectedImages,
    );

    if (!mounted) return;

    setState(() {
      _isSubmitting = false;
      _submittingStatusMessage = null;
    });

    switch (result) {
      case ReportCreationFailure(:final userFacingMessage):
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            key: const Key('submission_error_snackbar'),
            content: Text(userFacingMessage),
            backgroundColor: AppColors.error,
            duration: const Duration(seconds: 4),
          ),
        );
      case ReportSubmissionFullSuccess(:final report, :final uploadedAttachments):
        setState(() {
          _createdReport = report;
          _uploadedAttachments = uploadedAttachments;
          _failedImages = [];
          _isSuccess = true;
        });
        _showSuccessDialog();
      case ReportSubmissionPartialSuccess(
          :final report,
          :final uploadedAttachments,
          :final failedImages,
        ):
        setState(() {
          _createdReport = report;
          _uploadedAttachments = uploadedAttachments;
          _failedImages = failedImages;
          _isSuccess = false;
        });
    }
  }

  /// Retries uploading failed photos against the EXISTING reportId.
  /// Never re-calls createWasteReport.
  Future<void> _onRetryFailedPhotos() async {
    if (_isRetryingPhotos || _isSubmitting || _createdReport == null) return;
    if (_failedImages.isEmpty) return;

    setState(() {
      _isRetryingPhotos = true;
    });

    final failedList = _failedImages.map((f) => f.image).toList();
    final result = await _submissionService.retryFailedUploads(
      existingReport: _createdReport!,
      failedImages: failedList,
      previouslyUploaded: _uploadedAttachments,
    );

    if (!mounted) return;

    setState(() {
      _isRetryingPhotos = false;
    });

    switch (result) {
      case ReportCreationFailure():
        break;
      case ReportSubmissionFullSuccess(:final report, :final uploadedAttachments):
        setState(() {
          _createdReport = report;
          _uploadedAttachments = uploadedAttachments;
          _failedImages = [];
          _isSuccess = true;
        });
        _showSuccessDialog();
      case ReportSubmissionPartialSuccess(
          :final report,
          :final uploadedAttachments,
          :final failedImages,
        ):
        setState(() {
          _createdReport = report;
          _uploadedAttachments = uploadedAttachments;
          _failedImages = failedImages;
          _isSuccess = false;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            key: const Key('partial_retry_failed_snackbar'),
            content: Text(
              '${failedImages.length} photo(s) still could not be uploaded.',
            ),
            backgroundColor: AppColors.warning,
            duration: const Duration(seconds: 4),
          ),
        );
    }
  }

  /// Displays the full success dialog when report and all photos are committed.
  void _showSuccessDialog() {
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) {
        final report = _createdReport;
        final reference = report == null ? null : reportLabel(report.id, authoritative: report.reportReference);
        final photoCount = _uploadedAttachments.length;
        final photoText = photoCount == 0
            ? 'No photos attached.'
            : photoCount == 1
                ? '1 photo attached.'
                : '$photoCount photos attached.';

        return AlertDialog(
          key: const Key('submission_success_dialog'),
          shape: const RoundedRectangleBorder(borderRadius: AppSpacing.roundedMd),
          title: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(AppSpacing.xs),
                decoration: const BoxDecoration(
                  color: AppColors.primaryLight,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.check_circle,
                  color: AppColors.primary,
                  size: 24,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              const Expanded(
                child: Text(
                  'Report Submitted',
                  style: TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 18,
                    color: AppColors.textPrimary,
                  ),
                ),
              ),
            ],
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Your waste report has been submitted to the municipal team for review.',
                style: TextStyle(
                  fontSize: 14,
                  color: AppColors.textSecondary,
                  height: 1.4,
                ),
              ),
              if (reference != null) ...[
                const SizedBox(height: AppSpacing.sm),
                Text(
                  reference,
                  key: const Key('submission_success_reference_text'),
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
              ],
              const SizedBox(height: AppSpacing.xs),
              Text(
                photoText,
                key: const Key('submission_success_photos_text'),
                style: const TextStyle(
                  fontSize: 12,
                  color: AppColors.textMuted,
                ),
              ),
            ],
          ),
          actions: [
            AppButton.primary(
              key: const Key('submission_success_done_button'),
              label: 'Back to Home',
              onPressed: () {
                Navigator.of(dialogContext).pop();
                Navigator.of(context).maybePop();
              },
            ),
          ],
        );
      },
    );
  }

  Widget _buildPartialFailureCard(BuildContext context) {
    final totalPhotos = _uploadedAttachments.length + _failedImages.length;
    final uploadedCount = _uploadedAttachments.length;
    final failedCount = _failedImages.length;

    return AppCard(
      key: const Key('partial_success_card'),
      color: AppColors.warningLight.withValues(alpha: 0.3),
      borderSide: const BorderSide(color: AppColors.warningBorder),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(AppSpacing.xs),
                decoration: const BoxDecoration(
                  color: AppColors.warningLight,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.warning_amber_rounded,
                  color: AppColors.warning,
                  size: 20,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Report Submitted',
                      key: const Key('partial_success_header'),
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: AppColors.warningText,
                          ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      'Your report was saved, but $failedCount of $totalPhotos photo(s) failed to upload.',
                      key: const Key('partial_success_summary_text'),
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            '$uploadedCount of $totalPhotos photos uploaded successfully.',
            key: const Key('partial_success_progress_text'),
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w500,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          for (final failed in _failedImages)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Row(
                children: [
                  const Icon(Icons.error_outline, size: 14, color: AppColors.error),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      '${failed.image.fileName}: ${failed.userFacingMessage}',
                      style: const TextStyle(fontSize: 11, color: AppColors.error),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: AppSpacing.md),
          Row(
            children: [
              Expanded(
                child: AppButton.primary(
                  key: const Key('retry_failed_photos_button'),
                  label: 'Retry Failed Photos',
                  icon: Icons.refresh,
                  isLoading: _isRetryingPhotos,
                  onPressed: _isRetryingPhotos ? null : _onRetryFailedPhotos,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: AppButton.outlined(
                  key: const Key('finish_partial_submission_button'),
                  label: 'Finish',
                  onPressed: _isRetryingPhotos ? null : () => Navigator.of(context).maybePop(),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildFullSuccessCard(BuildContext context) {
    return AppCard(
      key: const Key('full_success_card'),
      color: AppColors.primaryLight.withValues(alpha: 0.3),
      borderSide: const BorderSide(color: AppColors.primaryBorder),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.check_circle, color: AppColors.primary, size: 24),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  'Report Submitted Successfully',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.primaryDark,
                      ),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          const Text(
            'Your report has been submitted to the municipal team for review.',
            style: TextStyle(fontSize: 13, color: AppColors.textSecondary),
          ),
          const SizedBox(height: AppSpacing.md),
          AppButton.primary(
            key: const Key('full_success_done_button'),
            label: 'Back to Home',
            icon: Icons.arrow_back,
            onPressed: () => Navigator.of(context).maybePop(),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final hasLocation = _selectedLatitude != null && _selectedLongitude != null;
    final charCount = _descriptionController.text.length;
    final isReportCreated = _createdReport != null;
    final isLocked = isReportCreated || _isSubmitting;

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          key: const Key('report_waste_back_button'),
          icon: const Icon(Icons.arrow_back),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
        title: const Text('Report Waste'),
        centerTitle: true,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 540),
            child: Form(
              key: _formKey,
              child: SingleChildScrollView(
                keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.lg,
                  vertical: AppSpacing.md,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // 1. Intro / Context
                    Text(
                      'Report a Waste Issue',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: AppColors.textPrimary,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.xxs),
                    Text(
                      'Provide details about illegal dumping, overflowing bins, or public waste issues.',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: AppColors.textSecondary,
                            height: 1.4,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.lg),

                    // 2. Waste Type Section (Required)
                    Row(
                      children: [
                        Text(
                          'Waste Type',
                          style: Theme.of(context).textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                        ),
                        const SizedBox(width: AppSpacing.xxs),
                        const Text(
                          '*',
                          style: TextStyle(
                            color: AppColors.error,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Wrap(
                      spacing: AppSpacing.xs,
                      runSpacing: AppSpacing.xs,
                      children: WasteType.values.map((type) {
                        final isSelected = _selectedWasteType == type;
                        return ChoiceChip(
                          key: Key('waste_type_chip_${type.value.toLowerCase()}'),
                          label: Text(type.displayName),
                          selected: isSelected,
                          onSelected: isLocked
                              ? null
                              : (selected) {
                                  setState(() {
                                    _selectedWasteType = selected ? type : null;
                                    if (_wasteTypeError != null) {
                                      _wasteTypeError = null;
                                    }
                                  });
                                },
                          selectedColor: AppColors.primaryLight,
                          backgroundColor: AppColors.surface,
                          side: BorderSide(
                            color: isSelected ? AppColors.primary : AppColors.border,
                            width: isSelected ? 1.5 : 1.0,
                          ),
                          labelStyle: TextStyle(
                            color: isSelected ? AppColors.primaryDark : AppColors.textPrimary,
                            fontWeight: isSelected ? FontWeight.w600 : FontWeight.w400,
                            fontSize: 13,
                          ),
                          shape: const RoundedRectangleBorder(
                            borderRadius: AppSpacing.roundedSm,
                          ),
                        );
                      }).toList(),
                    ),
                    if (_wasteTypeError != null) ...[
                      const SizedBox(height: AppSpacing.xxs),
                      Text(
                        _wasteTypeError!,
                        key: const Key('waste_type_error_text'),
                        style: const TextStyle(
                          color: AppColors.error,
                          fontSize: 12,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.lg),

                    // 3. Description Section (Required, 10-1000 chars)
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Row(
                          children: [
                            Text(
                              'Description',
                              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                                    fontWeight: FontWeight.w600,
                                    color: AppColors.textPrimary,
                                  ),
                            ),
                            const SizedBox(width: AppSpacing.xxs),
                            const Text(
                              '*',
                              style: TextStyle(
                                color: AppColors.error,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ],
                        ),
                        Text(
                          '$charCount / 1000',
                          key: const Key('description_character_count'),
                          style: TextStyle(
                            fontSize: 11,
                            color: charCount > 1000 ? AppColors.error : AppColors.textMuted,
                            fontWeight: charCount > 1000 ? FontWeight.bold : FontWeight.w500,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    AppTextField(
                      key: const Key('report_waste_description_field'),
                      controller: _descriptionController,
                      maxLines: 4,
                      enabled: !isLocked,
                      hint: 'Describe the waste issue, estimated amount, and hazards...',
                      errorText: _descriptionError,
                    ),
                    const SizedBox(height: AppSpacing.lg),

                    // 4. Location Section (Required, UI Foundation)
                    Row(
                      children: [
                        Text(
                          'Location',
                          style: Theme.of(context).textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                        ),
                        const SizedBox(width: AppSpacing.xxs),
                        const Text(
                          '*',
                          style: TextStyle(
                            color: AppColors.error,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    AppCard(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      color: hasLocation ? AppColors.primaryLight.withValues(alpha: 0.5) : AppColors.surface,
                      borderSide: BorderSide(
                        color: hasLocation ? AppColors.primaryBorder : AppColors.border,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.all(AppSpacing.xs),
                                decoration: BoxDecoration(
                                  color: hasLocation ? AppColors.primary : AppColors.surfaceSubtle,
                                  shape: BoxShape.circle,
                                ),
                                child: Icon(
                                  hasLocation ? Icons.check : Icons.location_on_outlined,
                                  size: 18,
                                  color: hasLocation ? Colors.white : AppColors.textSecondary,
                                ),
                              ),
                              const SizedBox(width: AppSpacing.sm),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      hasLocation ? 'Location Selected ✓' : 'No location selected',
                                      key: const Key('location_status_text'),
                                      style: TextStyle(
                                        fontWeight: FontWeight.w600,
                                        fontSize: 14,
                                        color: hasLocation ? AppColors.primaryDark : AppColors.textPrimary,
                                      ),
                                    ),
                                    const SizedBox(height: 2),
                                    Text(
                                      hasLocation
                                          ? '${_selectedLatitude!.toStringAsFixed(5)}, ${_selectedLongitude!.toStringAsFixed(5)}'
                                          : 'Select where the waste is located.',
                                      key: hasLocation
                                          ? const Key('selected_coordinates_text')
                                          : const Key('location_prompt_text'),
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: hasLocation ? AppColors.textPrimary : AppColors.textSecondary,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              if (hasLocation && !isLocked)
                                TextButton(
                                  key: const Key('clear_location_button'),
                                  onPressed: () {
                                    setState(() {
                                      _selectedLatitude = null;
                                      _selectedLongitude = null;
                                    });
                                  },
                                  child: const Text(
                                    'Change',
                                    style: TextStyle(fontSize: 12, color: AppColors.primary),
                                  ),
                                ),
                            ],
                          ),
                          if (!hasLocation) ...[
                            const SizedBox(height: AppSpacing.md),
                            OutlinedButtonTheme(
                              data: OutlinedButtonThemeData(
                                style: OutlinedButton.styleFrom(
                                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
                                ),
                              ),
                              child: LayoutBuilder(
                                builder: (context, constraints) {
                                  if (constraints.maxWidth < 420) {
                                    return Column(
                                      crossAxisAlignment: CrossAxisAlignment.stretch,
                                      children: [
                                        AppButton.outlined(
                                          key: const Key('use_current_location_button'),
                                          label: 'Use Current Location',
                                          icon: Icons.my_location,
                                          isLoading: _isLocatingCurrent,
                                          height: 40,
                                          onPressed: (_isLocatingCurrent || isLocked) ? null : _onUseCurrentLocation,
                                        ),
                                        const SizedBox(height: AppSpacing.xs),
                                        AppButton.outlined(
                                          key: const Key('choose_on_map_button'),
                                          label: 'Choose on Map',
                                          icon: Icons.map_outlined,
                                          height: 40,
                                          onPressed: (_isLocatingCurrent || isLocked) ? null : _onChooseLocationOnMap,
                                        ),
                                      ],
                                    );
                                  }
                                  return Row(
                                    children: [
                                      Expanded(
                                        child: AppButton.outlined(
                                          key: const Key('use_current_location_button'),
                                          label: 'Use Current Location',
                                          icon: Icons.my_location,
                                          isLoading: _isLocatingCurrent,
                                          height: 40,
                                          onPressed: (_isLocatingCurrent || isLocked) ? null : _onUseCurrentLocation,
                                        ),
                                      ),
                                      const SizedBox(width: AppSpacing.sm),
                                      Expanded(
                                        child: AppButton.outlined(
                                          key: const Key('choose_on_map_button'),
                                          label: 'Choose on Map',
                                          icon: Icons.map_outlined,
                                          height: 40,
                                          onPressed: (_isLocatingCurrent || isLocked) ? null : _onChooseLocationOnMap,
                                        ),
                                      ),
                                    ],
                                  );
                                },
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                    if (_locationError != null) ...[
                      const SizedBox(height: AppSpacing.xxs),
                      Text(
                        _locationError!,
                        key: const Key('location_error_text'),
                        style: const TextStyle(
                          color: AppColors.error,
                          fontSize: 12,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.lg),

                    // 5. Optional Address / Landmark Field
                    Text(
                      'Address / Landmark (optional)',
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.w600,
                            color: AppColors.textPrimary,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    AppTextField(
                      key: const Key('report_waste_address_field'),
                      controller: _addressController,
                      enabled: !isLocked,
                      hint: 'Near the market entrance, Main Street',
                      errorText: _addressError,
                    ),
                    const SizedBox(height: AppSpacing.lg),

                    // 6. Photo Evidence Section
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'Photo Evidence',
                          style: Theme.of(context).textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                        ),
                        Text(
                          '${_selectedImages.length} / 3',
                          key: const Key('photo_evidence_counter'),
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 2),
                    Text(
                      'Add up to 3 photos to help officers identify the issue.',
                      key: const Key('photo_evidence_subtitle'),
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: AppColors.textSecondary,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    AppCard(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (_selectedImages.isEmpty) ...[
                            Row(
                              children: [
                                Container(
                                  width: 44,
                                  height: 44,
                                  decoration: BoxDecoration(
                                    color: AppColors.surfaceSubtle,
                                    borderRadius: AppSpacing.roundedSm,
                                    border: Border.all(color: AppColors.border),
                                  ),
                                  child: const Icon(
                                    Icons.camera_alt_outlined,
                                    color: AppColors.textMuted,
                                    size: 22,
                                  ),
                                ),
                                const SizedBox(width: AppSpacing.sm),
                                const Expanded(
                                  child: Text(
                                    'Attach photos to help officers locate and assess the waste.',
                                    style: TextStyle(
                                      fontSize: 12,
                                      color: AppColors.textMuted,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                            if (!isLocked) ...[
                              const SizedBox(height: AppSpacing.md),
                              Align(
                                alignment: Alignment.centerLeft,
                                child: AppButton.outlined(
                                  key: const Key('add_photo_button'),
                                  label: 'Add Photo',
                                  icon: Icons.add_photo_alternate_outlined,
                                  height: 38,
                                  isLoading: _isPickingImage,
                                  onPressed: _isPickingImage ? null : _onAddPhotoPressed,
                                ),
                              ),
                            ],
                          ] else ...[
                            Wrap(
                              spacing: AppSpacing.sm,
                              runSpacing: AppSpacing.sm,
                              children: [
                                for (int i = 0; i < _selectedImages.length; i++)
                                  _buildPhotoPreviewTile(context, _selectedImages[i], i),
                                if (_selectedImages.length < 3 && !isLocked)
                                  _buildAddPhotoTile(context),
                              ],
                            ),
                          ],
                        ],
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xl),

                    // 7. Submission / Recovery Action Area
                    if (isReportCreated && _failedImages.isNotEmpty) ...[
                      _buildPartialFailureCard(context),
                    ] else if (isReportCreated && _failedImages.isEmpty) ...[
                      _buildFullSuccessCard(context),
                    ] else ...[
                      AppButton.primary(
                        key: const Key('submit_report_button'),
                        label: _isSubmitting
                            ? (_submittingStatusMessage ?? 'Submitting...')
                            : 'Submit Report',
                        icon: Icons.send_rounded,
                        isLoading: _isSubmitting,
                        onPressed: _isSubmitting ? null : _onSubmitReport,
                      ),
                    ],
                    const SizedBox(height: AppSpacing.xl),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
