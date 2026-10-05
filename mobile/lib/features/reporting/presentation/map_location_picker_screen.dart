import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../models/selected_location.dart';
import '../services/location_service.dart';

/// Screen allowing citizens to interactively pick a waste report location on a map.
/// Uses OpenStreetMap raster tiles, standard attribution, and returns a provider-neutral
/// [SelectedLocation] upon confirmation.
class MapLocationPickerScreen extends StatefulWidget {
  final double? initialLatitude;
  final double? initialLongitude;
  final LocationService? locationService;
  final TileProvider? tileProvider;

  /// Default fallback camera center (Colombo, Sri Lanka) when no initial
  /// coordinates are provided.
  /// IMPORTANT: This fallback center is NOT a selected report location.
  static const LatLng defaultFallbackCenter = LatLng(6.9271, 79.8612);

  const MapLocationPickerScreen({
    super.key,
    this.initialLatitude,
    this.initialLongitude,
    this.locationService,
    this.tileProvider,
  });

  @override
  State<MapLocationPickerScreen> createState() => _MapLocationPickerScreenState();
}

class _MapLocationPickerScreenState extends State<MapLocationPickerScreen> {
  late final MapController _mapController;
  late final LocationService _locationService;

  LatLng? _selectedPoint;
  bool _isLocatingCurrent = false;

  @override
  void initState() {
    super.initState();
    _mapController = MapController();
    _locationService = widget.locationService ?? const GeolocatorLocationService();

    // Priority 1: Existing selected report coordinates, if present
    if (widget.initialLatitude != null && widget.initialLongitude != null) {
      _selectedPoint = LatLng(widget.initialLatitude!, widget.initialLongitude!);
    }
  }

  @override
  void dispose() {
    _mapController.dispose();
    super.dispose();
  }

  LatLng get _initialCenter {
    if (_selectedPoint != null) {
      return _selectedPoint!;
    }
    return MapLocationPickerScreen.defaultFallbackCenter;
  }

  /// Explicit current-location action from map screen.
  Future<void> _onRecenterCurrentLocation() async {
    if (_isLocatingCurrent) return;

    setState(() {
      _isLocatingCurrent = true;
    });

    try {
      final result = await _locationService.getCurrentLocation();
      if (!mounted) return;

      switch (result) {
        case LocationSuccess(:final location):
          final point = LatLng(location.latitude, location.longitude);
          _mapController.move(point, 16.0);
          setState(() {
            _selectedPoint = point;
          });
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

  void _onConfirmLocation() {
    if (_selectedPoint == null) return;

    final location = SelectedLocation(
      latitude: _selectedPoint!.latitude,
      longitude: _selectedPoint!.longitude,
    );

    Navigator.of(context).pop(location);
  }

  @override
  Widget build(BuildContext context) {
    final hasSelection = _selectedPoint != null;

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          key: const Key('map_picker_back_button'),
          icon: const Icon(Icons.arrow_back),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
        title: const Text('Select Waste Location'),
        centerTitle: true,
      ),
      body: Stack(
        children: [
          // 1. Interactive OpenStreetMap
          FlutterMap(
            mapController: _mapController,
            options: MapOptions(
              initialCenter: _initialCenter,
              initialZoom: 14.0,
              minZoom: 3.0,
              maxZoom: 19.0,
              onTap: (tapPosition, point) {
                setState(() {
                  _selectedPoint = point;
                });
              },
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'com.smartwaste.mobile',
                tileProvider: widget.tileProvider,
              ),
              if (_selectedPoint != null)
                MarkerLayer(
                  markers: [
                    Marker(
                      key: const Key('selected_location_marker'),
                      point: _selectedPoint!,
                      width: 44,
                      height: 44,
                      alignment: Alignment.topCenter,
                      child: const Icon(
                        Icons.location_on,
                        color: AppColors.primary,
                        size: 40,
                      ),
                    ),
                  ],
                ),
              const SimpleAttributionWidget(
                source: Text('OpenStreetMap contributors'),
                alignment: Alignment.topRight,
              ),
            ],
          ),

          // 2. Floating Current Location / Recenter Control
          Positioned(
            right: AppSpacing.md,
            bottom: 180,
            child: FloatingActionButton.small(
              key: const Key('map_recenter_button'),
              heroTag: 'map_recenter_fab',
              backgroundColor: AppColors.surface,
              foregroundColor: AppColors.primary,
              elevation: 3,
              tooltip: 'Use My Location',
              onPressed: _isLocatingCurrent ? null : _onRecenterCurrentLocation,
              child: _isLocatingCurrent
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        valueColor: AlwaysStoppedAnimation<Color>(AppColors.primary),
                      ),
                    )
                  : const Icon(Icons.my_location, size: 20),
            ),
          ),

          // 3. Bottom Confirmation Panel
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: Container(
              padding: const EdgeInsets.all(AppSpacing.lg),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(AppSpacing.radiusLg),
                  topRight: Radius.circular(AppSpacing.radiusLg),
                ),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.08),
                    blurRadius: 10,
                    offset: const Offset(0, -2),
                  ),
                ],
                border: const Border(
                  top: BorderSide(color: AppColors.border),
                ),
              ),
              child: SafeArea(
                top: false,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.all(AppSpacing.xs),
                          decoration: BoxDecoration(
                            color: hasSelection ? AppColors.primary : AppColors.surfaceSubtle,
                            shape: BoxShape.circle,
                          ),
                          child: Icon(
                            hasSelection ? Icons.check : Icons.touch_app_outlined,
                            size: 18,
                            color: hasSelection ? Colors.white : AppColors.textSecondary,
                          ),
                        ),
                        const SizedBox(width: AppSpacing.sm),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                hasSelection ? 'Selected Location' : 'Tap Map to Select Location',
                                key: const Key('map_selection_status_text'),
                                style: const TextStyle(
                                  fontWeight: FontWeight.w600,
                                  fontSize: 14,
                                  color: AppColors.textPrimary,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                hasSelection
                                    ? '${_selectedPoint!.latitude.toStringAsFixed(5)}, ${_selectedPoint!.longitude.toStringAsFixed(5)}'
                                    : 'Pan and tap where the waste is located.',
                                key: hasSelection
                                    ? const Key('map_selected_coordinates_text')
                                    : const Key('map_no_selection_text'),
                                style: TextStyle(
                                  fontSize: 12,
                                  color: hasSelection ? AppColors.textPrimary : AppColors.textSecondary,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.md),
                    AppButton.primary(
                      key: const Key('use_this_location_button'),
                      label: 'Use This Location',
                      icon: Icons.check,
                      onPressed: hasSelection ? _onConfirmLocation : null,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
