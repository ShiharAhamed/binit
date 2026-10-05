import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../data/reporting_repository.dart';
import '../models/report_attachment_model.dart';
import '../models/report_reference.dart';
import '../models/waste_report_detail_model.dart';
import '../models/waste_report_status.dart';
import '../models/waste_report_status_history_model.dart';
import '../models/waste_type.dart';
import 'edit_report_screen.dart';
import 'manage_report_photos_screen.dart';

/// Read-only Citizen Report Detail screen (Step 9A.9.2).
/// Displays comprehensive report metadata, photo evidence thumbnails (with modal preview),
/// and chronological status transition history audit trail.
class ReportDetailScreen extends StatefulWidget {
  final String reportId;
  final ReportingRepository? repository;

  const ReportDetailScreen({
    super.key,
    required this.reportId,
    this.repository,
  });

  @override
  State<ReportDetailScreen> createState() => _ReportDetailScreenState();
}

class _ReportDetailScreenState extends State<ReportDetailScreen> {
  late final ReportingRepository _repository;

  WasteReportDetailModel? _report;
  List<WasteReportStatusHistoryModel>? _history;

  bool _isLoadingReport = true;
  bool _isLoadingHistory = true;
  bool _isCancelling = false;
  String? _reportError;
  String? _historyError;

  static final DateFormat _dateFormat = DateFormat('d MMM yyyy • h:mm a');

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? ReportingRepository();
    _loadAllData();
  }

  Future<void> _loadAllData({bool isRefresh = false}) async {
    if (!isRefresh) {
      setState(() {
        _isLoadingReport = true;
        _isLoadingHistory = true;
        _reportError = null;
        _historyError = null;
      });
    }

    await Future.wait([
      _loadReport(isRefresh: isRefresh),
      _loadHistory(isRefresh: isRefresh),
    ]);
  }

  Future<void> _loadReport({bool isRefresh = false}) async {
    if (!isRefresh && !_isLoadingReport) {
      setState(() {
        _isLoadingReport = true;
        _reportError = null;
      });
    }

    try {
      final report = await _repository.getWasteReport(widget.reportId);
      if (!mounted) return;
      setState(() {
        _report = report;
        _reportError = null;
        _isLoadingReport = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _reportError = e.message;
        _isLoadingReport = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _reportError = "Couldn't load report details. Please try again.";
        _isLoadingReport = false;
      });
    }
  }

  Future<void> _loadHistory({bool isRefresh = false}) async {
    if (!isRefresh && !_isLoadingHistory) {
      setState(() {
        _isLoadingHistory = true;
        _historyError = null;
      });
    }

    try {
      final history = await _repository.getWasteReportHistory(widget.reportId);
      if (!mounted) return;
      setState(() {
        _history = history;
        _historyError = null;
        _isLoadingHistory = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _historyError = e.message;
        _isLoadingHistory = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _historyError = "Couldn't load status history.";
        _isLoadingHistory = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text(
          'Report Details',
          style: TextStyle(
            color: AppColors.textPrimary,
            fontWeight: FontWeight.bold,
            fontSize: 18,
          ),
        ),
        backgroundColor: AppColors.surface,
        elevation: 0,
        iconTheme: const IconThemeData(color: AppColors.textPrimary),
        centerTitle: false,
        actions: [
          if (_report != null && _report!.status == WasteReportStatus.submitted)
            IconButton(
              key: const Key('edit_report_appbar_button'),
              icon: const Icon(Icons.edit_outlined),
              tooltip: 'Edit Report',
              onPressed: () => _onEditReport(_report!),
            ),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_isLoadingReport && _report == null) {
      return const Center(
        child: AppLoadingIndicator(
          key: Key('report_detail_loading'),
          message: 'Loading report details...',
        ),
      );
    }

    if (_reportError != null && _report == null) {
      return _buildReportErrorState();
    }

    final report = _report!;

    return RefreshIndicator(
      onRefresh: () => _loadAllData(isRefresh: true),
      color: AppColors.primary,
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_reportError != null) ...[
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text("Couldn't refresh this report. Showing the last loaded status."),
                    AppButton.outlined(
                      key: const Key('report_detail_refresh_retry_button'),
                      label: 'Retry',
                      icon: Icons.refresh,
                      onPressed: () => _loadAllData(isRefresh: true),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.md),
            ],
            // Top Summary Card
            _buildSummaryCard(report),

            const SizedBox(height: AppSpacing.md),

            // Description Card
            _buildDescriptionCard(report),

            const SizedBox(height: AppSpacing.md),

            // Location Card
            _buildLocationCard(report),

            const SizedBox(height: AppSpacing.md),

            // Photo Evidence Card
            _buildPhotoEvidenceCard(report),

            const SizedBox(height: AppSpacing.md),

            // Status History Card
            _buildStatusHistoryCard(),

            if (report.status == WasteReportStatus.submitted) ...[
              const SizedBox(height: AppSpacing.lg),
              AppButton.destructive(
                key: const Key('cancel_report_button'),
                label: 'Cancel Report',
                icon: Icons.cancel_outlined,
                isLoading: _isCancelling,
                onPressed: _isCancelling ? null : () => _showCancelConfirmationDialog(report),
              ),
            ],

            const SizedBox(height: AppSpacing.lg),
          ],
        ),
      ),
    );
  }

  Widget _buildReportErrorState() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: const Color(0xFFFEE2E2),
                borderRadius: BorderRadius.circular(AppSpacing.radiusFull),
              ),
              child: const Icon(
                Icons.error_outline_rounded,
                size: 40,
                color: Color(0xFFDC2626),
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            const Text(
              'Unable to Load Report',
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              _reportError ?? 'An unexpected error occurred.',
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 14,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            AppButton.primary(
              key: const Key('report_detail_retry_button'),
              label: 'Retry',
              icon: Icons.refresh,
              onPressed: () => _loadAllData(),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSummaryCard(WasteReportDetailModel report) {
    return AppCard(
      key: const Key('report_detail_summary_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header Row: Waste Type Badge + Status Badge
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Flexible(
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                  decoration: BoxDecoration(
                    color: AppColors.primaryLight,
                    borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                    border: Border.all(color: AppColors.primaryBorder),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _buildWasteTypeIcon(report.wasteType),
                      const SizedBox(width: 6),
                      Flexible(
                        child: Text(
                          report.wasteType.displayName,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            fontSize: 13,
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
            key: const Key('report_detail_reference'),
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              color: AppColors.primaryDark,
            ),
          ),

          const SizedBox(height: AppSpacing.xs),

          // Created Timestamp Row
          Row(
            children: [
              const Icon(
                Icons.schedule_outlined,
                size: 15,
                color: AppColors.textMuted,
              ),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  'Submitted on ${_dateFormat.format(report.createdAt.toLocal())}',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textMuted,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
            ],
          ),

          // Priority badge if assigned
          if (report.priority != null) ...[
            const SizedBox(height: AppSpacing.xs),
            Row(
              children: [
                const Icon(
                  Icons.flag_outlined,
                  size: 15,
                  color: AppColors.textSecondary,
                ),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    'Priority: ${report.priority!.displayName}',
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
          ],

          // Edit Report Action (Visible ONLY when status == Submitted)
          if (report.status == WasteReportStatus.submitted) ...[
            const SizedBox(height: AppSpacing.md),
            AppButton.outlined(
              key: const Key('edit_report_button'),
              label: 'Edit Report',
              icon: Icons.edit_outlined,
              height: 40,
              onPressed: () => _onEditReport(report),
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _onEditReport(WasteReportDetailModel report) async {
    final updatedReport = await Navigator.of(context).push<WasteReportDetailModel>(
      MaterialPageRoute(
        builder: (context) => EditReportScreen(
          reportId: report.id,
          initialReport: report,
          repository: _repository,
        ),
      ),
    );

    if (!mounted) return;

    if (updatedReport != null) {
      setState(() {
        _report = updatedReport;
      });
      _loadHistory(isRefresh: true);
    } else {
      _loadAllData(isRefresh: true);
    }
  }

  Future<void> _onManagePhotos(WasteReportDetailModel report) async {
    final didMutate = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (context) => ManageReportPhotosScreen(
          reportId: report.id,
          initialReport: report,
          repository: _repository,
        ),
      ),
    );

    if (!mounted) return;

    if (didMutate == true) {
      _loadAllData(isRefresh: true);
    }
  }

  void _showCancelConfirmationDialog(WasteReportDetailModel report) {
    showDialog<void>(
      context: context,
      barrierDismissible: !_isCancelling,
      builder: (dialogContext) {
        return AlertDialog(
          key: const Key('cancel_report_dialog'),
          title: const Text(
            'Cancel this report?',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          content: const Text(
            "This report will be marked as cancelled. You won't be able to edit it or add photos afterward.",
            style: TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
              height: 1.4,
            ),
          ),
          actionsOverflowButtonSpacing: 8,
          actions: [
            TextButton(
              key: const Key('keep_report_button'),
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Keep Report'),
            ),
            AppButton.destructive(
              key: const Key('confirm_cancel_report_button'),
              label: 'Cancel Report',
              isFullWidth: false,
              height: 36,
              onPressed: () {
                Navigator.of(dialogContext).pop();
                _executeCancelReport(report.id);
              },
            ),
          ],
        );
      },
    );
  }

  Future<void> _executeCancelReport(String reportId) async {
    if (_isCancelling) return;

    setState(() {
      _isCancelling = true;
    });

    try {
      await _repository.cancelWasteReport(reportId);
      if (!mounted) return;

      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          key: Key('cancel_report_success_snackbar'),
          content: Text('Report cancelled.'),
          backgroundColor: AppColors.primary,
          duration: Duration(seconds: 3),
        ),
      );

      await _loadAllData(isRefresh: true);
    } on ApiException catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      if (e.statusCode == 409) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            key: Key('cancel_report_conflict_snackbar'),
            content: Text(
              'This report can no longer be cancelled because its status has changed.',
            ),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 4),
          ),
        );
        await _loadAllData(isRefresh: true);
      } else if (e.statusCode == 403) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            key: const Key('cancel_report_error_snackbar'),
            content: Text(
              e.message.isNotEmpty
                  ? e.message
                  : "You don't have permission to cancel this report.",
            ),
            backgroundColor: AppColors.error,
            duration: const Duration(seconds: 4),
          ),
        );
      } else if (e.statusCode == 404) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            key: const Key('cancel_report_error_snackbar'),
            content: Text(
              e.message.isNotEmpty
                  ? e.message
                  : 'This report could not be found.',
            ),
            backgroundColor: AppColors.error,
            duration: const Duration(seconds: 4),
          ),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            key: const Key('cancel_report_error_snackbar'),
            content: Text(
              e.message.isNotEmpty
                  ? e.message
                  : "Couldn't cancel this report. Please try again.",
            ),
            backgroundColor: AppColors.error,
            duration: const Duration(seconds: 4),
          ),
        );
      }
    } catch (_) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          key: Key('cancel_report_error_snackbar'),
          content: Text("Couldn't cancel this report. Please try again."),
          backgroundColor: AppColors.error,
          duration: Duration(seconds: 4),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _isCancelling = false;
        });
      }
    }
  }

  Widget _buildDescriptionCard(WasteReportDetailModel report) {
    return AppCard(
      key: const Key('report_detail_description_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Description',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            report.description,
            style: const TextStyle(
              fontSize: 14,
              color: AppColors.textPrimary,
              height: 1.45,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLocationCard(WasteReportDetailModel report) {
    return AppCard(
      key: const Key('report_detail_location_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Location',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),

          // Address if provided
          if (report.addressText != null && report.addressText!.trim().isNotEmpty) ...[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Padding(
                  padding: EdgeInsets.only(top: 2),
                  child: Icon(
                    Icons.place_outlined,
                    size: 16,
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    report.addressText!.trim(),
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textPrimary,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 4),
          ],

          // Coordinates
          Row(
              children: [
                const Icon(
                  Icons.my_location_outlined,
                  size: 15,
                  color: AppColors.textSecondary,
                ),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    'Coordinates: ${report.latitude.toStringAsFixed(5)}, ${report.longitude.toStringAsFixed(5)}',
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
              ],
          ),
        ],
      ),
    );
  }

  Widget _buildPhotoEvidenceCard(WasteReportDetailModel report) {
    final attachments = report.attachments;

    return AppCard(
      key: const Key('report_detail_photos_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Expanded(
                child: Text(
                  'Photo Evidence',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.bold,
                    color: AppColors.textPrimary,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Text(
                '${attachments.length} / 3',
                style: const TextStyle(
                  fontSize: 12,
                  color: AppColors.textMuted,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),

          if (attachments.isEmpty)
            Container(
              key: const Key('report_no_photos'),
              width: double.infinity,
              padding: const EdgeInsets.symmetric(vertical: AppSpacing.lg, horizontal: AppSpacing.md),
              decoration: BoxDecoration(
                color: AppColors.background,
                borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                border: Border.all(color: AppColors.border),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: const [
                  Icon(
                    Icons.photo_camera_outlined,
                    size: 32,
                    color: AppColors.textMuted,
                  ),
                  SizedBox(height: AppSpacing.xs),
                  Text(
                    'No photo evidence attached.',
                    style: TextStyle(
                      fontSize: 13,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            )
          else
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.sm,
              children: attachments.map((attachment) => _buildPhotoThumbnail(attachment)).toList(),
            ),

          if (report.status == WasteReportStatus.submitted) ...[
            const SizedBox(height: AppSpacing.md),
            AppButton.outlined(
              key: const Key('manage_photos_button'),
              label: 'Manage Photos',
              icon: Icons.photo_library_outlined,
              height: 40,
              onPressed: () => _onManagePhotos(report),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildPhotoThumbnail(ReportAttachmentModel attachment) {
    return GestureDetector(
      key: Key('photo_thumbnail_${attachment.id}'),
      onTap: () => _showPhotoPreview(context, attachment.fileUrl),
      child: Container(
        width: 96,
        height: 96,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
          border: Border.all(color: AppColors.border),
          color: AppColors.surfaceSubtle,
        ),
        clipBehavior: Clip.antiAlias,
        child: Image.network(
          attachment.fileUrl,
          fit: BoxFit.cover,
          errorBuilder: (context, error, stackTrace) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(4.0),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: const [
                    Icon(
                      Icons.broken_image_outlined,
                      size: 24,
                      color: AppColors.textMuted,
                    ),
                    SizedBox(height: 4),
                    Text(
                      'Photo unavailable',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontSize: 9,
                        color: AppColors.textMuted,
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  void _showPhotoPreview(BuildContext context, String fileUrl) {
    showDialog<void>(
      context: context,
      barrierDismissible: true,
      builder: (dialogContext) {
        return Dialog(
          key: const Key('photo_preview_dialog'),
          backgroundColor: Colors.black87,
          insetPadding: const EdgeInsets.all(AppSpacing.md),
          child: Stack(
            alignment: Alignment.topRight,
            children: [
              Center(
                child: InteractiveViewer(
                  panEnabled: true,
                  minScale: 0.8,
                  maxScale: 4.0,
                  child: Image.network(
                    fileUrl,
                    fit: BoxFit.contain,
                    errorBuilder: (context, error, stackTrace) {
                      return Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: const [
                            Icon(
                              Icons.broken_image_outlined,
                              size: 48,
                              color: Colors.white70,
                            ),
                            SizedBox(height: 8),
                            Text(
                              'Unable to display photo preview',
                              style: TextStyle(color: Colors.white70, fontSize: 13),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                ),
              ),
              IconButton(
                key: const Key('photo_preview_close_button'),
                icon: const Icon(Icons.close, color: Colors.white, size: 28),
                onPressed: () => Navigator.of(dialogContext).pop(),
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _buildStatusHistoryCard() {
    return AppCard(
      key: const Key('report_detail_history_card'),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Status History',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),

          if (_isLoadingHistory && (_history == null || _history!.isEmpty))
            const Padding(
              padding: EdgeInsets.symmetric(vertical: AppSpacing.md),
              child: Center(
                child: AppLoadingIndicator(
                  key: Key('history_loading_indicator'),
                  size: 20,
                  strokeWidth: 2,
                  message: 'Loading status history...',
                ),
              ),
            )
          else if (_historyError != null && (_history == null || _history!.isEmpty))
            Container(
              key: const Key('history_error_card'),
              width: double.infinity,
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: const Color(0xFFFEF2F2),
                borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                border: Border.all(color: const Color(0xFFFECACA)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: const [
                      Icon(Icons.info_outline, size: 16, color: Color(0xFFDC2626)),
                      SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          "Couldn't load status history.",
                          style: TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w600,
                            color: Color(0xFF991B1B),
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.xs),
                  Align(
                    alignment: Alignment.centerRight,
                    child: AppButton.outlined(
                      key: const Key('history_retry_button'),
                      label: 'Retry',
                      isFullWidth: false,
                      height: 36.0,
                      onPressed: () => _loadHistory(),
                    ),
                  ),
                ],
              ),
            )
          else if (_history != null && _history!.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
              child: Text(
                'No status history available.',
                style: TextStyle(fontSize: 13, color: AppColors.textSecondary),
              ),
            )
          else if (_history != null)
            _buildTimeline(_history!),
        ],
      ),
    );
  }

  Widget _buildTimeline(List<WasteReportStatusHistoryModel> history) {
    return Column(
      children: List.generate(history.length, (index) {
        final item = history[index];
        final isLast = index == history.length - 1;
        return _buildTimelineItem(item, isLast: isLast);
      }),
    );
  }

  Widget _buildTimelineItem(WasteReportStatusHistoryModel item, {required bool isLast}) {
    final statusColor = _getStatusDotColor(item.toStatus);

    final String transitionTitle;
    if (item.fromStatus == null) {
      transitionTitle = item.toStatus.displayName;
    } else {
      transitionTitle = '${item.fromStatus!.displayName} → ${item.toStatus.displayName}';
    }

    final hasNotes = item.notes != null && item.notes!.trim().isNotEmpty;
    final isRejection = item.toStatus == WasteReportStatus.rejected;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Indicator column (dot + connector line)
        Column(
          children: [
            Container(
              width: 10,
              height: 10,
              decoration: BoxDecoration(
                color: statusColor,
                shape: BoxShape.circle,
              ),
            ),
            if (!isLast)
              Container(
                width: 2,
                height: hasNotes ? 65 : 45,
                color: AppColors.border,
              ),
          ],
        ),

        const SizedBox(width: AppSpacing.sm),

        // Event content
        Expanded(
          child: Padding(
            padding: EdgeInsets.only(bottom: isLast ? 0 : AppSpacing.sm),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  transitionTitle,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  _dateFormat.format(item.changedAt.toLocal()),
                  style: const TextStyle(
                    fontSize: 11,
                    color: AppColors.textMuted,
                  ),
                ),
                if (hasNotes) ...[
                  const SizedBox(height: 4),
                  Container(
                    key: Key('history_notes_${item.id}'),
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: isRejection ? const Color(0xFFFEF2F2) : AppColors.background,
                      borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                      border: Border.all(
                        color: isRejection ? const Color(0xFFFECACA) : AppColors.border,
                      ),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(
                          isRejection ? Icons.error_outline : Icons.note_outlined,
                          size: 13,
                          color: isRejection ? const Color(0xFFDC2626) : AppColors.textSecondary,
                        ),
                        const SizedBox(width: 4),
                        Expanded(
                          child: Text(
                            item.notes!.trim(),
                            style: TextStyle(
                              fontSize: 11,
                              color: isRejection ? const Color(0xFF991B1B) : AppColors.textSecondary,
                              height: 1.3,
                            ),
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
      ],
    );
  }

  Color _getStatusDotColor(WasteReportStatus status) {
    switch (status) {
      case WasteReportStatus.verified:
      case WasteReportStatus.resolved:
        return const Color(0xFF059669);
      case WasteReportStatus.submitted:
      case WasteReportStatus.underReview:
      case WasteReportStatus.scheduled:
      case WasteReportStatus.inProgress:
        return const Color(0xFFD97706);
      case WasteReportStatus.rejected:
        return const Color(0xFFDC2626);
      case WasteReportStatus.cancelled:
        return const Color(0xFF64748B);
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
    return Icon(icon, size: 14, color: AppColors.primaryDark);
  }

  Widget _buildStatusBadge(WasteReportStatus status) {
    Color bg;
    Color fg;
    Color border;

    switch (status) {
      case WasteReportStatus.verified:
        bg = const Color(0xFFDCFCE7);
        fg = const Color(0xFF065F46);
        border = const Color(0xFFA7F3D0);
        break;
      case WasteReportStatus.resolved:
        bg = const Color(0xFFD1FAE5);
        fg = const Color(0xFF047857);
        border = const Color(0xFFA7F3D0);
        break;
      case WasteReportStatus.submitted:
        bg = const Color(0xFFFEF3C7);
        fg = const Color(0xFF92400E);
        border = const Color(0xFFFDE68A);
        break;
      case WasteReportStatus.underReview:
        bg = const Color(0xFFFEF9C3);
        fg = const Color(0xFF854D0E);
        border = const Color(0xFFFEF08A);
        break;
      case WasteReportStatus.scheduled:
        bg = const Color(0xFFFFFBEB);
        fg = const Color(0xFFB45309);
        border = const Color(0xFFFDE68A);
        break;
      case WasteReportStatus.inProgress:
        bg = const Color(0xFFFFEDD5);
        fg = const Color(0xFF9A3412);
        border = const Color(0xFFFED7AA);
        break;
      case WasteReportStatus.rejected:
        bg = const Color(0xFFFEE2E2);
        fg = const Color(0xFF991B1B);
        border = const Color(0xFFFECACA);
        break;
      case WasteReportStatus.cancelled:
        bg = const Color(0xFFF1F5F9);
        fg = const Color(0xFF475569);
        border = const Color(0xFFCBD5E1);
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
}
