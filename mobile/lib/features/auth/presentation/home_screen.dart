import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_alert.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../providers/auth_provider.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final authState = ref.watch(authProvider);
    final user = authState.user;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Smart Waste Management System'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Logout',
            onPressed: () {
              ref.read(authProvider.notifier).logout();
            },
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.xl,
              vertical: AppSpacing.lg,
            ),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      'Smart Waste Management System',
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: AppColors.primaryDark,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    Text(
                      'Welcome, ${user?.fullName ?? 'User'}',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Row(
                      children: [
                        Text(
                          'Role: ',
                          style: TextStyle(
                            fontSize: 15,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 10,
                            vertical: 3,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.primaryLight,
                            borderRadius: AppSpacing.roundedFull,
                            border: Border.all(color: AppColors.primaryBorder),
                          ),
                          child: Text(
                            user?.role ?? 'Unknown',
                            style: const TextStyle(
                              color: AppColors.primaryDark,
                              fontWeight: FontWeight.bold,
                              fontSize: 13,
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (user?.email != null) ...[
                      const SizedBox(height: AppSpacing.xs),
                      Text(
                        'Email: ${user!.email}',
                        style: TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 14,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.lg),
                    const AppAlert.info(
                      message: 'Mobile Authentication Foundation Ready.\n'
                          'Subsequent features (Waste Reporting, Bin Status, Tasks) '
                          'will be mounted here.',
                    ),
                    const SizedBox(height: AppSpacing.lg),
                    AppButton.outlined(
                      key: const Key('home_change_password_button'),
                      onPressed: () {
                        context.push('/change-password');
                      },
                      icon: Icons.lock_outline,
                      label: 'Change Password',
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton.destructive(
                      key: const Key('home_logout_button'),
                      onPressed: () {
                        ref.read(authProvider.notifier).logout();
                      },
                      icon: Icons.logout,
                      label: 'Logout',
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
