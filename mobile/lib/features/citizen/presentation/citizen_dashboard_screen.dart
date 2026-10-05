import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../../auth/providers/auth_provider.dart';
import '../../reporting/data/reporting_repository.dart';
import '../../reporting/models/waste_report_list_item_model.dart';
import '../../reporting/models/waste_report_status.dart';
import '../../reporting/models/waste_type.dart';

/// Authenticated Citizen Dashboard — the primary landing screen for Citizen mobile users.
class CitizenDashboardScreen extends ConsumerStatefulWidget {
  final ReportingRepository? repository;

  const CitizenDashboardScreen({super.key, this.repository});

  @override
  ConsumerState<CitizenDashboardScreen> createState() => _CitizenDashboardScreenState();
}

class _CitizenDashboardScreenState extends ConsumerState<CitizenDashboardScreen> {
  final _recentActivityKey = GlobalKey<_RecentActivitySectionState>();

  Future<void> _refreshDashboard() async {
    await _recentActivityKey.currentState?._fetchRecentReports(isRefresh: true);
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);
    final user = authState.user;

    final firstName = user?.fullName.trim().split(RegExp(r'\s+')).first ?? 'Citizen';

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 540),
        child: RefreshIndicator(
          key: const Key('citizen_dashboard_refresh_indicator'),
          onRefresh: _refreshDashboard,
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.lg,
            vertical: AppSpacing.md,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // 1. Greeting & Context
              Text(
                'Hello, $firstName',
                key: const Key('citizen_greeting_text'),
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: AppColors.textPrimary,
                    ),
              ),
              const SizedBox(height: AppSpacing.xxs),
              Text(
                'Help keep your community clean and healthy.',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: AppColors.textSecondary,
                    ),
              ),
              const SizedBox(height: AppSpacing.lg),

              // 2. Primary Action Card — Report Waste (Strongest visual emphasis)
              _ReportWasteCtaCard(
                onTap: () {
                  context.push('/citizen/report-waste');
                },
              ),
              const SizedBox(height: AppSpacing.xl),

              // 3. Quick Access Section
              Text(
                'Quick Access',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: AppColors.textPrimary,
                    ),
              ),
              const SizedBox(height: AppSpacing.sm),
              _QuickAccessGrid(),
              const SizedBox(height: AppSpacing.xl),

              // 4. Recent Activity Section
              Text(
                'Recent Activity',
                key: const Key('citizen_recent_activity_section'),
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: AppColors.textPrimary,
                    ),
              ),
              const SizedBox(height: AppSpacing.sm),
              _RecentActivitySection(
                key: _recentActivityKey,
                repository: widget.repository,
              ),
              const SizedBox(height: AppSpacing.lg),
            ],
          ),
          ),
        ),
      ),
    );
  }
}

/// Prominent primary action card for reporting waste with location and photo.
class _ReportWasteCtaCard extends StatelessWidget {
  final VoidCallback onTap;

  const _ReportWasteCtaCard({required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
      child: InkWell(
        key: const Key('citizen_report_waste_cta'),
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.all(AppSpacing.xl),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [
                AppColors.primaryDark,
                AppColors.primaryHover,
              ],
            ),
            borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
            boxShadow: [
              BoxShadow(
                color: AppColors.primaryDark.withValues(alpha: 0.25),
                blurRadius: 12,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    padding: const EdgeInsets.all(AppSpacing.sm),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.15),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.add_a_photo_outlined,
                      color: Colors.white,
                      size: 26,
                    ),
                  ),
                  const Spacer(),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.primaryAccent.withValues(alpha: 0.25),
                      borderRadius: AppSpacing.roundedFull,
                      border: Border.all(
                        color: Colors.white.withValues(alpha: 0.2),
                      ),
                    ),
                    child: const Text(
                      'Primary Action',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.md),
              const Text(
                'Report Waste',
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                  letterSpacing: -0.3,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),
              const Text(
                'Report waste using a location, description and photo.',
                style: TextStyle(
                  color: Color(0xFFD1FAE5), // emerald-100
                  fontSize: 14,
                  height: 1.4,
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              LayoutBuilder(
                builder: (context, constraints) {
                  final actionButton = Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.xs + 2,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: AppSpacing.roundedFull,
                    ),
                    child: const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'Report Issue',
                          style: TextStyle(
                            color: AppColors.primaryDark,
                            fontWeight: FontWeight.bold,
                            fontSize: 13,
                          ),
                        ),
                        SizedBox(width: AppSpacing.xs),
                        Icon(
                          Icons.arrow_forward,
                          size: 16,
                          color: AppColors.primaryDark,
                        ),
                      ],
                    ),
                  );

                  final illustration = Image.asset(
                    'assets/images/waste_report_illustration.png',
                    key: const Key('citizen_report_waste_illustration'),
                    fit: BoxFit.contain,
                    alignment: Alignment.bottomRight,
                    excludeFromSemantics: true,
                  );

                  final isVeryNarrow = constraints.maxWidth < 230;
                  if (isVeryNarrow) {
                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        actionButton,
                        const SizedBox(height: AppSpacing.sm),
                        Align(
                          alignment: Alignment.bottomRight,
                          child: ConstrainedBox(
                            constraints: const BoxConstraints(
                              maxWidth: 120.0,
                              maxHeight: 50.0,
                            ),
                            child: illustration,
                          ),
                        ),
                      ],
                    );
                  }

                  return Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      actionButton,
                      const SizedBox(width: AppSpacing.xs),
                      Expanded(
                        child: Align(
                          alignment: Alignment.bottomRight,
                          child: ConstrainedBox(
                            constraints: BoxConstraints(
                              maxWidth: (constraints.maxWidth * 0.44).clamp(70.0, 160.0),
                              maxHeight: constraints.maxWidth < 280 ? 52.0 : 72.0,
                            ),
                            child: illustration,
                          ),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// 2-column responsive quick access grid for main citizen workflows.
class _QuickAccessGrid extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final items = [
      _QuickAccessCard(
        key: const Key('citizen_quick_access_reports'),
        title: 'My Reports',
        description: 'Track your submitted waste reports.',
        icon: Icons.assignment_outlined,
        onTap: () {
          context.go('/citizen/reports');
        },
      ),
      _QuickAccessCard(
        key: const Key('citizen_quick_access_bins'),
        title: 'Nearby Bins',
        description: 'Find waste bins near your location.',
        icon: Icons.delete_outline,
        onTap: () {
          context.push('/citizen/nearby-bins');
        },
      ),
      _QuickAccessCard(
        key: const Key('citizen_quick_access_complaints'),
        title: 'Complaints',
        description: 'Report or track service concerns.',
        icon: Icons.feedback_outlined,
        onTap: () {
          context.go('/citizen/complaints');
        },
      ),
      _QuickAccessCard(
        key: const Key('citizen_quick_access_profile'),
        title: 'Profile',
        description: 'Manage your account information.',
        icon: Icons.person_outline,
        onTap: () {
          context.go('/citizen/profile');
        },
      ),
    ];

    return LayoutBuilder(
      builder: (context, constraints) {
        final isNarrow = constraints.maxWidth < 310;
        if (isNarrow) {
          return Column(
            children: items
                .map(
                  (item) => Padding(
                    padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                    child: item,
                  ),
                )
                .toList(),
          );
        }

        final rows = <Widget>[];
        for (int i = 0; i < items.length; i += 2) {
          if (i + 1 < items.length) {
            rows.add(
              Row(
                children: [
                  Expanded(child: items[i]),
                  const SizedBox(width: AppSpacing.md),
                  Expanded(child: items[i + 1]),
                ],
              ),
            );
          } else {
            rows.add(
              Row(
                children: [
                  Expanded(child: items[i]),
                  const SizedBox(width: AppSpacing.md),
                  const Expanded(child: SizedBox.shrink()),
                ],
              ),
            );
          }
          if (i + 2 < items.length) {
            rows.add(const SizedBox(height: AppSpacing.md));
          }
        }

        return Column(
          children: rows,
        );
      },
    );
  }
}

/// Touch-friendly quick access tile.
class _QuickAccessCard extends StatelessWidget {
  final String title;
  final String description;
  final IconData icon;
  final VoidCallback onTap;

  const _QuickAccessCard({
    super.key,
    required this.title,
    required this.description,
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: const EdgeInsets.all(AppSpacing.md),
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            padding: const EdgeInsets.all(AppSpacing.xs),
            decoration: const BoxDecoration(
              color: AppColors.primaryLight,
              shape: BoxShape.circle,
            ),
            child: Icon(
              icon,
              color: AppColors.primary,
              size: 20,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            title,
            style: const TextStyle(
              fontWeight: FontWeight.w600,
              fontSize: 14,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.xxs),
          Text(
            description,
            style: const TextStyle(
              fontSize: 12,
              color: AppColors.textSecondary,
              height: 1.3,
            ),
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }
}

/// Recent Activity section rendering the latest 3 waste reports for the Citizen.
class _RecentActivitySection extends ConsumerStatefulWidget {
  final ReportingRepository? repository;

  const _RecentActivitySection({super.key, this.repository});

  @override
  ConsumerState<_RecentActivitySection> createState() => _RecentActivitySectionState();
}

class _RecentActivitySectionState extends ConsumerState<_RecentActivitySection> {
  bool _isLoading = true;
  bool _hasLoadedData = false;
  String? _errorMessage;
  List<WasteReportListItemModel> _recentReports = [];

  @override
  void initState() {
    super.initState();
    _fetchRecentReports();
  }

  Future<void> _fetchRecentReports({bool isRefresh = false}) async {
    if (!mounted) return;
    if (!isRefresh || !_hasLoadedData) {
      setState(() {
        _isLoading = true;
        _errorMessage = null;
      });
    }

    try {
      final ReportingRepository repository = widget.repository ?? ref.read(reportingRepositoryProvider);
      final response = await repository.getWasteReports(
        page: 1,
        pageSize: 3,
        sortBy: 'createdAt',
        sortDirection: 'desc',
      );

      if (mounted) {
        setState(() {
          _recentReports = response.items.take(3).toList();
          _hasLoadedData = true;
          _errorMessage = null;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          if (!_hasLoadedData) {
            _errorMessage = "Couldn't load recent activity.";
          }
          _isLoading = false;
        });
        if (isRefresh && _hasLoadedData) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text("Couldn't load recent activity.")),
          );
        }
      }
    }
  }

  String _formatSubmissionTime(DateTime dateTime) {
    final local = dateTime.toLocal();
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final date = DateTime(local.year, local.month, local.day);
    final timeStr = DateFormat('h:mm a').format(local);

    final diffDays = today.difference(date).inDays;
    if (diffDays == 0) {
      return 'Today • $timeStr';
    } else if (diffDays == 1) {
      return 'Yesterday • $timeStr';
    } else if (local.year == now.year) {
      return '${DateFormat('d MMM').format(local)} • $timeStr';
    } else {
      return '${DateFormat('d MMM yyyy').format(local)} • $timeStr';
    }
  }

  Widget _buildWasteTypeIcon(WasteType type) {
    IconData icon;
    switch (type) {
      case WasteType.organic:
        icon = Icons.eco_outlined;
        break;
      case WasteType.recyclable:
        icon = Icons.recycling_outlined;
        break;
      case WasteType.hazardous:
        icon = Icons.warning_amber_rounded;
        break;
      case WasteType.bulky:
        icon = Icons.inventory_2_outlined;
        break;
      case WasteType.general:
      case WasteType.other:
        icon = Icons.delete_outline;
        break;
    }
    return Icon(icon, size: 15, color: AppColors.primaryDark);
  }

  Widget _buildStatusBadge(WasteReportStatus status) {
    Color bg;
    Color fg;
    Color border;

    switch (status) {
      case WasteReportStatus.verified:
        bg = const Color(0xFFDCFCE7); // emerald-100
        fg = const Color(0xFF065F46); // emerald-800
        border = const Color(0xFFA7F3D0); // emerald-200
        break;
      case WasteReportStatus.resolved:
        bg = const Color(0xFFD1FAE5); // emerald-100
        fg = const Color(0xFF047857); // emerald-800
        border = const Color(0xFFA7F3D0); // emerald-200
        break;
      case WasteReportStatus.submitted:
        bg = const Color(0xFFFEF3C7); // amber-100
        fg = const Color(0xFF92400E); // amber-800
        border = const Color(0xFFFDE68A); // amber-200
        break;
      case WasteReportStatus.underReview:
        bg = const Color(0xFFFEF9C3); // yellow-100
        fg = const Color(0xFF854D0E); // yellow-800
        border = const Color(0xFFFEF08A); // yellow-200
        break;
      case WasteReportStatus.scheduled:
        bg = const Color(0xFFFFFBEB); // amber-50
        fg = const Color(0xFFB45309); // amber-700
        border = const Color(0xFFFDE68A); // amber-200
        break;
      case WasteReportStatus.inProgress:
        bg = const Color(0xFFFFEDD5); // orange-100
        fg = const Color(0xFF9A3412); // orange-800
        border = const Color(0xFFFED7AA); // orange-200
        break;
      case WasteReportStatus.rejected:
        bg = const Color(0xFFFEE2E2); // red-100
        fg = const Color(0xFF991B1B); // red-800
        border = const Color(0xFFFECACA); // red-200
        break;
      case WasteReportStatus.cancelled:
        bg = const Color(0xFFF1F5F9); // slate-100
        fg = const Color(0xFF475569); // slate-600
        border = const Color(0xFFCBD5E1); // slate-300
        break;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: AppSpacing.roundedFull,
        border: Border.all(color: border, width: 1),
      ),
      child: Text(
        status.displayName,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: fg,
        ),
      ),
    );
  }

  Widget _buildActivityRow(WasteReportListItemModel report) {
    return InkWell(
      key: Key('recent_activity_item_${report.id}'),
      onTap: () async {
        await context.push('/citizen/reports/${report.id}');
        if (mounted) {
          _fetchRecentReports();
        }
      },
      borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header row: Waste type badge/info + Status badge
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Flexible(
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      padding: const EdgeInsets.all(4),
                      decoration: const BoxDecoration(
                        color: AppColors.primaryLight,
                        shape: BoxShape.circle,
                      ),
                      child: _buildWasteTypeIcon(report.wasteType),
                    ),
                    const SizedBox(width: 8),
                    Flexible(
                      child: Text(
                        report.wasteType.displayName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              _buildStatusBadge(report.status),
            ],
          ),

          // Optional address row
          if (report.addressText != null && report.addressText!.trim().isNotEmpty) ...[
            const SizedBox(height: 4),
            Row(
              children: [
                const Icon(
                  Icons.place_outlined,
                  size: 13,
                  color: AppColors.textSecondary,
                ),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    report.addressText!.trim(),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
          ],

          // Submission date/time row
          const SizedBox(height: 4),
          Row(
            children: [
              const Icon(
                Icons.schedule_outlined,
                size: 13,
                color: AppColors.textMuted,
              ),
              const SizedBox(width: 4),
              Expanded(
                child: Text(
                  _formatSubmissionTime(report.createdAt),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 11,
                    color: AppColors.textMuted,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    ),
  );
}

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return AppCard(
        key: const Key('citizen_recent_activity_loading'),
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.xl,
        ),
        child: const Center(
          child: AppLoadingIndicator(
            size: 24.0,
            strokeWidth: 2.5,
            message: 'Loading recent activity...',
          ),
        ),
      );
    }

    if (_errorMessage != null) {
      return AppCard(
        key: const Key('citizen_recent_activity_error'),
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.error_outline_rounded,
                size: 28,
                color: AppColors.error,
              ),
              const SizedBox(height: AppSpacing.xs),
              const Text(
                "Couldn't load recent activity.",
                style: TextStyle(
                  fontSize: 13,
                  color: AppColors.textSecondary,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: AppSpacing.sm),
              AppButton.outlined(
                key: const Key('citizen_recent_activity_retry_button'),
                label: 'Retry',
                icon: Icons.refresh_rounded,
                height: 38.0,
                isFullWidth: false,
                onPressed: _fetchRecentReports,
              ),
            ],
          ),
        ),
      );
    }

    if (_recentReports.isEmpty) {
      return AppCard(
        key: const Key('citizen_recent_activity_empty'),
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.lg,
        ),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                padding: const EdgeInsets.all(AppSpacing.sm),
                decoration: const BoxDecoration(
                  color: AppColors.surfaceSubtle,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.inbox_outlined,
                  size: 24,
                  color: AppColors.textMuted,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),
              const Text(
                'No recent reports yet.',
                style: TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 14,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppSpacing.xxs),
              const Text(
                'Submit a waste report to see activity here.',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 12,
                  color: AppColors.textSecondary,
                  height: 1.4,
                ),
              ),
            ],
          ),
        ),
      );
    }

    return AppCard(
      key: const Key('citizen_recent_activity_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (int i = 0; i < _recentReports.length; i++) ...[
            if (i > 0) const Divider(height: 1, color: AppColors.border),
            _buildActivityRow(_recentReports[i]),
          ],
          const SizedBox(height: AppSpacing.xs),
          const Divider(height: 1, color: AppColors.border),
          const SizedBox(height: AppSpacing.sm),
          AppButton.outlined(
            key: const Key('citizen_view_all_reports_button'),
            label: 'View All Reports',
            icon: Icons.arrow_forward,
            height: 40.0,
            onPressed: () {
              context.go('/citizen/reports');
            },
          ),
        ],
      ),
    );
  }
}
