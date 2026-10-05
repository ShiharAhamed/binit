import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';

/// Standardized card surface for SmartWaste mobile application.
class AppCard extends StatelessWidget {
  final Widget child;
  final EdgeInsetsGeometry? padding;
  final EdgeInsetsGeometry? margin;
  final VoidCallback? onTap;
  final Color? color;
  final BorderSide? borderSide;

  const AppCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(AppSpacing.xl),
    this.margin,
    this.onTap,
    this.color,
    this.borderSide,
  });

  @override
  Widget build(BuildContext context) {
    final card = Container(
      margin: margin,
      decoration: BoxDecoration(
        color: color ?? AppColors.surface,
        borderRadius: AppSpacing.roundedMd,
        border: Border.fromBorderSide(
          borderSide ?? const BorderSide(color: AppColors.border),
        ),
      ),
      child: Padding(
        padding: padding ?? const EdgeInsets.all(AppSpacing.xl),
        child: child,
      ),
    );

    if (onTap != null) {
      return InkWell(
        onTap: onTap,
        borderRadius: AppSpacing.roundedMd,
        child: card,
      );
    }

    return card;
  }
}
