import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../models/route_stop_status.dart';

/// Standard badge representing a route stop's execution status.
class RouteStopStatusBadge extends StatelessWidget {
  final RouteStopStatus status;

  const RouteStopStatusBadge({
    super.key,
    required this.status,
  });

  @override
  Widget build(BuildContext context) {
    final (bgColor, textColor, borderColor, icon) = switch (status) {
      RouteStopStatus.pending => (
          AppColors.warningLight,
          AppColors.warningText,
          AppColors.warningBorder,
          Icons.schedule_outlined,
        ),
      RouteStopStatus.completed => (
          AppColors.successLight,
          AppColors.successText,
          AppColors.successBorder,
          Icons.check_circle_outline,
        ),
      RouteStopStatus.failed => (
          AppColors.errorLight,
          AppColors.errorText,
          AppColors.errorBorder,
          Icons.cancel_outlined,
        ),
      RouteStopStatus.skipped => (
          AppColors.surfaceSubtle,
          AppColors.textMuted,
          AppColors.border,
          Icons.redo_outlined,
        ),
    };

    return Container(
      key: ValueKey('stop_status_badge_${status.value.toLowerCase()}'),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.xs,
        vertical: 3,
      ),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: AppSpacing.roundedFull,
        border: Border.all(color: borderColor, width: 1),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            icon,
            size: 12,
            color: textColor,
          ),
          const SizedBox(width: AppSpacing.xxs),
          Text(
            status.displayName,
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w600,
              color: textColor,
            ),
          ),
        ],
      ),
    );
  }
}
