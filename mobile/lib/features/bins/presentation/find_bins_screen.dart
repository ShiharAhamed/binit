import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../../reporting/models/waste_type.dart';
import '../../reporting/models/selected_location.dart';
import '../../reporting/services/location_service.dart';
import '../data/public_waste_bins_repository.dart';
import '../models/public_bin_availability.dart';
import '../models/public_waste_bin_list_item_model.dart';
import '../models/public_waste_bin_query.dart';

/// Citizen list view for public roadside-bin discovery.
/// Location-based filtering and map presentation are intentionally deferred.
class FindBinsScreen extends StatefulWidget {
  final PublicWasteBinsRepository? repository;
  final LocationService? locationService;
  final TileProvider? tileProvider;

  const FindBinsScreen({
    super.key,
    this.repository,
    this.locationService,
    this.tileProvider,
  });

  @override
  State<FindBinsScreen> createState() => _FindBinsScreenState();
}

class _FindBinsScreenState extends State<FindBinsScreen> {
  final ScrollController _scrollController = ScrollController();
  late final MapController _mapController;
  late final PublicWasteBinsRepository _repository;
  late final LocationService _locationService;

  List<PublicWasteBinListItemModel> _bins = [];
  WasteType? _selectedWasteType;
  bool _isLoadingInitial = true;
  bool _isLoadingMore = false;
  bool _isRefreshing = false;
  String? _errorMessage;
  String? _loadMoreErrorMessage;
  int _currentPage = 1;
  bool _hasMore = false;
  int _requestGeneration = 0;
  _BinDiscoveryView _view = _BinDiscoveryView.list;
  SelectedLocation? _nearbyLocation;
  PublicWasteBinListItemModel? _selectedBin;
  bool _isLocating = false;
  bool _mapReady = false;
  bool _shouldFitMap = true;

  static const double _nearbyRadiusKm = 5.0;
  static const LatLng _fallbackCenter = LatLng(6.9271, 79.8612);

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? PublicWasteBinsRepository();
    _locationService =
        widget.locationService ?? const GeolocatorLocationService();
    _mapController = MapController();
    _scrollController.addListener(_onScroll);
    _fetchBins(page: 1);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    _mapController.dispose();
    super.dispose();
  }

  bool get _isFiltered => _selectedWasteType != null;

  void _onScroll() {
    if (!_scrollController.hasClients ||
        !_hasMore ||
        _isLoadingMore ||
        _isLoadingInitial ||
        _isRefreshing) {
      return;
    }
    if (_scrollController.position.maxScrollExtent -
            _scrollController.position.pixels <=
        200) {
      _fetchBins(page: _currentPage + 1, isLoadMore: true);
    }
  }

  Future<void> _onRefresh() => _fetchBins(page: 1, isRefresh: true);

  void _onWasteTypeChanged(WasteType? wasteType) {
    if (_selectedWasteType == wasteType) return;
    setState(() => _selectedWasteType = wasteType);
    _shouldFitMap = true;
    _fetchBins(page: 1);
  }

  void _onViewChanged(_BinDiscoveryView view) {
    if (_view == view) return;
    setState(() => _view = view);
    if (view == _BinDiscoveryView.map) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _fitMapToBins());
    }
  }

  Future<void> _useMyLocation() async {
    if (_isLocating) return;
    setState(() => _isLocating = true);
    try {
      final result = await _locationService.getCurrentLocation();
      if (!mounted) return;
      switch (result) {
        case LocationSuccess(:final location):
          setState(() {
            _nearbyLocation = location;
            _selectedBin = null;
            _shouldFitMap = true;
          });
          await _fetchBins(page: 1);
          if (_mapReady) {
            _mapController.move(
              LatLng(location.latitude, location.longitude),
              14,
            );
          }
        case LocationFailure(:final reason, :final message):
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(message),
              action: reason == LocationFailureReason.serviceDisabled
                  ? SnackBarAction(
                      label: 'Settings',
                      onPressed: _locationService.openLocationSettings,
                    )
                  : reason == LocationFailureReason.permissionDeniedForever
                  ? SnackBarAction(
                      label: 'Settings',
                      onPressed: _locationService.openAppSettings,
                    )
                  : null,
            ),
          );
      }
    } finally {
      if (mounted) setState(() => _isLocating = false);
    }
  }

  void _clearNearbySearch() {
    if (_nearbyLocation == null) return;
    setState(() {
      _nearbyLocation = null;
      _selectedBin = null;
      _shouldFitMap = true;
    });
    _fetchBins(page: 1);
  }

  Future<void> _fetchBins({
    required int page,
    bool isRefresh = false,
    bool isLoadMore = false,
  }) async {
    if (isLoadMore && _isLoadingMore) return;
    final requestGeneration = ++_requestGeneration;

    setState(() {
      if (isLoadMore) {
        _isLoadingMore = true;
        _loadMoreErrorMessage = null;
      } else if (isRefresh) {
        _isRefreshing = true;
      } else {
        _isLoadingInitial = true;
        _errorMessage = null;
      }
    });

    try {
      final result = await _repository.getPublicWasteBins(
        PublicWasteBinQuery(
          latitude: _nearbyLocation?.latitude,
          longitude: _nearbyLocation?.longitude,
          radiusKm: _nearbyLocation == null ? null : _nearbyRadiusKm,
          wasteType: _selectedWasteType,
          page: page,
          pageSize: 20,
        ),
      );
      if (!mounted || requestGeneration != _requestGeneration) return;

      setState(() {
        if (isLoadMore) {
          final existingIds = _bins.map((bin) => bin.id).toSet();
          _bins = [
            ..._bins,
            ...result.items.where((bin) => !existingIds.contains(bin.id)),
          ];
        } else {
          _bins = result.items;
          _selectedBin = null;
        }
        _currentPage = result.page;
        _hasMore = result.hasMore;
        _errorMessage = null;
        _loadMoreErrorMessage = null;
      });
      WidgetsBinding.instance.addPostFrameCallback((_) => _fitMapToBins());
    } on ApiException catch (error) {
      if (!mounted || requestGeneration != _requestGeneration) return;
      setState(() {
        if (isLoadMore) {
          _loadMoreErrorMessage = error.message;
        } else {
          _errorMessage = error.message;
        }
      });
    } catch (_) {
      if (!mounted || requestGeneration != _requestGeneration) return;
      setState(() {
        if (isLoadMore) {
          _loadMoreErrorMessage = 'Unable to load more bins. Tap to retry.';
        } else {
          _errorMessage = 'Unable to load public bins. Please check your connection and try again.';
        }
      });
    } finally {
      if (mounted && requestGeneration == _requestGeneration) {
        setState(() {
          _isLoadingInitial = false;
          _isLoadingMore = false;
          _isRefreshing = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Find a Bin')),
      body: Column(
        children: [
          _buildDiscoveryControls(),
          _buildFilter(),
          Expanded(child: _buildBody()),
        ],
      ),
    );
  }

  Widget _buildDiscoveryControls() {
    final isNearby = _nearbyLocation != null;
    return Container(
      padding: const EdgeInsets.fromLTRB(
        AppSpacing.md,
        AppSpacing.sm,
        AppSpacing.md,
        AppSpacing.xs,
      ),
      color: AppColors.surface,
      child: Column(
        children: [
          SegmentedButton<_BinDiscoveryView>(
            key: const Key('bin_view_switch'),
            segments: const [
              ButtonSegment(
                value: _BinDiscoveryView.list,
                icon: Icon(Icons.view_list_outlined),
                label: Text('List'),
              ),
              ButtonSegment(
                value: _BinDiscoveryView.map,
                icon: Icon(Icons.map_outlined),
                label: Text('Map'),
              ),
            ],
            selected: {_view},
            onSelectionChanged: (views) => _onViewChanged(views.first),
          ),
          const SizedBox(height: AppSpacing.xs),
          LayoutBuilder(
            builder: (context, constraints) {
              final action = isNearby
                  ? AppButton.text(
                      key: const Key('clear_nearby_search_button'),
                      label: 'Clear nearby',
                      onPressed: _clearNearbySearch,
                    )
                  : AppButton.text(
                      key: const Key('use_my_location_button'),
                      label: 'Use My Location',
                      icon: Icons.my_location_outlined,
                      isLoading: _isLocating,
                      onPressed: _isLocating ? null : _useMyLocation,
                    );
              final status = isNearby
                  ? Semantics(
                      label: 'Nearby search active within 5 kilometres',
                      child: const Text(
                        'Nearby search active · within 5 km',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.primaryDark,
                        ),
                      ),
                    )
                  : const Text(
                      'Browse public bins or search near your location.',
                      style: TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    );
              if (constraints.maxWidth < 330) {
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    status,
                    const SizedBox(height: AppSpacing.xxs),
                    action,
                  ],
                );
              }
              return Row(
                children: [
                  Expanded(child: status),
                  action,
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Widget _buildFilter() {
    return Container(
      padding: const EdgeInsets.fromLTRB(
        AppSpacing.md,
        AppSpacing.sm,
        AppSpacing.md,
        AppSpacing.sm,
      ),
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(bottom: BorderSide(color: AppColors.border)),
      ),
      child: InputDecorator(
        decoration: const InputDecoration(
          labelText: 'Waste type',
          prefixIcon: Icon(Icons.recycling_outlined),
          isDense: true,
        ),
        child: DropdownButtonHideUnderline(
          child: DropdownButton<WasteType?>(
            key: const Key('bin_waste_type_filter'),
            value: _selectedWasteType,
            isExpanded: true,
            items: [
              const DropdownMenuItem<WasteType?>(
                value: null,
                child: Text('All waste types'),
              ),
              ...WasteType.values.map(
                (type) => DropdownMenuItem<WasteType?>(
                  value: type,
                  child: Text(type.displayName),
                ),
              ),
            ],
            onChanged: _onWasteTypeChanged,
          ),
        ),
      ),
    );
  }

  Widget _buildBody() {
    if (_isLoadingInitial) {
      return const Center(
        child: AppLoadingIndicator(message: 'Loading public bins...'),
      );
    }
    if (_errorMessage != null && _bins.isEmpty) return _buildErrorState();
    if (_view == _BinDiscoveryView.map) return _buildMapView();
    if (_bins.isEmpty) return _buildEmptyState();
    return RefreshIndicator(
      onRefresh: _onRefresh,
      color: AppColors.primary,
      child: ListView.separated(
        key: const Key('find_bins_list_view'),
        controller: _scrollController,
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount:
            _bins.length +
            (_hasMore || _isLoadingMore || _loadMoreErrorMessage != null
                ? 1
                : 0),
        separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
        itemBuilder: (_, index) {
          if (index == _bins.length) return _buildPaginationFooter();
          return _BinCard(
            bin: _bins[index],
            onTap: () => _openBinDetails(_bins[index]),
          );
        },
      ),
    );
  }

  Widget _buildMapView() {
    final markers = _bins
        .where(_hasValidCoordinates)
        .map(_buildMarker)
        .toList(growable: false);
    return Stack(
      children: [
        FlutterMap(
          mapController: _mapController,
          options: MapOptions(
            initialCenter: _fallbackCenter,
            initialZoom: 12,
            minZoom: 3,
            maxZoom: 19,
            onMapReady: () {
              _mapReady = true;
              _fitMapToBins();
            },
            onPositionChanged: (_, hasGesture) {
              if (hasGesture) _shouldFitMap = false;
            },
          ),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.smartwaste.mobile',
              tileProvider: widget.tileProvider,
            ),
            if (_nearbyLocation != null)
              MarkerLayer(
                markers: [
                  Marker(
                    key: const Key('citizen_location_marker'),
                    point: LatLng(
                      _nearbyLocation!.latitude,
                      _nearbyLocation!.longitude,
                    ),
                    width: 42,
                    height: 42,
                    child: const Icon(
                      Icons.my_location,
                      color: AppColors.info,
                      size: 30,
                    ),
                  ),
                ],
              ),
            if (markers.isNotEmpty) MarkerLayer(markers: markers),
            const SimpleAttributionWidget(
              source: Text('OpenStreetMap contributors'),
              alignment: Alignment.topRight,
            ),
          ],
        ),
        if (markers.isEmpty)
          const Center(
            child: Card(
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.md),
                child: Text('No bin locations are available for this result.'),
              ),
            ),
          ),
        if (_selectedBin != null) _buildMapPreview(_selectedBin!),
        if (_hasMore || _isLoadingMore || _loadMoreErrorMessage != null)
          Positioned(
            left: AppSpacing.md,
            right: AppSpacing.md,
            bottom: _selectedBin == null ? AppSpacing.md : 185,
            child: _buildPaginationFooter(),
          ),
      ],
    );
  }

  Marker _buildMarker(PublicWasteBinListItemModel bin) {
    final presentation = _AvailabilityPresentation.from(bin.publicAvailability);
    return Marker(
      key: Key('public_bin_marker_${bin.id}'),
      point: LatLng(bin.latitude, bin.longitude),
      width: 48,
      height: 48,
      child: Semantics(
        label: '${bin.binCode}, ${presentation.label}',
        button: true,
        child: GestureDetector(
          onTap: () => setState(() => _selectedBin = bin),
          child: Icon(
            presentation.icon,
            size: 36,
            color: presentation.foreground,
          ),
        ),
      ),
    );
  }

  Widget _buildMapPreview(PublicWasteBinListItemModel bin) {
    final location = bin.addressText?.trim().isNotEmpty == true
        ? bin.addressText!.trim()
        : '${bin.latitude.toStringAsFixed(4)}, ${bin.longitude.toStringAsFixed(4)}';
    return Positioned(
      left: AppSpacing.md,
      right: AppSpacing.md,
      bottom: AppSpacing.md,
      child: Material(
        elevation: 5,
        borderRadius: AppSpacing.roundedLg,
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      bin.binCode,
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                  ),
                  IconButton(
                    key: const Key('close_bin_map_preview'),
                    tooltip: 'Close preview',
                    onPressed: () => setState(() => _selectedBin = null),
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
              Text(
                location,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(color: AppColors.textSecondary),
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                'Accepted: ${bin.acceptedWasteTypes.map((type) => type.displayName).join(', ')}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: AppSpacing.xs),
              Text('Availability: ${bin.publicAvailability.value}'),
              if (bin.distanceMeters != null)
                Text(_BinCard._formatDistance(bin.distanceMeters!)),
              const SizedBox(height: AppSpacing.sm),
              Align(
                alignment: Alignment.centerRight,
                child: AppButton.text(
                  key: const Key('view_bin_details_from_map_preview'),
                  label: 'View Details',
                  icon: Icons.arrow_forward_outlined,
                  onPressed: () => _openBinDetails(bin),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  bool _hasValidCoordinates(PublicWasteBinListItemModel bin) =>
      bin.latitude >= -90 &&
      bin.latitude <= 90 &&
      bin.longitude >= -180 &&
      bin.longitude <= 180;

  void _openBinDetails(PublicWasteBinListItemModel bin) {
    context.push('/citizen/nearby-bins/${bin.id}');
  }

  void _fitMapToBins() {
    if (!_mapReady || !_shouldFitMap || _view != _BinDiscoveryView.map) return;
    final points = _bins
        .where(_hasValidCoordinates)
        .map((bin) => LatLng(bin.latitude, bin.longitude))
        .toList();
    if (_nearbyLocation != null) {
      points.add(LatLng(_nearbyLocation!.latitude, _nearbyLocation!.longitude));
    }
    if (points.isEmpty) return;
    if (points.length == 1) {
      _mapController.move(points.single, 14);
    } else {
      _mapController.fitCamera(
        CameraFit.bounds(
          bounds: LatLngBounds.fromPoints(points),
          padding: const EdgeInsets.all(48),
        ),
      );
    }
    _shouldFitMap = false;
  }

  Widget _buildPaginationFooter() {
    if (_isLoadingMore) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.md),
        child: Center(
          child: SizedBox(
            width: 24,
            height: 24,
            child: CircularProgressIndicator(strokeWidth: 2),
          ),
        ),
      );
    }
    if (_loadMoreErrorMessage != null) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
        child: Column(
          children: [
            Text(
              _loadMoreErrorMessage!,
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.errorText),
            ),
            AppButton.text(
              key: const Key('retry_load_more_bins_button'),
              label: 'Retry',
              onPressed: () =>
                  _fetchBins(page: _currentPage + 1, isLoadMore: true),
            ),
          ],
        ),
      );
    }
    if (_hasMore) {
      return Center(
        child: AppButton.text(
          key: const Key('load_more_bins_button'),
          label: 'Load more',
          icon: Icons.expand_more,
          onPressed: () => _fetchBins(page: _currentPage + 1, isLoadMore: true),
        ),
      );
    }
    return const SizedBox.shrink();
  }

  Widget _buildEmptyState() {
    return Center(
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.xxl),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              _isFiltered ? Icons.search_off_outlined : Icons.delete_outline,
              size: 48,
              color: AppColors.textSecondary,
            ),
            const SizedBox(height: AppSpacing.md),
            Text(
              _isFiltered ? 'No matching bins' : 'No public bins available',
              key: Key(
                _isFiltered ? 'filtered_empty_bins_title' : 'empty_bins_title',
              ),
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              _isFiltered
                  ? 'Try another waste type.'
                  : 'Please check back later for registered public bins.',
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.textSecondary),
            ),
            if (_isFiltered) ...[
              const SizedBox(height: AppSpacing.lg),
              AppButton.text(
                key: const Key('clear_bin_filter_button'),
                label: 'Clear filter',
                onPressed: () => _onWasteTypeChanged(null),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildErrorState() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xxl),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline, size: 48, color: AppColors.error),
            const SizedBox(height: AppSpacing.md),
            const Text(
              'Unable to Load Bins',
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
              key: const Key('retry_load_bins_button'),
              label: 'Retry',
              onPressed: () => _fetchBins(page: 1),
            ),
          ],
        ),
      ),
    );
  }
}

class _BinCard extends StatelessWidget {
  final PublicWasteBinListItemModel bin;
  final VoidCallback onTap;

  const _BinCard({required this.bin, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final availability = _AvailabilityPresentation.from(bin.publicAvailability);
    final location = bin.addressText?.trim().isNotEmpty == true
        ? bin.addressText!.trim()
        : '${bin.latitude.toStringAsFixed(4)}, ${bin.longitude.toStringAsFixed(4)}';
    return Semantics(
      button: true,
      label: 'View details for ${bin.binCode}',
      child: InkWell(
        key: Key('public_bin_card_${bin.id}'),
        onTap: onTap,
        borderRadius: AppSpacing.roundedLg,
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: AppSpacing.roundedLg,
            border: Border.all(color: AppColors.border),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      bin.binCode,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  _AvailabilityBadge(presentation: availability),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              Row(
                children: [
                  const Icon(
                    Icons.place_outlined,
                    size: 16,
                    color: AppColors.textSecondary,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: Text(
                      location,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              Text(
                'Accepted waste types',
                style: Theme.of(context).textTheme.labelMedium
                    ?.copyWith(color: AppColors.textSecondary),
              ),
              const SizedBox(height: AppSpacing.xs),
              Wrap(
                spacing: AppSpacing.xs,
                runSpacing: AppSpacing.xs,
                children: bin.acceptedWasteTypes
                    .map((type) => _WasteTypeChip(label: type.displayName))
                    .toList(),
              ),
              if (bin.distanceMeters != null) ...[
                const SizedBox(height: AppSpacing.sm),
                Row(
                  children: [
                    const Icon(
                      Icons.near_me_outlined,
                      size: 16,
                      color: AppColors.textSecondary,
                    ),
                    const SizedBox(width: AppSpacing.xs),
                    Text(
                      _formatDistance(bin.distanceMeters!),
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  static String _formatDistance(double meters) {
    if (meters >= 1000) return '${(meters / 1000).toStringAsFixed(1)} km away';
    return '${meters.round()} m away';
  }
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
        fontSize: 11,
        fontWeight: FontWeight.w600,
        color: AppColors.primaryDark,
      ),
    ),
  );
}

class _AvailabilityPresentation {
  final String label;
  final IconData icon;
  final Color background;
  final Color foreground;

  const _AvailabilityPresentation(
    this.label,
    this.icon,
    this.background,
    this.foreground,
  );

  factory _AvailabilityPresentation.from(PublicBinAvailability availability) {
    switch (availability) {
      case PublicBinAvailability.usable:
        return const _AvailabilityPresentation(
          'Usable',
          Icons.check_circle_outline,
          AppColors.successLight,
          AppColors.successText,
        );
      case PublicBinAvailability.warning:
        return const _AvailabilityPresentation(
          'Warning',
          Icons.warning_amber_outlined,
          AppColors.warningLight,
          AppColors.warningText,
        );
      case PublicBinAvailability.full:
        return const _AvailabilityPresentation(
          'Full',
          Icons.delete_outline,
          AppColors.errorLight,
          AppColors.errorText,
        );
      case PublicBinAvailability.unavailable:
        return const _AvailabilityPresentation(
          'Unavailable',
          Icons.block_outlined,
          AppColors.surfaceSubtle,
          AppColors.textSecondary,
        );
      case PublicBinAvailability.unknown:
        return const _AvailabilityPresentation(
          'Unknown',
          Icons.help_outline,
          AppColors.infoLight,
          AppColors.infoText,
        );
    }
  }
}

class _AvailabilityBadge extends StatelessWidget {
  final _AvailabilityPresentation presentation;
  const _AvailabilityBadge({required this.presentation});

  @override
  Widget build(BuildContext context) => Semantics(
    label: 'Availability: ${presentation.label}',
    child: Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.xs,
        vertical: AppSpacing.xxs,
      ),
      decoration: BoxDecoration(
        color: presentation.background,
        borderRadius: AppSpacing.roundedFull,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(presentation.icon, size: 14, color: presentation.foreground),
          const SizedBox(width: AppSpacing.xxs),
          Text(
            presentation.label,
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w700,
              color: presentation.foreground,
            ),
          ),
        ],
      ),
    ),
  );
}

enum _BinDiscoveryView { list, map }
