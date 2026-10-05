import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_alert.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../models/assignment_detail_model.dart';
import '../models/assignment_history_model.dart';
import '../providers/driver_history_providers.dart';
import 'widgets/assignment_progress_card.dart';
import 'widgets/driver_assignment_status_badge.dart';
import 'widgets/driver_stop_card.dart';

/// Read-only results and stop details for a terminal Driver assignment.
class DriverHistoryDetailScreen extends ConsumerWidget {
  final String assignmentId;

  const DriverHistoryDetailScreen({super.key, required this.assignmentId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detailAsync = ref.watch(driverHistoryDetailProvider(assignmentId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Assignment Results'),
        leading: IconButton(
          tooltip: 'Back to history',
          onPressed: () => context.canPop()
              ? context.pop()
              : context.go('/driver/history'),
          icon: const Icon(Icons.arrow_back),
        ),
      ),
      body: detailAsync.when(
        loading: () => const AppLoadingIndicator(message: 'Loading assignment results…'),
        error: (error, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const AppAlert.error(
                  title: 'Unable to load assignment results',
                  message: 'Please try again.',
                ),
                const SizedBox(height: AppSpacing.sm),
                AppButton.outlined(
                  key: const Key('driver_history_detail_retry'),
                  label: 'Try again',
                  icon: Icons.refresh,
                  isFullWidth: false,
                  onPressed: () => ref.invalidate(
                    driverHistoryDetailProvider(assignmentId),
                  ),
                ),
              ],
            ),
          ),
        ),
        data: (assignment) => _HistoryDetailBody(assignment: assignment),
      ),
    );
  }
}

class _HistoryDetailBody extends StatelessWidget {
  final AssignmentDetailModel assignment;

  const _HistoryDetailBody({required this.assignment});

  @override
  Widget build(BuildContext context) {
    final terminalRecords = assignment.history
        .where((entry) => entry.toStatus.isTerminal)
        .toList(growable: false);
    final AssignmentHistoryModel? terminalRecord = terminalRecords.isEmpty
        ? null
        : terminalRecords.reduce(
            (latest, entry) => entry.changedAt.isAfter(latest.changedAt)
                ? entry
                : latest,
          );
    final orderedStops = assignment.route?.orderedStops ?? const [];

    return ListView(
        key: const Key('driver_history_detail'),
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          AppCard(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(Icons.local_shipping_outlined, color: AppColors.primary),
                    const SizedBox(width: AppSpacing.sm),
                    Expanded(
                      child: Text(
                        assignment.vehicleRegistrationNumber.isNotEmpty
                            ? assignment.vehicleRegistrationNumber
                            : 'Assigned vehicle',
                        style: Theme.of(context).textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.bold,
                              color: AppColors.textPrimary,
                            ),
                      ),
                    ),
                    DriverAssignmentStatusBadge(status: assignment.status),
                  ],
                ),
                const SizedBox(height: AppSpacing.md),
                _DetailLine(
                  label: 'Assigned',
                  value: DateFormat('d MMM yyyy • h:mm a').format(assignment.assignedAt.toLocal()),
                ),
                if (terminalRecord != null) ...[
                  const SizedBox(height: AppSpacing.xs),
                  _DetailLine(
                    label: 'Finalized',
                    value: DateFormat('d MMM yyyy • h:mm a').format(terminalRecord.changedAt.toLocal()),
                  ),
                ],
                const SizedBox(height: AppSpacing.xs),
                _DetailLine(
                  label: 'Stops',
                  value: '${assignment.completedStopCount} completed • ${assignment.failedStopCount} failed • ${assignment.stopCount} total',
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          AssignmentProgressCard(assignment: assignment),
          const SizedBox(height: AppSpacing.lg),
          Text(
            'Route stops',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                ),
          ),
          const SizedBox(height: AppSpacing.xxs),
          const Text(
            'Recorded in the saved route sequence.',
            style: TextStyle(color: AppColors.textSecondary),
          ),
          const SizedBox(height: AppSpacing.sm),
          if (orderedStops.isEmpty)
            const AppAlert.info(
              message: 'No route stops were returned for this assignment.',
            )
          else
            for (final stop in orderedStops) ...[
              DriverStopCard(stop: stop),
              const SizedBox(height: AppSpacing.sm),
            ],
        ],
    );
  }
}

class _DetailLine extends StatelessWidget {
  final String label;
  final String value;

  const _DetailLine({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 68,
          child: Text(
            label,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(color: AppColors.textPrimary, fontSize: 13),
          ),
        ),
      ],
    );
  }
}
