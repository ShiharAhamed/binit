import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../models/collection_assignment_status.dart';

/// Consistent, read-only presentation of an assignment lifecycle status.
class DriverAssignmentStatusBadge extends StatelessWidget {
  final CollectionAssignmentStatus status;

  const DriverAssignmentStatusBadge({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final (background, foreground, border, icon) = switch (status) {
      CollectionAssignmentStatus.assigned => (
          AppColors.infoLight,
          AppColors.infoText,
          AppColors.infoBorder,
          Icons.assignment_outlined,
        ),
      CollectionAssignmentStatus.inProgress => (
          AppColors.warningLight,
          AppColors.warningText,
          AppColors.warningBorder,
          Icons.directions_car_outlined,
        ),
      CollectionAssignmentStatus.completed => (
          AppColors.successLight,
          AppColors.successText,
          AppColors.successBorder,
          Icons.check_circle_outline,
        ),
      CollectionAssignmentStatus.partiallyCompleted => (
          AppColors.warningLight,
          AppColors.warningText,
          AppColors.warningBorder,
          Icons.task_alt_outlined,
        ),
      CollectionAssignmentStatus.failed => (
          AppColors.errorLight,
          AppColors.errorText,
          AppColors.errorBorder,
          Icons.cancel_outlined,
        ),
      CollectionAssignmentStatus.cancelled => (
          AppColors.surfaceSubtle,
          AppColors.textSecondary,
          AppColors.border,
          Icons.block_outlined,
        ),
    };

    return Container(
      key: Key('driver_assignment_status_${status.value.toLowerCase()}'),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.xs,
        vertical: AppSpacing.xxs + 1,
      ),
      decoration: BoxDecoration(
        color: background,
        borderRadius: AppSpacing.roundedFull,
        border: Border.all(color: border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 13, color: foreground),
          const SizedBox(width: AppSpacing.xxs),
          Text(
            status.displayName,
            style: TextStyle(
              color: foreground,
              fontSize: 11,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}
