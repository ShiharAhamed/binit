import 'dart:async';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../data/complaints_repository.dart';
import '../models/complaint_model.dart';

/// Citizen "My Complaints" list screen.
/// Displays a paginated, searchable, status-filtered list of complaints
/// authoritatively scoped to the authenticated Citizen.
class CitizenComplaintsScreen extends StatefulWidget {
  final ComplaintsRepository? repository;
  final bool showAppBar;

  const CitizenComplaintsScreen({
    super.key,
    this.repository,
    this.showAppBar = false,
  });

  @override
  State<CitizenComplaintsScreen> createState() => CitizenComplaintsScreenState();
}

class CitizenComplaintsScreenState extends State<CitizenComplaintsScreen> {
  late final ComplaintsRepository _repository;
  final ScrollController _scrollController = ScrollController();
  final TextEditingController _searchController = TextEditingController();
  Timer? _debounceTimer;

  List<ComplaintSummaryModel> _complaints = [];
  bool _isLoadingInitial = true;
  bool _isLoadingMore = false;
  bool _isRefreshing = false;
  String? _errorMessage;
  String? _loadMoreErrorMessage;

  int _currentPage = 1;
  int _totalPages = 1;
  int _totalCount = 0;
  bool _hasMore = false;
  ComplaintStatus? _selectedStatus;

  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? ComplaintsRepository();
    _scrollController.addListener(_onScroll);
    _fetchComplaints(page: 1);
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
        _fetchComplaints(page: _currentPage + 1, isLoadMore: true);
      }
    }
  }

  void _onSearchChanged(String query) {
    _debounceTimer?.cancel();
    _debounceTimer = Timer(const Duration(milliseconds: 400), () {
      if (mounted) {
        _fetchComplaints(page: 1);
      }
    });
    setState(() {});
  }

  void _onClearSearch() {
    _debounceTimer?.cancel();
    _searchController.clear();
    _fetchComplaints(page: 1);
    setState(() {});
  }

  void _onStatusFilterSelected(ComplaintStatus? status) {
    if (_selectedStatus == status) return;
    setState(() {
      _selectedStatus = status;
    });
    _fetchComplaints(page: 1);
  }

  Future<void> _onRefresh() async {
    await _fetchComplaints(page: 1, isRefresh: true);
  }

  Future<void> _fetchComplaints({
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
      final PagedComplaintsModel result = await _repository.getMyComplaints(
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
          final existingIds = _complaints.map((c) => c.id).toSet();
          final newItems = result.items.where((c) => !existingIds.contains(c.id)).toList();
          _complaints = [..._complaints, ...newItems];
        } else {
          _complaints = result.items;
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
          _loadMoreErrorMessage = 'Unable to load more complaints. Tap to retry.';
        } else {
          _errorMessage = 'Unable to load complaints. Please check your connection and try again.';
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
          padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.md, AppSpacing.xs),
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
                      'My Complaints',
                      key: const Key('citizen_complaints_title'),
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: AppColors.textPrimary,
                          ),
                    ),
                    const SizedBox(height: 2),
                    const Text(
                      'Track your service complaints and their current status.',
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
                key: const Key('submit_complaint_top_action_button'),
                label: 'Submit',
                icon: Icons.add,
                height: 36,
                isFullWidth: false,
                onPressed: () async {
                  await context.push('/citizen/complaints/new');
                  if (mounted) {
                    _fetchComplaints(page: 1, isRefresh: true);
                  }
                },
              ),
            ],
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
              // Search Field
              TextField(
                key: const Key('search_complaints_input'),
                controller: _searchController,
                onChanged: _onSearchChanged,
                style: const TextStyle(fontSize: 14, color: AppColors.textPrimary),
                decoration: InputDecoration(
                  hintText: 'Search by subject...',
                  hintStyle: const TextStyle(fontSize: 13, color: AppColors.textMuted),
                  filled: true,
                  fillColor: AppColors.background,
                  prefixIcon: const Icon(Icons.search_rounded, size: 20, color: AppColors.textSecondary),
                  suffixIcon: _searchController.text.isNotEmpty
                      ? IconButton(
                          key: const Key('clear_complaints_search_button'),
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

              // Status Selector
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
          title: const Text('Complaints'),
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
      body: SafeArea(child: content),
    );
  }

  Widget _buildStatusSelector() {
    final hasActiveFilter = _selectedStatus != null;
    final displayLabel = _selectedStatus?.displayName ?? 'All Statuses';

    return Material(
      color: Colors.transparent,
      child: InkWell(
        key: const Key('complaints_status_filter_selector'),
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
          child: Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.md, AppSpacing.md),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
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
                  'Select a complaint status to filter your list',
                  style: TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: AppSpacing.xs),
                const Divider(color: AppColors.border, height: 1),
                const SizedBox(height: AppSpacing.xs),
                _buildStatusOptionTile(
                  context: bottomSheetContext,
                  label: 'All Statuses',
                  isSelected: _selectedStatus == null,
                  key: const Key('complaint_status_option_all'),
                  onTap: () {
                    Navigator.pop(bottomSheetContext);
                    _onStatusFilterSelected(null);
                  },
                ),
                ...ComplaintStatus.values.map(
                  (status) => _buildStatusOptionTile(
                    context: bottomSheetContext,
                    label: status.displayName,
                    status: status,
                    isSelected: _selectedStatus == status,
                    key: Key('complaint_status_option_${status.value.toLowerCase()}'),
                    onTap: () {
                      Navigator.pop(bottomSheetContext);
                      _onStatusFilterSelected(status);
                    },
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildStatusOptionTile({
    required BuildContext context,
    required String label,
    ComplaintStatus? status,
    required bool isSelected,
    required Key key,
    required VoidCallback onTap,
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
            vertical: AppSpacing.sm + 2,
          ),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primaryLight.withValues(alpha: 0.5) : Colors.transparent,
            borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
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
    );
  }

  Widget _buildStatusDot(ComplaintStatus status) {
    Color dotColor;
    switch (status) {
      case ComplaintStatus.resolved:
        dotColor = const Color(0xFF059669); // emerald-600
        break;
      case ComplaintStatus.inReview:
        dotColor = const Color(0xFF2563EB); // blue-600
        break;
      case ComplaintStatus.submitted:
        dotColor = const Color(0xFFD97706); // amber-600
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
        child: AppLoadingIndicator(message: 'Loading complaints...'),
      );
    }

    if (_errorMessage != null && _complaints.isEmpty) {
      return _buildErrorState();
    }

    if (_complaints.isEmpty) {
      return _buildEmptyState();
    }

    return RefreshIndicator(
      onRefresh: _onRefresh,
      color: AppColors.primary,
      child: ListView.separated(
        key: const Key('my_complaints_list_view'),
        controller: _scrollController,
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: _complaints.length + (_hasMore || _isLoadingMore || _loadMoreErrorMessage != null ? 1 : 0),
        separatorBuilder: (context, index) => const SizedBox(height: AppSpacing.sm),
        itemBuilder: (context, index) {
          if (index == _complaints.length) {
            return _buildPaginationFooter();
          }
          return _buildComplaintCard(_complaints[index]);
        },
      ),
    );
  }

  Widget _buildComplaintCard(ComplaintSummaryModel complaint) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        key: Key('complaint_card_${complaint.id}'),
        onTap: () async {
          await context.push('/citizen/complaints/${complaint.id}');
          if (mounted) {
            _fetchComplaints(page: 1, isRefresh: true);
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
              // Header Row: Category Badge + Status Badge
              Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Flexible(
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: AppColors.surfaceSubtle,
                        borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                        border: Border.all(color: AppColors.border),
                      ),
                      child: Text(
                        complaint.category.displayName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  _buildStatusBadge(complaint.status),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),

              // Subject
              Text(
                complaint.subject,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                  height: 1.3,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),

              // Footer row: Submitted date and Location indicator
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
                      _formatDateTime(complaint.createdAt),
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textMuted,
                      ),
                    ),
                  ),
                  if (complaint.hasLocation) ...[
                    const SizedBox(width: AppSpacing.xs),
                    const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.location_on_outlined,
                          size: 13,
                          color: AppColors.textSecondary,
                        ),
                        SizedBox(width: 2),
                        Text(
                          'Location',
                          style: TextStyle(
                            fontSize: 11,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ],
                  const SizedBox(width: AppSpacing.xs),
                  const Icon(
                    Icons.chevron_right,
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

  Widget _buildStatusBadge(ComplaintStatus status) {
    Color bg;
    Color fg;
    Color border;

    switch (status) {
      case ComplaintStatus.resolved:
        bg = const Color(0xFFDCFCE7); // emerald-100
        fg = const Color(0xFF065F46); // emerald-800
        border = const Color(0xFFA7F3D0); // emerald-200
        break;
      case ComplaintStatus.inReview:
        bg = const Color(0xFFDBEAFE); // blue-100
        fg = const Color(0xFF1E40AF); // blue-800
        border = const Color(0xFFBFDBFE); // blue-200
        break;
      case ComplaintStatus.submitted:
        bg = const Color(0xFFFEF3C7); // amber-100
        fg = const Color(0xFF92400E); // amber-800
        border = const Color(0xFFFDE68A); // amber-200
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
                key: const Key('retry_load_more_complaints_button'),
                onPressed: () => _fetchComplaints(page: _currentPage + 1, isLoadMore: true),
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
            key: const Key('load_more_complaints_button'),
            onPressed: () => _fetchComplaints(page: _currentPage + 1, isLoadMore: true),
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
                isFiltered ? Icons.search_off_outlined : Icons.feedback_outlined,
                size: 48,
                color: isFiltered ? AppColors.textSecondary : AppColors.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            Text(
              isFiltered ? 'No matching complaints' : 'No complaints yet',
              key: isFiltered
                  ? const Key('filtered_complaints_empty_state_title')
                  : const Key('empty_complaints_state_title'),
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
                  : 'Submitted complaints will appear here.',
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 14,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            if (isFiltered)
              AppButton.text(
                key: const Key('clear_complaint_filters_button'),
                label: 'Clear Filters',
                onPressed: () {
                  _searchController.clear();
                  _selectedStatus = null;
                  _fetchComplaints(page: 1);
                },
              )
            else
              AppButton.primary(
                key: const Key('empty_state_submit_complaint_button'),
                label: 'Submit Complaint',
                icon: Icons.add,
                onPressed: () async {
                  await context.push('/citizen/complaints/new');
                  if (mounted) {
                    _fetchComplaints(page: 1, isRefresh: true);
                  }
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
              'Unable to Load Complaints',
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
              key: const Key('retry_load_complaints_button'),
              label: 'Retry',
              onPressed: () => _fetchComplaints(page: 1),
            ),
          ],
        ),
      ),
    );
  }

  // Exposed getters for widget testing
  List<ComplaintSummaryModel> get complaints => List.unmodifiable(_complaints);
  bool get isLoadingInitial => _isLoadingInitial;
  bool get isLoadingMore => _isLoadingMore;
  bool get hasMore => _hasMore;
  int get currentPage => _currentPage;
  int get totalPages => _totalPages;
  int get totalCount => _totalCount;
  ComplaintStatus? get selectedStatus => _selectedStatus;
}
