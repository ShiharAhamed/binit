import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_alert.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../../bins/services/external_directions_launcher.dart';
import '../models/assignment_detail_model.dart';
import '../models/collection_assignment_status.dart';
import '../models/route_read_model.dart';
import '../models/route_stop_model.dart';
import '../providers/driver_dashboard_providers.dart';
import 'widgets/driver_stop_card.dart';

/// Screen displaying the Driver's assigned collection route on OpenStreetMap
/// with ordered stop markers, sequence polyline, and external navigation actions.
class DriverRouteScreen extends ConsumerStatefulWidget {
  final ExternalDirectionsLauncher? directionsLauncher;
  final TileProvider? tileProvider;

  const DriverRouteScreen({
    super.key,
    this.directionsLauncher,
    this.tileProvider,
  });

  @override
  ConsumerState<DriverRouteScreen> createState() => _DriverRouteScreenState();
}

class _DriverRouteScreenState extends ConsumerState<DriverRouteScreen> {
  late final ExternalDirectionsLauncher _directionsLauncher;
  final MapController _mapController = MapController();
  RouteStopModel? _selectedStop;
  bool _mapReady = false;

  @override
  void initState() {
    super.initState();
    _directionsLauncher =
        widget.directionsLauncher ?? const UrlLauncherExternalDirectionsLauncher();
  }

  @override
  void dispose() {
    _mapController.dispose();
    super.dispose();
  }

  Future<void> _openDirections(RouteStopModel stop) async {
    if (!stop.hasValidCoordinates) return;
    final wasOpened = await _directionsLauncher.openDirections(
      latitude: stop.task.latitude!,
      longitude: stop.task.longitude!,
    );
    if (!wasOpened && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Unable to open external maps. Please check your map apps.',
          ),
        ),
      );
    }
  }

  void _fitCamera(List<LatLng> points) {
    if (!_mapReady || points.isEmpty) return;
    if (points.length == 1) {
      _mapController.move(points.first, 14.0);
    } else {
      _mapController.fitCamera(
        CameraFit.bounds(
          bounds: LatLngBounds.fromPoints(points),
          padding: const EdgeInsets.all(40),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final assignmentAsync = ref.watch(driverCurrentAssignmentProvider);

    return RefreshIndicator(
      key: const Key('driver_route_refresh_indicator'),
      onRefresh: () async {
        try {
          ref.invalidate(driverCurrentAssignmentProvider);
          await ref.read(driverCurrentAssignmentProvider.future);
        } catch (_) {
          // Errors are surfaced via AsyncValue.error
        }
      },
      child: Align(
        alignment: Alignment.topCenter,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 540),
          child: assignmentAsync.when(
            data: (assignment) {
              if (assignment == null ||
                  assignment.route == null ||
                  assignment.route!.stops.isEmpty) {
                return _buildEmptyState(context);
              }
              return _buildRouteContent(context, assignment, assignment.route!);
            },
            loading: () => const SingleChildScrollView(
              physics: AlwaysScrollableScrollPhysics(),
              child: SizedBox(
                height: 300,
                child: AppLoadingIndicator(
                  message: 'Loading collection route...',
                ),
              ),
            ),
            error: (error, _) => _buildErrorState(context, error),
          ),
        ),
      ),
    );
  }

  Widget _buildEmptyState(BuildContext context) {
    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.xl,
        vertical: AppSpacing.xxl,
      ),
      child: AppCard(
        key: const Key('driver_route_empty_state'),
        padding: const EdgeInsets.all(AppSpacing.xxl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 72,
              height: 72,
              decoration: const BoxDecoration(
                color: AppColors.primaryLight,
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.alt_route,
                size: 36,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            Text(
              'No active route assigned',
              key: const Key('driver_route_empty_title'),
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: AppColors.textPrimary,
                  ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: AppSpacing.sm),
            const Text(
              'You do not have an active collection route at this time. When an assignment is dispatched, your route sequence and stop map will appear here.',
              key: Key('driver_route_empty_description'),
              style: TextStyle(
                fontSize: 14,
                color: AppColors.textSecondary,
                height: 1.4,
              ),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildErrorState(BuildContext context, Object error) {
    final errorMessage = error is ApiException
        ? error.message
        : 'Failed to load route. Please verify your connection.';

    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: AppSpacing.xl,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          AppAlert.error(
            key: const Key('driver_route_error_alert'),
            message: errorMessage,
          ),
          const SizedBox(height: AppSpacing.md),
          AppButton.outlined(
            key: const Key('driver_route_retry_button'),
            label: 'Retry',
            icon: Icons.refresh,
            onPressed: () {
              ref.invalidate(driverCurrentAssignmentProvider);
            },
          ),
        ],
      ),
    );
  }

  Widget _buildRouteContent(
    BuildContext context,
    AssignmentDetailModel assignment,
    RouteReadModel route,
  ) {
    final orderedStops = route.orderedStops;
    final mappableStops = route.mappableStops;

    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: AppSpacing.lg,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // 1. Operational Context Header
          _buildRouteHeader(context, assignment, route),
          const SizedBox(height: AppSpacing.md),

          // 2. OpenStreetMap Section
          _buildMapSection(context, mappableStops),
          const SizedBox(height: AppSpacing.xs),

          // 3. Mandatory Sequence Disclaimer
          _buildSequenceDisclaimer(),
          const SizedBox(height: AppSpacing.lg),

          // 4. Ordered Stop List Section
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Text(
                  'Stop Sequence (${orderedStops.length})',
                  key: const Key('driver_route_stops_heading'),
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                      ),
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              Text(
                '${mappableStops.length} mapped',
                key: const Key('driver_route_stops_count_text'),
                style: const TextStyle(
                  fontSize: 12,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),

          // Stop Cards in Sequence Order
          ...orderedStops.map((stop) {
            final isSelected = _selectedStop?.id == stop.id;
            return Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.sm),
              child: DriverStopCard(
                key: Key('driver_stop_card_${stop.sequence}'),
                stop: stop,
                isSelected: isSelected,
                showOpenInMaps: true,
                onTap: () {
                  setState(() {
                    _selectedStop = stop;
                  });
                  if (stop.hasValidCoordinates && _mapReady) {
                    _mapController.move(
                      LatLng(stop.task.latitude!, stop.task.longitude!),
                      15.0,
                    );
                  }
                },
                onOpenInMaps: () => _openDirections(stop),
              ),
            );
          }),
        ],
      ),
    );
  }

  Widget _buildRouteHeader(
    BuildContext context,
    AssignmentDetailModel assignment,
    RouteReadModel route,
  ) {
    final isInProgress =
        assignment.status == CollectionAssignmentStatus.inProgress;
    final statusColor =
        isInProgress ? AppColors.primaryDark : AppColors.infoText;
    final statusBg =
        isInProgress ? AppColors.primaryLight : AppColors.infoLight;
    final statusBorder =
        isInProgress ? AppColors.primaryBorder : AppColors.infoBorder;

    final vehicleText = assignment.vehicleRegistrationNumber.isNotEmpty
        ? assignment.vehicleRegistrationNumber
        : 'Unassigned Vehicle';

    final totalStops = route.orderedStops.length;
    final mappedStops = route.mappableStops.length;

    return AppCard(
      key: const Key('driver_route_header_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: const BoxDecoration(
              color: AppColors.primaryLight,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.local_shipping_outlined,
              color: AppColors.primaryDark,
              size: 24,
            ),
          ),
          const SizedBox(width: AppSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Flexible(
                      child: Text(
                        vehicleText,
                        key: const Key('driver_route_vehicle_plate'),
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                          color: AppColors.textPrimary,
                        ),
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.xs),
                    Container(
                      key: const Key('driver_route_status_chip'),
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppSpacing.xs + 2,
                        vertical: 2,
                      ),
                      decoration: BoxDecoration(
                        color: statusBg,
                        borderRadius: AppSpacing.roundedFull,
                        border: Border.all(color: statusBorder),
                      ),
                      child: Text(
                        assignment.status.displayName,
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          color: statusColor,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 2),
                Text(
                  '$totalStops stops · $mappedStops mapped',
                  key: const Key('driver_route_stops_summary'),
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMapSection(BuildContext context, List<RouteStopModel> mappableStops) {
    if (mappableStops.isEmpty) {
      return Container(
        key: const Key('driver_route_no_mappable_stops'),
        width: double.infinity,
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.xl,
        ),
        decoration: BoxDecoration(
          color: AppColors.surfaceSubtle,
          borderRadius: AppSpacing.roundedMd,
          border: Border.all(color: AppColors.border),
        ),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              width: 52,
              height: 52,
              decoration: const BoxDecoration(
                color: AppColors.warningLight,
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.map_outlined,
                size: 28,
                color: AppColors.warningText,
              ),
            ),
            const SizedBox(height: AppSpacing.sm),
            const Text(
              'No mapped stop locations available',
              key: Key('driver_route_no_coords_title'),
              style: TextStyle(
                fontWeight: FontWeight.bold,
                fontSize: 15,
                color: AppColors.textPrimary,
              ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: AppSpacing.xs),
            const Text(
              'Stop locations for this route do not have valid map coordinates to display.',
              style: TextStyle(
                fontSize: 12,
                color: AppColors.textSecondary,
                height: 1.3,
              ),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      );
    }

    final points = mappableStops
        .map((s) => LatLng(s.task.latitude!, s.task.longitude!))
        .toList(growable: false);

    return Container(
      key: const Key('driver_route_map_container'),
      height: 280,
      decoration: BoxDecoration(
        borderRadius: AppSpacing.roundedMd,
        border: Border.all(color: AppColors.border),
      ),
      child: ClipRRect(
        borderRadius: AppSpacing.roundedMd,
        child: FlutterMap(
          mapController: _mapController,
          options: MapOptions(
            initialCenter: points.first,
            initialZoom: points.length == 1 ? 14.0 : 12.0,
            minZoom: 3,
            maxZoom: 19,
            onMapReady: () {
              _mapReady = true;
              _fitCamera(points);
            },
          ),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.smartwaste.mobile',
              tileProvider: widget.tileProvider,
            ),
            if (points.length >= 2)
              PolylineLayer(
                polylines: [
                  Polyline(
                    points: points,
                    color: AppColors.primary,
                    strokeWidth: 3.5,
                  ),
                ],
              ),
            MarkerLayer(
              markers: mappableStops.map((stop) {
                final isSelected = _selectedStop?.id == stop.id;
                return Marker(
                  key: Key('driver_route_marker_${stop.sequence}'),
                  point: LatLng(stop.task.latitude!, stop.task.longitude!),
                  width: 38,
                  height: 38,
                  child: GestureDetector(
                    onTap: () {
                      setState(() {
                        _selectedStop = stop;
                      });
                    },
                    child: Container(
                      decoration: BoxDecoration(
                        color:
                            isSelected ? AppColors.primaryDark : AppColors.primary,
                        shape: BoxShape.circle,
                        border: Border.all(
                          color: Colors.white,
                          width: isSelected ? 2.5 : 2.0,
                        ),
                        boxShadow: const [
                          BoxShadow(
                            color: Colors.black26,
                            blurRadius: 4,
                            offset: Offset(0, 2),
                          ),
                        ],
                      ),
                      alignment: Alignment.center,
                      child: Text(
                        '#${stop.sequence}',
                        key: Key('driver_route_marker_text_${stop.sequence}'),
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.bold,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ),
                );
              }).toList(growable: false),
            ),
            const _MapAttribution(),
          ],
        ),
      ),
    );
  }

  Widget _buildSequenceDisclaimer() {
    return Container(
      key: const Key('driver_route_sequence_disclaimer'),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: AppSpacing.sm,
      ),
      decoration: BoxDecoration(
        color: AppColors.surfaceSubtle,
        borderRadius: AppSpacing.roundedSm,
        border: Border.all(color: AppColors.border),
      ),
      child: const Row(
        children: [
          Icon(
            Icons.info_outline,
            size: 16,
            color: AppColors.textSecondary,
          ),
          SizedBox(width: AppSpacing.xs),
          Expanded(
            child: Text(
              'Stop sequence — not driving directions.',
              key: Key('driver_route_disclaimer_text'),
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w500,
                color: AppColors.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
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
            '© OpenStreetMap contributors',
            style: TextStyle(
              fontSize: 9,
              color: AppColors.textSecondary,
            ),
          ),
        ),
      );
}
