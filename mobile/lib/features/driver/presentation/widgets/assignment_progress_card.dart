import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../models/assignment_detail_model.dart';
import '../../providers/driver_dashboard_providers.dart';

/// Reusable card displaying an assignment's stop completion progress and status breakdown.
class AssignmentProgressCard extends StatelessWidget {
  final AssignmentDetailModel assignment;

  const AssignmentProgressCard({
    super.key,
    required this.assignment,
  });

  @override
  Widget build(BuildContext context) {
    final progress = DriverStopProgress.fromAssignment(assignment);
    final percent = (progress.progressFraction * 100).round();

    return AppCard(
      key: const Key('assignment_progress_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header: Title and Completion Count
          Wrap(
            alignment: WrapAlignment.spaceBetween,
            crossAxisAlignment: WrapCrossAlignment.center,
            spacing: AppSpacing.xs,
            runSpacing: AppSpacing.xxs,
            children: [
              Text(
                'Stop Progress',
                style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: AppColors.textPrimary,
                    ),
              ),
              Text(
                '${progress.completedStops} of ${progress.totalStops} completed ($percent%)',
                key: const Key('assignment_progress_ratio_text'),
                style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),

          // Linear Progress Bar
          ClipRRect(
            borderRadius: AppSpacing.roundedFull,
            child: LinearProgressIndicator(
              key: const Key('assignment_progress_bar'),
              value: progress.progressFraction.clamp(0.0, 1.0),
              minHeight: 8,
              backgroundColor: AppColors.surfaceSubtle,
              valueColor:
                  const AlwaysStoppedAnimation<Color>(AppColors.primary),
            ),
          ),
          const SizedBox(height: AppSpacing.md),

          // Status Breakdown Counters
          Wrap(
            spacing: AppSpacing.xs,
            runSpacing: AppSpacing.xs,
            children: [
              _buildProgressChip(
                label: '${progress.completedStops} Completed',
                bgColor: AppColors.successLight,
                textColor: AppColors.successText,
                borderColor: AppColors.successBorder,
                key: const Key('assignment_progress_chip_completed'),
              ),
              _buildProgressChip(
                label: '${progress.pendingStops} Pending',
                bgColor: AppColors.warningLight,
                textColor: AppColors.warningText,
                borderColor: AppColors.warningBorder,
                key: const Key('assignment_progress_chip_pending'),
              ),
              if (progress.failedStops > 0)
                _buildProgressChip(
                  label: '${progress.failedStops} Failed',
                  bgColor: AppColors.errorLight,
                  textColor: AppColors.errorText,
                  borderColor: AppColors.errorBorder,
                  key: const Key('assignment_progress_chip_failed'),
                ),
              if (progress.skippedStops > 0)
                _buildProgressChip(
                  label: '${progress.skippedStops} Skipped',
                  bgColor: AppColors.surfaceSubtle,
                  textColor: AppColors.textMuted,
                  borderColor: AppColors.border,
                  key: const Key('assignment_progress_chip_skipped'),
                ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildProgressChip({
    required String label,
    required Color bgColor,
    required Color textColor,
    required Color borderColor,
    required Key key,
  }) {
    return Container(
      key: key,
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs + 2, vertical: 3),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: AppSpacing.roundedFull,
        border: Border.all(color: borderColor),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: textColor,
        ),
      ),
    );
  }
}
