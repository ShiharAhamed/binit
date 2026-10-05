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
import '../models/assignment_summary_model.dart';
import '../models/collection_assignment_status.dart';
import '../providers/driver_history_providers.dart';
import 'widgets/driver_assignment_status_badge.dart';

enum _HistoryFilter {
  all('All terminal results', null),
  completed('Completed', CollectionAssignmentStatus.completed),
  partiallyCompleted(
    'Partially completed',
    CollectionAssignmentStatus.partiallyCompleted,
  ),
  failed('Failed', CollectionAssignmentStatus.failed),
  cancelled('Cancelled', CollectionAssignmentStatus.cancelled);

  final String label;
  final CollectionAssignmentStatus? status;

  const _HistoryFilter(this.label, this.status);
}

/// Read-only, server-paginated assignment results for the authenticated Driver.
class DriverHistoryScreen extends ConsumerStatefulWidget {
  const DriverHistoryScreen({super.key});

  @override
  ConsumerState<DriverHistoryScreen> createState() => _DriverHistoryScreenState();
}

class _DriverHistoryScreenState extends ConsumerState<DriverHistoryScreen> {
  _HistoryFilter _filter = _HistoryFilter.all;
  int _page = 1;

  DriverAssignmentHistoryQuery get _query => DriverAssignmentHistoryQuery(
        status: _filter.status,
        page: _page,
      );

  @override
  Widget build(BuildContext context) {
    final pageAsync = ref.watch(driverAssignmentHistoryPageProvider(_query));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Assignment History'),
        leading: IconButton(
          tooltip: 'Back',
          onPressed: () => context.canPop()
              ? context.pop()
              : context.go('/driver/dashboard'),
          icon: const Icon(Icons.arrow_back),
        ),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(driverAssignmentHistoryPageProvider(_query));
          await ref.read(driverAssignmentHistoryPageProvider(_query).future);
        },
        child: ListView(
          key: const Key('driver_history_list'),
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(AppSpacing.lg),
          children: [
            Text(
              'Past assignments',
              style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: AppColors.textPrimary,
                  ),
            ),
            const SizedBox(height: AppSpacing.xxs),
            const Text(
              'Review completed, partially completed, failed, or cancelled collection work.',
              style: TextStyle(color: AppColors.textSecondary, height: 1.35),
            ),
            const SizedBox(height: AppSpacing.lg),
            _HistoryFilterControl(
              filter: _filter,
              onChanged: (filter) => setState(() {
                _filter = filter;
                _page = 1;
              }),
            ),
            const SizedBox(height: AppSpacing.md),
            pageAsync.when(
              loading: () => const Padding(
                padding: EdgeInsets.only(top: AppSpacing.xl),
                child: AppLoadingIndicator(message: 'Loading assignment history…'),
              ),
              error: (error, _) => _HistoryErrorState(
                onRetry: () => ref.invalidate(
                  driverAssignmentHistoryPageProvider(_query),
                ),
              ),
              data: (page) {
                final assignments = terminalAssignments(page.items).toList();
                if (assignments.isEmpty) {
                  return const _HistoryEmptyState();
                }

                return Column(
                  children: [
                    for (final assignment in assignments) ...[
                      _HistoryAssignmentCard(
                        assignment: assignment,
                        onTap: () => context.push(
                          '/driver/history/${assignment.id}',
                        ),
                      ),
                      const SizedBox(height: AppSpacing.sm),
                    ],
                    _HistoryPagination(
                      page: page.page,
                      totalPages: page.totalPages,
                      onPrevious: page.page > 1
                          ? () => setState(() => _page = page.page - 1)
                          : null,
                      onNext: page.hasMore
                          ? () => setState(() => _page = page.page + 1)
                          : null,
                    ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _HistoryFilterControl extends StatelessWidget {
  final _HistoryFilter filter;
  final ValueChanged<_HistoryFilter> onChanged;

  const _HistoryFilterControl({required this.filter, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return InputDecorator(
      decoration: const InputDecoration(
        labelText: 'Outcome',
        prefixIcon: Icon(Icons.filter_list_outlined),
      ),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<_HistoryFilter>(
          key: const Key('driver_history_status_filter'),
          value: filter,
          isExpanded: true,
          items: _HistoryFilter.values
              .map(
                (item) => DropdownMenuItem(value: item, child: Text(item.label)),
              )
              .toList(growable: false),
          onChanged: (value) {
            if (value != null) onChanged(value);
          },
        ),
      ),
    );
  }
}

class _HistoryAssignmentCard extends StatelessWidget {
  final AssignmentSummaryModel assignment;
  final VoidCallback onTap;

  const _HistoryAssignmentCard({
    required this.assignment,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final stops = assignment.stopCount == 1 ? 'stop' : 'stops';
    return AppCard(
      key: Key('driver_history_assignment_${assignment.id}'),
      padding: const EdgeInsets.all(AppSpacing.md),
      onTap: onTap,
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
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              DriverAssignmentStatusBadge(status: assignment.status),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            'Assigned ${DateFormat('d MMM yyyy • h:mm a').format(assignment.assignedAt.toLocal())}',
            style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            '${assignment.completedStopCount} completed • '
            '${assignment.failedStopCount} failed • '
            '${assignment.stopCount} $stops',
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          const Align(
            alignment: Alignment.centerRight,
            child: Text(
              'View results',
              style: TextStyle(
                color: AppColors.primary,
                fontSize: 12,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _HistoryPagination extends StatelessWidget {
  final int page;
  final int totalPages;
  final VoidCallback? onPrevious;
  final VoidCallback? onNext;

  const _HistoryPagination({
    required this.page,
    required this.totalPages,
    this.onPrevious,
    this.onNext,
  });

  @override
  Widget build(BuildContext context) {
    if (totalPages <= 1) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.md),
      child: Row(
        children: [
          AppButton.outlined(
            key: const Key('driver_history_previous_page'),
            label: 'Previous',
            isFullWidth: false,
            height: 40,
            onPressed: onPrevious,
          ),
          Expanded(
            child: Text(
              'Page $page of $totalPages',
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 12,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          AppButton.outlined(
            key: const Key('driver_history_next_page'),
            label: 'Next',
            isFullWidth: false,
            height: 40,
            onPressed: onNext,
          ),
        ],
      ),
    );
  }
}

class _HistoryEmptyState extends StatelessWidget {
  const _HistoryEmptyState();

  @override
  Widget build(BuildContext context) {
    return AppCard(
      key: const Key('driver_history_empty_state'),
      padding: const EdgeInsets.all(AppSpacing.xl),
      child: const Column(
        children: [
          Icon(Icons.history_outlined, size: 36, color: AppColors.textMuted),
          SizedBox(height: AppSpacing.sm),
          Text(
            'No past assignments yet',
            style: TextStyle(
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          SizedBox(height: AppSpacing.xxs),
          Text(
            'Finished collection work will appear here.',
            textAlign: TextAlign.center,
            style: TextStyle(color: AppColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

class _HistoryErrorState extends StatelessWidget {
  final VoidCallback onRetry;

  const _HistoryErrorState({required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        const AppAlert.error(
          title: 'Unable to load history',
          message: 'Please check your connection and try again.',
        ),
        const SizedBox(height: AppSpacing.sm),
        AppButton.outlined(
          key: const Key('driver_history_retry'),
          label: 'Try again',
          icon: Icons.refresh,
          isFullWidth: false,
          onPressed: onRetry,
        ),
      ],
    );
  }
}
