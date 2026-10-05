import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../models/route_stop_model.dart';
import 'route_stop_status_badge.dart';

/// Reusable card displaying an individual ordered route stop and its collection task details.
class DriverStopCard extends StatelessWidget {
  final RouteStopModel stop;
  final bool isSelected;
  final VoidCallback? onTap;
  final VoidCallback? onOpenInMaps;
  final bool showOpenInMaps;
  final VoidCallback? onComplete;
  final VoidCallback? onFail;
  final bool isExecuting;

  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');
  static const double _stopActionHeight = 48;

  const DriverStopCard({
    super.key,
    required this.stop,
    this.isSelected = false,
    this.onTap,
    this.onOpenInMaps,
    this.showOpenInMaps = false,
    this.onComplete,
    this.onFail,
    this.isExecuting = false,
  });

  @override
  Widget build(BuildContext context) {
    final task = stop.task;
    final taskCodeLabel = task.taskCode.isNotEmpty
        ? task.taskCode
        : 'Stop #${stop.sequence}';

    final isReport = task.isReportTask;
    final targetLabel = isReport ? 'Citizen Report' : 'Waste Bin';
    final targetIcon = isReport
        ? Icons.assignment_outlined
        : Icons.delete_outline;

    return AppCard(
      key: Key('driver_stop_card_surface_${stop.sequence}'),
      padding: const EdgeInsets.all(AppSpacing.md),
      onTap: onTap,
      borderSide: isSelected
          ? const BorderSide(color: AppColors.primary, width: 2)
          : const BorderSide(color: AppColors.border),
      color: isSelected ? AppColors.surfaceSubtle : AppColors.surface,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // 1. Header: Sequence #, Task Code, Target Type & Status Badge
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Sequence Circle
              Container(
                width: 32,
                height: 32,
                decoration: const BoxDecoration(
                  color: AppColors.primaryLight,
                  shape: BoxShape.circle,
                ),
                alignment: Alignment.center,
                child: Text(
                  '#${stop.sequence}',
                  key: Key('driver_stop_sequence_${stop.sequence}'),
                  style: const TextStyle(
                    color: AppColors.primaryDark,
                    fontWeight: FontWeight.bold,
                    fontSize: 13,
                  ),
                ),
              ),
              const SizedBox(width: AppSpacing.sm),

              // Task Code and Target Type
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      taskCodeLabel,
                      key: Key('driver_stop_task_code_${stop.sequence}'),
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 15,
                        color: AppColors.textPrimary,
                      ),
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 2),
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          targetIcon,
                          size: 13,
                          color: AppColors.textSecondary,
                        ),
                        const SizedBox(width: 4),
                        Flexible(
                          child: Text(
                            targetLabel,
                            style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w500,
                              color: AppColors.textSecondary,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(width: AppSpacing.xs),

              // Stop Status Badge
              RouteStopStatusBadge(status: stop.status),
            ],
          ),

          const SizedBox(height: AppSpacing.sm),
          const Divider(color: AppColors.divider, height: 1),
          const SizedBox(height: AppSpacing.sm),

          // 2. Address / Location
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Icon(
                Icons.location_on_outlined,
                size: 16,
                color: AppColors.textSecondary,
              ),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      task.addressText?.isNotEmpty == true
                          ? task.addressText!
                          : 'Location specified on route',
                      key: Key('driver_stop_address_${stop.sequence}'),
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textPrimary,
                        height: 1.3,
                      ),
                    ),
                    if (stop.hasValidCoordinates) ...[
                      const SizedBox(height: 2),
                      Text(
                        '${task.latitude!.toStringAsFixed(4)}, ${task.longitude!.toStringAsFixed(4)}',
                        key: Key('driver_stop_coords_${stop.sequence}'),
                        style: const TextStyle(
                          fontSize: 11,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ] else ...[
                      const SizedBox(height: 2),
                      Text(
                        'Coordinates not available',
                        key: Key('driver_stop_no_coords_${stop.sequence}'),
                        style: const TextStyle(
                          fontSize: 11,
                          color: AppColors.warningText,
                          fontStyle: FontStyle.italic,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),

          // 3. Collection Reason (if present)
          if (task.collectionReason.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.xs),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Icon(
                  Icons.info_outline,
                  size: 16,
                  color: AppColors.textSecondary,
                ),
                const SizedBox(width: AppSpacing.xs),
                Expanded(
                  child: Text(
                    'Reason: ${task.collectionReason}',
                    key: Key('driver_stop_reason_${stop.sequence}'),
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                      height: 1.3,
                    ),
                  ),
                ),
              ],
            ),
          ],

          // 4. Scheduled Date & Time
          const SizedBox(height: AppSpacing.xs),
          Row(
            children: [
              const Icon(
                Icons.event_outlined,
                size: 16,
                color: AppColors.textSecondary,
              ),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: Text(
                  'Scheduled: ${_dateFormat.format(task.scheduledAt.toLocal())}',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ),
            ],
          ),

          // 5. Failed Details (if Failed)
          if (stop.status.isFailed) ...[
            const SizedBox(height: AppSpacing.sm),
            Container(
              key: Key('driver_stop_failed_box_${stop.sequence}'),
              width: double.infinity,
              padding: const EdgeInsets.all(AppSpacing.sm),
              decoration: BoxDecoration(
                color: AppColors.errorLight,
                borderRadius: AppSpacing.roundedSm,
                border: Border.all(color: AppColors.errorBorder),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(
                        Icons.warning_amber_rounded,
                        size: 16,
                        color: AppColors.error,
                      ),
                      const SizedBox(width: AppSpacing.xs),
                      Expanded(
                        child: Text(
                          stop.failureReason?.isNotEmpty == true
                              ? 'Failed: ${stop.failureReason}'
                              : 'Failed: Reason not specified',
                          key: Key(
                            'driver_stop_failure_reason_${stop.sequence}',
                          ),
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: AppColors.errorText,
                          ),
                        ),
                      ),
                    ],
                  ),
                  if (stop.failedAt != null) ...[
                    const SizedBox(height: 2),
                    Padding(
                      padding: const EdgeInsets.only(left: 22),
                      child: Text(
                        'Failed at: ${_dateFormat.format(stop.failedAt!.toLocal())}',
                        style: const TextStyle(
                          fontSize: 11,
                          color: AppColors.errorText,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],

          // 6. Completed Details (if Completed)
          if (stop.status.isCompleted && stop.completedAt != null) ...[
            const SizedBox(height: AppSpacing.sm),
            Container(
              key: Key('driver_stop_completed_box_${stop.sequence}'),
              width: double.infinity,
              padding: const EdgeInsets.all(AppSpacing.xs + 2),
              decoration: BoxDecoration(
                color: AppColors.successLight,
                borderRadius: AppSpacing.roundedSm,
                border: Border.all(color: AppColors.successBorder),
              ),
              child: Row(
                children: [
                  const Icon(
                    Icons.check_circle_outline,
                    size: 15,
                    color: AppColors.success,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: Text(
                      'Completed at: ${_dateFormat.format(stop.completedAt!.toLocal())}',
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w500,
                        color: AppColors.successText,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],

          // 7. Open in Maps Action
          if (showOpenInMaps || onOpenInMaps != null) ...[
            const SizedBox(height: AppSpacing.sm),
            Align(
              alignment: Alignment.centerRight,
              child: AppButton.outlined(
                key: Key('driver_stop_open_maps_${stop.sequence}'),
                label: 'Open in Maps',
                icon: Icons.map_outlined,
                isFullWidth: false,
                height: 36,
                onPressed: stop.hasValidCoordinates ? onOpenInMaps : null,
              ),
            ),
          ],

          // 8. Driver execution actions are supplied only by the Tasks screen
          // for pending stops on an in-progress assignment.
          if (stop.status.isPending &&
              (onComplete != null || onFail != null)) ...[
            const SizedBox(height: AppSpacing.sm),
            Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (onFail != null)
                  AppButton.destructive(
                    key: Key('driver_stop_fail_${stop.sequence}'),
                    label: 'Fail stop',
                    icon: Icons.report_problem_outlined,
                    height: _stopActionHeight,
                    isLoading: isExecuting,
                    onPressed: isExecuting ? null : onFail,
                  ),
                if (onFail != null && onComplete != null)
                  const SizedBox(height: AppSpacing.sm),
                if (onComplete != null)
                  AppButton.primary(
                    key: Key('driver_stop_complete_${stop.sequence}'),
                    label: 'Complete stop',
                    icon: Icons.check_circle_outline,
                    height: _stopActionHeight,
                    isLoading: isExecuting,
                    onPressed: isExecuting ? null : onComplete,
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
