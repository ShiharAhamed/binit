import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:intl/intl.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../data/complaints_repository.dart';
import '../models/complaint_model.dart';

/// Citizen-facing, read-only detail view of a single complaint.
class ComplaintDetailScreen extends StatefulWidget {
  final String complaintId;
  final ComplaintsRepository? repository;
  final TileProvider? tileProvider;

  const ComplaintDetailScreen({
    super.key,
    required this.complaintId,
    this.repository,
    this.tileProvider,
  });

  @override
  State<ComplaintDetailScreen> createState() => ComplaintDetailScreenState();
}

class ComplaintDetailScreenState extends State<ComplaintDetailScreen> {
  late final ComplaintsRepository _repository;

  ComplaintDetailModel? _complaint;
  bool _isLoading = true;
  String? _errorMessage;

  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? ComplaintsRepository();
    _loadComplaint();
  }

  Future<void> _loadComplaint() async {
    if (mounted) {
      setState(() {
        _isLoading = true;
        _errorMessage = null;
      });
    }

    try {
      final complaint = await _repository.getComplaintById(widget.complaintId);
      if (!mounted) return;
      setState(() {
        _complaint = complaint;
        _isLoading = false;
        _errorMessage = null;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = error.statusCode == 404
            ? 'This complaint could not be found.'
            : error.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = 'Unable to load complaint details. Please check your connection and try again.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        leading: IconButton(
          key: const Key('complaint_detail_back_button'),
          icon: const Icon(Icons.arrow_back),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
        title: const Text('Complaint Details'),
        centerTitle: true,
        actions: [
          IconButton(
            key: const Key('refresh_complaint_detail_button'),
            tooltip: 'Refresh details',
            icon: const Icon(Icons.refresh),
            onPressed: _isLoading ? null : _loadComplaint,
          ),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_isLoading && _complaint == null) {
      return const Center(
        child: AppLoadingIndicator(
          key: Key('complaint_detail_loading'),
          message: 'Loading complaint details...',
        ),
      );
    }

    if (_errorMessage != null && _complaint == null) {
      return _buildErrorState();
    }

    final complaint = _complaint!;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 600),
        child: RefreshIndicator(
          onRefresh: _loadComplaint,
          color: AppColors.primary,
          child: ListView(
            key: const Key('complaint_detail_scroll_view'),
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(AppSpacing.md),
            children: [
              if (_errorMessage != null) _buildRefreshErrorBanner(),
              _buildHeaderCard(complaint),
              const SizedBox(height: AppSpacing.sm),
              _buildDetailsCard(complaint),
              const SizedBox(height: AppSpacing.sm),
              _buildLocationCard(complaint),
              const SizedBox(height: AppSpacing.sm),
              _buildResolutionCard(complaint),
              const SizedBox(height: AppSpacing.xl),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildRefreshErrorBanner() => Container(
        key: const Key('complaint_detail_refresh_error'),
        margin: const EdgeInsets.only(bottom: AppSpacing.md),
        padding: const EdgeInsets.all(AppSpacing.sm),
        decoration: BoxDecoration(
          color: AppColors.errorLight,
          borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
          border: Border.all(color: AppColors.errorBorder),
        ),
        child: Text(
          _errorMessage!,
          style: const TextStyle(color: AppColors.errorText),
        ),
      );

  Widget _buildHeaderCard(ComplaintDetailModel complaint) {
    return AppCard(
      key: const Key('complaint_detail_header_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  complaint.subject,
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: AppColors.textPrimary,
                    height: 1.3,
                  ),
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              _buildStatusBadge(complaint.status),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            decoration: BoxDecoration(
              color: AppColors.surfaceSubtle,
              borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
              border: Border.all(color: AppColors.border),
            ),
            child: Text(
              complaint.category.displayName,
              style: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: AppColors.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDetailsCard(ComplaintDetailModel complaint) {
    return AppCard(
      key: const Key('complaint_detail_info_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Complaint Description',
            style: TextStyle(
              fontSize: 15,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            complaint.description,
            style: const TextStyle(
              fontSize: 14,
              color: AppColors.textPrimary,
              height: 1.45,
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          const Divider(height: 1, color: AppColors.border),
          const SizedBox(height: AppSpacing.md),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: _buildMetaItem(
                  icon: Icons.schedule_outlined,
                  label: 'Submitted Date',
                  value: _dateFormat.format(complaint.createdAt.toLocal()),
                ),
              ),
              if (complaint.updatedAt != null) ...[
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: _buildMetaItem(
                    icon: Icons.update_outlined,
                    label: 'Last Updated',
                    value: _dateFormat.format(complaint.updatedAt!.toLocal()),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildMetaItem({
    required IconData icon,
    required String label,
    required String value,
  }) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 16, color: AppColors.textSecondary),
        const SizedBox(width: AppSpacing.xs),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(
                  fontSize: 11,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 1),
              Text(
                value,
                style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildLocationCard(ComplaintDetailModel complaint) {
    final hasCoordinates = complaint.latitude != null &&
        complaint.longitude != null &&
        complaint.latitude! >= -90 &&
        complaint.latitude! <= 90 &&
        complaint.longitude! >= -180 &&
        complaint.longitude! <= 180;

    return AppCard(
      key: const Key('complaint_detail_location_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.place_outlined, size: 18, color: AppColors.textPrimary),
              const SizedBox(width: AppSpacing.xs),
              const Text(
                'Reported Location',
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          if (complaint.locationDescription != null &&
              complaint.locationDescription!.trim().isNotEmpty) ...[
            Container(
              padding: const EdgeInsets.all(AppSpacing.sm),
              decoration: BoxDecoration(
                color: AppColors.surfaceSubtle,
                borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(Icons.info_outline, size: 16, color: AppColors.textSecondary),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: Text(
                      complaint.locationDescription!.trim(),
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.sm),
          ],
          if (hasCoordinates) ...[
            Text(
              '${complaint.latitude!.toStringAsFixed(5)}, ${complaint.longitude!.toStringAsFixed(5)}',
              key: const Key('complaint_detail_coordinates_text'),
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            ClipRRect(
              borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
              child: SizedBox(
                height: 180,
                width: double.infinity,
                child: FlutterMap(
                  options: MapOptions(
                    initialCenter: LatLng(complaint.latitude!, complaint.longitude!),
                    initialZoom: 15.0,
                    minZoom: 3.0,
                    maxZoom: 19.0,
                  ),
                  children: [
                    TileLayer(
                      urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                      userAgentPackageName: 'com.smartwaste.mobile',
                      tileProvider: widget.tileProvider,
                    ),
                    MarkerLayer(
                      markers: [
                        Marker(
                          key: const Key('complaint_detail_marker'),
                          point: LatLng(complaint.latitude!, complaint.longitude!),
                          width: 44,
                          height: 44,
                          alignment: Alignment.topCenter,
                          child: const Icon(
                            Icons.location_on,
                            color: AppColors.primaryDark,
                            size: 40,
                          ),
                        ),
                      ],
                    ),
                    Align(
                      alignment: Alignment.topRight,
                      child: Container(
                        margin: const EdgeInsets.all(4),
                        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
                        decoration: BoxDecoration(
                          color: AppColors.surface.withValues(alpha: 0.85),
                          borderRadius: BorderRadius.circular(2),
                        ),
                        child: const Text(
                          'OpenStreetMap',
                          style: TextStyle(fontSize: 9, color: AppColors.textSecondary),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ] else ...[
            const Row(
              children: [
                Icon(Icons.location_off_outlined, size: 16, color: AppColors.textMuted),
                SizedBox(width: AppSpacing.xs),
                Expanded(
                  child: Text(
                    'No map location was provided for this complaint.',
                    key: Key('complaint_detail_no_location_text'),
                    style: TextStyle(fontSize: 13, color: AppColors.textSecondary),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildResolutionCard(ComplaintDetailModel complaint) {
    final isResolved = complaint.status.isResolved;

    return AppCard(
      key: const Key('complaint_detail_resolution_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      color: isResolved ? const Color(0xFFF0FDF4) : AppColors.surface, // emerald-50
      borderSide: BorderSide(
        color: isResolved ? const Color(0xFFA7F3D0) : AppColors.border,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(
                isResolved ? Icons.check_circle_outline : Icons.hourglass_top_outlined,
                size: 18,
                color: isResolved ? AppColors.primaryDark : AppColors.textSecondary,
              ),
              const SizedBox(width: AppSpacing.xs),
              Text(
                'Resolution',
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.bold,
                  color: isResolved ? AppColors.primaryDark : AppColors.textPrimary,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.xs),
          if (isResolved) ...[
            if (complaint.resolutionNote != null &&
                complaint.resolutionNote!.trim().isNotEmpty) ...[
              Text(
                complaint.resolutionNote!.trim(),
                key: const Key('complaint_resolution_note_text'),
                style: const TextStyle(
                  fontSize: 14,
                  color: AppColors.textPrimary,
                  height: 1.4,
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
            ],
            if (complaint.resolvedAt != null) ...[
              Row(
                children: [
                  const Icon(Icons.event_available_outlined, size: 14, color: AppColors.primaryDark),
                  const SizedBox(width: 4),
                  Text(
                    'Resolved on ${_dateFormat.format(complaint.resolvedAt!.toLocal())}',
                    key: const Key('complaint_resolved_at_text'),
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ],
            if (complaint.resolvedByUserName != null &&
                complaint.resolvedByUserName!.trim().isNotEmpty) ...[
              const SizedBox(height: 2),
              Row(
                children: [
                  const Icon(Icons.person_outline, size: 14, color: AppColors.textSecondary),
                  const SizedBox(width: 4),
                  Text(
                    'Resolved by ${complaint.resolvedByUserName!.trim()}',
                    key: const Key('complaint_resolved_by_text'),
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ],
          ] else ...[
            const Text(
              'This complaint is currently awaiting staff review and resolution.',
              key: Key('complaint_resolution_pending_text'),
              style: TextStyle(
                fontSize: 13,
                color: AppColors.textSecondary,
              ),
            ),
          ],
        ],
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

  Widget _buildErrorState() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xxl),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline, size: 48, color: AppColors.error),
            const SizedBox(height: AppSpacing.md),
            const Text(
              'Unable to Load Complaint',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              _errorMessage ?? 'Please check your connection and try again.',
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.textSecondary),
            ),
            const SizedBox(height: AppSpacing.lg),
            AppButton.primary(
              key: const Key('retry_load_complaint_detail_button'),
              label: 'Retry',
              onPressed: _loadComplaint,
            ),
          ],
        ),
      ),
    );
  }

  // Getters for testing
  ComplaintDetailModel? get complaint => _complaint;
  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
}
