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
import '../data/operations_repository.dart';
import '../models/create_operational_issue_request.dart';
import '../models/operational_issue_model.dart';

/// Screen allowing drivers to report a new operational issue from the field.
/// Features Issue Type selection, Title, multiline Description,
/// optional OpenStreetMap location picker with GPS / tap selection,
/// and optional Location Description.
class ReportOperationalIssueScreen extends StatefulWidget {
  final OperationsRepository? repository;
  final LocationService? locationService;
  final TileProvider? tileProvider;

  /// Default fallback camera center (Colombo, Sri Lanka) when no initial
  /// coordinates are selected.
  static const LatLng defaultFallbackCenter = LatLng(6.9271, 79.8612);

  const ReportOperationalIssueScreen({
    super.key,
    this.repository,
    this.locationService,
    this.tileProvider,
  });

  @override
  State<ReportOperationalIssueScreen> createState() =>
      ReportOperationalIssueScreenState();
}

class ReportOperationalIssueScreenState
    extends State<ReportOperationalIssueScreen> {
  final _formKey = GlobalKey<FormState>();

  late final OperationsRepository _repository;
  late final LocationService _locationService;
  late final MapController _mapController;

  OperationalIssueType? _selectedIssueType;
  late final TextEditingController _titleController;
  late final TextEditingController _descriptionController;
  late final TextEditingController _locationDescriptionController;

  double? _selectedLatitude;
  double? _selectedLongitude;

  bool _isLocatingCurrent = false;
  bool _isSubmitting = false;

  String? _issueTypeError;
  String? _titleError;
  String? _descriptionError;
  String? _locationDescriptionError;
  String? _submissionError;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? OperationsRepository();
    _locationService =
        widget.locationService ?? const GeolocatorLocationService();
    _mapController = MapController();

    _titleController = TextEditingController();
    _descriptionController = TextEditingController();
    _locationDescriptionController = TextEditingController();

    _titleController.addListener(_clearErrorsOnEdit);
    _descriptionController.addListener(_clearErrorsOnEdit);
    _locationDescriptionController.addListener(_clearErrorsOnEdit);
  }

  void _clearErrorsOnEdit() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _mapController.dispose();
    _titleController.dispose();
    _descriptionController.dispose();
    _locationDescriptionController.dispose();
    super.dispose();
  }

  LatLng get _currentMapCenter {
    if (_selectedLatitude != null && _selectedLongitude != null) {
      return LatLng(_selectedLatitude!, _selectedLongitude!);
    }
    return ReportOperationalIssueScreen.defaultFallbackCenter;
  }

  bool _validateForm() {
    bool isValid = true;

    // 1. Issue Type validation (Required)
    if (_selectedIssueType == null) {
      _issueTypeError = 'Please select an issue type.';
      isValid = false;
    } else {
      _issueTypeError = null;
    }

    // 2. Title validation (Required, 5-200 chars)
    final title = _titleController.text.trim();
    if (title.isEmpty) {
      _titleError = 'Please enter a title.';
      isValid = false;
    } else if (title.length < 5) {
      _titleError = 'Title must be at least 5 characters.';
      isValid = false;
    } else if (title.length > 200) {
      _titleError = 'Title cannot exceed 200 characters.';
      isValid = false;
    } else {
      _titleError = null;
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
      _locationDescriptionError =
          'Location description cannot exceed 500 characters.';
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
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Location cleared.'),
        duration: Duration(seconds: 2),
      ),
    );
  }

  void _onMapTap(TapPosition tapPosition, LatLng latLng) {
    setState(() {
      _selectedLatitude = latLng.latitude;
      _selectedLongitude = latLng.longitude;
    });
  }

  Future<void> _onSubmit() async {
    if (_isSubmitting) return;

    if (!_validateForm()) return;

    setState(() {
      _isSubmitting = true;
      _submissionError = null;
    });

    final request = CreateOperationalIssueRequest(
      issueType: _selectedIssueType!,
      title: _titleController.text.trim(),
      description: _descriptionController.text.trim(),
      latitude: _selectedLatitude,
      longitude: _selectedLongitude,
      locationDescription: _locationDescriptionController.text.trim().isEmpty
          ? null
          : _locationDescriptionController.text.trim(),
    );

    try {
      final createdIssue = await _repository.createOperationalIssue(request);

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Operational issue reported successfully.'),
          backgroundColor: AppColors.primary,
          duration: Duration(seconds: 2),
        ),
      );

      // Navigate to the newly created issue detail screen
      context.pushReplacement('/driver/incidents/${createdIssue.id}');
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _submissionError = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _submissionError =
            'An unexpected error occurred. Please check your connection and try again.';
      });
    } finally {
      if (mounted) {
        setState(() {
          _isSubmitting = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Report Operational Issue'),
        centerTitle: true,
        backgroundColor: AppColors.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        leading: IconButton(
          key: const Key('report_issue_back_button'),
          icon: const Icon(Icons.arrow_back, color: AppColors.textPrimary),
          tooltip: 'Back',
          onPressed: () => context.pop(),
        ),
        bottom: const PreferredSize(
          preferredSize: Size.fromHeight(1),
          child: Divider(height: 1, color: AppColors.border),
        ),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Submission Error Alert (if present)
                if (_submissionError != null) ...[
                  Container(
                    padding: const EdgeInsets.all(AppSpacing.md),
                    decoration: BoxDecoration(
                      color: AppColors.error.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                      border: Border.all(
                        color: AppColors.error.withValues(alpha: 0.25),
                      ),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.error_outline_rounded,
                            color: AppColors.error, size: 20),
                        const SizedBox(width: AppSpacing.sm),
                        Expanded(
                          child: Text(
                            _submissionError!,
                            key: const Key('submission_error_text'),
                            style: const TextStyle(
                              fontSize: 13,
                              color: AppColors.error,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),
                ],

                // 1. Issue Information Card
                AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Issue Details',
                        style:
                            Theme.of(context).textTheme.titleSmall?.copyWith(
                                  fontWeight: FontWeight.bold,
                                  color: AppColors.textPrimary,
                                ),
                      ),
                      const SizedBox(height: AppSpacing.md),

                      // Issue Type Selection
                      _buildIssueTypeDropdown(),
                      if (_issueTypeError != null) ...[
                        const SizedBox(height: 4),
                        Text(
                          _issueTypeError!,
                          key: const Key('issue_type_error_text'),
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.error,
                          ),
                        ),
                      ],
                      const SizedBox(height: AppSpacing.md),

                      // Title Field
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Row(
                            children: [
                              Text(
                                'Title',
                                style: Theme.of(context)
                                    .textTheme
                                    .titleSmall
                                    ?.copyWith(
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
                            '${_titleController.text.length} / 200',
                            key: const Key('title_char_count'),
                            style: TextStyle(
                              fontSize: 11,
                              color: _titleController.text.length > 200
                                  ? AppColors.error
                                  : AppColors.textMuted,
                              fontWeight: _titleController.text.length > 200
                                  ? FontWeight.bold
                                  : FontWeight.w500,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      AppTextField(
                        key: const Key('issue_title_input'),
                        controller: _titleController,
                        enabled: !_isSubmitting,
                        hint: 'Brief summary of the issue (e.g. Engine overheating on Main St)',
                        errorText: _titleError,
                        textInputAction: TextInputAction.next,
                      ),
                      const SizedBox(height: AppSpacing.md),

                      // Description Field
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Row(
                            children: [
                              Text(
                                'Description',
                                style: Theme.of(context)
                                    .textTheme
                                    .titleSmall
                                    ?.copyWith(
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
                            '${_descriptionController.text.length} / 2000',
                            key: const Key('description_char_count'),
                            style: TextStyle(
                              fontSize: 11,
                              color: _descriptionController.text.length > 2000
                                  ? AppColors.error
                                  : AppColors.textMuted,
                              fontWeight:
                                  _descriptionController.text.length > 2000
                                      ? FontWeight.bold
                                      : FontWeight.w500,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      AppTextField(
                        key: const Key('issue_description_input'),
                        controller: _descriptionController,
                        enabled: !_isSubmitting,
                        hint: 'Describe what happened, any immediate impact, and relevant context...',
                        errorText: _descriptionError,
                        maxLines: 5,
                        textInputAction: TextInputAction.newline,
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.md),

                // 2. Optional Location Card
                AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            'Location (Optional)',
                            style: Theme.of(context)
                                .textTheme
                                .titleSmall
                                ?.copyWith(
                                  fontWeight: FontWeight.bold,
                                  color: AppColors.textPrimary,
                                ),
                          ),
                          if (_selectedLatitude != null &&
                              _selectedLongitude != null)
                            TextButton.icon(
                              key: const Key('clear_location_button'),
                              onPressed: _onClearLocation,
                              icon: const Icon(Icons.clear,
                                  size: 16, color: AppColors.error),
                              label: const Text(
                                'Clear Location',
                                style: TextStyle(
                                  fontSize: 12,
                                  color: AppColors.error,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              style: TextButton.styleFrom(
                                visualDensity: VisualDensity.compact,
                                padding: EdgeInsets.zero,
                              ),
                            ),
                        ],
                      ),
                      const SizedBox(height: 4),
                      const Text(
                        'Add where the issue occurred to help staff understand the situation. Tap the map to select or use GPS.',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                      const SizedBox(height: AppSpacing.md),

                      // Map Container
                      _buildMapPicker(),
                      const SizedBox(height: AppSpacing.sm),

                      // Current Location Action & Selected Coordinates Indicator
                      Row(
                        children: [
                          Expanded(
                            child: OutlinedButton.icon(
                              key: const Key('use_current_location_button'),
                              onPressed: _isLocatingCurrent
                                  ? null
                                  : _onUseCurrentLocation,
                              icon: _isLocatingCurrent
                                  ? const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                        color: AppColors.primary,
                                      ),
                                    )
                                  : const Icon(Icons.my_location_rounded,
                                      size: 16, color: AppColors.primary),
                              label: Text(
                                _isLocatingCurrent
                                    ? 'Acquiring GPS...'
                                    : 'Use Current Location',
                                style: const TextStyle(
                                  fontSize: 13,
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.primary,
                                ),
                              ),
                              style: OutlinedButton.styleFrom(
                                side: const BorderSide(
                                    color: AppColors.primary),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(
                                      AppSpacing.radiusMd),
                                ),
                                padding: const EdgeInsets.symmetric(
                                    vertical: AppSpacing.sm),
                              ),
                            ),
                          ),
                        ],
                      ),
                      if (_selectedLatitude != null &&
                          _selectedLongitude != null) ...[
                        const SizedBox(height: AppSpacing.xs),
                        Text(
                          'Selected: ${_selectedLatitude!.toStringAsFixed(5)}, ${_selectedLongitude!.toStringAsFixed(5)}',
                          key: const Key('selected_coordinates_text'),
                          style: const TextStyle(
                            fontSize: 11,
                            color: AppColors.textMuted,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ],
                      const SizedBox(height: AppSpacing.md),

                      // Location Description Field
                      AppTextField(
                        key: const Key('issue_location_description_input'),
                        controller: _locationDescriptionController,
                        enabled: !_isSubmitting,
                        label: 'Location Description (Optional)',
                        hint: 'e.g. Near the east gate, beside the railway crossing',
                        errorText: _locationDescriptionError,
                        textInputAction: TextInputAction.done,
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.lg),

                // Submit Button
                AppButton.primary(
                  key: const Key('submit_operational_issue_button'),
                  label: 'Report Operational Issue',
                  icon: Icons.send_rounded,
                  isLoading: _isSubmitting,
                  onPressed: _isSubmitting ? null : _onSubmit,
                ),
                const SizedBox(height: AppSpacing.xl),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildIssueTypeDropdown() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Text(
              'Issue Type',
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(width: 4),
            const Text(
              '*',
              style: TextStyle(
                color: AppColors.error,
                fontWeight: FontWeight.bold,
              ),
            ),
          ],
        ),
        const SizedBox(height: 6),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
            border: Border.all(
              color: _issueTypeError != null
                  ? AppColors.error
                  : AppColors.border,
              width: 1,
            ),
          ),
          child: DropdownButtonHideUnderline(
            child: DropdownButton<OperationalIssueType>(
              key: const Key('issue_type_dropdown'),
              value: _selectedIssueType,
              isExpanded: true,
              hint: const Text(
                'Select issue type...',
                style: TextStyle(fontSize: 14, color: AppColors.textMuted),
              ),
              icon: const Icon(Icons.keyboard_arrow_down_rounded,
                  color: AppColors.textSecondary),
              items: OperationalIssueType.values.map((type) {
                return DropdownMenuItem<OperationalIssueType>(
                  key: Key('type_option_${type.value.toLowerCase()}'),
                  value: type,
                  child: Text(
                    type.displayName,
                    style: const TextStyle(
                      fontSize: 14,
                      color: AppColors.textPrimary,
                    ),
                  ),
                );
              }).toList(),
              onChanged: (value) {
                setState(() {
                  _selectedIssueType = value;
                  _issueTypeError = null;
                });
              },
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildMapPicker() {
    return Container(
      height: 220,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      child: Stack(
        children: [
          FlutterMap(
            mapController: _mapController,
            options: MapOptions(
              initialCenter: _currentMapCenter,
              initialZoom: _selectedLatitude != null ? 15.0 : 12.0,
              onTap: _onMapTap,
              interactionOptions: const InteractionOptions(
                flags: InteractiveFlag.all & ~InteractiveFlag.rotate,
              ),
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'com.smartwaste.mobile',
                tileProvider: widget.tileProvider,
              ),
              if (_selectedLatitude != null && _selectedLongitude != null)
                MarkerLayer(
                  markers: [
                    Marker(
                      point: LatLng(_selectedLatitude!, _selectedLongitude!),
                      width: 40,
                      height: 40,
                      alignment: Alignment.topCenter,
                      child: const Icon(
                        Icons.location_pin,
                        color: AppColors.primary,
                        size: 40,
                        shadows: [
                          Shadow(
                            color: Colors.black26,
                            blurRadius: 4,
                            offset: Offset(0, 2),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
            ],
          ),
          Positioned(
            top: 8,
            right: 8,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: AppColors.surface.withValues(alpha: 0.9),
                borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                border: Border.all(color: AppColors.border),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    _selectedLatitude != null
                        ? Icons.check_circle_rounded
                        : Icons.touch_app_outlined,
                    size: 14,
                    color: _selectedLatitude != null
                        ? AppColors.primary
                        : AppColors.textSecondary,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    _selectedLatitude != null
                        ? 'Location Selected'
                        : 'Tap map to select',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: _selectedLatitude != null
                          ? AppColors.primary
                          : AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
