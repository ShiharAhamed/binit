import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../data/operations_repository.dart';
import '../models/operational_issue_model.dart';

/// Screen displaying comprehensive read-only details of an operational issue.
/// Includes status, issue type, title, multiline description, read-only
/// OpenStreetMap location map if present, and resolution details when resolved.
class OperationalIssueDetailScreen extends StatefulWidget {
  final String issueId;
  final OperationsRepository? repository;
  final TileProvider? tileProvider;

  const OperationalIssueDetailScreen({
    super.key,
    required this.issueId,
    this.repository,
    this.tileProvider,
  });

  @override
  State<OperationalIssueDetailScreen> createState() =>
      OperationalIssueDetailScreenState();
}

class OperationalIssueDetailScreenState
    extends State<OperationalIssueDetailScreen> {
  late final OperationsRepository _repository;
  late Future<OperationalIssueDetailModel> _detailFuture;

  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? OperationsRepository();
    _loadDetail();
  }

  void _loadDetail() {
    setState(() {
      _detailFuture = _repository.getOperationalIssueById(widget.issueId);
    });
  }

  Future<void> _onRefresh() async {
    _loadDetail();
    await _detailFuture;
  }

  String _formatDateTime(DateTime dateTime) {
    return _dateFormat.format(dateTime.toLocal());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Operational Issue Details'),
        centerTitle: true,
        backgroundColor: AppColors.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        leading: IconButton(
          key: const Key('issue_detail_back_button'),
          icon: const Icon(Icons.arrow_back, color: AppColors.textPrimary),
          tooltip: 'Back',
          onPressed: () => context.pop(),
        ),
        bottom: const PreferredSize(
          preferredSize: Size.fromHeight(1),
          child: Divider(height: 1, color: AppColors.border),
        ),
      ),
      body: SafeArea(
        child: FutureBuilder<OperationalIssueDetailModel>(
          future: _detailFuture,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const Center(
                child: AppLoadingIndicator(
                  message: 'Loading operational issue details...',
                ),
              );
            }

            if (snapshot.hasError) {
              final error = snapshot.error;
              final errorMessage = error is ApiException
                  ? error.message
                  : 'Unable to load operational issue details. Please try again.';

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
                          borderRadius:
                              BorderRadius.circular(AppSpacing.radiusMd),
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
                                errorMessage,
                                key: const Key('issue_detail_error_text'),
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
                        key: const Key('retry_fetch_issue_detail_button'),
                        label: 'Retry',
                        icon: Icons.refresh_rounded,
                        isFullWidth: false,
                        onPressed: _loadDetail,
                      ),
                    ],
                  ),
                ),
              );
            }

            final issue = snapshot.data!;
            return RefreshIndicator(
              onRefresh: _onRefresh,
              color: AppColors.primary,
              child: SingleChildScrollView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // 1. Status & Header Card
                    _buildHeaderCard(issue),
                    const SizedBox(height: AppSpacing.md),

                    // 2. Issue Details Card
                    _buildDetailsCard(issue),
                    const SizedBox(height: AppSpacing.md),

                    // 3. Location Card
                    _buildLocationCard(issue),
                    const SizedBox(height: AppSpacing.md),

                    // 4. Resolution Card
                    _buildResolutionCard(issue),
                    const SizedBox(height: AppSpacing.xl),
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _buildHeaderCard(OperationalIssueDetailModel issue) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              _buildIssueTypeBadge(issue.issueType),
              _buildStatusBadge(issue.status),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            issue.title,
            key: const Key('detail_issue_title'),
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                ),
          ),
          const SizedBox(height: AppSpacing.xs),
          Row(
            children: [
              const Icon(
                Icons.access_time_rounded,
                size: 14,
                color: AppColors.textMuted,
              ),
              const SizedBox(width: 4),
              Text(
                'Reported: ${_formatDateTime(issue.createdAt)}',
                key: const Key('detail_issue_reported_date'),
                style: const TextStyle(
                  fontSize: 12,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
          if (issue.updatedAt != null && issue.updatedAt != issue.createdAt) ...[
            const SizedBox(height: 2),
            Row(
              children: [
                const Icon(
                  Icons.update_rounded,
                  size: 14,
                  color: AppColors.textMuted,
                ),
                const SizedBox(width: 4),
                Text(
                  'Updated: ${_formatDateTime(issue.updatedAt!)}',
                  key: const Key('detail_issue_updated_date'),
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildDetailsCard(OperationalIssueDetailModel issue) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Description',
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                ),
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            issue.description,
            key: const Key('detail_issue_description'),
            style: const TextStyle(
              fontSize: 14,
              height: 1.5,
              color: AppColors.textPrimary,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLocationCard(OperationalIssueDetailModel issue) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Reported Location',
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                ),
          ),
          const SizedBox(height: AppSpacing.sm),
          if (issue.hasLocation) ...[
            Container(
              height: 200,
              clipBehavior: Clip.antiAlias,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                border: Border.all(color: AppColors.border),
              ),
              child: FlutterMap(
                options: MapOptions(
                  initialCenter: LatLng(issue.latitude!, issue.longitude!),
                  initialZoom: 15.0,
                  interactionOptions: const InteractionOptions(
                    flags: InteractiveFlag.none,
                  ),
                ),
                children: [
                  TileLayer(
                    urlTemplate:
                        'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                    userAgentPackageName: 'com.smartwaste.mobile',
                    tileProvider: widget.tileProvider,
                  ),
                  MarkerLayer(
                    markers: [
                      Marker(
                        point: LatLng(issue.latitude!, issue.longitude!),
                        width: 40,
                        height: 40,
                        alignment: Alignment.topCenter,
                        child: const Icon(
                          Icons.location_pin,
                          color: AppColors.primary,
                          size: 40,
                          shadows: [
                            Shadow(
                              color: Colors.black26,
                              blurRadius: 4,
                              offset: Offset(0, 2),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Row(
              children: [
                const Icon(
                  Icons.pin_drop_outlined,
                  size: 14,
                  color: AppColors.primary,
                ),
                const SizedBox(width: 4),
                Text(
                  '${issue.latitude!.toStringAsFixed(5)}, ${issue.longitude!.toStringAsFixed(5)}',
                  key: const Key('detail_issue_coordinates'),
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ] else ...[
            Container(
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: AppColors.background,
                borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                border: Border.all(color: AppColors.border),
              ),
              child: const Row(
                children: [
                  Icon(
                    Icons.location_off_outlined,
                    size: 20,
                    color: AppColors.textMuted,
                  ),
                  SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      'No map coordinates were provided for this issue.',
                      key: Key('detail_no_location_message'),
                      style: TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
          if (issue.locationDescription != null &&
              issue.locationDescription!.trim().isNotEmpty) ...[
            const SizedBox(height: AppSpacing.sm),
            Container(
              padding: const EdgeInsets.all(AppSpacing.sm),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                border: Border.all(color: AppColors.border),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(
                    Icons.info_outline_rounded,
                    size: 16,
                    color: AppColors.textSecondary,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: Text(
                      issue.locationDescription!.trim(),
                      key: const Key('detail_issue_location_description'),
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildResolutionCard(OperationalIssueDetailModel issue) {
    if (issue.status.isResolved) {
      return AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(
                  Icons.check_circle_rounded,
                  size: 20,
                  color: AppColors.primary,
                ),
                const SizedBox(width: AppSpacing.xs),
                Text(
                  'Resolution Details',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                      ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            if (issue.resolutionNote != null &&
                issue.resolutionNote!.trim().isNotEmpty) ...[
              Text(
                issue.resolutionNote!.trim(),
                key: const Key('detail_issue_resolution_note'),
                style: const TextStyle(
                  fontSize: 14,
                  height: 1.5,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
            ],
            if (issue.resolvedAt != null) ...[
              Row(
                children: [
                  const Icon(
                    Icons.event_available_rounded,
                    size: 14,
                    color: AppColors.textMuted,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    'Resolved on ${_formatDateTime(issue.resolvedAt!)}',
                    key: const Key('detail_issue_resolved_date'),
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ],
            if (issue.resolvedByUserName != null &&
                issue.resolvedByUserName!.trim().isNotEmpty) ...[
              const SizedBox(height: 2),
              Row(
                children: [
                  const Icon(
                    Icons.person_outline_rounded,
                    size: 14,
                    color: AppColors.textMuted,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    'Resolved by: ${issue.resolvedByUserName!.trim()}',
                    key: const Key('detail_issue_resolved_by'),
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      );
    }

    return AppCard(
      child: Row(
        children: [
          const Icon(
            Icons.pending_actions_rounded,
            size: 20,
            color: Color(0xFFD97706), // Amber 600
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Resolution Pending',
                  key: Key('detail_resolution_pending_title'),
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  issue.status.isInReview
                      ? 'Staff is actively reviewing this issue.'
                      : 'This operational issue has been submitted and is awaiting staff review.',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
        ],
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
        key: const Key('detail_issue_type_badge'),
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
        bg = const Color(0xFFFEF3C7);
        fg = const Color(0xFF92400E);
        border = const Color(0xFFFDE68A);
        break;
      case OperationalIssueStatus.inReview:
        bg = const Color(0xFFE0F2FE);
        fg = const Color(0xFF0369A1);
        border = const Color(0xFFBAE6FD);
        break;
      case OperationalIssueStatus.resolved:
        bg = const Color(0xFFDCFCE7);
        fg = const Color(0xFF15803D);
        border = const Color(0xFFBBF7D0);
        break;
    }

    return Container(
      key: Key('detail_status_badge_${status.value.toLowerCase()}'),
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
}
