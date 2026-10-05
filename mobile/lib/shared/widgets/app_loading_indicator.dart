import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';

/// Standardized loading indicator for SmartWaste mobile application.
class AppLoadingIndicator extends StatelessWidget {
  final String? message;
  final double size;
  final double strokeWidth;
  final Color? color;

  const AppLoadingIndicator({
    super.key,
    this.message,
    this.size = 36.0,
    this.strokeWidth = 3.0,
    this.color,
  });

  @override
  Widget build(BuildContext context) {
    final indicator = SizedBox(
      height: size,
      width: size,
      child: CircularProgressIndicator(
        strokeWidth: strokeWidth,
        valueColor: AlwaysStoppedAnimation<Color>(color ?? AppColors.primary),
      ),
    );

    if (message == null) {
      return Center(child: indicator);
    }

    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          indicator,
          const SizedBox(height: AppSpacing.md),
          Text(
            message!,
            style: TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}
