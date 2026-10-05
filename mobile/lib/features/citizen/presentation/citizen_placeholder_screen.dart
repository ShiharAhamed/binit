import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';

/// Reusable screen for Citizen feature destinations.
class CitizenPlaceholderScreen extends StatelessWidget {
  final String title;
  final String description;
  final IconData icon;
  final bool hasScaffold;
  final VoidCallback? onBack;

  const CitizenPlaceholderScreen({
    super.key,
    required this.title,
    required this.description,
    required this.icon,
    this.hasScaffold = false,
    this.onBack,
  });

  @override
  Widget build(BuildContext context) {
    final body = Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.xl,
          vertical: AppSpacing.xxl,
        ),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: AppCard(
            padding: const EdgeInsets.all(AppSpacing.xxl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // Icon Circle
                Container(
                  width: 80,
                  height: 80,
                  decoration: const BoxDecoration(
                    color: AppColors.primaryLight,
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    icon,
                    size: 40,
                    color: AppColors.primary,
                  ),
                ),
                const SizedBox(height: AppSpacing.lg),

                // Title
                Text(
                  title,
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                      ),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: AppSpacing.sm),

                // Description
                Text(
                  description,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: AppColors.textSecondary,
                        height: 1.5,
                      ),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: AppSpacing.xl),

                // Back Action
                AppButton.outlined(
                  key: const Key('placeholder_back_button'),
                  label: 'Back to Dashboard',
                  icon: Icons.arrow_back,
                  onPressed: onBack ?? () {
                    context.go('/citizen/dashboard');
                  },
                ),
              ],
            ),
          ),
        ),
      ),
    );

    if (hasScaffold) {
      return Scaffold(
        backgroundColor: AppColors.dashboardBackground,
        appBar: AppBar(
          backgroundColor: AppColors.dashboardBackground,
          surfaceTintColor: Colors.transparent,
          title: Text(title),
          leading: BackButton(
            onPressed: onBack ?? () {
              context.go('/citizen/dashboard');
            },
          ),
        ),
        body: SafeArea(child: body),
      );
    }

    return body;
  }
}
