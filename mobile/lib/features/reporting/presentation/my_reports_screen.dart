import 'dart:async';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../data/reporting_repository.dart';
import '../models/paged_waste_reports_model.dart';
import '../models/report_reference.dart';
import '../models/waste_report_list_item_model.dart';
import '../models/waste_report_status.dart';
import '../models/waste_type.dart';

/// Citizen "My Reports" list screen (Step 9A.9.1).
/// Displays a paginated, searchable, status-filtered list of waste reports
/// authoritatively scoped to the authenticated Citizen.
class MyReportsScreen extends StatefulWidget {
  final ReportingRepository? repository;
  final bool showAppBar;

  const MyReportsScreen({
    super.key,
    this.repository,
    this.showAppBar = false,
  });

  @override
  State<MyReportsScreen> createState() => MyReportsScreenState();
}

class MyReportsScreenState extends State<MyReportsScreen> {
  late final ReportingRepository _repository;
  final ScrollController _scrollController = ScrollController();
  final TextEditingController _searchController = TextEditingController();
  Timer? _debounceTimer;

  List<WasteReportListItemModel> _reports = [];
  bool _isLoadingInitial = true;
  bool _isLoadingMore = false;
  bool _isRefreshing = false;
  String? _errorMessage;
  String? _loadMoreErrorMessage;

  int _currentPage = 1;
  int _totalPages = 1;
  int _totalCount = 0;
  bool _hasMore = false;
  WasteReportStatus? _selectedStatus;

  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? ReportingRepository();
    _scrollController.addListener(_onScroll);
    _fetchReports(page: 1);
  }

  @override
  void dispose() {
    _debounceTimer?.cancel();
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    _searchController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (!_scrollController.hasClients) return;
    final maxScroll = _scrollController.position.maxScrollExtent;
    final currentScroll = _scrollController.position.pixels;
    if (maxScroll - currentScroll <= 200) {
      if (_hasMore && !_isLoadingMore && !_isLoadingInitial && !_isRefreshing) {
        _fetchReports(page: _currentPage + 1, isLoadMore: true);
      }
    }
  }

  void _onSearchChanged(String query) {
    _debounceTimer?.cancel();
    _debounceTimer = Timer(const Duration(milliseconds: 400), () {
      if (mounted) {
        _fetchReports(page: 1);
      }
    });
    setState(() {}); // Updates clear icon visibility
  }

  void _onClearSearch() {
    _debounceTimer?.cancel();
    _searchController.clear();
    _fetchReports(page: 1);
    setState(() {});
  }

  void _onStatusFilterSelected(WasteReportStatus? status) {
    if (_selectedStatus == status) return;
    setState(() {
      _selectedStatus = status;
    });
    _fetchReports(page: 1);
  }

  Future<void> _onRefresh() async {
    await _fetchReports(page: 1, isRefresh: true);
  }

  Future<void> _fetchReports({
    required int page,
    bool isRefresh = false,
    bool isLoadMore = false,
  }) async {
    if (isLoadMore) {
      if (_isLoadingMore) return;
      setState(() {
        _isLoadingMore = true;
        _loadMoreErrorMessage = null;
      });
    } else if (isRefresh) {
      setState(() {
        _isRefreshing = true;
      });
    } else {
      setState(() {
        _isLoadingInitial = true;
        _errorMessage = null;
      });
    }

    try {
      final search = _searchController.text.trim().isEmpty ? null : _searchController.text.trim();
      final PagedWasteReportsModel result = await _repository.getWasteReports(
        page: page,
        pageSize: 20,
        status: _selectedStatus,
        search: search,
        sortBy: 'createdAt',
        sortDirection: 'desc',
      );

      if (!mounted) return;

      setState(() {
        if (isLoadMore) {
          // Deduplicate by ID in case backend had new insertions
          final existingIds = _reports.map((r) => r.id).toSet();
          final newItems = result.items.where((r) => !existingIds.contains(r.id)).toList();
          _reports = [..._reports, ...newItems];
        } else {
          _reports = result.items;
        }

        _currentPage = result.page;
        _totalPages = result.totalPages;
        _totalCount = result.totalCount;
        _hasMore = result.hasMore;
        _errorMessage = null;
        _loadMoreErrorMessage = null;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        if (isLoadMore) {
          _loadMoreErrorMessage = e.message;
        } else {
          _errorMessage = e.message;
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        if (isLoadMore) {
          _loadMoreErrorMessage = 'Unable to load more reports. Tap to retry.';
        } else {
          _errorMessage = 'Unable to load reports. Please check your connection and try again.';
        }
      });
    } finally {
      if (mounted) {
        setState(() {
          _isLoadingInitial = false;
          _isLoadingMore = false;
          _isRefreshing = false;
        });
      }
    }
  }

  bool get _isFiltered =>
      _searchController.text.trim().isNotEmpty || _selectedStatus != null;

  @override
  Widget build(BuildContext context) {
    final content = Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Subtitle information area beneath AppBar (No duplicate "My Reports" heading)
          Container(
            padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.md, 0),
            color: AppColors.surface,
            child: const Text(
              'Track your submitted waste reports',
              style: TextStyle(
                fontSize: 13,
                color: AppColors.textSecondary,
              ),
            ),
          ),

          // Search and Status Selector Container
          Container(
            padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.xs, AppSpacing.md, AppSpacing.sm),
            decoration: const BoxDecoration(
              color: AppColors.surface,
              border: Border(
                bottom: BorderSide(color: AppColors.border, width: 1),
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // 1. Search Bar
                TextField(
                  key: const Key('search_reports_input'),
                  controller: _searchController,
                  onChanged: _onSearchChanged,
                  style: const TextStyle(fontSize: 14, color: AppColors.textPrimary),
                  decoration: InputDecoration(
                    hintText: 'Search by description or location...',
                    hintStyle: const TextStyle(fontSize: 13, color: AppColors.textMuted),
                    filled: true,
                    fillColor: AppColors.background,
                    prefixIcon: const Icon(Icons.search_rounded, size: 20, color: AppColors.textSecondary),
                    suffixIcon: _searchController.text.isNotEmpty
                        ? IconButton(
                            key: const Key('clear_search_button'),
                            icon: const Icon(Icons.cancel_rounded, size: 18, color: AppColors.textMuted),
                            onPressed: _onClearSearch,
                          )
                        : null,
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.sm,
                    ),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                      borderSide: const BorderSide(color: AppColors.border),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                      borderSide: const BorderSide(color: AppColors.border),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                      borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
                    ),
                  ),
                ),

                const SizedBox(height: AppSpacing.xs),

                // 2. Compact, mobile-friendly Status Selector
                _buildStatusSelector(),
              ],
            ),
          ),

          // 3. Body Content (Reports Feed, Loading, Empty, or Error)
          Expanded(
            child: _buildBody(),
          ),
        ],
      );

    if (widget.showAppBar) {
      return Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          title: const Text('My Reports'),
          centerTitle: true,
          backgroundColor: AppColors.surface,
          surfaceTintColor: Colors.transparent,
          elevation: 0,
          bottom: const PreferredSize(
            preferredSize: Size.fromHeight(1),
            child: Divider(height: 1, color: AppColors.border),
          ),
        ),
        body: content,
      );
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      body: content,
    );
  }

  Widget _buildStatusSelector() {
    final hasActiveFilter = _selectedStatus != null;
    final displayLabel = _selectedStatus?.displayName ?? 'All Reports';

    return Material(
      color: Colors.transparent,
      child: InkWell(
        key: const Key('status_filter_selector'),
        onTap: () => _showStatusFilterBottomSheet(context),
        borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.md,
            vertical: 10,
          ),
          decoration: BoxDecoration(
            color: hasActiveFilter ? AppColors.primaryLight.withValues(alpha: 0.4) : AppColors.surface,
            borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
            border: Border.all(
              color: hasActiveFilter ? AppColors.primary : AppColors.border,
              width: hasActiveFilter ? 1.5 : 1,
            ),
          ),
          child: Row(
            children: [
              Icon(
                Icons.tune_rounded,
                size: 18,
                color: hasActiveFilter ? AppColors.primaryDark : AppColors.textSecondary,
              ),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: Row(
                  children: [
                    Text(
                      'Status: ',
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w500,
                        color: hasActiveFilter ? AppColors.primaryDark : AppColors.textSecondary,
                      ),
                    ),
                    Flexible(
                      child: Text(
                        displayLabel,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: hasActiveFilter ? FontWeight.bold : FontWeight.w600,
                          color: hasActiveFilter ? AppColors.primaryDark : AppColors.textPrimary,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              Icon(
                Icons.keyboard_arrow_down_rounded,
                size: 20,
                color: hasActiveFilter ? AppColors.primaryDark : AppColors.textSecondary,
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showStatusFilterBottomSheet(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      backgroundColor: AppColors.surface,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(AppSpacing.radiusLg)),
      ),
      builder: (bottomSheetContext) {
        return SafeArea(
          child: ConstrainedBox(
            constraints: BoxConstraints(
              maxHeight: MediaQuery.of(context).size.height * 0.75,
            ),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.md, AppSpacing.md),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Drag Handle
                  Center(
                    child: Container(
                      width: 36,
                      height: 4,
                      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                      decoration: BoxDecoration(
                        color: AppColors.border,
                        borderRadius: AppSpacing.roundedFull,
                      ),
                    ),
                  ),

                  // Header
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Filter by Status',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      IconButton(
                        icon: const Icon(Icons.close, size: 20, color: AppColors.textSecondary),
                        onPressed: () => Navigator.pop(bottomSheetContext),
                        padding: EdgeInsets.zero,
                        constraints: const BoxConstraints(),
                      ),
                    ],
                  ),
                  const SizedBox(height: 2),
                  const Text(
                    'Select a report status to filter your list',
                    style: TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),

                  const SizedBox(height: AppSpacing.xs),
                  const Divider(color: AppColors.border, height: 1),
                  const SizedBox(height: AppSpacing.xs),

                  // Vertical list of statuses
                  Flexible(
                    child: SingleChildScrollView(
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          _buildStatusOptionTile(
                            context: bottomSheetContext,
                            label: 'All Reports',
                            isSelected: _selectedStatus == null,
                            key: const Key('status_option_all'),
                            onTap: () {
                              Navigator.pop(bottomSheetContext);
                              _onStatusFilterSelected(null);
                            },
                          ),
                          ...WasteReportStatus.values.map((status) {
                            return _buildStatusOptionTile(
                              context: bottomSheetContext,
                              label: status.displayName,
                              status: status,
                              isSelected: _selectedStatus == status,
                              key: Key('status_option_${status.value.toLowerCase()}'),
                              onTap: () {
                                Navigator.pop(bottomSheetContext);
                                _onStatusFilterSelected(status);
                              },
                            );
                          }),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  Widget _buildStatusOptionTile({
    required BuildContext context,
    required String label,
    required bool isSelected,
    required Key key,
    required VoidCallback onTap,
    WasteReportStatus? status,
  }) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Material(
        color: isSelected ? AppColors.primaryLight : Colors.transparent,
        borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
        child: InkWell(
          key: key,
          onTap: onTap,
          borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.sm,
              vertical: 10,
            ),
            child: Row(
              children: [
                if (status != null)
                  _buildStatusDot(status)
                else
                  Container(
                    width: 8,
                    height: 8,
                    margin: const EdgeInsets.only(right: AppSpacing.sm),
                    decoration: const BoxDecoration(
                      color: AppColors.textSecondary,
                      shape: BoxShape.circle,
                    ),
                  ),
                const SizedBox(width: AppSpacing.xs),
                Expanded(
                  child: Text(
                    label,
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: isSelected ? FontWeight.bold : FontWeight.w500,
                      color: isSelected ? AppColors.primaryDark : AppColors.textPrimary,
                    ),
                  ),
                ),
                if (isSelected)
                  const Icon(
                    Icons.check_circle_rounded,
                    size: 18,
                    color: AppColors.primaryDark,
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildStatusDot(WasteReportStatus status) {
    Color dotColor;
    switch (status) {
      case WasteReportStatus.verified:
      case WasteReportStatus.resolved:
        dotColor = const Color(0xFF059669); // emerald-600
        break;
      case WasteReportStatus.submitted:
      case WasteReportStatus.underReview:
      case WasteReportStatus.scheduled:
      case WasteReportStatus.inProgress:
        dotColor = const Color(0xFFD97706); // amber-600
        break;
      case WasteReportStatus.rejected:
        dotColor = const Color(0xFFDC2626); // red-600
        break;
      case WasteReportStatus.cancelled:
        dotColor = const Color(0xFF64748B); // slate-500
        break;
    }

    return Container(
      width: 8,
      height: 8,
      margin: const EdgeInsets.only(right: AppSpacing.sm),
      decoration: BoxDecoration(
        color: dotColor,
        shape: BoxShape.circle,
      ),
    );
  }

  Widget _buildBody() {
    if (_isLoadingInitial) {
      return const Center(
        child: AppLoadingIndicator(message: 'Loading reports...'),
      );
    }

    if (_errorMessage != null && _reports.isEmpty) {
      return _buildErrorState();
    }

    if (_reports.isEmpty) {
      return _buildEmptyState();
    }

    return RefreshIndicator(
      onRefresh: _onRefresh,
      color: AppColors.primary,
      child: ListView.separated(
        key: const Key('my_reports_list_view'),
        controller: _scrollController,
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: _reports.length + (_hasMore || _isLoadingMore || _loadMoreErrorMessage != null ? 1 : 0),
        separatorBuilder: (context, index) => const SizedBox(height: AppSpacing.sm),
        itemBuilder: (context, index) {
          if (index == _reports.length) {
            return _buildPaginationFooter();
          }
          return _buildReportCard(_reports[index]);
        },
      ),
    );
  }

  Widget _buildReportCard(WasteReportListItemModel report) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        key: Key('report_card_${report.id}'),
        onTap: () async {
          await context.push('/citizen/reports/${report.id}');
          if (mounted) {
            _fetchReports(page: 1, isRefresh: true);
          }
        },
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
            border: Border.all(color: AppColors.border),
            boxShadow: const [
              BoxShadow(
                color: Color(0x08000000),
                blurRadius: 6,
                offset: Offset(0, 2),
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header Row: Waste Type Category Badge + Status Badge
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Flexible(
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: AppColors.primaryLight,
                    borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                    border: Border.all(color: AppColors.primaryBorder),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _buildWasteTypeIcon(report.wasteType),
                      const SizedBox(width: 4),
                      Flexible(
                        child: Text(
                          report.wasteType.displayName,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: AppColors.primaryDark,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              _buildStatusBadge(report.status),
            ],
          ),

          const SizedBox(height: AppSpacing.sm),

          Text(
            reportLabel(report.id, authoritative: report.reportReference),
            key: Key('report_reference_${report.id}'),
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              color: AppColors.primaryDark,
            ),
          ),

          const SizedBox(height: AppSpacing.xs),

          // Description Preview
          Text(
            report.description,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w500,
              color: AppColors.textPrimary,
              height: 1.35,
            ),
          ),

          const SizedBox(height: AppSpacing.xs),

          // Location / Address Row
          Row(
            children: [
              const Icon(
                Icons.place_outlined,
                size: 14,
                color: AppColors.textSecondary,
              ),
              const SizedBox(width: 4),
              Expanded(
                child: Text(
                  report.addressText != null && report.addressText!.trim().isNotEmpty
                      ? report.addressText!.trim()
                      : '${report.latitude.toStringAsFixed(4)}, ${report.longitude.toStringAsFixed(4)}',
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

          const SizedBox(height: 3),

          // Date / Time
          Row(
            children: [
              const Icon(
                Icons.schedule_outlined,
                size: 14,
                color: AppColors.textMuted,
              ),
              const SizedBox(width: 4),
              Expanded(
                child: Text(
                  _dateFormat.format(report.createdAt.toLocal()),
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

          // Photo count badge if attachments exist
          if (report.attachmentCount > 0) ...[
            const SizedBox(height: AppSpacing.xs),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
              decoration: BoxDecoration(
                color: AppColors.background,
                borderRadius: AppSpacing.roundedFull,
                border: Border.all(color: AppColors.border),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(
                    Icons.photo_camera_outlined,
                    size: 13,
                    color: AppColors.textSecondary,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    '${report.attachmentCount} ${report.attachmentCount == 1 ? 'photo' : 'photos'}',
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    ),
  ),
);
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
    return Icon(icon, size: 14, color: AppColors.primaryDark);
  }

  Widget _buildStatusBadge(WasteReportStatus status) {
    Color bg;
    Color fg;
    Color border;

    switch (status) {
      // Primary / green family
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

      // Soft amber / warm accent family
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

      // Muted red family
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

  Widget _buildPaginationFooter() {
    if (_isLoadingMore) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.md),
        child: Center(
          child: SizedBox(
            width: 24,
            height: 24,
            child: CircularProgressIndicator(strokeWidth: 2),
          ),
        ),
      );
    }

    if (_loadMoreErrorMessage != null) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.sm),
          decoration: BoxDecoration(
            color: AppColors.errorLight,
            borderRadius: AppSpacing.roundedSm,
            border: Border.all(color: AppColors.errorBorder),
          ),
          child: Row(
            children: [
              const Icon(Icons.warning_amber_rounded, size: 16, color: AppColors.error),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: Text(
                  _loadMoreErrorMessage!,
                  style: const TextStyle(fontSize: 12, color: AppColors.errorText),
                ),
              ),
              TextButton(
                key: const Key('retry_load_more_button'),
                onPressed: () => _fetchReports(page: _currentPage + 1, isLoadMore: true),
                child: const Text('Retry', style: TextStyle(fontSize: 12, color: AppColors.primary)),
              ),
            ],
          ),
        ),
      );
    }

    if (_hasMore) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
        child: Center(
          child: TextButton.icon(
            key: const Key('load_more_button'),
            onPressed: () => _fetchReports(page: _currentPage + 1, isLoadMore: true),
            icon: const Icon(Icons.expand_more, size: 18),
            label: const Text('Load More'),
          ),
        ),
      );
    }

    return const SizedBox.shrink();
  }

  Widget _buildEmptyState() {
    final isFiltered = _isFiltered;

    return Center(
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.xxl),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              padding: const EdgeInsets.all(AppSpacing.lg),
              decoration: BoxDecoration(
                color: isFiltered ? AppColors.surface : AppColors.primaryLight,
                shape: BoxShape.circle,
              ),
              child: Icon(
                isFiltered ? Icons.search_off_outlined : Icons.assignment_outlined,
                size: 48,
                color: isFiltered ? AppColors.textSecondary : AppColors.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            Text(
              isFiltered ? 'No matching reports' : 'No reports yet',
              key: isFiltered
                  ? const Key('filtered_empty_state_title')
                  : const Key('empty_reports_state_title'),
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              isFiltered
                  ? 'Try changing your search or filter.'
                  : "You haven't submitted any waste reports.",
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 14,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            if (isFiltered)
              AppButton.text(
                key: const Key('clear_filters_button'),
                label: 'Clear Filters',
                onPressed: () {
                  _searchController.clear();
                  _selectedStatus = null;
                  _fetchReports(page: 1);
                },
              )
            else
              AppButton.primary(
                key: const Key('empty_state_report_waste_cta'),
                label: 'Report Waste',
                icon: Icons.add_a_photo_outlined,
                onPressed: () {
                  context.push('/citizen/report-waste');
                },
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildErrorState() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xxl),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(
              Icons.error_outline,
              size: 48,
              color: AppColors.error,
            ),
            const SizedBox(height: AppSpacing.md),
            const Text(
              'Unable to Load Reports',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              _errorMessage ?? 'Please check your connection and try again.',
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 14,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            AppButton.primary(
              key: const Key('retry_load_reports_button'),
              label: 'Retry',
              onPressed: () => _fetchReports(page: 1),
            ),
          ],
        ),
      ),
    );
  }

  // Getters exposed for widget testing
  List<WasteReportListItemModel> get reports => List.unmodifiable(_reports);
  bool get isLoadingInitial => _isLoadingInitial;
  bool get isLoadingMore => _isLoadingMore;
  bool get hasMore => _hasMore;
  int get currentPage => _currentPage;
  int get totalPages => _totalPages;
  int get totalCount => _totalCount;
  WasteReportStatus? get selectedStatus => _selectedStatus;
}
