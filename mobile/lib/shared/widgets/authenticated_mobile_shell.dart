import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../features/auth/providers/auth_provider.dart';

/// Navigation item definition for authenticated mobile shells.
class MobileNavDestination {
  final String label;
  final IconData icon;
  final IconData selectedIcon;
  final String route;
  final Key? key;

  const MobileNavDestination({
    required this.label,
    required this.icon,
    required this.selectedIcon,
    required this.route,
    this.key,
  });
}

/// Helper function to open the authenticated user account modal sheet.
void showAccountBottomSheet(BuildContext context, WidgetRef ref) {
  final authState = ref.read(authProvider);
  final user = authState.user;

  String getInitials(String? name) {
    if (name == null || name.trim().isEmpty) return 'U';
    final parts = name.trim().split(RegExp(r'\s+'));
    if (parts.length == 1) return parts.first[0].toUpperCase();
    return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
  }

  showModalBottomSheet<void>(
    context: context,
    backgroundColor: AppColors.surface,
    isScrollControlled: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(AppSpacing.radiusLg)),
    ),
    builder: (sheetContext) {
      return SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.xl,
            vertical: AppSpacing.md,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              // Drag Handle
              Container(
                width: 36,
                height: 4,
                margin: const EdgeInsets.only(bottom: AppSpacing.lg),
                decoration: BoxDecoration(
                  color: AppColors.border,
                  borderRadius: AppSpacing.roundedFull,
                ),
              ),

              // User Info Card
              Row(
                children: [
                  CircleAvatar(
                    radius: 24,
                    backgroundColor: AppColors.primaryLight,
                    child: Text(
                      getInitials(user?.fullName),
                      style: const TextStyle(
                        color: AppColors.primaryDark,
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          user?.fullName ?? (user?.role ?? 'User'),
                          style: Theme.of(sheetContext).textTheme.titleMedium?.copyWith(
                                fontWeight: FontWeight.bold,
                                color: AppColors.textPrimary,
                              ),
                          overflow: TextOverflow.ellipsis,
                        ),
                        if (user?.email != null) ...[
                          const SizedBox(height: 2),
                          Text(
                            user!.email,
                            style: Theme.of(sheetContext).textTheme.bodySmall?.copyWith(
                                  color: AppColors.textSecondary,
                                ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ],
                      ],
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.primaryLight,
                      borderRadius: AppSpacing.roundedFull,
                      border: Border.all(color: AppColors.primaryBorder),
                    ),
                    child: Text(
                      user?.role ?? 'User',
                      style: const TextStyle(
                        color: AppColors.primaryDark,
                        fontSize: 12,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
              ),

              const SizedBox(height: AppSpacing.lg),
              const Divider(color: AppColors.border, height: 1),
              const SizedBox(height: AppSpacing.xs),

              // Account Actions
              ListTile(
                key: const Key('account_sheet_change_password'),
                leading: const Icon(Icons.lock_outline, color: AppColors.textPrimary),
                title: const Text(
                  'Change Password',
                  style: TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                contentPadding: EdgeInsets.zero,
                onTap: () {
                  Navigator.pop(sheetContext);
                  context.push('/change-password');
                },
              ),
              ListTile(
                key: const Key('account_sheet_logout'),
                leading: const Icon(Icons.logout, color: AppColors.error),
                title: const Text(
                  'Logout',
                  style: TextStyle(
                    color: AppColors.error,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                contentPadding: EdgeInsets.zero,
                onTap: () {
                  Navigator.pop(sheetContext);
                  ref.read(authProvider.notifier).logout();
                },
              ),
            ],
          ),
        ),
      );
    },
  );
}

/// Reusable authenticated mobile scaffold shell supporting configurable destinations,
/// app bar actions, and bottom navigation.
class AuthenticatedMobileShell extends ConsumerWidget {
  final Widget child;
  final String currentLocation;
  final List<MobileNavDestination> destinations;
  final String? title;
  final List<Widget>? actions;
  final bool showBottomNav;
  final Key? bottomNavKey;
  final String? notificationRoute;
  final Key? notificationKey;
  final Key? accountKey;
  final bool showNotificationAction;
  final String? accountRoute;
  final Color? backgroundColor;

  const AuthenticatedMobileShell({
    super.key,
    required this.child,
    required this.currentLocation,
    required this.destinations,
    this.title,
    this.actions,
    this.showBottomNav = true,
    this.bottomNavKey,
    this.notificationRoute,
    this.notificationKey,
    this.accountKey,
    this.showNotificationAction = true,
    this.accountRoute,
    this.backgroundColor,
  });

  int _calculateSelectedIndex() {
    final homeRoute = destinations.isNotEmpty ? destinations.first.route : '';
    for (int i = 0; i < destinations.length; i++) {
      final route = destinations[i].route;
      if (currentLocation == route) {
        return i;
      }
      if (route != homeRoute && currentLocation.startsWith(route)) {
        return i;
      }
    }
    return 0;
  }

  String _resolveTitle(int selectedIndex) {
    if (title != null) return title!;
    if (selectedIndex >= 0 && selectedIndex < destinations.length) {
      final homeRoute = destinations.isNotEmpty ? destinations.first.route : '';
      if (destinations[selectedIndex].route == homeRoute) {
        return 'SmartWaste';
      }
      return destinations[selectedIndex].label;
    }
    return 'SmartWaste';
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final selectedIndex = _calculateSelectedIndex();
    final effectiveTitle = _resolveTitle(selectedIndex);

    final defaultNotificationRoute = notificationRoute ??
        (currentLocation.startsWith('/driver')
            ? '/driver/notifications'
            : '/citizen/notifications');
    final defaultNotificationKey = notificationKey ??
        (currentLocation.startsWith('/driver')
            ? const Key('driver_notification_button')
            : const Key('citizen_notification_button'));
    final defaultAccountKey = accountKey ??
        (currentLocation.startsWith('/driver')
            ? const Key('driver_account_button')
            : const Key('citizen_account_button'));
    final effectiveBottomNavKey = bottomNavKey ??
        (currentLocation.startsWith('/driver')
            ? const Key('driver_bottom_nav')
            : const Key('citizen_bottom_nav'));

    final defaultActions = [
      if (showNotificationAction)
        IconButton(
          key: defaultNotificationKey,
          icon: const Icon(Icons.notifications_outlined),
          tooltip: 'Notifications',
          onPressed: () {
            context.push(defaultNotificationRoute);
          },
        ),
      IconButton(
        key: defaultAccountKey,
        icon: const Icon(Icons.account_circle_outlined),
        tooltip: 'Account',
        onPressed: () {
          if (accountRoute != null) {
            context.go(accountRoute!);
          } else {
            showAccountBottomSheet(context, ref);
          }
        },
      ),
      const SizedBox(width: AppSpacing.xs),
    ];

    final isHome = selectedIndex == 0;

    return PopScope(
      canPop: isHome,
      onPopInvokedWithResult: (didPop, result) {
        if (didPop) return;
        context.go(destinations.first.route);
      },
      child: Scaffold(
        backgroundColor: backgroundColor ?? AppColors.dashboardBackground,
        appBar: AppBar(
          backgroundColor: backgroundColor ?? AppColors.dashboardBackground,
          surfaceTintColor: Colors.transparent,
          centerTitle: false,
          titleSpacing: AppSpacing.md,
          title: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (effectiveTitle == 'SmartWaste') ...[
                const Icon(
                  Icons.eco_outlined,
                  color: AppColors.primary,
                  size: 22,
                ),
                const SizedBox(width: AppSpacing.xs),
              ],
              Flexible(
                child: Text(
                  effectiveTitle,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 18,
                  ),
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
          actions: actions ?? defaultActions,
        ),
        body: SafeArea(
          child: child,
        ),
        bottomNavigationBar: showBottomNav && destinations.isNotEmpty
            ? NavigationBar(
                key: effectiveBottomNavKey,
                selectedIndex: selectedIndex,
                indicatorColor: AppColors.primaryDark,
                onDestinationSelected: (index) {
                  if (index != selectedIndex && index < destinations.length) {
                    context.go(destinations[index].route);
                  }
                },
                destinations: destinations.map((d) {
                  return NavigationDestination(
                    key: d.key,
                    icon: Icon(d.icon),
                    selectedIcon: Icon(d.selectedIcon, color: Colors.white),
                    label: d.label,
                  );
                }).toList(),
              )
            : null,
      ),
    );
  }
}
