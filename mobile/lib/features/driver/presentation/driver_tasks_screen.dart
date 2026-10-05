import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

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
import 'widgets/driver_stop_card.dart';

/// Screen displaying the ordered collection tasks for the Driver's current active assignment.
class DriverTasksScreen extends ConsumerWidget {
  const DriverTasksScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final assignmentAsync = ref.watch(driverCurrentAssignmentProvider);

    return RefreshIndicator(
      key: const Key('driver_tasks_refresh_indicator'),
      onRefresh: () async {
        try {
          ref.invalidate(driverCurrentAssignmentProvider);
          await ref.read(driverCurrentAssignmentProvider.future);
        } catch (_) {
          // Errors are surfaced through AsyncValue.error
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
              return _buildTasksList(context, ref, assignment);
            },
            loading: () => const SingleChildScrollView(
              physics: AlwaysScrollableScrollPhysics(),
              child: SizedBox(
                height: 300,
                child: AppLoadingIndicator(
                  message: 'Loading collection tasks...',
                ),
              ),
            ),
            error: (error, _) => _buildErrorState(context, ref, error),
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
        key: const Key('driver_tasks_empty_state'),
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
                Icons.checklist_rtl_outlined,
                size: 36,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            Text(
              'No collection tasks assigned',
              key: const Key('driver_tasks_empty_title'),
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(
              'You do not have an active collection assignment at this time. New tasks will appear here when an assignment is dispatched.',
              key: const Key('driver_tasks_empty_description'),
              style: Theme.of(context).textTheme.bodyMedium
                  ?.copyWith(color: AppColors.textSecondary, height: 1.5),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildErrorState(BuildContext context, WidgetRef ref, Object error) {
    final message = error is ApiException
        ? error.message
        : 'Unable to load collection tasks. Please try again.';

    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.xl,
        vertical: AppSpacing.xxl,
      ),
      child: Column(
        children: [
          AppAlert.error(
            key: const Key('driver_tasks_error_alert'),
            message: message,
          ),
          const SizedBox(height: AppSpacing.md),
          AppButton.outlined(
            key: const Key('driver_tasks_retry_button'),
            label: 'Retry',
            icon: Icons.refresh,
            onPressed: () => ref.invalidate(driverCurrentAssignmentProvider),
          ),
        ],
      ),
    );
  }

  Widget _buildTasksList(
    BuildContext context,
    WidgetRef ref,
    AssignmentDetailModel assignment,
  ) {
    // Authoritative sequence sorting
    final List<RouteStopModel> stops = assignment.route?.orderedStops ?? [];

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

    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: AppSpacing.md,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // 1. Assignment Operational Context Banner
          Container(
            key: const Key('driver_tasks_context_banner'),
            padding: const EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: AppSpacing.roundedMd,
              border: Border.all(color: AppColors.border),
              boxShadow: const [
                BoxShadow(
                  color: Color(0x06000000),
                  blurRadius: 4,
                  offset: Offset(0, 2),
                ),
              ],
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Flexible(
                      child: Text(
                        assignment.vehicleRegistrationNumber.isNotEmpty
                            ? assignment.vehicleRegistrationNumber
                            : 'Assigned Vehicle',
                        key: const Key('driver_tasks_vehicle_text'),
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
                        key: const Key('driver_tasks_status_badge'),
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          color: statusColor,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.xxs),
                Text(
                  '${stops.length} ${stops.length == 1 ? 'stop' : 'stops'} on route · Ordered strictly by sequence',
                  key: const Key('driver_tasks_stop_count_text'),
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.md),

          if (!isInProgress) ...[
            const AppAlert.info(
              key: Key('driver_tasks_start_required_alert'),
              title: 'Start collection first',
              message: 'Stop outcomes become available after you start this assignment from My Assignment.',
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton.outlined(
              key: const Key('driver_tasks_open_assignment_button'),
              label: 'Open My Assignment',
              icon: Icons.assignment_outlined,
              onPressed: () => context.go('/driver/assignment'),
            ),
            const SizedBox(height: AppSpacing.md),
          ] else ...[
            const AppAlert.info(
              key: Key('driver_tasks_any_pending_stop_alert'),
              message: 'Route sequence is a display aid. You may record an outcome for any pending stop.',
            ),
            const SizedBox(height: AppSpacing.md),
          ],

          // 2. Ordered Stops
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
              DriverStopCard(
                key: Key('driver_stop_card_${stops[i].sequence}'),
                stop: stops[i],
                onComplete: isInProgress && stops[i].status.isPending
                    ? () => _showCompleteStopConfirmation(
                        context,
                        assignment.id,
                        stops[i],
                      )
                    : null,
                onFail: isInProgress && stops[i].status.isPending
                    ? () =>
                          _showFailStopDialog(context, assignment.id, stops[i])
                    : null,
                isExecuting: ref
                    .watch(driverExecutionControllerProvider)
                    .isLoading,
              ),
              if (i < stops.length - 1) const SizedBox(height: AppSpacing.sm),
            ],
          ],

          const SizedBox(height: AppSpacing.xl),
        ],
      ),
    );
  }
}

Future<void> _showCompleteStopConfirmation(
  BuildContext pageContext,
  String assignmentId,
  RouteStopModel stop,
) {
  return showDialog<void>(
    context: pageContext,
    builder: (dialogContext) => Consumer(
      builder: (context, ref, _) {
        final execution = ref.watch(driverExecutionControllerProvider);
        final isSubmitting = execution.isRunningFor(
          DriverExecutionOperation.completeStop,
          assignmentId,
          expectedStopId: stop.id,
        );
        final error =
            execution.operation == DriverExecutionOperation.completeStop &&
                execution.assignmentId == assignmentId &&
                execution.stopId == stop.id
            ? execution.error
            : null;

        return AlertDialog(
          title: const Text('Complete stop?'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Mark ${stop.task.taskCode.isNotEmpty ? stop.task.taskCode : 'this stop'} as completed?',
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
              key: const Key('driver_confirm_complete_stop_button'),
              label: 'Complete stop',
              isFullWidth: false,
              isLoading: isSubmitting,
              onPressed: isSubmitting
                  ? null
                  : () async {
                      final succeeded = await ref
                          .read(driverExecutionControllerProvider.notifier)
                          .completeStop(assignmentId, stop.id);
                      if (!dialogContext.mounted) return;
                      if (succeeded) {
                        Navigator.of(dialogContext).pop();
                        if (pageContext.mounted) {
                          ScaffoldMessenger.of(pageContext).showSnackBar(
                            const SnackBar(
                              content: Text('Stop marked as completed.'),
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

Future<void> _showFailStopDialog(
  BuildContext pageContext,
  String assignmentId,
  RouteStopModel stop,
) => showDialog<void>(
  context: pageContext,
  builder: (_) => _FailStopDialog(
    pageContext: pageContext,
    assignmentId: assignmentId,
    stop: stop,
  ),
);

/// Owns the failure-reason controller for the complete dialog lifetime.
///
/// A successful failure refreshes the parent task list, which can remove the
/// failed stop while this dialog is closing. Keeping the controller in the
/// dialog's State avoids disposing it from the asynchronous `showDialog`
/// future while Flutter is still tearing down inherited dependencies.
class _FailStopDialog extends ConsumerStatefulWidget {
  final BuildContext pageContext;
  final String assignmentId;
  final RouteStopModel stop;

  const _FailStopDialog({
    required this.pageContext,
    required this.assignmentId,
    required this.stop,
  });

  @override
  ConsumerState<_FailStopDialog> createState() => _FailStopDialogState();
}

class _FailStopDialogState extends ConsumerState<_FailStopDialog> {
  final _reasonController = TextEditingController();
  String? _localError;

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final reason = _reasonController.text.trim();
    if (reason.length < 5 || reason.length > 500) {
      setState(() {
        _localError = 'Enter a reason between 5 and 500 characters.';
      });
      return;
    }

    setState(() => _localError = null);
    final succeeded = await ref
        .read(driverExecutionControllerProvider.notifier)
        .failStop(widget.assignmentId, widget.stop.id, reason);
    if (!mounted || !succeeded) return;

    Navigator.of(context).pop();
    if (widget.pageContext.mounted) {
      ScaffoldMessenger.of(
        widget.pageContext,
      ).showSnackBar(const SnackBar(content: Text('Stop marked as failed.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final execution = ref.watch(driverExecutionControllerProvider);
    final isSubmitting = execution.isRunningFor(
      DriverExecutionOperation.failStop,
      widget.assignmentId,
      expectedStopId: widget.stop.id,
    );
    final requestError =
        execution.operation == DriverExecutionOperation.failStop &&
            execution.assignmentId == widget.assignmentId &&
            execution.stopId == widget.stop.id
        ? execution.error
        : null;

    return AlertDialog(
      title: const Text('Fail stop'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Provide a reason between 5 and 500 characters.'),
            const SizedBox(height: AppSpacing.md),
            TextField(
              key: const Key('driver_fail_stop_reason_field'),
              controller: _reasonController,
              enabled: !isSubmitting,
              minLines: 3,
              maxLines: 5,
              maxLength: 500,
              textCapitalization: TextCapitalization.sentences,
              decoration: InputDecoration(
                labelText: 'Failure reason',
                hintText: 'Explain why this stop could not be completed',
                errorText: _localError,
              ),
            ),
            if (requestError != null)
              AppAlert.error(
                message: driverExecutionErrorMessage(requestError),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: isSubmitting ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        AppButton.destructive(
          key: const Key('driver_confirm_fail_stop_button'),
          label: 'Fail stop',
          isFullWidth: false,
          isLoading: isSubmitting,
          onPressed: isSubmitting ? null : _submit,
        ),
      ],
    );
  }
}
