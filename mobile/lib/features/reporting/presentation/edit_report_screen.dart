import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../data/reporting_repository.dart';
import '../models/selected_location.dart';
import '../models/update_waste_report_request.dart';
import '../models/waste_report_detail_model.dart';
import '../models/waste_report_status.dart';
import '../models/waste_type.dart';
import '../services/location_service.dart';
import 'map_location_picker_screen.dart';

/// Screen allowing citizens to edit core fields of their submitted waste report (Step 9A.9.3a).
/// Permitted ONLY when the report is in Submitted status.
/// Editable fields: Waste Type, Description (10-1000 chars), Coordinates, Address (max 500 chars).
/// Photo attachments, status, priority, and citizen assignment cannot be altered.
class EditReportScreen extends StatefulWidget {
  final String reportId;
  final WasteReportDetailModel? initialReport;
  final ReportingRepository? repository;
  final LocationService? locationService;
  final TileProvider? tileProvider;

  const EditReportScreen({
    super.key,
    required this.reportId,
    this.initialReport,
    this.repository,
    this.locationService,
    this.tileProvider,
  });

  @override
  State<EditReportScreen> createState() => _EditReportScreenState();
}

class _EditReportScreenState extends State<EditReportScreen> {
  final _formKey = GlobalKey<FormState>();

  late final ReportingRepository _repository;
  late final LocationService _locationService;

  WasteReportDetailModel? _originalReport;
  bool _isLoading = false;
  String? _loadError;

  // Form State
  WasteType? _selectedWasteType;
  late final TextEditingController _descriptionController;
  late final TextEditingController _addressController;
  double? _selectedLatitude;
  double? _selectedLongitude;

  // UI state
  bool _isLocatingCurrent = false;
  bool _isSubmitting = false;

  // Validation errors
  String? _wasteTypeError;
  String? _descriptionError;
  String? _locationError;
  String? _addressError;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? ReportingRepository();
    _locationService = widget.locationService ?? const GeolocatorLocationService();

    _descriptionController = TextEditingController();
    _addressController = TextEditingController();

    _descriptionController.addListener(() {
      if (mounted) setState(() {});
    });

    if (widget.initialReport != null) {
      _applyReportData(widget.initialReport!);
    } else {
      _fetchInitialReport();
    }
  }

  void _applyReportData(WasteReportDetailModel report) {
    _originalReport = report;
    _selectedWasteType = report.wasteType;
    _descriptionController.text = report.description;
    _addressController.text = report.addressText ?? '';
    _selectedLatitude = report.latitude;
    _selectedLongitude = report.longitude;
  }

  Future<void> _fetchInitialReport() async {
    setState(() {
      _isLoading = true;
      _loadError = null;
    });

    try {
      final report = await _repository.getWasteReport(widget.reportId);
      if (!mounted) return;

      if (report.status != WasteReportStatus.submitted) {
        setState(() {
          _isLoading = false;
          _loadError = 'Only submitted reports can be edited. This report is ${report.status.displayName}.';
        });
        return;
      }

      setState(() {
        _isLoading = false;
        _applyReportData(report);
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _loadError = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _loadError = 'Unable to load report details for editing.';
      });
    }
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  /// Validates all form inputs locally against SmartWaste business rules.
  bool _validateForm() {
    bool isValid = true;

    // 1. Waste Type validation
    if (_selectedWasteType == null) {
      _wasteTypeError = 'Please select a waste type.';
      isValid = false;
    } else {
      _wasteTypeError = null;
    }

    // 2. Description validation (10 - 1000 chars)
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

    // 3. Location validation (coordinates required, -90..90, -180..180)
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

    // 4. Address validation (Optional, max 500 chars)
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

  /// Obtains current GPS coordinates using LocationService.
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
            _locationError = null;
          });
        case LocationFailure(:final message):
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(message),
              duration: const Duration(seconds: 4),
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
    if (_isLocatingCurrent || _isSubmitting) return;

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

  /// Compares current form state against [_originalReport] and dispatches PATCH if changes exist.
  Future<void> _onSaveChanges() async {
    if (_isSubmitting || _isLoading || _originalReport == null) return;

    final isValid = _validateForm();
    if (!isValid) return;

    final orig = _originalReport!;

    final currentDesc = _descriptionController.text.trim();
    final currentType = _selectedWasteType!;
    final currentLat = _selectedLatitude!;
    final currentLon = _selectedLongitude!;
    final currentAddress = _addressController.text.trim();
    final origAddress = orig.addressText?.trim() ?? '';

    String? changedDesc;
    if (currentDesc != orig.description.trim()) {
      changedDesc = currentDesc;
    }

    WasteType? changedType;
    if (currentType != orig.wasteType) {
      changedType = currentType;
    }

    double? changedLat;
    double? changedLon;
    if (currentLat != orig.latitude || currentLon != orig.longitude) {
      changedLat = currentLat;
      changedLon = currentLon;
    }

    String? changedAddress;
    if (currentAddress != origAddress) {
      // If cleared, sends "" to trigger backend null assignment; otherwise trimmed string
      changedAddress = currentAddress;
    }

    final request = UpdateWasteReportRequest(
      description: changedDesc,
      wasteType: changedType,
      latitude: changedLat,
      longitude: changedLon,
      addressText: changedAddress,
    );

    if (request.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          key: Key('no_changes_snackbar'),
          content: Text('No changes to save.'),
          duration: Duration(seconds: 2),
        ),
      );
      return;
    }

    setState(() {
      _isSubmitting = true;
    });

    try {
      final updatedReport = await _repository.updateWasteReport(
        reportId: widget.reportId,
        request: request,
      );

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          key: Key('edit_report_success_snackbar'),
          content: Text('Report updated successfully.'),
          backgroundColor: AppColors.primary,
          duration: Duration(seconds: 2),
        ),
      );

      Navigator.of(context).pop(updatedReport);
    } on ApiException catch (e) {
      if (!mounted) return;

      if (e.statusCode == 409) {
        // Status conflict: report transitioned by officer while form was open
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            key: Key('edit_report_conflict_snackbar'),
            content: Text('This report can no longer be edited because its status has changed.'),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 4),
          ),
        );
        Navigator.of(context).pop();
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            key: const Key('edit_report_error_snackbar'),
            content: Text(e.message),
            backgroundColor: AppColors.error,
            duration: const Duration(seconds: 4),
          ),
        );
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          key: Key('edit_report_error_snackbar'),
          content: Text('Failed to update report. Please try again.'),
          backgroundColor: AppColors.error,
          duration: Duration(seconds: 4),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _isSubmitting = false;
        });
      }
    }
  }

  void _onCancel() {
    Navigator.of(context).maybePop();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text(
          'Edit Report',
          style: TextStyle(
            color: AppColors.textPrimary,
            fontWeight: FontWeight.bold,
            fontSize: 18,
          ),
        ),
        backgroundColor: AppColors.surface,
        elevation: 0,
        iconTheme: const IconThemeData(color: AppColors.textPrimary),
        centerTitle: false,
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_isLoading) {
      return const Center(
        child: AppLoadingIndicator(
          key: Key('edit_report_loading'),
          message: 'Loading report...',
        ),
      );
    }

    if (_loadError != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.xl),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(
                Icons.error_outline_rounded,
                size: 48,
                color: AppColors.error,
              ),
              const SizedBox(height: AppSpacing.md),
              Text(
                _loadError!,
                key: const Key('edit_report_error_text'),
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 14,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppSpacing.lg),
              AppButton.primary(
                key: const Key('edit_report_retry_button'),
                label: 'Retry',
                icon: Icons.refresh,
                onPressed: _fetchInitialReport,
              ),
            ],
          ),
        ),
      );
    }

    final charCount = _descriptionController.text.length;
    final hasLocation = _selectedLatitude != null && _selectedLongitude != null;

    return Center(
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
                // Info banner explaining that only core fields can be updated while Submitted
                Container(
                  padding: const EdgeInsets.all(AppSpacing.sm),
                  decoration: BoxDecoration(
                    color: AppColors.primaryLight,
                    borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                    border: Border.all(color: AppColors.primaryBorder),
                  ),
                  child: Row(
                    children: const [
                      Icon(Icons.info_outline, size: 18, color: AppColors.primaryDark),
                      SizedBox(width: AppSpacing.xs),
                      Expanded(
                        child: Text(
                          'You can update report details while it is pending review.',
                          style: TextStyle(
                            fontSize: 12,
                            color: AppColors.primaryDark,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.md),

                // 1. Waste Type Selection (Required)
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
                      onSelected: _isSubmitting
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

                // 2. Description (Required, 10-1000 chars)
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
                        color: charCount > 1000
                            ? AppColors.error
                            : (charCount < 10 ? AppColors.textSecondary : AppColors.primary),
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.xs),
                AppTextField(
                  key: const Key('edit_description_field'),
                  controller: _descriptionController,
                  hint: 'Describe the waste issue, estimated size, or hazards...',
                  maxLines: 4,
                  enabled: !_isSubmitting,
                  errorText: _descriptionError,
                ),
                const SizedBox(height: AppSpacing.lg),

                // 3. Location Section (Required coordinates)
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
                  key: const Key('edit_location_card'),
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
                          if (hasLocation && !_isSubmitting)
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
                                      onPressed: (_isLocatingCurrent || _isSubmitting)
                                          ? null
                                          : _onUseCurrentLocation,
                                    ),
                                    const SizedBox(height: AppSpacing.xs),
                                    AppButton.outlined(
                                      key: const Key('choose_on_map_button'),
                                      label: 'Choose on Map',
                                      icon: Icons.map_outlined,
                                      height: 40,
                                      onPressed: (_isLocatingCurrent || _isSubmitting)
                                          ? null
                                          : _onChooseLocationOnMap,
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
                                      onPressed: (_isLocatingCurrent || _isSubmitting)
                                          ? null
                                          : _onUseCurrentLocation,
                                    ),
                                  ),
                                  const SizedBox(width: AppSpacing.sm),
                                  Expanded(
                                    child: AppButton.outlined(
                                      key: const Key('choose_on_map_button'),
                                      label: 'Choose on Map',
                                      icon: Icons.map_outlined,
                                      height: 40,
                                      onPressed: (_isLocatingCurrent || _isSubmitting)
                                          ? null
                                          : _onChooseLocationOnMap,
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

                // 4. Address / Landmark (Optional, max 500 chars)
                Text(
                  'Address or Landmark',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                ),
                const SizedBox(height: AppSpacing.xxs),
                Text(
                  'Optional: provide nearby landmark, street name, or notes to help locate.',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                      ),
                ),
                const SizedBox(height: AppSpacing.xs),
                AppTextField(
                  key: const Key('edit_address_field'),
                  controller: _addressController,
                  hint: 'e.g., Near bus stop, opposite police station...',
                  enabled: !_isSubmitting,
                  errorText: _addressError,
                ),
                const SizedBox(height: AppSpacing.lg),

                // 5. Existing Photos (Read-Only)
                if (_originalReport != null && _originalReport!.attachments.isNotEmpty) ...[
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        'Attached Photos',
                        style: Theme.of(context).textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.w600,
                              color: AppColors.textPrimary,
                            ),
                      ),
                      Text(
                        '${_originalReport!.attachments.length} attached',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textMuted,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.xs),
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: _originalReport!.attachments.map((att) {
                      return Container(
                        width: 72,
                        height: 72,
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                          border: Border.all(color: AppColors.border),
                          color: AppColors.surfaceSubtle,
                        ),
                        clipBehavior: Clip.antiAlias,
                        child: Image.network(
                          att.fileUrl,
                          fit: BoxFit.cover,
                          errorBuilder: (_, _, _) => const Center(
                            child: Icon(Icons.broken_image_outlined, size: 24, color: AppColors.textMuted),
                          ),
                        ),
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: AppSpacing.lg),
                ],

                // 6. Action Buttons (Save Changes & Cancel)
                AppButton.primary(
                  key: const Key('save_changes_button'),
                  label: 'Save Changes',
                  icon: Icons.save_outlined,
                  isLoading: _isSubmitting,
                  onPressed: _isSubmitting ? null : _onSaveChanges,
                ),
                const SizedBox(height: AppSpacing.sm),
                AppButton.outlined(
                  key: const Key('cancel_button'),
                  label: 'Cancel',
                  onPressed: _isSubmitting ? null : _onCancel,
                ),
                const SizedBox(height: AppSpacing.xl),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
