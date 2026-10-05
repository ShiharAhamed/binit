import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:intl/intl.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../../reporting/models/waste_type.dart';
import '../data/public_waste_bins_repository.dart';
import '../models/public_bin_availability.dart';
import '../models/public_waste_bin_detail_model.dart';
import '../services/external_directions_launcher.dart';

/// Citizen-facing, read-only view of a single public waste bin.
class BinDetailsScreen extends StatefulWidget {
  final String binId;
  final PublicWasteBinsRepository? repository;
  final TileProvider? tileProvider;
  final ExternalDirectionsLauncher? directionsLauncher;

  const BinDetailsScreen({
    super.key,
    required this.binId,
    this.repository,
    this.tileProvider,
    this.directionsLauncher,
  });

  @override
  State<BinDetailsScreen> createState() => _BinDetailsScreenState();
}

class _BinDetailsScreenState extends State<BinDetailsScreen> {
  late final PublicWasteBinsRepository _repository;
  late final ExternalDirectionsLauncher _directionsLauncher;

  PublicWasteBinDetailModel? _bin;
  bool _isLoading = true;
  bool _isOpeningDirections = false;
  String? _errorMessage;

  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? PublicWasteBinsRepository();
    _directionsLauncher =
        widget.directionsLauncher ??
        const UrlLauncherExternalDirectionsLauncher();
    _loadBin();
  }

  Future<void> _loadBin() async {
    if (_isLoading && _bin == null && _errorMessage == null) {
      // The initial request started by initState is already in flight.
    } else if (mounted) {
      setState(() {
        _isLoading = true;
        _errorMessage = null;
      });
    }

    try {
      final bin = await _repository.getPublicWasteBin(widget.binId);
      if (!mounted) return;
      setState(() {
        _bin = bin;
        _isLoading = false;
        _errorMessage = null;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = error.statusCode == 404
            ? 'This bin is no longer available.'
            : error.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = 'Unable to load bin details. Please check your connection and try again.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Bin Details'),
        actions: [
          IconButton(
            key: const Key('refresh_bin_detail_button'),
            tooltip: 'Refresh bin details',
            onPressed: _isLoading ? null : _loadBin,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_isLoading && _bin == null) {
      return const Center(
        child: AppLoadingIndicator(
          key: Key('bin_detail_loading'),
          message: 'Loading bin details...',
        ),
      );
    }

    if (_errorMessage != null && _bin == null) return _buildErrorState();

    final bin = _bin!;
    return LayoutBuilder(
      builder: (context, constraints) {
        final mapHeight = (constraints.maxHeight * 0.25).clamp(150.0, 190.0);
        return RefreshIndicator(
          onRefresh: _loadBin,
          color: AppColors.primary,
          child: ListView(
            key: const Key('bin_detail_scroll_view'),
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.xs,
            ),
            children: [
              if (_errorMessage != null) _buildRefreshErrorBanner(),
              _buildHeader(bin),
              const SizedBox(height: AppSpacing.xs),
              _buildBinInformation(bin),
              const SizedBox(height: AppSpacing.xs),
              _buildWasteTypes(bin.acceptedWasteTypes),
              const SizedBox(height: AppSpacing.xs),
              _buildLocation(bin, mapHeight: mapHeight),
            ],
          ),
        );
      },
    );
  }

  Widget _buildRefreshErrorBanner() => Container(
    key: const Key('bin_detail_refresh_error'),
    margin: const EdgeInsets.only(bottom: AppSpacing.md),
    padding: const EdgeInsets.all(AppSpacing.sm),
    decoration: BoxDecoration(
      color: AppColors.errorLight,
      borderRadius: AppSpacing.roundedMd,
      border: Border.all(color: AppColors.errorBorder),
    ),
    child: Text(
      _errorMessage!,
      style: const TextStyle(color: AppColors.errorText),
    ),
  );

  Widget _buildHeader(PublicWasteBinDetailModel bin) {
    final availability = _BinAvailabilityPresentation.from(
      bin.publicAvailability,
    );
    return AppCard(
      key: const Key('bin_detail_status_section'),
      padding: const EdgeInsets.all(AppSpacing.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  bin.binCode,
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: AppColors.textPrimary,
                  ),
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              _AvailabilityBadge(presentation: availability),
            ],
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            availability.explanation,
            style: const TextStyle(color: AppColors.textSecondary),
          ),
          if (bin.isCollectionScheduled) ...[
            const SizedBox(height: AppSpacing.xs),
            Container(
              key: const Key('collection_scheduled_indicator'),
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.xs,
                vertical: AppSpacing.xxs,
              ),
              decoration: BoxDecoration(
                color: AppColors.infoLight,
                borderRadius: AppSpacing.roundedFull,
              ),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    Icons.event_available_outlined,
                    size: 14,
                    color: AppColors.infoText,
                  ),
                  SizedBox(width: AppSpacing.xs),
                  Text(
                    'Collection scheduled',
                    style: TextStyle(
                      color: AppColors.infoText,
                      fontWeight: FontWeight.w600,
                      fontSize: 12,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildBinInformation(PublicWasteBinDetailModel bin) => AppCard(
    key: const Key('bin_detail_information_section'),
    padding: const EdgeInsets.all(AppSpacing.sm),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Bin information',
          style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.xs),
        LayoutBuilder(
          builder: (context, constraints) {
            final items = [
              _CompactInfoItem(
                icon: Icons.inventory_2_outlined,
                label: 'Capacity',
                value: '${bin.capacityLiters} L',
              ),
              if (bin.lastObservedAt != null)
                _CompactInfoItem(
                  icon: Icons.update_outlined,
                  label: 'Last observed',
                  value: _dateFormat.format(bin.lastObservedAt!.toLocal()),
                ),
            ];
            if (items.length == 1 || constraints.maxWidth < 330) {
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  items[0],
                  if (items.length > 1) ...[
                    const SizedBox(height: AppSpacing.xs),
                    items[1],
                  ],
                ],
              );
            }
            return Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(child: items[0]),
                const SizedBox(width: AppSpacing.sm),
                Expanded(child: items[1]),
              ],
            );
          },
        ),
      ],
    ),
  );

  Widget _buildWasteTypes(List<WasteType> wasteTypes) => AppCard(
    key: const Key('bin_detail_waste_types_section'),
    padding: const EdgeInsets.all(AppSpacing.sm),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Accepted waste types',
          style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.xs),
        if (wasteTypes.isEmpty)
          const Text(
            'No accepted waste types are listed.',
            style: TextStyle(color: AppColors.textSecondary),
          )
        else
          Wrap(
            spacing: AppSpacing.xs,
            runSpacing: AppSpacing.xs,
            children: wasteTypes
                .map((type) => _WasteTypeChip(label: type.displayName))
                .toList(),
          ),
      ],
    ),
  );

  Widget _buildLocation(
    PublicWasteBinDetailModel bin, {
    required double mapHeight,
  }) {
    final hasCoordinates = _hasValidCoordinates(bin);
    final locationText = bin.addressText?.trim().isNotEmpty == true
        ? bin.addressText!.trim()
        : '${bin.latitude.toStringAsFixed(4)}, ${bin.longitude.toStringAsFixed(4)}';
    return AppCard(
      key: const Key('bin_detail_location_section'),
      padding: const EdgeInsets.all(AppSpacing.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Location',
            style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            locationText,
            key: const Key('bin_detail_location_text'),
            style: const TextStyle(color: AppColors.textSecondary),
          ),
          if (hasCoordinates) ...[
            const SizedBox(height: AppSpacing.xs),
            SizedBox(
              height: mapHeight,
              child: ClipRRect(
                borderRadius: AppSpacing.roundedMd,
                child: FlutterMap(
                  options: MapOptions(
                    initialCenter: LatLng(bin.latitude, bin.longitude),
                    initialZoom: 15,
                    minZoom: 3,
                    maxZoom: 19,
                  ),
                  children: [
                    TileLayer(
                      urlTemplate:
                          'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                      userAgentPackageName: 'com.smartwaste.mobile',
                      tileProvider: widget.tileProvider,
                    ),
                    MarkerLayer(
                      markers: [
                        Marker(
                          key: const Key('bin_detail_location_marker'),
                          point: LatLng(bin.latitude, bin.longitude),
                          width: 48,
                          height: 48,
                          child: const Icon(
                            Icons.location_on,
                            size: 40,
                            color: AppColors.primaryDark,
                          ),
                        ),
                      ],
                    ),
                    const _MapAttribution(),
                  ],
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.sm),
            AppButton.outlined(
              key: const Key('get_directions_button'),
              label: 'Get Directions',
              icon: Icons.directions_outlined,
              isLoading: _isOpeningDirections,
              onPressed: _isOpeningDirections
                  ? null
                  : () => _openDirections(bin),
            ),
          ] else ...[
            const SizedBox(height: AppSpacing.sm),
            const Text(
              'Location unavailable',
              key: Key('bin_detail_location_unavailable'),
              style: TextStyle(color: AppColors.textSecondary),
            ),
            const SizedBox(height: AppSpacing.sm),
            const AppButton.outlined(
              key: Key('get_directions_unavailable_button'),
              label: 'Get Directions',
              icon: Icons.directions_outlined,
              onPressed: null,
            ),
            const SizedBox(height: AppSpacing.xs),
            const Text(
              'Directions are unavailable because this bin has no valid saved location.',
              style: TextStyle(color: AppColors.textSecondary),
            ),
          ],
        ],
      ),
    );
  }

  bool _hasValidCoordinates(PublicWasteBinDetailModel bin) =>
      bin.latitude >= -90 &&
      bin.latitude <= 90 &&
      bin.longitude >= -180 &&
      bin.longitude <= 180;

  Future<void> _openDirections(PublicWasteBinDetailModel bin) async {
    if (_isOpeningDirections || !_hasValidCoordinates(bin)) return;

    setState(() => _isOpeningDirections = true);
    var wasOpened = false;
    try {
      wasOpened = await _directionsLauncher.openDirections(
        latitude: bin.latitude,
        longitude: bin.longitude,
      );
    } catch (_) {
      wasOpened = false;
    } finally {
      if (mounted) setState(() => _isOpeningDirections = false);
    }

    if (!mounted || wasOpened) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text(
          'Unable to open directions. Please try another maps app.',
        ),
      ),
    );
  }

  Widget _buildErrorState() => Center(
    child: Padding(
      padding: const EdgeInsets.all(AppSpacing.xxl),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.error_outline, size: 48, color: AppColors.error),
          const SizedBox(height: AppSpacing.md),
          const Text(
            'Unable to Load Bin Details',
            style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            _errorMessage!,
            textAlign: TextAlign.center,
            style: const TextStyle(color: AppColors.textSecondary),
          ),
          const SizedBox(height: AppSpacing.lg),
          AppButton.primary(
            key: const Key('retry_load_bin_detail_button'),
            label: 'Retry',
            onPressed: _loadBin,
          ),
        ],
      ),
    ),
  );
}

class _CompactInfoItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;

  const _CompactInfoItem({
    required this.icon,
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) => Row(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Icon(icon, size: 16, color: AppColors.textSecondary),
      const SizedBox(width: AppSpacing.xs),
      Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 12,
              ),
            ),
            Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
            ),
          ],
        ),
      ),
    ],
  );
}

class _WasteTypeChip extends StatelessWidget {
  final String label;

  const _WasteTypeChip({required this.label});

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(
      horizontal: AppSpacing.xs,
      vertical: AppSpacing.xxs,
    ),
    decoration: BoxDecoration(
      color: AppColors.primaryLight,
      borderRadius: AppSpacing.roundedFull,
    ),
    child: Text(
      label,
      style: const TextStyle(
        color: AppColors.primaryDark,
        fontWeight: FontWeight.w600,
        fontSize: 12,
      ),
    ),
  );
}

class _MapAttribution extends StatelessWidget {
  const _MapAttribution();

  @override
  Widget build(BuildContext context) => Align(
    alignment: Alignment.topRight,
    child: Container(
      margin: const EdgeInsets.all(AppSpacing.xxs),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.xxs,
        vertical: 2,
      ),
      decoration: BoxDecoration(
        color: AppColors.surface.withValues(alpha: 0.88),
        borderRadius: AppSpacing.roundedSm,
      ),
      child: const Text(
        'OpenStreetMap contributors',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(fontSize: 10, color: AppColors.textSecondary),
      ),
    ),
  );
}

class _BinAvailabilityPresentation {
  final String label;
  final String explanation;
  final IconData icon;
  final Color background;
  final Color foreground;

  const _BinAvailabilityPresentation(
    this.label,
    this.explanation,
    this.icon,
    this.background,
    this.foreground,
  );

  factory _BinAvailabilityPresentation.from(
    PublicBinAvailability availability,
  ) {
    switch (availability) {
      case PublicBinAvailability.usable:
        return const _BinAvailabilityPresentation(
          'Usable',
          'Available for use.',
          Icons.check_circle_outline,
          AppColors.successLight,
          AppColors.successText,
        );
      case PublicBinAvailability.warning:
        return const _BinAvailabilityPresentation(
          'Warning',
          'Getting full. Consider another bin if possible.',
          Icons.warning_amber_outlined,
          AppColors.warningLight,
          AppColors.warningText,
        );
      case PublicBinAvailability.full:
        return const _BinAvailabilityPresentation(
          'Full',
          'Full. Choose another bin.',
          Icons.delete_outline,
          AppColors.errorLight,
          AppColors.errorText,
        );
      case PublicBinAvailability.unavailable:
        return const _BinAvailabilityPresentation(
          'Unavailable',
          'Currently unavailable.',
          Icons.block_outlined,
          AppColors.surfaceSubtle,
          AppColors.textSecondary,
        );
      case PublicBinAvailability.unknown:
        return const _BinAvailabilityPresentation(
          'Unknown',
          'Current condition has not been confirmed.',
          Icons.help_outline,
          AppColors.infoLight,
          AppColors.infoText,
        );
    }
  }
}

class _AvailabilityBadge extends StatelessWidget {
  final _BinAvailabilityPresentation presentation;

  const _AvailabilityBadge({required this.presentation});

  @override
  Widget build(BuildContext context) => Semantics(
    label: 'Availability: ${presentation.label}',
    child: Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: AppSpacing.xs,
      ),
      decoration: BoxDecoration(
        color: presentation.background,
        borderRadius: AppSpacing.roundedFull,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(presentation.icon, size: 17, color: presentation.foreground),
          const SizedBox(width: AppSpacing.xs),
          Text(
            presentation.label,
            style: TextStyle(
              color: presentation.foreground,
              fontWeight: FontWeight.bold,
            ),
          ),
        ],
      ),
    ),
  );
}
