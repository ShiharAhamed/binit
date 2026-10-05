import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_alert.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../models/assignment_detail_model.dart';
import '../models/collection_assignment_status.dart';
import '../models/route_stop_model.dart';
import '../providers/driver_dashboard_providers.dart';
import '../providers/driver_execution_controller.dart';
import 'widgets/assignment_progress_card.dart';
import 'widgets/driver_stop_card.dart';

/// Screen presenting the full operational overview and ordered route stops
/// for the Driver's current collection assignment.
class DriverAssignmentScreen extends ConsumerWidget {
  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');

  const DriverAssignmentScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final assignmentAsync = ref.watch(driverCurrentAssignmentProvider);

    return Scaffold(
      backgroundColor: AppColors.dashboardBackground,
      appBar: AppBar(
        backgroundColor: AppColors.dashboardBackground,
        surfaceTintColor: Colors.transparent,
        title: const Text(
          'My Assignment',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
        ),
        leading: BackButton(
          key: const Key('driver_assignment_appbar_back_button'),
          onPressed: () {
            if (context.canPop()) {
              context.pop();
            } else {
              context.go('/driver/dashboard');
            }
          },
        ),
      ),
      body: SafeArea(
        child: RefreshIndicator(
          key: const Key('driver_assignment_refresh_indicator'),
          onRefresh: () async {
            try {
              ref.invalidate(driverCurrentAssignmentProvider);
              await ref.read(driverCurrentAssignmentProvider.future);
            } catch (_) {
              // Surfaced through AsyncValue.error
            }
          },
          child: Align(
            alignment: Alignment.topCenter,
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 540),
              child: assignmentAsync.when(
                data: (assignment) {
                  if (assignment == null) {
                    return _buildEmptyState(context);
                  }
                  return _buildAssignmentContent(context, ref, assignment);
                },
                loading: () => const SingleChildScrollView(
                  physics: AlwaysScrollableScrollPhysics(),
                  child: SizedBox(
                    height: 300,
                    child: AppLoadingIndicator(
                      message: 'Loading assignment details...',
                    ),
                  ),
                ),
                error: (error, _) => _buildErrorState(context, ref, error),
              ),
            ),
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
        key: const Key('driver_assignment_empty_state'),
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
                Icons.assignment_outlined,
                size: 36,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            Text(
              'No Active Assignment',
              key: const Key('driver_assignment_empty_title'),
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(
              'You do not have an active collection assignment at this time.',
              key: const Key('driver_assignment_empty_description'),
              style: Theme.of(context).textTheme.bodyMedium
                  ?.copyWith(color: AppColors.textSecondary, height: 1.5),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: AppSpacing.xl),
            AppButton.outlined(
              key: const Key('driver_assignment_back_to_dashboard_button'),
              label: 'Back to Dashboard',
              icon: Icons.arrow_back,
              onPressed: () => context.go('/driver/dashboard'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildErrorState(BuildContext context, WidgetRef ref, Object error) {
    final message = error is ApiException
        ? error.message
        : 'Unable to load assignment details. Please try again.';

    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.xl,
        vertical: AppSpacing.xxl,
      ),
      child: Column(
        children: [
          AppAlert.error(
            key: const Key('driver_assignment_error_alert'),
            message: message,
          ),
          const SizedBox(height: AppSpacing.md),
          AppButton.outlined(
            key: const Key('driver_assignment_retry_button'),
            label: 'Retry',
            icon: Icons.refresh,
            onPressed: () => ref.invalidate(driverCurrentAssignmentProvider),
          ),
        ],
      ),
    );
  }

  Widget _buildAssignmentContent(
    BuildContext context,
    WidgetRef ref,
    AssignmentDetailModel assignment,
  ) {
    final isInProgress =
        assignment.status == CollectionAssignmentStatus.inProgress;
    final statusColor = isInProgress
        ? AppColors.primaryDark
        : AppColors.infoText;
    final statusBg = isInProgress
        ? AppColors.primaryLight
        : AppColors.infoLight;
    final statusBorder = isInProgress
        ? AppColors.primaryBorder
        : AppColors.infoBorder;

    // Started timestamp if InProgress
    DateTime? startedAt;
    if (isInProgress && assignment.history.isNotEmpty) {
      for (final h in assignment.history) {
        if (h.toStatus == CollectionAssignmentStatus.inProgress) {
          startedAt = h.changedAt;
          break;
        }
      }
    }

    // Sequence ordered stops
    final List<RouteStopModel> stops = assignment.route?.orderedStops ?? [];

    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: AppSpacing.md,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // 1. Assignment Overview Card
          AppCard(
            key: const Key('driver_assignment_overview_card'),
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Vehicle Plate and Status
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: statusBg,
                        borderRadius: AppSpacing.roundedSm,
                      ),
                      child: Icon(
                        isInProgress
                            ? Icons.local_shipping_outlined
                            : Icons.assignment_outlined,
                        color: statusColor,
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
                                  assignment
                                          .vehicleRegistrationNumber
                                          .isNotEmpty
                                      ? assignment.vehicleRegistrationNumber
                                      : 'Assigned Vehicle',
                                  key: const Key(
                                    'driver_assignment_vehicle_title',
                                  ),
                                  style: const TextStyle(
                                    fontSize: 18,
                                    fontWeight: FontWeight.bold,
                                    color: AppColors.textPrimary,
                                  ),
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                              const SizedBox(width: AppSpacing.xs),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: AppSpacing.xs + 2,
                                  vertical: 3,
                                ),
                                decoration: BoxDecoration(
                                  color: statusBg,
                                  borderRadius: AppSpacing.roundedFull,
                                  border: Border.all(color: statusBorder),
                                ),
                                child: Text(
                                  assignment.status.displayName,
                                  key: const Key(
                                    'driver_assignment_status_chip',
                                  ),
                                  style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.w600,
                                    color: statusColor,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ],
                ),

                const SizedBox(height: AppSpacing.md),
                const Divider(color: AppColors.divider, height: 1),
                const SizedBox(height: AppSpacing.md),

                // Assignment Metadata Grid
                if (assignment.driverName.isNotEmpty) ...[
                  _buildMetaRow(
                    icon: Icons.person_outline,
                    label: 'Driver',
                    value: assignment.driverName,
                    key: const Key('driver_assignment_driver_name'),
                  ),
                  const SizedBox(height: AppSpacing.xs),
                ],

                _buildMetaRow(
                  icon: Icons.calendar_today_outlined,
                  label: 'Assigned',
                  value: _dateFormat.format(assignment.assignedAt.toLocal()),
                  key: const Key('driver_assignment_assigned_at'),
                ),

                if (startedAt != null) ...[
                  const SizedBox(height: AppSpacing.xs),
                  _buildMetaRow(
                    icon: Icons.play_circle_outline,
                    label: 'Started',
                    value: _dateFormat.format(startedAt.toLocal()),
                    key: const Key('driver_assignment_started_at'),
                  ),
                ],

                if (assignment.route?.routingMethod != null) ...[
                  const SizedBox(height: AppSpacing.xs),
                  _buildMetaRow(
                    icon: Icons.alt_route,
                    label: 'Routing',
                    value: assignment.route!.routingMethod,
                    key: const Key('driver_assignment_routing_method'),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.md),

          // 2. Progress Summary Card
          AssignmentProgressCard(assignment: assignment),
          const SizedBox(height: AppSpacing.md),

          _buildExecutionPanel(context, ref, assignment),
          const SizedBox(height: AppSpacing.lg),

          // 3. Ordered Stops Section
          Text(
            'Route Stops (${stops.length})',
            key: const Key('driver_assignment_stops_header'),
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.xxs),
          const Text(
            'Ordered strictly by route sequence.',
            style: TextStyle(fontSize: 12, color: AppColors.textSecondary),
          ),
          const SizedBox(height: AppSpacing.sm),

          if (stops.isEmpty) ...[
            const AppCard(
              padding: EdgeInsets.all(AppSpacing.xl),
              child: Center(
                child: Text(
                  'No collection stops on this route.',
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 14,
                  ),
                ),
              ),
            ),
          ] else ...[
            for (int i = 0; i < stops.length; i++) ...[
              DriverStopCard(stop: stops[i]),
              if (i < stops.length - 1) const SizedBox(height: AppSpacing.sm),
            ],
          ],

          const SizedBox(height: AppSpacing.xl),
        ],
      ),
    );
  }

  Widget _buildExecutionPanel(
    BuildContext context,
    WidgetRef ref,
    AssignmentDetailModel assignment,
  ) {
    final execution = ref.watch(driverExecutionControllerProvider);
    final progress = DriverStopProgress.fromAssignment(assignment);
    final isAssigned = assignment.status == CollectionAssignmentStatus.assigned;
    final isInProgress =
        assignment.status == CollectionAssignmentStatus.inProgress;

    if (!isAssigned && !isInProgress) return const SizedBox.shrink();

    if (isAssigned) {
      final isStarting = execution.isRunningFor(
        DriverExecutionOperation.start,
        assignment.id,
      );
      final error =
          execution.operation == DriverExecutionOperation.start &&
              execution.assignmentId == assignment.id
          ? execution.error
          : null;
      return AppCard(
        key: const Key('driver_assignment_start_panel'),
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Ready to begin?',
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppSpacing.xxs),
            const Text(
              'Start collection when you are ready to record route-stop outcomes.',
              style: TextStyle(color: AppColors.textSecondary, fontSize: 13),
            ),
            if (error != null) ...[
              const SizedBox(height: AppSpacing.sm),
              AppAlert.error(message: driverExecutionErrorMessage(error)),
            ],
            const SizedBox(height: AppSpacing.md),
            AppButton.primary(
              key: const Key('driver_assignment_start_button'),
              label: 'Start collection',
              icon: Icons.play_arrow,
              isLoading: isStarting,
              onPressed: execution.isLoading
                  ? null
                  : () => _showStartConfirmation(context, assignment.id),
            ),
          ],
        ),
      );
    }

    final canFinalize = progress.pendingStops == 0 && progress.totalStops > 0;
    final isFinalizing = execution.isRunningFor(
      DriverExecutionOperation.finalize,
      assignment.id,
    );
    final error =
        execution.operation == DriverExecutionOperation.finalize &&
            execution.assignmentId == assignment.id
        ? execution.error
        : null;
    return AppCard(
      key: const Key('driver_assignment_finalize_panel'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Finalization',
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.xxs),
          Text(
            canFinalize
                ? 'All route stops have an outcome. Finalize this collection run when you are ready.'
                : '${progress.pendingStops} ${progress.pendingStops == 1 ? 'stop remains' : 'stops remain'} before this assignment can be finalized.',
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
            ),
          ),
          if (error != null) ...[
            const SizedBox(height: AppSpacing.sm),
            AppAlert.error(message: driverExecutionErrorMessage(error)),
          ],
          const SizedBox(height: AppSpacing.md),
          AppButton.primary(
            key: const Key('driver_assignment_finalize_button'),
            label: 'Finalize assignment',
            icon: Icons.task_alt_outlined,
            isLoading: isFinalizing,
            onPressed: !canFinalize || execution.isLoading
                ? null
                : () => _showFinalizeConfirmation(context, assignment.id),
          ),
        ],
      ),
    );
  }

  Widget _buildMetaRow({
    required IconData icon,
    required String label,
    required String value,
    required Key key,
  }) {
    return Row(
      children: [
        Icon(icon, size: 15, color: AppColors.textSecondary),
        const SizedBox(width: AppSpacing.xs),
        Text(
          '$label: ',
          style: const TextStyle(fontSize: 13, color: AppColors.textSecondary),
        ),
        Expanded(
          child: Text(
            value,
            key: key,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );
  }
}

Future<void> _showStartConfirmation(
  BuildContext pageContext,
  String assignmentId,
) {
  return showDialog<void>(
    context: pageContext,
    builder: (dialogContext) => Consumer(
      builder: (context, ref, _) {
        final execution = ref.watch(driverExecutionControllerProvider);
        final isSubmitting = execution.isRunningFor(
          DriverExecutionOperation.start,
          assignmentId,
        );
        final error =
            execution.operation == DriverExecutionOperation.start &&
                execution.assignmentId == assignmentId
            ? execution.error
            : null;
        return AlertDialog(
          title: const Text('Start collection?'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Starting makes the assignment and its collection tasks in progress.',
              ),
              if (error != null) ...[
                const SizedBox(height: AppSpacing.md),
                AppAlert.error(message: driverExecutionErrorMessage(error)),
              ],
            ],
          ),
          actions: [
            TextButton(
              onPressed: isSubmitting
                  ? null
                  : () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancel'),
            ),
            AppButton.primary(
              key: const Key('driver_confirm_start_button'),
              label: 'Start collection',
              isFullWidth: false,
              isLoading: isSubmitting,
              onPressed: isSubmitting
                  ? null
                  : () async {
                      final succeeded = await ref
                          .read(driverExecutionControllerProvider.notifier)
                          .startAssignment(assignmentId);
                      if (!dialogContext.mounted) return;
                      if (succeeded) {
                        Navigator.of(dialogContext).pop();
                        if (pageContext.mounted) {
                          ScaffoldMessenger.of(pageContext).showSnackBar(
                            const SnackBar(
                              content: Text('Collection started.'),
                            ),
                          );
                        }
                      }
                    },
            ),
          ],
        );
      },
    ),
  );
}

Future<void> _showFinalizeConfirmation(
  BuildContext pageContext,
  String assignmentId,
) {
  return showDialog<void>(
    context: pageContext,
    builder: (dialogContext) => Consumer(
      builder: (context, ref, _) {
        final execution = ref.watch(driverExecutionControllerProvider);
        final isSubmitting = execution.isRunningFor(
          DriverExecutionOperation.finalize,
          assignmentId,
        );
        final error =
            execution.operation == DriverExecutionOperation.finalize &&
                execution.assignmentId == assignmentId
            ? execution.error
            : null;
        return AlertDialog(
          title: const Text('Finalize assignment?'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'This confirms the recorded stop outcomes and releases the assignment resources.',
              ),
              if (error != null) ...[
                const SizedBox(height: AppSpacing.md),
                AppAlert.error(message: driverExecutionErrorMessage(error)),
              ],
            ],
          ),
          actions: [
            TextButton(
              onPressed: isSubmitting
                  ? null
                  : () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancel'),
            ),
            AppButton.primary(
              key: const Key('driver_confirm_finalize_button'),
              label: 'Finalize assignment',
              isFullWidth: false,
              isLoading: isSubmitting,
              onPressed: isSubmitting
                  ? null
                  : () async {
                      final succeeded = await ref
                          .read(driverExecutionControllerProvider.notifier)
                          .finalizeAssignment(assignmentId);
                      if (!dialogContext.mounted) return;
                      if (succeeded) {
                        Navigator.of(dialogContext).pop();
                        if (pageContext.mounted) {
                          ScaffoldMessenger.of(pageContext).showSnackBar(
                            const SnackBar(
                              content: Text('Assignment finalized.'),
                            ),
                          );
                          pageContext.go('/driver/dashboard');
                        }
                      }
                    },
            ),
          ],
        );
      },
    ),
  );
}
