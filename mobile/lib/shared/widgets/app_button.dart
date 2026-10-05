import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';

enum AppButtonVariant {
  primary,
  secondary,
  outlined,
  destructive,
  text,
}

/// Standardized action button for SmartWaste mobile application.
class AppButton extends StatelessWidget {
  final VoidCallback? onPressed;
  final String? label;
  final Widget? child;
  final IconData? icon;
  final bool isLoading;
  final AppButtonVariant variant;
  final bool isFullWidth;
  final double height;

  const AppButton({
    super.key,
    required this.onPressed,
    this.label,
    this.child,
    this.icon,
    this.isLoading = false,
    this.variant = AppButtonVariant.primary,
    this.isFullWidth = true,
    this.height = 48.0,
  }) : assert(label != null || child != null, 'Must provide label or child');

  const AppButton.primary({
    super.key,
    required this.onPressed,
    this.label,
    this.child,
    this.icon,
    this.isLoading = false,
    this.isFullWidth = true,
    this.height = 48.0,
  }) : variant = AppButtonVariant.primary,
       assert(label != null || child != null, 'Must provide label or child');

  const AppButton.outlined({
    super.key,
    required this.onPressed,
    this.label,
    this.child,
    this.icon,
    this.isLoading = false,
    this.isFullWidth = true,
    this.height = 48.0,
  }) : variant = AppButtonVariant.outlined,
       assert(label != null || child != null, 'Must provide label or child');

  const AppButton.destructive({
    super.key,
    required this.onPressed,
    this.label,
    this.child,
    this.icon,
    this.isLoading = false,
    this.isFullWidth = true,
    this.height = 48.0,
  }) : variant = AppButtonVariant.destructive,
       assert(label != null || child != null, 'Must provide label or child');

  const AppButton.text({
    super.key,
    required this.onPressed,
    this.label,
    this.child,
    this.icon,
    this.isLoading = false,
    this.isFullWidth = false,
    this.height = 36.0,
  }) : variant = AppButtonVariant.text,
       assert(label != null || child != null, 'Must provide label or child');

  @override
  Widget build(BuildContext context) {
    final effectiveChild = isLoading
        ? SizedBox(
            height: 20,
            width: 20,
            child: CircularProgressIndicator(
              strokeWidth: 2,
              valueColor: AlwaysStoppedAnimation<Color>(
                variant == AppButtonVariant.primary || variant == AppButtonVariant.destructive
                    ? Colors.white
                    : AppColors.primary,
              ),
            ),
          )
        : (icon != null
            ? Row(
                mainAxisSize: isFullWidth ? MainAxisSize.max : MainAxisSize.min,
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(icon, size: 18),
                  const SizedBox(width: AppSpacing.xs),
                  Flexible(
                    child: child ??
                        Text(
                          label!,
                          overflow: TextOverflow.ellipsis,
                        ),
                  ),
                ],
              )
            : (child ?? Text(label!, overflow: TextOverflow.ellipsis)));

    Widget button;

    switch (variant) {
      case AppButtonVariant.primary:
        button = ElevatedButton(
          onPressed: isLoading ? null : onPressed,
          style: ElevatedButton.styleFrom(
            minimumSize: Size(isFullWidth ? double.infinity : 0, height),
          ),
          child: effectiveChild,
        );
        break;

      case AppButtonVariant.secondary:
        button = ElevatedButton(
          onPressed: isLoading ? null : onPressed,
          style: ElevatedButton.styleFrom(
            backgroundColor: AppColors.primaryLight,
            foregroundColor: AppColors.primaryDark,
            elevation: 0,
            minimumSize: Size(isFullWidth ? double.infinity : 0, height),
          ),
          child: effectiveChild,
        );
        break;

      case AppButtonVariant.outlined:
        button = OutlinedButton(
          onPressed: isLoading ? null : onPressed,
          style: OutlinedButton.styleFrom(
            minimumSize: Size(isFullWidth ? double.infinity : 0, height),
          ),
          child: effectiveChild,
        );
        break;

      case AppButtonVariant.destructive:
        button = OutlinedButton(
          onPressed: isLoading ? null : onPressed,
          style: OutlinedButton.styleFrom(
            foregroundColor: AppColors.error,
            side: const BorderSide(color: AppColors.error, width: 1.2),
            minimumSize: Size(isFullWidth ? double.infinity : 0, height),
          ),
          child: effectiveChild,
        );
        break;

      case AppButtonVariant.text:
        button = TextButton(
          onPressed: isLoading ? null : onPressed,
          style: TextButton.styleFrom(
            minimumSize: Size(isFullWidth ? double.infinity : 0, height),
          ),
          child: effectiveChild,
        );
        break;
    }

    return button;
  }
}
