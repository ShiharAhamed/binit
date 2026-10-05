import 'package:flutter/material.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../../shared/widgets/app_loading_indicator.dart';
import '../data/reporting_repository.dart';
import '../models/report_attachment_model.dart';
import '../models/selected_report_image.dart';
import '../models/waste_report_detail_model.dart';
import '../models/waste_report_status.dart';
import '../services/image_picker_service.dart';

/// Screen allowing citizens to manage photo evidence attachments for an existing submitted waste report (Step 9A.9.3b).
/// Permitted ONLY when the report is in Submitted status.
/// Citizens can add up to 3 photos (JPEG/PNG/WebP, max 5 MB each) or remove existing photos.
class ManageReportPhotosScreen extends StatefulWidget {
  final String reportId;
  final WasteReportDetailModel? initialReport;
  final ReportingRepository? repository;
  final ImagePickerService? imagePickerService;
  final Widget Function(BuildContext context, SelectedReportImage image)? imagePreviewBuilder;

  const ManageReportPhotosScreen({
    super.key,
    required this.reportId,
    this.initialReport,
    this.repository,
    this.imagePickerService,
    this.imagePreviewBuilder,
  });

  @override
  State<ManageReportPhotosScreen> createState() => _ManageReportPhotosScreenState();
}

class _ManageReportPhotosScreenState extends State<ManageReportPhotosScreen> {
  late final ReportingRepository _repository;
  late final ImagePickerService _imagePickerService;

  late List<ReportAttachmentModel> _attachments;

  bool _isLoading = false;
  String? _loadError;
  bool _isUploading = false;
  String? _deletingAttachmentId;
  bool _statusLocked = false;
  bool _didMutate = false;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? ReportingRepository();
    _imagePickerService = widget.imagePickerService ?? DefaultImagePickerService();

    if (widget.initialReport != null) {
      _attachments = List<ReportAttachmentModel>.from(widget.initialReport!.attachments);
      if (widget.initialReport!.status != WasteReportStatus.submitted) {
        _statusLocked = true;
      }
    } else {
      _isLoading = true;
      _attachments = [];
      _fetchReport();
    }
  }

  Future<void> _fetchReport() async {
    setState(() {
      _isLoading = true;
      _loadError = null;
    });

    try {
      final report = await _repository.getWasteReport(widget.reportId);
      if (!mounted) return;

      setState(() {
        _attachments = List<ReportAttachmentModel>.from(report.attachments);
        _isLoading = false;
        if (report.status != WasteReportStatus.submitted) {
          _statusLocked = true;
        }
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _loadError = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _loadError = "Couldn't load report details.";
      });
    }
  }

  void _onAddPhotoPressed() {
    if (_isUploading || _statusLocked || _attachments.length >= 3) return;

    final remaining = 3 - _attachments.length;

    showModalBottomSheet<void>(
      context: context,
      backgroundColor: AppColors.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (sheetContext) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.sm,
            ),
            child: Column(
              key: const Key('add_photo_bottom_sheet'),
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Center(
                  child: Container(
                    width: 36,
                    height: 4,
                    margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                    decoration: BoxDecoration(
                      color: AppColors.border,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs),
                  child: Text(
                    'Add Photo',
                    style: Theme.of(sheetContext).textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.bold,
                          color: AppColors.textPrimary,
                        ),
                  ),
                ),
                const SizedBox(height: AppSpacing.xs),
                ListTile(
                  key: const Key('take_photo_option'),
                  leading: const Icon(Icons.camera_alt_outlined, color: AppColors.primary),
                  title: const Text('Take Photo'),
                  subtitle: const Text('Use camera to capture evidence'),
                  onTap: () {
                    Navigator.of(sheetContext).pop();
                    _onTakePhoto();
                  },
                ),
                ListTile(
                  key: const Key('choose_gallery_option'),
                  leading: const Icon(Icons.photo_library_outlined, color: AppColors.primary),
                  title: const Text('Choose from Gallery'),
                  subtitle: Text('Select up to $remaining photo${remaining > 1 ? 's' : ''}'),
                  onTap: () {
                    Navigator.of(sheetContext).pop();
                    _onPickFromGallery(remaining);
                  },
                ),
                const SizedBox(height: AppSpacing.xs),
                AppButton.text(
                  key: const Key('cancel_photo_picker_button'),
                  label: 'Cancel',
                  onPressed: () => Navigator.of(sheetContext).pop(),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _onTakePhoto() async {
    if (_isUploading || _statusLocked) return;

    if (_attachments.length >= 3) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('You can add up to 3 photos.'),
          duration: Duration(seconds: 2),
        ),
      );
      return;
    }

    SelectedReportImage? image;
    try {
      image = await _imagePickerService.takePhoto();
    } on ImagePickerException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message), duration: const Duration(seconds: 3)),
      );
      return;
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Failed to capture photo. Please try again.'), duration: Duration(seconds: 3)),
      );
      return;
    }

    if (image == null || !mounted) return;

    setState(() {
      _isUploading = true;
    });

    try {
      final newAttachment = await _repository.uploadAttachment(
        reportId: widget.reportId,
        filePath: image.path,
        fileName: image.fileName,
        fileType: image.fileType,
      );

      if (!mounted) return;

      setState(() {
        _attachments.add(newAttachment);
        _didMutate = true;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Photo added successfully.'),
          backgroundColor: AppColors.primary,
          duration: Duration(seconds: 2),
        ),
      );
    } on ApiException catch (e) {
      if (!mounted) return;
      if (e.statusCode == 409) {
        setState(() {
          _statusLocked = true;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            key: Key('photo_status_conflict_snackbar'),
            content: Text('This report can no longer be changed because its status has changed.'),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 4),
          ),
        );
      } else if (e.statusCode == 403) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text("You don't have access to change this report."),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 4),
          ),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(e.message),
            backgroundColor: AppColors.error,
            duration: const Duration(seconds: 4),
          ),
        );
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Failed to upload photo. Please try again.'),
          backgroundColor: AppColors.error,
          duration: Duration(seconds: 4),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _isUploading = false;
        });
      }
    }
  }

  Future<void> _onPickFromGallery(int maxImages) async {
    if (_isUploading || _statusLocked) return;

    List<SelectedReportImage> images;
    try {
      images = await _imagePickerService.pickFromGallery(maxImages: maxImages);
    } on ImagePickerException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message), duration: const Duration(seconds: 3)),
      );
      return;
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Failed to select photos. Please try again.'), duration: Duration(seconds: 3)),
      );
      return;
    }

    if (images.isEmpty || !mounted) return;

    setState(() {
      _isUploading = true;
    });

    int successCount = 0;
    int failCount = 0;
    bool hadConflict = false;
    bool hadForbidden = false;

    for (final img in images) {
      if (!mounted || _statusLocked) break;
      if (_attachments.length >= 3) break;

      try {
        final newAttachment = await _repository.uploadAttachment(
          reportId: widget.reportId,
          filePath: img.path,
          fileName: img.fileName,
          fileType: img.fileType,
        );

        if (!mounted) return;

        setState(() {
          _attachments.add(newAttachment);
          _didMutate = true;
        });
        successCount++;
      } on ApiException catch (e) {
        failCount++;
        if (e.statusCode == 409) {
          hadConflict = true;
          setState(() {
            _statusLocked = true;
          });
          break;
        } else if (e.statusCode == 403) {
          hadForbidden = true;
          break;
        }
      } catch (_) {
        failCount++;
      }
    }

    if (!mounted) return;

    setState(() {
      _isUploading = false;
    });

    if (hadConflict) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          key: Key('photo_status_conflict_snackbar'),
          content: Text('This report can no longer be changed because its status has changed.'),
          backgroundColor: AppColors.error,
          duration: Duration(seconds: 4),
        ),
      );
    } else if (hadForbidden) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text("You don't have access to change this report."),
          backgroundColor: AppColors.error,
          duration: Duration(seconds: 4),
        ),
      );
    } else if (successCount > 0 && failCount == 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('$successCount photo${successCount > 1 ? 's' : ''} uploaded successfully.'),
          backgroundColor: AppColors.primary,
          duration: const Duration(seconds: 2),
        ),
      );
    } else if (successCount > 0 && failCount > 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('$successCount photo${successCount > 1 ? 's' : ''} uploaded. $failCount photo${failCount > 1 ? 's' : ''} couldn\'t be uploaded.'),
          backgroundColor: const Color(0xFFD97706),
          duration: const Duration(seconds: 4),
        ),
      );
    } else if (failCount > 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Failed to upload photo${failCount > 1 ? 's' : ''}. Please try again.'),
          backgroundColor: AppColors.error,
          duration: const Duration(seconds: 4),
        ),
      );
    }
  }

  void _onRemoveAttachmentTapped(ReportAttachmentModel attachment) {
    if (_isUploading || _statusLocked || _deletingAttachmentId != null) return;

    showDialog<void>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          key: const Key('delete_attachment_dialog'),
          title: const Text(
            'Remove this photo?',
            style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
          ),
          content: const Text(
            'This photo will be removed from your report.',
            style: TextStyle(fontSize: 14, color: AppColors.textSecondary),
          ),
          actions: [
            TextButton(
              key: const Key('cancel_delete_button'),
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancel'),
            ),
            AppButton.destructive(
              key: const Key('confirm_delete_button'),
              label: 'Remove',
              isFullWidth: false,
              height: 36,
              onPressed: () {
                Navigator.of(dialogContext).pop();
                _deleteAttachment(attachment);
              },
            ),
          ],
        );
      },
    );
  }

  Future<void> _deleteAttachment(ReportAttachmentModel attachment) async {
    setState(() {
      _deletingAttachmentId = attachment.id;
    });

    try {
      await _repository.deleteAttachment(
        reportId: widget.reportId,
        attachmentId: attachment.id,
      );

      if (!mounted) return;

      setState(() {
        _attachments.removeWhere((a) => a.id == attachment.id);
        _didMutate = true;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Photo removed successfully.'),
          backgroundColor: AppColors.primary,
          duration: Duration(seconds: 2),
        ),
      );
    } on ApiException catch (e) {
      if (!mounted) return;

      if (e.statusCode == 409) {
        setState(() {
          _statusLocked = true;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            key: Key('photo_status_conflict_snackbar'),
            content: Text('This report can no longer be changed because its status has changed.'),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 4),
          ),
        );
      } else if (e.statusCode == 403) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text("You don't have access to change this report."),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 4),
          ),
        );
      } else if (e.statusCode == 404) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('This attachment could not be found.'),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 4),
          ),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text("Couldn't remove this photo. Please try again."),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 4),
          ),
        );
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text("Couldn't remove this photo. Please try again."),
          backgroundColor: AppColors.error,
          duration: Duration(seconds: 4),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _deletingAttachmentId = null;
        });
      }
    }
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
                            Icon(Icons.broken_image_outlined, size: 48, color: Colors.white70),
                            SizedBox(height: 8),
                            Text('Unable to display photo preview', style: TextStyle(color: Colors.white70, fontSize: 13)),
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

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (didPop) return;
        Navigator.of(context).pop(_didMutate);
      },
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          title: const Text(
            'Manage Photos',
            style: TextStyle(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.bold,
              fontSize: 18,
            ),
          ),
          backgroundColor: AppColors.surface,
          elevation: 0,
          iconTheme: const IconThemeData(color: AppColors.textPrimary),
          leading: BackButton(
            key: const Key('manage_photos_back_button'),
            onPressed: () => Navigator.of(context).pop(_didMutate),
          ),
          centerTitle: false,
        ),
        body: _buildBody(),
      ),
    );
  }

  Widget _buildBody() {
    if (_isLoading) {
      return const Center(
        child: AppLoadingIndicator(
          key: Key('manage_photos_loading'),
          message: 'Loading report photos...',
        ),
      );
    }

    if (_loadError != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.xl),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline_rounded, size: 48, color: AppColors.error),
              const SizedBox(height: AppSpacing.md),
              Text(
                _loadError!,
                key: const Key('manage_photos_error_text'),
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 14, color: AppColors.textSecondary),
              ),
              const SizedBox(height: AppSpacing.lg),
              AppButton.primary(
                key: const Key('manage_photos_retry_button'),
                label: 'Retry',
                icon: Icons.refresh,
                onPressed: _fetchReport,
              ),
            ],
          ),
        ),
      );
    }

    final count = _attachments.length;
    final canAddMore = count < 3 && !_statusLocked;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 540),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Status Lock Notice if not Submitted
              if (_statusLocked) ...[
                Container(
                  key: const Key('status_locked_banner'),
                  padding: const EdgeInsets.all(AppSpacing.sm),
                  decoration: BoxDecoration(
                    color: const Color(0xFFFEF2F2),
                    borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                    border: Border.all(color: const Color(0xFFFECACA)),
                  ),
                  child: Row(
                    children: const [
                      Icon(Icons.lock_outline, size: 18, color: Color(0xFFDC2626)),
                      SizedBox(width: AppSpacing.xs),
                      Expanded(
                        child: Text(
                          'Photos can only be added or removed while the report is in Submitted status.',
                          style: TextStyle(
                            fontSize: 12,
                            color: Color(0xFF991B1B),
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.md),
              ],

              // Main Photo Card
              AppCard(
                key: const Key('manage_photos_card'),
                padding: const EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Header Row: Title & Counter
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
                          '$count / 3 photos',
                          key: const Key('manage_photos_counter'),
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.textMuted,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    const Text(
                      'Clear photographic evidence helps municipal officers inspect and route waste issues faster.',
                      style: TextStyle(fontSize: 12, color: AppColors.textSecondary),
                    ),
                    const SizedBox(height: AppSpacing.md),

                    // Photo Grid or Empty State
                    if (_attachments.isEmpty)
                      Container(
                        key: const Key('manage_photos_empty_state'),
                        width: double.infinity,
                        padding: const EdgeInsets.symmetric(vertical: AppSpacing.xl, horizontal: AppSpacing.md),
                        decoration: BoxDecoration(
                          color: AppColors.background,
                          borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
                          border: Border.all(color: AppColors.border),
                        ),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: const [
                            Icon(Icons.photo_camera_outlined, size: 36, color: AppColors.textMuted),
                            SizedBox(height: AppSpacing.xs),
                            Text(
                              'No photos attached.',
                              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: AppColors.textSecondary),
                            ),
                            SizedBox(height: 2),
                            Text(
                              'You can add up to 3 photos as evidence.',
                              style: TextStyle(fontSize: 11, color: AppColors.textMuted),
                            ),
                          ],
                        ),
                      )
                    else
                      Wrap(
                        spacing: AppSpacing.sm,
                        runSpacing: AppSpacing.sm,
                        children: _attachments.map((att) => _buildAttachmentTile(att)).toList(),
                      ),

                    const SizedBox(height: AppSpacing.md),

                    // Add Photo CTA or Max notice
                    if (canAddMore) ...[
                      AppButton.outlined(
                        key: const Key('add_photo_button'),
                        label: 'Add Photo',
                        icon: Icons.add_a_photo_outlined,
                        isLoading: _isUploading,
                        onPressed: _isUploading ? null : _onAddPhotoPressed,
                      ),
                    ] else if (count >= 3) ...[
                      Container(
                        key: const Key('max_photos_reached_notice'),
                        width: double.infinity,
                        padding: const EdgeInsets.all(AppSpacing.sm),
                        decoration: BoxDecoration(
                          color: AppColors.surfaceSubtle,
                          borderRadius: BorderRadius.circular(AppSpacing.radiusSm),
                          border: Border.all(color: AppColors.border),
                        ),
                        child: const Row(
                          children: [
                            Icon(Icons.info_outline, size: 16, color: AppColors.textSecondary),
                            SizedBox(width: 8),
                            Expanded(
                              child: Text(
                                'Maximum of 3 photos reached.',
                                style: TextStyle(fontSize: 12, color: AppColors.textSecondary, fontWeight: FontWeight.w500),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ],
                ),
              ),

              const SizedBox(height: AppSpacing.lg),

              // Done / Return Button
              AppButton.primary(
                key: const Key('done_button'),
                label: 'Done',
                icon: Icons.check,
                onPressed: () => Navigator.of(context).pop(_didMutate),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildAttachmentTile(ReportAttachmentModel attachment) {
    final isDeleting = _deletingAttachmentId == attachment.id;

    return Container(
      key: Key('attachment_tile_${attachment.id}'),
      width: 96,
      height: 96,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
        border: Border.all(color: AppColors.border),
        color: AppColors.surfaceSubtle,
      ),
      clipBehavior: Clip.antiAlias,
      child: Stack(
        fit: StackFit.expand,
        children: [
          // Image thumbnail with preview tap
          GestureDetector(
            key: Key('photo_preview_gesture_${attachment.id}'),
            behavior: HitTestBehavior.opaque,
            onTap: () => _showPhotoPreview(context, attachment.fileUrl),
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
                        Icon(Icons.broken_image_outlined, size: 24, color: AppColors.textMuted),
                        SizedBox(height: 4),
                        Text('Photo unavailable', textAlign: TextAlign.center, style: TextStyle(fontSize: 9, color: AppColors.textMuted)),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),

          // Delete Button or Loading Spinner on top right
          if (!_statusLocked)
            Positioned(
              top: 4,
              right: 4,
              child: isDeleting
                  ? Container(
                      padding: const EdgeInsets.all(4),
                      decoration: const BoxDecoration(color: Colors.black54, shape: BoxShape.circle),
                      child: const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      ),
                    )
                  : Material(
                      color: Colors.black54,
                      shape: const CircleBorder(),
                      child: InkWell(
                        key: Key('remove_attachment_button_${attachment.id}'),
                        customBorder: const CircleBorder(),
                        onTap: (_isUploading || _deletingAttachmentId != null)
                            ? null
                            : () => _onRemoveAttachmentTapped(attachment),
                        child: const Padding(
                          padding: EdgeInsets.all(4),
                          child: Icon(Icons.delete_outline, size: 16, color: Colors.white),
                        ),
                      ),
                    ),
            ),
        ],
      ),
    );
  }
}
