import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_alert.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../auth/providers/auth_provider.dart';
import '../models/assignment_detail_model.dart';
import '../models/assignment_summary_model.dart';
import '../models/collection_assignment_status.dart';
import '../models/driver_availability_status.dart';
import '../providers/driver_dashboard_providers.dart';
import '../providers/driver_history_providers.dart';
import 'widgets/driver_assignment_status_badge.dart';

/// Authenticated Driver Dashboard — operational command center for drivers in the field.
class DriverDashboardScreen extends ConsumerWidget {
  const DriverDashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final authState = ref.watch(authProvider);
    final user = authState.user;

    final firstName = user?.fullName.trim().split(RegExp(r'\s+')).first ?? 'Driver';

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 540),
        child: RefreshIndicator(
          key: const Key('driver_dashboard_refresh_indicator'),
          onRefresh: () async {
            try {
              await Future.wait([
                ref.refresh(driverSelfProvider.future),
                ref.refresh(driverCurrentAssignmentProvider.future),
                ref.refresh(driverRecentAssignmentsProvider.future),
              ]);
            } catch (_) {
              // Silently caught; individual providers expose async error states
            }
          },
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.lg,
              vertical: AppSpacing.md,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // 1. Driver Header & Operational Context
                Text(
                  'Hello, $firstName',
                  key: const Key('driver_greeting_text'),
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                      ),
                ),
                const SizedBox(height: AppSpacing.xxs),
                Text(
                  'Ready for today\'s operations. Stay updated with your collection assignments and route.',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: AppColors.textSecondary,
                      ),
                ),
                const SizedBox(height: AppSpacing.md),

                // Duty & Occupancy Status Card
                const _DriverOperationalStatusCard(),
                const SizedBox(height: AppSpacing.lg),

                // 2. Current Assignment — Primary Dashboard Section
                Text(
                  'Current Assignment',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                      ),
                ),
                const SizedBox(height: AppSpacing.sm),
                _CurrentAssignmentCard(
                  onViewAssignment: () {
                    context.push('/driver/assignment');
                  },
                ),
                const SizedBox(height: AppSpacing.xl),

                // 3. Driver Quick Actions
                Text(
                  'Quick Actions',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                      ),
                ),
                const SizedBox(height: AppSpacing.sm),
                const _DriverQuickActionsGrid(),
                const SizedBox(height: AppSpacing.xl),

                // 4. Today's Work / Task Status Section
                const _TodaysWorkSection(),
                const SizedBox(height: AppSpacing.xl),

                // 5. Recent Operational Updates Section
                Text(
                  'Recent Updates',
                  key: const Key('driver_recent_updates_section'),
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                      ),
                ),
                const SizedBox(height: AppSpacing.sm),
                const _RecentUpdatesSection(),
                const SizedBox(height: AppSpacing.lg),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Compact status card presenting driver-controlled duty availability and read-only assignment occupancy.
class _DriverOperationalStatusCard extends ConsumerWidget {
  const _DriverOperationalStatusCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final driverSelfAsync = ref.watch(driverSelfProvider);
    final availabilityMutation = ref.watch(driverAvailabilityControllerProvider);
    final isMutating = availabilityMutation.isLoading;

    // Listen for availability update errors and surface user-friendly SnackBar
    ref.listen<AsyncValue<void>>(
      driverAvailabilityControllerProvider,
      (previous, next) {
        if (next.hasError && !next.isLoading) {
          final error = next.error;
          final message = error is ApiException
              ? error.message
              : 'Failed to update availability. Please try again.';
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(message),
              backgroundColor: AppColors.error,
              behavior: SnackBarBehavior.floating,
            ),
          );
        }
      },
    );

    return driverSelfAsync.when(
      data: (driverSelf) {
        if (driverSelf == null) return const SizedBox.shrink();

        final isAvailable =
            driverSelf.availabilityStatus == DriverAvailabilityStatus.available;
        final isOccupied = driverSelf.isOccupied;

        return Container(
          key: const Key('driver_operational_status_card'),
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.md,
            vertical: AppSpacing.sm,
          ),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: AppSpacing.roundedMd,
            border: Border.all(color: AppColors.border),
            boxShadow: const [
              BoxShadow(
                color: Color(0x06000000),
                blurRadius: 6,
                offset: Offset(0, 2),
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Wrap(
                alignment: WrapAlignment.spaceBetween,
                crossAxisAlignment: WrapCrossAlignment.center,
                runSpacing: AppSpacing.xs,
                spacing: AppSpacing.xs,
                children: [
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(
                        Icons.badge_outlined,
                        size: 16,
                        color: AppColors.textSecondary,
                      ),
                      const SizedBox(width: AppSpacing.xs),
                      Text(
                        'Duty Status',
                        style: Theme.of(context).textTheme.labelMedium?.copyWith(
                              fontWeight: FontWeight.w600,
                              color: AppColors.textSecondary,
                            ),
                      ),
                    ],
                  ),
                  _OccupancyBadge(isOccupied: isOccupied),
                ],
              ),
              const SizedBox(height: AppSpacing.xs),
              Row(
                children: [
                  Expanded(
                    child: _DutySegmentButton(
                      label: 'Available',
                      icon: Icons.check_circle_outline,
                      isActive: isAvailable,
                      isLoading: isMutating && !isAvailable,
                      activeColor: AppColors.primary,
                      activeBackground: AppColors.primaryLight,
                      key: const Key('driver_availability_available_btn'),
                      onTap: isMutating || isAvailable
                          ? null
                          : () {
                              ref
                                  .read(driverAvailabilityControllerProvider.notifier)
                                  .updateAvailability(DriverAvailabilityStatus.available);
                            },
                    ),
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: _DutySegmentButton(
                      label: 'Off Duty',
                      icon: Icons.pause_circle_outline,
                      isActive: !isAvailable,
                      isLoading: isMutating && isAvailable,
                      activeColor: AppColors.textSecondary,
                      activeBackground: AppColors.surfaceSubtle,
                      key: const Key('driver_availability_off_duty_btn'),
                      onTap: isMutating || !isAvailable
                          ? null
                          : () {
                              ref
                                  .read(driverAvailabilityControllerProvider.notifier)
                                  .updateAvailability(DriverAvailabilityStatus.offDuty);
                            },
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
      loading: () => Container(
        height: 76,
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: AppSpacing.roundedMd,
          border: Border.all(color: AppColors.border),
        ),
        alignment: Alignment.center,
        child: const SizedBox(
          width: 20,
          height: 20,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
      ),
      error: (error, _) => AppAlert.error(
        key: const Key('driver_self_error_alert'),
        message: error is ApiException
            ? error.message
            : 'Unable to load driver status.',
      ),
    );
  }
}

/// Segment button for driver duty toggling with overflow-safe text handling.
class _DutySegmentButton extends StatelessWidget {
  final String label;
  final IconData icon;
  final bool isActive;
  final bool isLoading;
  final Color activeColor;
  final Color activeBackground;
  final VoidCallback? onTap;

  const _DutySegmentButton({
    super.key,
    required this.label,
    required this.icon,
    required this.isActive,
    required this.isLoading,
    required this.activeColor,
    required this.activeBackground,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final borderColor = isActive ? activeColor : AppColors.border;
    final bgColor = isActive ? activeBackground : Colors.transparent;
    final textColor = isActive ? activeColor : AppColors.textSecondary;

    return Material(
      color: bgColor,
      borderRadius: AppSpacing.roundedSm,
      child: InkWell(
        onTap: onTap,
        borderRadius: AppSpacing.roundedSm,
        child: Container(
          height: 38,
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs),
          decoration: BoxDecoration(
            borderRadius: AppSpacing.roundedSm,
            border: Border.all(color: borderColor, width: isActive ? 1.5 : 1.0),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (isLoading)
                SizedBox(
                  width: 14,
                  height: 14,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    valueColor: AlwaysStoppedAnimation<Color>(textColor),
                  ),
                )
              else
                Icon(
                  icon,
                  size: 16,
                  color: textColor,
                ),
              const SizedBox(width: AppSpacing.xs),
              Flexible(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: isActive ? FontWeight.bold : FontWeight.w500,
                    color: textColor,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Read-only chip representing backend-derived driver assignment occupancy.
class _OccupancyBadge extends StatelessWidget {
  final bool isOccupied;

  const _OccupancyBadge({required this.isOccupied});

  @override
  Widget build(BuildContext context) {
    final bgColor = isOccupied ? AppColors.warningLight : AppColors.surfaceSubtle;
    final textColor = isOccupied ? AppColors.warningText : AppColors.textSecondary;
    final borderColor = isOccupied ? AppColors.warningBorder : AppColors.border;
    final label = isOccupied ? 'Occupied' : 'Unassigned';
    final dotColor = isOccupied ? AppColors.warning : AppColors.textMuted;

    return Container(
      key: const Key('driver_occupancy_badge'),
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs + 4, vertical: 3),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: AppSpacing.roundedFull,
        border: Border.all(color: borderColor, width: 1),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 6,
            height: 6,
            decoration: BoxDecoration(
              color: dotColor,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: AppSpacing.xs),
          Flexible(
            child: Text(
              'Assignment: $label',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: textColor,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Primary action card representing the Driver's current operational assignment.
class _CurrentAssignmentCard extends ConsumerWidget {
  final VoidCallback onViewAssignment;

  const _CurrentAssignmentCard({required this.onViewAssignment});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final assignmentAsync = ref.watch(driverCurrentAssignmentProvider);

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppSpacing.roundedMd,
        border: Border.all(color: AppColors.primaryBorder, width: 1.5),
        boxShadow: const [
          BoxShadow(
            color: Color(0x0A064E3B),
            blurRadius: 10,
            offset: Offset(0, 4),
          ),
        ],
      ),
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: assignmentAsync.when(
        data: (assignment) {
          if (assignment == null) {
            return _buildNoActiveAssignment(context);
          }
          if (assignment.status == CollectionAssignmentStatus.inProgress) {
            return _buildInProgressAssignment(context, assignment);
          }
          return _buildAssignedAssignment(context, assignment);
        },
        loading: () => const Center(
          child: Padding(
            padding: EdgeInsets.symmetric(vertical: AppSpacing.md),
            child: SizedBox(
              width: 24,
              height: 24,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
          ),
        ),
        error: (error, _) => Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            AppAlert.error(
              message: error is ApiException
                  ? error.message
                  : 'Unable to load current assignment.',
            ),
            const SizedBox(height: AppSpacing.sm),
            Align(
              alignment: Alignment.centerRight,
              child: AppButton.text(
                label: 'Retry',
                onPressed: () => ref.invalidate(driverCurrentAssignmentProvider),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildNoActiveAssignment(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 44,
              height: 44,
              decoration: const BoxDecoration(
                color: AppColors.primaryLight,
                borderRadius: AppSpacing.roundedSm,
              ),
              child: const Icon(
                Icons.assignment_outlined,
                color: AppColors.primaryDark,
                size: 24,
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'No active assignment',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.bold,
                          color: AppColors.textPrimary,
                        ),
                  ),
                  const SizedBox(height: AppSpacing.xxs),
                  Text(
                    'No active collection assignment is available right now.',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                  ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.lg),
        const AppButton.primary(
          key: Key('driver_view_assignment_button'),
          label: 'View Assignment',
          icon: Icons.arrow_forward,
          onPressed: null, // Disabled when no assignment exists
        ),
      ],
    );
  }

  Widget _buildAssignedAssignment(
      BuildContext context, AssignmentDetailModel assignment) {
    final vehicleLabel = assignment.vehicleRegistrationNumber.isNotEmpty
        ? assignment.vehicleRegistrationNumber
        : 'Vehicle Assigned';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 44,
              height: 44,
              decoration: const BoxDecoration(
                color: AppColors.infoLight,
                borderRadius: AppSpacing.roundedSm,
              ),
              child: const Icon(
                Icons.assignment_turned_in_outlined,
                color: AppColors.info,
                size: 24,
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Flexible(
                        child: Text(
                          vehicleLabel,
                          key: const Key('driver_assignment_vehicle_text'),
                          style:
                              Theme.of(context).textTheme.titleMedium?.copyWith(
                                    fontWeight: FontWeight.bold,
                                    color: AppColors.textPrimary,
                                  ),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      const SizedBox(width: AppSpacing.xs),
                      _AssignmentStatusBadge(status: assignment.status),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.xxs),
                  Text(
                    '${assignment.stopCount} ${assignment.stopCount == 1 ? 'stop' : 'stops'} scheduled · Ready to start',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                  ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.lg),
        AppButton.primary(
          key: const Key('driver_view_assignment_button'),
          label: 'View Assignment',
          icon: Icons.arrow_forward,
          onPressed: onViewAssignment,
        ),
      ],
    );
  }

  Widget _buildInProgressAssignment(
      BuildContext context, AssignmentDetailModel assignment) {
    final vehicleLabel = assignment.vehicleRegistrationNumber.isNotEmpty
        ? assignment.vehicleRegistrationNumber
        : 'Vehicle Assigned';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 44,
              height: 44,
              decoration: const BoxDecoration(
                color: AppColors.primaryLight,
                borderRadius: AppSpacing.roundedSm,
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
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Flexible(
                        child: Text(
                          vehicleLabel,
                          key: const Key('driver_assignment_vehicle_text'),
                          style:
                              Theme.of(context).textTheme.titleMedium?.copyWith(
                                    fontWeight: FontWeight.bold,
                                    color: AppColors.textPrimary,
                                  ),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      const SizedBox(width: AppSpacing.xs),
                      _AssignmentStatusBadge(status: assignment.status),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.xxs),
                  Text(
                    '${assignment.completedStopCount} of ${assignment.stopCount} stops completed · In progress',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                  ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.lg),
        AppButton.primary(
          key: const Key('driver_view_assignment_button'),
          label: 'Continue Assignment',
          icon: Icons.arrow_forward,
          onPressed: onViewAssignment,
        ),
      ],
    );
  }
}

/// Compact badge displaying the collection assignment lifecycle status.
class _AssignmentStatusBadge extends StatelessWidget {
  final CollectionAssignmentStatus status;

  const _AssignmentStatusBadge({required this.status});

  @override
  Widget build(BuildContext context) {
    final isInProgress = status == CollectionAssignmentStatus.inProgress;
    final bgColor = isInProgress ? AppColors.primaryLight : AppColors.infoLight;
    final textColor = isInProgress ? AppColors.primaryDark : AppColors.infoText;
    final borderColor =
        isInProgress ? AppColors.primaryBorder : AppColors.infoBorder;

    return Container(
      key: const Key('driver_assignment_status_badge'),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.xs + 2,
        vertical: 2,
      ),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: AppSpacing.roundedFull,
        border: Border.all(color: borderColor),
      ),
      child: Text(
        status.displayName,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: textColor,
        ),
      ),
    );
  }
}

/// Quick action data model for Driver operational shortcuts.
class _DriverQuickActionItem {
  final String label;
  final String description;
  final IconData icon;
  final String route;
  final Key key;
  final bool isShellTab;

  const _DriverQuickActionItem({
    required this.label,
    required this.description,
    required this.icon,
    required this.route,
    required this.key,
    this.isShellTab = false,
  });
}

/// Responsive grid rendering the 4 primary Driver quick actions.
class _DriverQuickActionsGrid extends StatelessWidget {
  const _DriverQuickActionsGrid();

  static const List<_DriverQuickActionItem> _items = [
    _DriverQuickActionItem(
      label: 'Collection Tasks',
      description: 'View your assigned collection work.',
      icon: Icons.checklist,
      route: '/driver/tasks',
      key: Key('driver_quick_action_tasks'),
      isShellTab: true,
    ),
    _DriverQuickActionItem(
      label: 'Route',
      description: 'View your collection route and stops.',
      icon: Icons.alt_route,
      route: '/driver/route',
      key: Key('driver_quick_action_route'),
      isShellTab: true,
    ),
    _DriverQuickActionItem(
      label: 'Report Incident',
      description: 'Report a problem during collection.',
      icon: Icons.warning_amber_rounded,
      route: '/driver/incidents',
      key: Key('driver_quick_action_incidents'),
      isShellTab: false,
    ),
    _DriverQuickActionItem(
      label: 'Profile',
      description: 'View your account and profile actions.',
      icon: Icons.person_outline,
      route: '/driver/profile',
      key: Key('driver_quick_action_profile'),
      isShellTab: true,
    ),
  ];

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final isCompact = constraints.maxWidth < 340;

        if (isCompact) {
          return Column(
            children: _items.map((item) {
              return Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                child: _QuickActionCard(item: item),
              );
            }).toList(),
          );
        }

        // 2-column grid layout
        return Column(
          children: [
            Row(
              children: [
                Expanded(child: _QuickActionCard(item: _items[0])),
                const SizedBox(width: AppSpacing.sm),
                Expanded(child: _QuickActionCard(item: _items[1])),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            Row(
              children: [
                Expanded(child: _QuickActionCard(item: _items[2])),
                const SizedBox(width: AppSpacing.sm),
                Expanded(child: _QuickActionCard(item: _items[3])),
              ],
            ),
          ],
        );
      },
    );
  }
}

/// Individual touch card for Driver quick actions.
class _QuickActionCard extends StatelessWidget {
  final _DriverQuickActionItem item;

  const _QuickActionCard({required this.item});

  @override
  Widget build(BuildContext context) {
    return AppCard(
      key: item.key,
      onTap: () {
        if (item.isShellTab) {
          context.go(item.route);
        } else {
          context.push(item.route);
        }
      },
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 38,
            height: 38,
            decoration: const BoxDecoration(
              color: AppColors.primaryLight,
              shape: BoxShape.circle,
            ),
            child: Icon(
              item.icon,
              color: AppColors.primary,
              size: 20,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            item.label,
            style: const TextStyle(
              fontWeight: FontWeight.bold,
              fontSize: 14,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            item.description,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
              height: 1.3,
            ),
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }
}

/// Section rendering the Today's Work summary based on the active assignment.
class _TodaysWorkSection extends ConsumerWidget {
  const _TodaysWorkSection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final assignmentAsync = ref.watch(driverCurrentAssignmentProvider);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Today\'s Work',
          key: const Key('driver_todays_work_section'),
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
        ),
        const SizedBox(height: AppSpacing.sm),
        assignmentAsync.when(
          data: (assignment) {
            if (assignment == null) {
              return const _TodaysWorkEmptyState();
            }
            return _TodaysWorkActiveCard(assignment: assignment);
          },
          loading: () => const AppCard(
            padding: EdgeInsets.all(AppSpacing.md),
            child: Center(
              child: SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
          ),
          error: (_, _) => const _TodaysWorkEmptyState(),
        ),
      ],
    );
  }
}

/// Active progress card displaying stop progress and completion counters.
class _TodaysWorkActiveCard extends StatelessWidget {
  final AssignmentDetailModel assignment;

  const _TodaysWorkActiveCard({required this.assignment});

  @override
  Widget build(BuildContext context) {
    final progress = DriverStopProgress.fromAssignment(assignment);
    final stopLabel =
        '${progress.totalStops} ${progress.totalStops == 1 ? 'Stop' : 'Stops'}';

    final breakdownParts = <String>[
      '${progress.completedStops} Completed',
      '${progress.failedStops} Failed',
      '${progress.pendingStops} Pending',
      if (progress.skippedStops > 0) '${progress.skippedStops} Skipped',
    ];
    final breakdownText = breakdownParts.join(' · ');

    return AppCard(
      key: const Key('driver_todays_work_active_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: const BoxDecoration(
                  color: AppColors.primaryLight,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.event_note_outlined,
                  color: AppColors.primary,
                  size: 20,
                ),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Flexible(
                          child: Text(
                            stopLabel,
                            key: const Key('driver_todays_work_stop_count'),
                            style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 14,
                              color: AppColors.textPrimary,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: AppSpacing.xs),
                        _AssignmentStatusBadge(status: assignment.status),
                      ],
                    ),
                    const SizedBox(height: 2),
                    Text(
                      breakdownText,
                      key: const Key('driver_todays_work_breakdown'),
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12,
                        height: 1.3,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          ClipRRect(
            borderRadius: AppSpacing.roundedFull,
            child: LinearProgressIndicator(
              key: const Key('driver_todays_work_progress_bar'),
              value: progress.progressFraction.clamp(0.0, 1.0),
              minHeight: 6,
              backgroundColor: AppColors.surfaceSubtle,
              valueColor:
                  const AlwaysStoppedAnimation<Color>(AppColors.primary),
            ),
          ),
        ],
      ),
    );
  }
}

/// Compact, polished empty state for Today's Work / Task Status.
class _TodaysWorkEmptyState extends StatelessWidget {
  const _TodaysWorkEmptyState();

  @override
  Widget build(BuildContext context) {
    return AppCard(
      key: const Key('driver_todays_work_empty_state'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: const BoxDecoration(
              color: AppColors.primaryLight,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.event_note_outlined,
              color: AppColors.primary,
              size: 20,
            ),
          ),
          const SizedBox(width: AppSpacing.md),
          const Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'No Tasks In Progress',
                  style: TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 14,
                    color: AppColors.textPrimary,
                  ),
                ),
                SizedBox(height: 2),
                Text(
                  'Your assigned collection tasks and progress will appear here.',
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                    height: 1.3,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// A deliberately isolated terminal-results query. Its failure must never
/// interfere with the Driver's current-assignment dashboard state.
class _RecentUpdatesSection extends ConsumerWidget {
  const _RecentUpdatesSection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final recentAsync = ref.watch(driverRecentAssignmentsProvider);

    return recentAsync.when(
      loading: () => const AppCard(
        padding: EdgeInsets.all(AppSpacing.md),
        child: Row(
          children: [
            SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
            SizedBox(width: AppSpacing.sm),
            Expanded(
              child: Text(
                'Loading recent work…',
                style: TextStyle(color: AppColors.textSecondary),
              ),
            ),
          ],
        ),
      ),
      error: (_, _) => AppCard(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Row(
          children: [
            const Expanded(
              child: Text(
                'Recent completed work could not be loaded.',
                style: TextStyle(color: AppColors.textSecondary),
              ),
            ),
            AppButton.text(
              key: const Key('driver_recent_updates_retry'),
              label: 'Retry',
              onPressed: () => ref.invalidate(driverRecentAssignmentsProvider),
            ),
          ],
        ),
      ),
      data: (assignments) => _RecentUpdatesContent(assignments: assignments),
    );
  }
}

class _RecentUpdatesContent extends StatelessWidget {
  final List<AssignmentSummaryModel> assignments;

  const _RecentUpdatesContent({required this.assignments});

  @override
  Widget build(BuildContext context) {
    return AppCard(
      key: const Key('driver_recent_updates_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (assignments.isEmpty)
            const _RecentUpdatesEmptyState()
          else
            for (final assignment in assignments) ...[
              _RecentAssignmentRow(assignment: assignment),
              if (assignment != assignments.last)
                const Divider(height: AppSpacing.lg, color: AppColors.divider),
            ],
          const SizedBox(height: AppSpacing.sm),
          Align(
            alignment: Alignment.centerRight,
            child: AppButton.text(
              key: const Key('driver_view_history_button'),
              label: 'View History',
              icon: Icons.history_outlined,
              onPressed: () => context.push('/driver/history'),
            ),
          ),
        ],
      ),
    );
  }
}

class _RecentAssignmentRow extends StatelessWidget {
  final AssignmentSummaryModel assignment;

  const _RecentAssignmentRow({required this.assignment});

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.assignment_turned_in_outlined, color: AppColors.primary),
        const SizedBox(width: AppSpacing.sm),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                assignment.vehicleRegistrationNumber.isEmpty
                    ? 'Collection assignment'
                    : assignment.vehicleRegistrationNumber,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                '${assignment.completedStopCount} completed • ${assignment.failedStopCount} failed',
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 12,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: AppSpacing.xs),
        DriverAssignmentStatusBadge(status: assignment.status),
      ],
    );
  }
}

class _RecentUpdatesEmptyState extends StatelessWidget {
  const _RecentUpdatesEmptyState();

  @override
  Widget build(BuildContext context) {
    return const Row(
      children: [
        Icon(Icons.history_toggle_off, color: AppColors.primary, size: 20),
        SizedBox(width: AppSpacing.sm),
        Expanded(
          child: Text(
            'No recent completed work.',
            key: Key('driver_recent_updates_empty'),
            style: TextStyle(color: AppColors.textSecondary),
          ),
        ),
      ],
    );
  }
}
