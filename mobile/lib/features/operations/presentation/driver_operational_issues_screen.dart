import 'dart:async';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../data/operations_repository.dart';
import '../models/operational_issue_model.dart';

/// Driver "Operational Issues" list screen mounted at `/driver/incidents`.
/// Displays a paginated, searchable, status-filtered list of operational issues
/// authoritatively scoped to the authenticated Driver.
class DriverOperationalIssuesScreen extends StatefulWidget {
  final OperationsRepository? repository;
  final bool showAppBar;

  const DriverOperationalIssuesScreen({
    super.key,
    this.repository,
    this.showAppBar = true,
  });

  @override
  State<DriverOperationalIssuesScreen> createState() =>
      DriverOperationalIssuesScreenState();
}

class DriverOperationalIssuesScreenState
    extends State<DriverOperationalIssuesScreen> {
  late final OperationsRepository _repository;
  final ScrollController _scrollController = ScrollController();
  final TextEditingController _searchController = TextEditingController();
  Timer? _debounceTimer;

  List<OperationalIssueSummaryModel> _issues = [];
  bool _isLoadingInitial = true;
  bool _isLoadingMore = false;
  bool _isRefreshing = false;
  String? _errorMessage;
  String? _loadMoreErrorMessage;

  int _currentPage = 1;
  int _totalPages = 1;
  int _totalCount = 0;
  bool _hasMore = false;
  OperationalIssueStatus? _selectedStatus;

  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? OperationsRepository();
    _scrollController.addListener(_onScroll);
    _fetchIssues(page: 1);
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
      if (_hasMore &&
          _currentPage < _totalPages &&
          !_isLoadingMore &&
          !_isLoadingInitial &&
          !_isRefreshing) {
        _fetchIssues(page: _currentPage + 1, isLoadMore: true);
      }
    }
  }

  void _onSearchChanged(String query) {
    _debounceTimer?.cancel();
    _debounceTimer = Timer(const Duration(milliseconds: 400), () {
      if (mounted) {
        _fetchIssues(page: 1);
      }
    });
    setState(() {});
  }

  void _onClearSearch() {
    _debounceTimer?.cancel();
    _searchController.clear();
    _fetchIssues(page: 1);
    setState(() {});
  }

  void _onStatusFilterSelected(OperationalIssueStatus? status) {
    if (_selectedStatus == status) return;
    setState(() {
      _selectedStatus = status;
    });
    _fetchIssues(page: 1);
  }

  Future<void> _onRefresh() async {
    await _fetchIssues(page: 1, isRefresh: true);
  }

  Future<void> _fetchIssues({
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
      final search = _searchController.text.trim().isEmpty
          ? null
          : _searchController.text.trim();
      final PagedOperationalIssuesModel result =
          await _repository.getMyOperationalIssues(
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
          final existingIds = _issues.map((i) => i.id).toSet();
          final newItems =
              result.items.where((i) => !existingIds.contains(i.id)).toList();
          _issues = [..._issues, ...newItems];
        } else {
          _issues = result.items;
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
          _loadMoreErrorMessage =
              'Unable to load more operational issues. Tap to retry.';
        } else {
          _errorMessage =
              'Unable to load operational issues. Please check your connection and try again.';
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

  String _formatDateTime(DateTime dateTime) {
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
    } else {
      return _dateFormat.format(local);
    }
  }

  @override
  Widget build(BuildContext context) {
    final content = Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Header / Action Bar
        Container(
          padding: const EdgeInsets.fromLTRB(
              AppSpacing.md, AppSpacing.sm, AppSpacing.md, AppSpacing.xs),
          color: AppColors.surface,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Operational Issues',
                      key: const Key('driver_operational_issues_title'),
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: AppColors.textPrimary,
                          ),
                    ),
                    const SizedBox(height: 2),
                    const Text(
                      'Report and track operational issues encountered during your work.',
                      style: TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              AppButton.primary(
                key: const Key('report_issue_top_action_button'),
                label: 'Report Issue',
                icon: Icons.add,
                height: 36,
                isFullWidth: false,
                onPressed: () async {
                  await context.push('/driver/incidents/new');
                  if (mounted) {
                    _fetchIssues(page: 1, isRefresh: true);
                  }
                },
              ),
            ],
          ),
        ),

        // Search and Status / Type Selector Container
        Container(
          padding: const EdgeInsets.fromLTRB(
              AppSpacing.md, AppSpacing.xs, AppSpacing.md, AppSpacing.sm),
          decoration: const BoxDecoration(
            color: AppColors.surface,
            border: Border(
              bottom: BorderSide(color: AppColors.border, width: 1),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Search Field
              TextField(
                key: const Key('search_issues_input'),
                controller: _searchController,
                onChanged: _onSearchChanged,
                style:
                    const TextStyle(fontSize: 14, color: AppColors.textPrimary),
                decoration: InputDecoration(
                  hintText: 'Search by title...',
                  hintStyle:
                      const TextStyle(fontSize: 13, color: AppColors.textMuted),
                  filled: true,
                  fillColor: AppColors.background,
                  prefixIcon: const Icon(Icons.search_rounded,
                      size: 20, color: AppColors.textSecondary),
                  suffixIcon: _searchController.text.isNotEmpty
                      ? IconButton(
                          key: const Key('clear_issues_search_button'),
                          icon: const Icon(Icons.cancel_rounded,
                              size: 18, color: AppColors.textMuted),
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
                    borderSide:
                        const BorderSide(color: AppColors.primary, width: 1.5),
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.xs),

              // Status Selector Chips
              _buildStatusSelector(),
            ],
          ),
        ),

        // Body Content
        Expanded(
          child: _buildBody(),
        ),
      ],
    );

    if (widget.showAppBar) {
      return Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          title: const Text('Operational Issues'),
          centerTitle: true,
          backgroundColor: AppColors.surface,
          surfaceTintColor: Colors.transparent,
          elevation: 0,
          leading: context.canPop()
              ? IconButton(
                  icon: const Icon(Icons.arrow_back, color: AppColors.textPrimary),
                  tooltip: 'Back',
                  onPressed: () => context.pop(),
                )
              : null,
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
      body: SafeArea(child: content),
    );
  }

  Widget _buildStatusSelector() {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          _buildFilterChip(
            label: 'All (${_totalCount > 0 ? _totalCount : _issues.length})',
            isSelected: _selectedStatus == null,
            onTap: () => _onStatusFilterSelected(null),
            key: const Key('status_filter_all'),
          ),
          const SizedBox(width: AppSpacing.xs),
          _buildFilterChip(
            label: 'Reported',
            isSelected: _selectedStatus == OperationalIssueStatus.reported,
            onTap: () =>
                _onStatusFilterSelected(OperationalIssueStatus.reported),
            key: const Key('status_filter_reported'),
          ),
          const SizedBox(width: AppSpacing.xs),
          _buildFilterChip(
            label: 'In Review',
            isSelected: _selectedStatus == OperationalIssueStatus.inReview,
            onTap: () =>
                _onStatusFilterSelected(OperationalIssueStatus.inReview),
            key: const Key('status_filter_inreview'),
          ),
          const SizedBox(width: AppSpacing.xs),
          _buildFilterChip(
            label: 'Resolved',
            isSelected: _selectedStatus == OperationalIssueStatus.resolved,
            onTap: () =>
                _onStatusFilterSelected(OperationalIssueStatus.resolved),
            key: const Key('status_filter_resolved'),
          ),
        ],
      ),
    );
  }

  Widget _buildFilterChip({
    required String label,
    required bool isSelected,
    required VoidCallback onTap,
    required Key key,
  }) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        key: key,
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.sm,
            vertical: 4,
          ),
          decoration: BoxDecoration(
            color: isSelected
                ? AppColors.primary.withValues(alpha: 0.12)
                : Colors.transparent,
            borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
            border: Border.all(
              color: isSelected ? AppColors.primary : AppColors.border,
              width: 1,
            ),
          ),
          child: Text(
            label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: isSelected ? FontWeight.w600 : FontWeight.normal,
              color: isSelected ? AppColors.primary : AppColors.textSecondary,
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildBody() {
    if (_isLoadingInitial) {
      return const Center(
        child: AppLoadingIndicator(
          message: 'Loading operational issues...',
        ),
      );
    }

    if (_errorMessage != null) {
      return Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                padding: const EdgeInsets.all(AppSpacing.md),
                decoration: BoxDecoration(
                  color: AppColors.error.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                  border: Border.all(
                    color: AppColors.error.withValues(alpha: 0.25),
                  ),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.error_outline_rounded,
                        color: AppColors.error, size: 24),
                    const SizedBox(width: AppSpacing.sm),
                    Expanded(
                      child: Text(
                        _errorMessage!,
                        style: const TextStyle(
                          fontSize: 13,
                          color: AppColors.error,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              AppButton.outlined(
                key: const Key('retry_fetch_issues_button'),
                label: 'Retry',
                icon: Icons.refresh_rounded,
                isFullWidth: false,
                onPressed: () => _fetchIssues(page: 1),
              ),
            ],
          ),
        ),
      );
    }

    if (_issues.isEmpty) {
      return _buildEmptyState();
    }

    return RefreshIndicator(
      onRefresh: _onRefresh,
      color: AppColors.primary,
      child: ListView.separated(
        controller: _scrollController,
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: _issues.length + (_hasMore || _loadMoreErrorMessage != null ? 1 : 0),
        separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
        itemBuilder: (context, index) {
          if (index >= _issues.length) {
            return _buildLoadMoreTile();
          }
          final issue = _issues[index];
          return _buildIssueCard(issue);
        },
      ),
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(AppSpacing.lg),
              decoration: BoxDecoration(
                color: AppColors.surface,
                shape: BoxShape.circle,
                border: Border.all(color: AppColors.border),
              ),
              child: const Icon(
                Icons.assignment_turned_in_outlined,
                size: 40,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            Text(
              _isFiltered
                  ? 'No matching operational issues'
                  : 'No operational issues reported yet.',
              key: const Key('empty_issues_message'),
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: AppColors.textPrimary,
                  ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              _isFiltered
                  ? 'Try adjusting your search query or status filter.'
                  : 'Issues you report will appear here.',
              style: const TextStyle(
                fontSize: 13,
                color: AppColors.textSecondary,
              ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: AppSpacing.lg),
            if (!_isFiltered)
              AppButton.primary(
                key: const Key('empty_report_issue_button'),
                label: 'Report Operational Issue',
                icon: Icons.add,
                isFullWidth: false,
                onPressed: () async {
                  await context.push('/driver/incidents/new');
                  if (mounted) {
                    _fetchIssues(page: 1, isRefresh: true);
                  }
                },
              )
            else
              AppButton.outlined(
                key: const Key('reset_filters_button'),
                label: 'Clear Filters',
                icon: Icons.filter_alt_off_outlined,
                isFullWidth: false,
                onPressed: () {
                  _searchController.clear();
                  _selectedStatus = null;
                  _fetchIssues(page: 1);
                },
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildIssueCard(OperationalIssueSummaryModel issue) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        key: Key('issue_card_${issue.id}'),
        onTap: () async {
          await context.push('/driver/incidents/${issue.id}');
          if (mounted) {
            _fetchIssues(page: 1, isRefresh: true);
          }
        },
        borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
            border: Border.all(color: AppColors.border, width: 1),
            boxShadow: const [
              BoxShadow(
                color: Color(0x04000000),
                blurRadius: 4,
                offset: Offset(0, 1),
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Row: Type Chip + Status Badge
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  _buildIssueTypeBadge(issue.issueType),
                  _buildStatusBadge(issue.status),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),

              // Title
              Text(
                issue.title,
                key: Key('issue_title_${issue.id}'),
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: AppSpacing.sm),

              // Footer: Date + Location indicator + Chevron
              Row(
                children: [
                  const Icon(
                    Icons.access_time_rounded,
                    size: 13,
                    color: AppColors.textMuted,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    _formatDateTime(issue.createdAt),
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  if (issue.hasLocation) ...[
                    const SizedBox(width: AppSpacing.sm),
                    const Icon(
                      Icons.location_on_outlined,
                      size: 13,
                      color: AppColors.primary,
                    ),
                    const SizedBox(width: 2),
                    const Text(
                      'Location added',
                      style: TextStyle(
                        fontSize: 11,
                        color: AppColors.primary,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                  const Spacer(),
                  const Icon(
                    Icons.chevron_right_rounded,
                    size: 18,
                    color: AppColors.textMuted,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildIssueTypeBadge(OperationalIssueType type) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
        border: Border.all(color: AppColors.border, width: 1),
      ),
      child: Text(
        type.displayName,
        style: const TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: AppColors.textPrimary,
        ),
      ),
    );
  }

  Widget _buildStatusBadge(OperationalIssueStatus status) {
    Color bg;
    Color fg;
    Color border;

    switch (status) {
      case OperationalIssueStatus.reported:
        bg = const Color(0xFFFEF3C7); // Amber 100
        fg = const Color(0xFF92400E); // Amber 800
        border = const Color(0xFFFDE68A); // Amber 200
        break;
      case OperationalIssueStatus.inReview:
        bg = const Color(0xFFE0F2FE); // Sky 100
        fg = const Color(0xFF0369A1); // Sky 700
        border = const Color(0xFFBAE6FD); // Sky 200
        break;
      case OperationalIssueStatus.resolved:
        bg = const Color(0xFFDCFCE7); // Emerald 100
        fg = const Color(0xFF15803D); // Emerald 700
        border = const Color(0xFFBBF7D0); // Emerald 200
        break;
    }

    return Container(
      key: Key('issue_status_badge_${status.value.toLowerCase()}'),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
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

  Widget _buildLoadMoreTile() {
    if (_loadMoreErrorMessage != null) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
        child: Column(
          children: [
            Text(
              _loadMoreErrorMessage!,
              style: const TextStyle(fontSize: 12, color: AppColors.error),
            ),
            const SizedBox(height: AppSpacing.xs),
            AppButton.outlined(
              label: 'Retry Load More',
              icon: Icons.refresh_rounded,
              height: 32,
              isFullWidth: false,
              onPressed: () =>
                  _fetchIssues(page: _currentPage + 1, isLoadMore: true),
            ),
          ],
        ),
      );
    }

    return const Padding(
      padding: EdgeInsets.symmetric(vertical: AppSpacing.md),
      child: Center(
        child: SizedBox(
          width: 24,
          height: 24,
          child: CircularProgressIndicator(
            strokeWidth: 2,
            color: AppColors.primary,
          ),
        ),
      ),
    );
  }
}
