import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';

enum AppAlertVariant {
  info,
  success,
  warning,
  error,
}

/// Standardized alert / message banner for SmartWaste mobile application.
class AppAlert extends StatelessWidget {
  final String message;
  final String? title;
  final AppAlertVariant variant;
  final IconData? icon;
  final EdgeInsetsGeometry? margin;
  final EdgeInsetsGeometry padding;

  const AppAlert({
    super.key,
    required this.message,
    this.title,
    this.variant = AppAlertVariant.info,
    this.icon,
    this.margin,
    this.padding = const EdgeInsets.all(AppSpacing.sm),
  });

  const AppAlert.error({
    super.key,
    required this.message,
    this.title,
    this.icon,
    this.margin,
    this.padding = const EdgeInsets.all(AppSpacing.sm),
  }) : variant = AppAlertVariant.error;

  const AppAlert.warning({
    super.key,
    required this.message,
    this.title,
    this.icon,
    this.margin,
    this.padding = const EdgeInsets.all(AppSpacing.sm),
  }) : variant = AppAlertVariant.warning;

  const AppAlert.success({
    super.key,
    required this.message,
    this.title,
    this.icon,
    this.margin,
    this.padding = const EdgeInsets.all(AppSpacing.sm),
  }) : variant = AppAlertVariant.success;

  const AppAlert.info({
    super.key,
    required this.message,
    this.title,
    this.icon,
    this.margin,
    this.padding = const EdgeInsets.all(AppSpacing.sm),
  }) : variant = AppAlertVariant.info;

  @override
  Widget build(BuildContext context) {
    Color backgroundColor;
    Color borderColor;
    Color iconColor;
    Color textColor;
    IconData defaultIcon;

    switch (variant) {
      case AppAlertVariant.error:
        backgroundColor = AppColors.errorLight;
        borderColor = AppColors.errorBorder;
        iconColor = AppColors.error;
        textColor = AppColors.errorText;
        defaultIcon = Icons.error_outline;
        break;
      case AppAlertVariant.warning:
        backgroundColor = AppColors.warningLight;
        borderColor = AppColors.warningBorder;
        iconColor = AppColors.warning;
        textColor = AppColors.warningText;
        defaultIcon = Icons.warning_amber_rounded;
        break;
      case AppAlertVariant.success:
        backgroundColor = AppColors.successLight;
        borderColor = AppColors.successBorder;
        iconColor = AppColors.success;
        textColor = AppColors.successText;
        defaultIcon = Icons.check_circle_outline;
        break;
      case AppAlertVariant.info:
        backgroundColor = AppColors.infoLight;
        borderColor = AppColors.infoBorder;
        iconColor = AppColors.info;
        textColor = AppColors.infoText;
        defaultIcon = Icons.info_outline;
        break;
    }

    return Container(
      margin: margin,
      padding: padding,
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: AppSpacing.roundedSm,
        border: Border.all(color: borderColor),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            icon ?? defaultIcon,
            color: iconColor,
            size: 20,
          ),
          const SizedBox(width: AppSpacing.xs),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                if (title != null) ...[
                  Text(
                    title!,
                    style: TextStyle(
                      color: textColor,
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 2),
                ],
                Text(
                  message,
                  style: TextStyle(
                    color: textColor,
                    fontSize: 13,
                    height: 1.35,
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
