import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../reporting/services/location_service.dart';
import '../data/complaints_repository.dart';
import '../models/complaint_model.dart';
import '../models/create_complaint_request.dart';

/// Screen allowing citizens to submit a new service complaint.
/// Features Category selection, Subject, multiline Description,
/// optional OpenStreetMap location picker with GPS / tap selection,
/// and optional Location Description.
class SubmitComplaintScreen extends StatefulWidget {
  final ComplaintsRepository? repository;
  final LocationService? locationService;
  final TileProvider? tileProvider;

  /// Default fallback camera center (Colombo, Sri Lanka) when no initial
  /// coordinates are selected.
  static const LatLng defaultFallbackCenter = LatLng(6.9271, 79.8612);

  const SubmitComplaintScreen({
    super.key,
    this.repository,
    this.locationService,
    this.tileProvider,
  });

  @override
  State<SubmitComplaintScreen> createState() => SubmitComplaintScreenState();
}

class SubmitComplaintScreenState extends State<SubmitComplaintScreen> {
  final _formKey = GlobalKey<FormState>();

  late final ComplaintsRepository _repository;
  late final LocationService _locationService;
  late final MapController _mapController;

  ComplaintCategory? _selectedCategory;
  late final TextEditingController _subjectController;
  late final TextEditingController _descriptionController;
  late final TextEditingController _locationDescriptionController;

  double? _selectedLatitude;
  double? _selectedLongitude;

  bool _isLocatingCurrent = false;
  bool _isSubmitting = false;

  String? _categoryError;
  String? _subjectError;
  String? _descriptionError;
  String? _locationDescriptionError;
  String? _submissionError;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? ComplaintsRepository();
    _locationService = widget.locationService ?? const GeolocatorLocationService();
    _mapController = MapController();

    _subjectController = TextEditingController();
    _descriptionController = TextEditingController();
    _locationDescriptionController = TextEditingController();

    _subjectController.addListener(_clearErrorsOnEdit);
    _descriptionController.addListener(_clearErrorsOnEdit);
    _locationDescriptionController.addListener(_clearErrorsOnEdit);
  }

  void _clearErrorsOnEdit() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _mapController.dispose();
    _subjectController.dispose();
    _descriptionController.dispose();
    _locationDescriptionController.dispose();
    super.dispose();
  }

  LatLng get _currentMapCenter {
    if (_selectedLatitude != null && _selectedLongitude != null) {
      return LatLng(_selectedLatitude!, _selectedLongitude!);
    }
    return SubmitComplaintScreen.defaultFallbackCenter;
  }

  bool _validateForm() {
    bool isValid = true;

    // 1. Category validation (Required)
    if (_selectedCategory == null) {
      _categoryError = 'Please select a complaint category.';
      isValid = false;
    } else {
      _categoryError = null;
    }

    // 2. Subject validation (Required, 5-200 chars)
    final subject = _subjectController.text.trim();
    if (subject.isEmpty) {
      _subjectError = 'Please enter a subject.';
      isValid = false;
    } else if (subject.length < 5) {
      _subjectError = 'Subject must be at least 5 characters.';
      isValid = false;
    } else if (subject.length > 200) {
      _subjectError = 'Subject cannot exceed 200 characters.';
      isValid = false;
    } else {
      _subjectError = null;
    }

    // 3. Description validation (Required, 10-2000 chars)
    final description = _descriptionController.text.trim();
    if (description.isEmpty) {
      _descriptionError = 'Please enter a description.';
      isValid = false;
    } else if (description.length < 10) {
      _descriptionError = 'Description must be at least 10 characters.';
      isValid = false;
    } else if (description.length > 2000) {
      _descriptionError = 'Description cannot exceed 2000 characters.';
      isValid = false;
    } else {
      _descriptionError = null;
    }

    // 4. Location Description validation (Optional, max 500 chars)
    final locDesc = _locationDescriptionController.text.trim();
    if (locDesc.length > 500) {
      _locationDescriptionError = 'Location description cannot exceed 500 characters.';
      isValid = false;
    } else {
      _locationDescriptionError = null;
    }

    setState(() {});
    return isValid;
  }

  Future<void> _onUseCurrentLocation() async {
    if (_isLocatingCurrent || _isSubmitting) return;

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
          });
          _mapController.move(
            LatLng(location.latitude, location.longitude),
            15.0,
          );
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Selected current device location.'),
              duration: Duration(seconds: 2),
            ),
          );
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

  void _onClearLocation() {
    setState(() {
      _selectedLatitude = null;
      _selectedLongitude = null;
    });
  }

  Future<void> _onSubmit() async {
    if (_isSubmitting) return;

    final isValid = _validateForm();
    if (!isValid) return;

    setState(() {
      _isSubmitting = true;
      _submissionError = null;
    });

    final request = CreateComplaintRequest(
      category: _selectedCategory!,
      subject: _subjectController.text.trim(),
      description: _descriptionController.text.trim(),
      latitude: _selectedLatitude,
      longitude: _selectedLongitude,
      locationDescription: _locationDescriptionController.text.trim().isEmpty
          ? null
          : _locationDescriptionController.text.trim(),
    );

    try {
      final created = await _repository.createComplaint(request);
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Complaint submitted successfully.'),
          backgroundColor: AppColors.primary,
          duration: Duration(seconds: 3),
        ),
      );

      // Navigate to detail screen of newly created complaint
      context.pushReplacement('/citizen/complaints/${created.id}');
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _submissionError = e.message;
        _isSubmitting = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.message),
          backgroundColor: AppColors.error,
          duration: const Duration(seconds: 4),
        ),
      );
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _submissionError = 'Unable to submit complaint. Please check your connection and try again.';
        _isSubmitting = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Unable to submit complaint. Please try again.'),
          backgroundColor: AppColors.error,
          duration: Duration(seconds: 4),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final hasLocation = _selectedLatitude != null && _selectedLongitude != null;
    final subjectLength = _subjectController.text.length;
    final descLength = _descriptionController.text.length;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        leading: IconButton(
          key: const Key('submit_complaint_back_button'),
          icon: const Icon(Icons.arrow_back),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
        title: const Text('Submit Complaint'),
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
                    // Heading / Helper Text
                    Text(
                      'Report a Service Concern',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: AppColors.textPrimary,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.xxs),
                    Text(
                      'Tell us about missed collections, delays, or service quality issues.',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: AppColors.textSecondary,
                            height: 1.4,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.lg),

                    if (_submissionError != null) ...[
                      Container(
                        key: const Key('submit_complaint_error_banner'),
                        padding: const EdgeInsets.all(AppSpacing.sm),
                        decoration: BoxDecoration(
                          color: AppColors.errorLight,
                          borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                          border: Border.all(color: AppColors.errorBorder),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.error_outline, color: AppColors.error, size: 20),
                            const SizedBox(width: AppSpacing.xs),
                            Expanded(
                              child: Text(
                                _submissionError!,
                                style: const TextStyle(fontSize: 13, color: AppColors.errorText),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                    ],

                    // 1. Category Field (Required)
                    Row(
                      children: [
                        Text(
                          'Category',
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
                    DropdownButtonFormField<ComplaintCategory>(
                      key: const Key('complaint_category_dropdown'),
                      initialValue: _selectedCategory,
                      decoration: InputDecoration(
                        hintText: 'Select category...',
                        hintStyle: const TextStyle(fontSize: 14, color: AppColors.textMuted),
                        filled: true,
                        fillColor: AppColors.surface,
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: AppSpacing.md,
                          vertical: AppSpacing.sm,
                        ),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                          borderSide: const BorderSide(color: AppColors.border),
                        ),
                        enabledBorder: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                          borderSide: const BorderSide(color: AppColors.border),
                        ),
                        focusedBorder: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                          borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
                        ),
                      ),
                      items: ComplaintCategory.values.map((cat) {
                        return DropdownMenuItem<ComplaintCategory>(
                          key: Key('category_option_${cat.value.toLowerCase()}'),
                          value: cat,
                          child: Text(cat.displayName),
                        );
                      }).toList(),
                      onChanged: _isSubmitting
                          ? null
                          : (value) {
                              setState(() {
                                _selectedCategory = value;
                                _categoryError = null;
                              });
                            },
                    ),
                    if (_categoryError != null) ...[
                      const SizedBox(height: AppSpacing.xxs),
                      Text(
                        _categoryError!,
                        key: const Key('category_error_text'),
                        style: const TextStyle(
                          color: AppColors.error,
                          fontSize: 12,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.lg),

                    // 2. Subject Field (Required, 5-200 chars)
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Row(
                          children: [
                            Text(
                              'Subject',
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
                          '$subjectLength / 200',
                          key: const Key('subject_char_count'),
                          style: TextStyle(
                            fontSize: 11,
                            color: subjectLength > 200 ? AppColors.error : AppColors.textMuted,
                            fontWeight: subjectLength > 200 ? FontWeight.bold : FontWeight.w500,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    AppTextField(
                      key: const Key('complaint_subject_field'),
                      controller: _subjectController,
                      enabled: !_isSubmitting,
                      hint: 'Brief summary of the issue...',
                      errorText: _subjectError,
                    ),
                    const SizedBox(height: AppSpacing.lg),

                    // 3. Description Field (Required, 10-2000 chars)
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
                          '$descLength / 2000',
                          key: const Key('description_char_count'),
                          style: TextStyle(
                            fontSize: 11,
                            color: descLength > 2000 ? AppColors.error : AppColors.textMuted,
                            fontWeight: descLength > 2000 ? FontWeight.bold : FontWeight.w500,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    AppTextField(
                      key: const Key('complaint_description_field'),
                      controller: _descriptionController,
                      maxLines: 5,
                      enabled: !_isSubmitting,
                      hint: 'Provide complete details regarding the issue, date/time noticed, and any relevant circumstances...',
                      errorText: _descriptionError,
                    ),
                    const SizedBox(height: AppSpacing.xl),

                    // 4. Location Section (Optional)
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'Location (Optional)',
                          style: Theme.of(context).textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                        ),
                        if (hasLocation && !_isSubmitting)
                          TextButton.icon(
                            key: const Key('clear_location_button'),
                            onPressed: _onClearLocation,
                            icon: const Icon(Icons.close, size: 16, color: AppColors.error),
                            label: const Text(
                              'Clear Location',
                              style: TextStyle(fontSize: 12, color: AppColors.error),
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xxs),
                    const Text(
                      'Add the location where the issue occurred to help staff understand the complaint.',
                      style: TextStyle(fontSize: 12, color: AppColors.textSecondary),
                    ),
                    const SizedBox(height: AppSpacing.xs),

                    AppCard(
                      padding: const EdgeInsets.all(AppSpacing.sm),
                      color: hasLocation
                          ? AppColors.primaryLight.withValues(alpha: 0.3)
                          : AppColors.surface,
                      borderSide: BorderSide(
                        color: hasLocation ? AppColors.primaryBorder : AppColors.border,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          // Status & Action Bar
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
                                  size: 16,
                                  color: hasLocation ? Colors.white : AppColors.textSecondary,
                                ),
                              ),
                              const SizedBox(width: AppSpacing.xs),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      hasLocation ? 'Location Selected ✓' : 'No location selected',
                                      key: const Key('complaint_location_status_text'),
                                      style: TextStyle(
                                        fontWeight: FontWeight.w600,
                                        fontSize: 13,
                                        color: hasLocation ? AppColors.primaryDark : AppColors.textPrimary,
                                      ),
                                    ),
                                    Text(
                                      hasLocation
                                          ? '${_selectedLatitude!.toStringAsFixed(5)}, ${_selectedLongitude!.toStringAsFixed(5)}'
                                          : 'Tap map to place marker or use current GPS location.',
                                      key: hasLocation
                                          ? const Key('complaint_selected_coordinates_text')
                                          : const Key('complaint_location_prompt_text'),
                                      style: TextStyle(
                                        fontSize: 11,
                                        color: hasLocation ? AppColors.textPrimary : AppColors.textSecondary,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              OutlinedButton.icon(
                                key: const Key('use_current_location_button'),
                                onPressed: _isSubmitting || _isLocatingCurrent
                                    ? null
                                    : _onUseCurrentLocation,
                                icon: _isLocatingCurrent
                                    ? const SizedBox(
                                        width: 14,
                                        height: 14,
                                        child: CircularProgressIndicator(strokeWidth: 2),
                                      )
                                    : const Icon(Icons.my_location, size: 14),
                                label: const Text('My GPS', style: TextStyle(fontSize: 12)),
                                style: OutlinedButton.styleFrom(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: AppSpacing.sm,
                                    vertical: AppSpacing.xs,
                                  ),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: AppSpacing.sm),

                          // Embedded Map Container
                          ClipRRect(
                            borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                            child: SizedBox(
                              height: 180,
                              width: double.infinity,
                              child: Stack(
                                children: [
                                  FlutterMap(
                                    mapController: _mapController,
                                    options: MapOptions(
                                      initialCenter: _currentMapCenter,
                                      initialZoom: hasLocation ? 15.0 : 13.0,
                                      minZoom: 3.0,
                                      maxZoom: 19.0,
                                      onTap: _isSubmitting
                                          ? null
                                          : (tapPosition, point) {
                                              setState(() {
                                                _selectedLatitude = point.latitude;
                                                _selectedLongitude = point.longitude;
                                              });
                                            },
                                    ),
                                    children: [
                                      TileLayer(
                                        urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                                        userAgentPackageName: 'com.smartwaste.mobile',
                                        tileProvider: widget.tileProvider,
                                      ),
                                      if (hasLocation)
                                        MarkerLayer(
                                          markers: [
                                            Marker(
                                              key: const Key('submit_complaint_marker'),
                                              point: LatLng(_selectedLatitude!, _selectedLongitude!),
                                              width: 40,
                                              height: 40,
                                              alignment: Alignment.topCenter,
                                              child: const Icon(
                                                Icons.location_on,
                                                color: AppColors.primaryDark,
                                                size: 38,
                                              ),
                                            ),
                                          ],
                                        ),
                                      Align(
                                        alignment: Alignment.topRight,
                                        child: Container(
                                          margin: const EdgeInsets.all(4),
                                          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
                                          decoration: BoxDecoration(
                                            color: AppColors.surface.withValues(alpha: 0.85),
                                            borderRadius: BorderRadius.circular(2),
                                          ),
                                          child: const Text(
                                            'OSM',
                                            style: TextStyle(fontSize: 9, color: AppColors.textSecondary),
                                          ),
                                        ),
                                      ),
                                    ],
                                  ),
                                  if (!hasLocation)
                                    Positioned(
                                      left: 8,
                                      bottom: 8,
                                      child: Container(
                                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                                        decoration: BoxDecoration(
                                          color: AppColors.surface.withValues(alpha: 0.9),
                                          borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                                          boxShadow: const [
                                            BoxShadow(
                                              color: Color(0x1A000000),
                                              blurRadius: 4,
                                              offset: Offset(0, 1),
                                            ),
                                          ],
                                        ),
                                        child: const Row(
                                          mainAxisSize: MainAxisSize.min,
                                          children: [
                                            Icon(Icons.touch_app, size: 13, color: AppColors.primary),
                                            SizedBox(width: 4),
                                            Text(
                                              'Tap map to select location',
                                              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w500),
                                            ),
                                          ],
                                        ),
                                      ),
                                    ),
                                ],
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),

                    // 5. Location Description Field (Optional, max 500 chars)
                    Text(
                      'Location Description (Optional)',
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.w600,
                            color: AppColors.textPrimary,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    AppTextField(
                      key: const Key('complaint_location_description_field'),
                      controller: _locationDescriptionController,
                      enabled: !_isSubmitting,
                      hint: 'e.g., Near the main gate, beside the public market...',
                      errorText: _locationDescriptionError,
                    ),
                    const SizedBox(height: AppSpacing.xxl),

                    // 6. Submit Button
                    AppButton.primary(
                      key: const Key('submit_complaint_button'),
                      label: 'Submit Complaint',
                      icon: Icons.send_rounded,
                      isLoading: _isSubmitting,
                      onPressed: _isSubmitting ? null : _onSubmit,
                    ),
                    const SizedBox(height: AppSpacing.lg),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  // Getters for testing
  ComplaintCategory? get selectedCategory => _selectedCategory;
  double? get selectedLatitude => _selectedLatitude;
  double? get selectedLongitude => _selectedLongitude;
  bool get isSubmitting => _isSubmitting;
  bool get isLocatingCurrent => _isLocatingCurrent;
}
