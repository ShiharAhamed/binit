import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../auth/providers/auth_provider.dart';

class CitizenProfileScreen extends ConsumerWidget {
  const CitizenProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authProvider).user;

    return Align(
      alignment: Alignment.topCenter,
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
                const CircleAvatar(
                  radius: 40,
                  backgroundColor: AppColors.primaryLight,
                  child: Icon(
                    Icons.person_outline,
                    size: 40,
                    color: AppColors.primary,
                  ),
                ),
                const SizedBox(height: AppSpacing.lg),
                Text(
                  'Profile',
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: AppColors.textPrimary,
                  ),
                ),
                if (user != null) ...[
                  const SizedBox(height: AppSpacing.xl),
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(AppSpacing.md),
                    decoration: BoxDecoration(
                      color: AppColors.surfaceSubtle,
                      borderRadius: AppSpacing.roundedMd,
                      border: Border.all(color: AppColors.border),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          user.fullName,
                          style: Theme.of(context).textTheme.titleMedium
                              ?.copyWith(
                                fontWeight: FontWeight.bold,
                                color: AppColors.textPrimary,
                              ),
                        ),
                        const SizedBox(height: AppSpacing.xxs),
                        Text(
                          user.email,
                          style: Theme.of(context).textTheme.bodyMedium
                              ?.copyWith(color: AppColors.textSecondary),
                        ),
                        const SizedBox(height: AppSpacing.xs),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: AppSpacing.xs,
                            vertical: AppSpacing.xxs,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.primaryLight,
                            borderRadius: AppSpacing.roundedFull,
                            border: Border.all(color: AppColors.primaryBorder),
                          ),
                          child: Text(
                            user.role,
                            style: Theme.of(context).textTheme.labelSmall
                                ?.copyWith(
                                  color: AppColors.primaryDark,
                                  fontWeight: FontWeight.bold,
                                ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                const SizedBox(height: AppSpacing.xl),
                AppButton.outlined(
                  key: const Key('citizen_profile_change_password_button'),
                  label: 'Change Password',
                  icon: Icons.lock_outline,
                  onPressed: () {
                    context.push('/change-password');
                  },
                ),
                const SizedBox(height: AppSpacing.sm),
                AppButton.outlined(
                  key: const Key('citizen_profile_back_button'),
                  label: 'Back to Dashboard',
                  icon: Icons.arrow_back,
                  onPressed: () {
                    context.go('/citizen/dashboard');
                  },
                ),
                const SizedBox(height: AppSpacing.sm),
                AppButton.destructive(
                  key: const Key('citizen_profile_logout_button'),
                  label: 'Logout',
                  icon: Icons.logout,
                  onPressed: () {
                    ref.read(authProvider.notifier).logout();
                  },
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
