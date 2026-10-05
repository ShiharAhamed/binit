import React, { useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { reportingApi } from '../api/reportingApi';
import { useWasteReportDetail } from '../hooks/useWasteReportDetail';
import { useWasteReportHistory } from '../hooks/useWasteReportHistory';
import { WasteReportStatusBadge } from '../components/WasteReportStatusBadge';
import { WasteReportPriorityBadge } from '../components/WasteReportPriorityBadge';
import { ReportMapView } from '../components/ReportMapView';
import { ReportPhotoGallery } from '../components/ReportPhotoGallery';
import { ReportStatusHistoryTimeline } from '../components/ReportStatusHistoryTimeline';
import { StartReviewModal } from '../components/StartReviewModal';
import { VerifyReportModal } from '../components/VerifyReportModal';
import { RejectReportModal } from '../components/RejectReportModal';
import { Card, CardHeader, CardTitle, CardContent } from '../../../components/ui/Card';
import { Button } from '../../../components/ui/Button';
import { Alert } from '../../../components/ui/Alert';
import { useAuthStore } from '../../../store/authStore';
import { WASTE_TYPE_LABELS } from '../types/reporting';
import { reportLabel, reportReference } from '../../../utils/displayReferences';
import { canReviewWasteReports } from '../utils/reportReviewAuthority';

function formatDate(isoString: string): string {
  try {
    const d = new Date(isoString);
    return new Intl.DateTimeFormat('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(d);
  } catch {
    return isoString;
  }
}

export const WasteReportDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuthStore();

  const canReview = canReviewWasteReports(user?.role);
  const listPath = user?.role === 'MunicipalManager' ? '/manager/reports' : '/officer/waste-reports';

  const [isStartReviewModalOpen, setIsStartReviewModalOpen] = useState(false);
  const [isVerifyModalOpen, setIsVerifyModalOpen] = useState(false);
  const [isRejectModalOpen, setIsRejectModalOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [isMutating, setIsMutating] = useState(false);
  const [actionFeedback, setActionFeedback] = useState<{
    type: 'success' | 'error';
    message: string;
  } | null>(null);

  const {
    report,
    isLoading: isReportLoading,
    isError: isReportError,
    error: reportError,
    refetch: refetchReport,
  } = useWasteReportDetail(id);

  const {
    history,
    isLoading: isHistoryLoading,
    isError: isHistoryError,
    error: historyError,
    refetch: refetchHistory,
  } = useWasteReportHistory(id);

  const handleRefreshAll = () => {
    refetchReport();
    refetchHistory();
  };

  const handleConfirmStartReview = async () => {
    if (!report || isMutating) return;

    setIsMutating(true);
    setActionFeedback(null);

    try {
      await reportingApi.startReview(report.id);
      setIsStartReviewModalOpen(false);
      setActionFeedback({
        type: 'success',
        message: 'Review started successfully.',
      });

      // Invalidate list queries so operational list updates upon return
      queryClient.invalidateQueries({ queryKey: ['waste-reports'] });
      await Promise.all([refetchReport(), refetchHistory()]);
    } catch (err: unknown) {
      setIsStartReviewModalOpen(false);
      let userFacingMessage = "Couldn't start the review. Please try again.";

      if (axios.isAxiosError(err)) {
        const statusCode = err.response?.status;
        if (statusCode === 409) {
          userFacingMessage =
            'This report can no longer be started for review because its status has changed.';
          await Promise.all([refetchReport(), refetchHistory()]);
        } else if (statusCode === 403) {
          userFacingMessage = "You don't have permission to start review for this report.";
        } else if (statusCode === 404) {
          userFacingMessage = 'This report could not be found.';
          await refetchReport();
        } else if (statusCode === 401) {
          userFacingMessage = 'Your session has expired. Please log in again.';
        } else if (err.response?.data?.detail && typeof err.response.data.detail === 'string') {
          userFacingMessage = err.response.data.detail;
        }
      }

      setActionFeedback({
        type: 'error',
        message: userFacingMessage,
      });
    } finally {
      setIsMutating(false);
    }
  };

  const handleConfirmVerify = async (priority: import('../types/reporting').WasteReportPriority) => {
    if (!report || isMutating) return;

    setIsMutating(true);
    setActionFeedback(null);

    try {
      await reportingApi.verifyReport(report.id, priority);
      setIsVerifyModalOpen(false);
      setActionFeedback({
        type: 'success',
        message: 'Report verified successfully.',
      });

      // Invalidate list queries so operational list updates upon return
      queryClient.invalidateQueries({ queryKey: ['waste-reports'] });
      await Promise.all([refetchReport(), refetchHistory()]);
    } catch (err: unknown) {
      setIsVerifyModalOpen(false);
      let userFacingMessage = "Couldn't verify this report. Please try again.";

      if (axios.isAxiosError(err)) {
        const statusCode = err.response?.status;
        if (statusCode === 409) {
          userFacingMessage =
            'This report can no longer be updated because its status has changed.';
          await Promise.all([refetchReport(), refetchHistory()]);
        } else if (statusCode === 403) {
          userFacingMessage = "You don't have permission to review this report.";
        } else if (statusCode === 404) {
          userFacingMessage = 'This report could not be found.';
          await refetchReport();
        } else if (statusCode === 401) {
          userFacingMessage = 'Your session has expired. Please log in again.';
        } else if (err.response?.data?.detail && typeof err.response.data.detail === 'string') {
          userFacingMessage = err.response.data.detail;
        }
      }

      setActionFeedback({
        type: 'error',
        message: userFacingMessage,
      });
    } finally {
      setIsMutating(false);
    }
  };

  const handleConfirmReject = async () => {
    if (!report || isMutating) return;

    setIsMutating(true);
    setActionFeedback(null);

    try {
      await reportingApi.rejectReport(report.id, rejectReason.trim());
      setIsRejectModalOpen(false);
      setRejectReason(''); // Clear reason on success
      setActionFeedback({
        type: 'success',
        message: 'Report rejected.',
      });

      // Invalidate list queries so operational list updates upon return
      queryClient.invalidateQueries({ queryKey: ['waste-reports'] });
      await Promise.all([refetchReport(), refetchHistory()]);
    } catch (err: unknown) {
      setIsRejectModalOpen(false);
      // Retain rejectReason in state so user doesn't lose entered text
      let userFacingMessage = "Couldn't reject this report. Please try again.";

      if (axios.isAxiosError(err)) {
        const statusCode = err.response?.status;
        if (statusCode === 409) {
          userFacingMessage =
            'This report can no longer be updated because its status has changed.';
          await Promise.all([refetchReport(), refetchHistory()]);
        } else if (statusCode === 403) {
          userFacingMessage = "You don't have permission to review this report.";
        } else if (statusCode === 404) {
          userFacingMessage = 'This report could not be found.';
          await refetchReport();
        } else if (statusCode === 401) {
          userFacingMessage = 'Your session has expired. Please log in again.';
        } else if (err.response?.data?.detail && typeof err.response.data.detail === 'string') {
          userFacingMessage = err.response.data.detail;
        }
      }

      setActionFeedback({
        type: 'error',
        message: userFacingMessage,
      });
    } finally {
      setIsMutating(false);
    }
  };

  // 1. Loading State
  if (isReportLoading) {
    return (
      <div className="space-y-6 animate-pulse" data-testid="report-detail-loading">
        <div className="flex items-center justify-between">
          <div className="h-6 w-48 bg-slate-200 rounded" />
          <div className="h-9 w-24 bg-slate-200 rounded" />
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-6">
            <div className="h-44 bg-slate-200 rounded-xl" />
            <div className="h-32 bg-slate-200 rounded-xl" />
            <div className="h-80 bg-slate-200 rounded-xl" />
          </div>
          <div className="space-y-6">
            <div className="h-72 bg-slate-200 rounded-xl" />
            <div className="h-80 bg-slate-200 rounded-xl" />
          </div>
        </div>
      </div>
    );
  }

  // 2. Error State
  if (isReportError || !report) {
    return (
      <div className="space-y-6" data-testid="report-detail-error">
        <div className="flex items-center gap-2">
          <Link
            to={listPath}
            data-testid="back-to-reports-link"
            className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            Back to Waste Reports
          </Link>
        </div>

        <Alert variant="error" title="Failed to load waste report">
          <p className="mt-1">
            {reportError?.message || 'The requested waste report could not be found or failed to load.'}
          </p>
          <div className="mt-4 flex gap-3">
            <Button variant="secondary" size="sm" onClick={() => refetchReport()}>
              Retry
            </Button>
            <Button variant="ghost" size="sm" onClick={() => navigate(listPath)}>
              Return to List
            </Button>
          </div>
        </Alert>
      </div>
    );
  }

  const shortRef = reportLabel(report.id, report.reportReference);
  const locationText =
    report.addressText && report.addressText.trim()
      ? report.addressText
      : `${report.latitude.toFixed(5)}, ${report.longitude.toFixed(5)}`;

  return (
    <div className="space-y-6 pb-12" data-testid="report-detail-page">
      {/* Back Link & Navigation Bar */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <Link
          to={listPath}
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800 transition-colors"
          data-testid="back-to-reports-link"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          Back to Waste Reports
        </Link>

        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={handleRefreshAll}
            className="inline-flex items-center gap-1.5 text-xs"
            aria-label="Refresh report details and history"
          >
            <svg className="w-3.5 h-3.5 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refresh
          </Button>
        </div>
      </div>

      {/* Action Feedback Alert */}
      {actionFeedback && (
        <Alert
          variant={actionFeedback.type === 'success' ? 'success' : 'error'}
          title={actionFeedback.type === 'success' ? 'Success' : 'Action Failed'}
          data-testid="action-feedback-alert"
        >
          {actionFeedback.message}
        </Alert>
      )}

      {/* Main Header / Title Card */}
      <div className="bg-white rounded-xl border border-slate-200 p-5 sm:p-6 shadow-2xs">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="font-mono text-xs font-bold text-emerald-800 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200/80">
                {shortRef}
              </span>
              <WasteReportStatusBadge status={report.status} />
              <WasteReportPriorityBadge priority={report.priority} />
            </div>
            <h1 className="text-xl sm:text-2xl font-bold text-slate-900 mt-2">
              Waste Report Details
            </h1>
            <p className="text-xs text-slate-500 mt-1 font-mono">
              Reference: {reportReference(report.id, report.reportReference)}
            </p>
          </div>

          {/* Report lifecycle actions are available to authorized staff. */}
          <div className="flex items-center sm:justify-end gap-3">
            {canReview && report.status === 'Submitted' ? (
              <Button
                type="button"
                variant="primary"
                size="sm"
                disabled={isMutating}
                onClick={() => {
                  setActionFeedback(null);
                  setIsStartReviewModalOpen(true);
                }}
                className="inline-flex items-center gap-1.5 text-xs font-semibold shadow-xs"
                data-testid="start-review-button"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4" />
                </svg>
                <span>Start Review</span>
              </Button>
            ) : canReview && report.status === 'UnderReview' ? (
              <div className="flex items-center gap-2.5">
                <Button
                  type="button"
                  variant="primary"
                  size="sm"
                  disabled={isMutating}
                  onClick={() => {
                    setActionFeedback(null);
                    setIsVerifyModalOpen(true);
                  }}
                  className="inline-flex items-center gap-1.5 text-xs font-semibold shadow-xs"
                  data-testid="verify-report-button"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                  </svg>
                  <span>Verify Report</span>
                </Button>
                <Button
                  type="button"
                  variant="danger"
                  size="sm"
                  disabled={isMutating}
                  onClick={() => {
                    setActionFeedback(null);
                    setIsRejectModalOpen(true);
                  }}
                  className="inline-flex items-center gap-1.5 text-xs font-semibold shadow-xs"
                  data-testid="reject-report-button"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                  </svg>
                  <span>Reject Report</span>
                </Button>
              </div>
            ) : (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200">
                <svg className="w-3.5 h-3.5 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                </svg>
                {report.status === 'UnderReview' ? 'Under Review' : 'Read-Only Review Mode'}
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Two-Column Responsive Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left / Main Column (2/3 width on desktop) */}
        <div className="lg:col-span-2 space-y-6">
          {/* 1. Core Attributes & Citizen Details */}
          <Card>
            <CardHeader className="mb-4">
              <CardTitle className="text-base font-bold text-slate-900">Report Overview</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs">
                <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100">
                  <dt className="text-slate-500 font-medium">Waste Type</dt>
                  <dd className="mt-1 text-sm font-semibold text-slate-900">
                    {WASTE_TYPE_LABELS[report.wasteType] || report.wasteType}
                  </dd>
                </div>

                <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100">
                  <dt className="text-slate-500 font-medium">Reported By</dt>
                  <dd className="mt-1 text-sm font-semibold text-slate-900">
                    {report.citizenName || 'Citizen'}
                  </dd>
                </div>

                <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100">
                  <dt className="text-slate-500 font-medium">Date Submitted</dt>
                  <dd className="mt-1 text-sm font-medium text-slate-800">
                    {formatDate(report.createdAt)}
                  </dd>
                </div>

                <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100">
                  <dt className="text-slate-500 font-medium">Last Updated</dt>
                  <dd className="mt-1 text-sm font-medium text-slate-800">
                    {report.updatedAt ? formatDate(report.updatedAt) : 'None'}
                  </dd>
                </div>

                {report.verifiedByUserName && (
                  <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100 sm:col-span-2">
                    <dt className="text-slate-500 font-medium">Verification Details</dt>
                    <dd className="mt-1 text-xs text-slate-800">
                      Verified by <strong className="font-semibold">{report.verifiedByUserName}</strong>
                      {report.verifiedAt && ` on ${formatDate(report.verifiedAt)}`}
                    </dd>
                  </div>
                )}
              </dl>
            </CardContent>
          </Card>

          {/* 2. Citizen Description */}
          <Card>
            <CardHeader className="mb-3">
              <CardTitle className="text-base font-bold text-slate-900">Citizen Description</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="bg-slate-50/70 rounded-lg p-4 border border-slate-100 text-xs sm:text-sm text-slate-800 leading-relaxed whitespace-pre-wrap break-words">
                {report.description}
              </div>
            </CardContent>
          </Card>

          {/* 3. Real OpenStreetMap Location */}
          <Card>
            <CardHeader className="mb-4">
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle className="text-base font-bold text-slate-900">Incident Location</CardTitle>
                  <p className="text-xs text-slate-500 mt-0.5">
                    Real interactive OpenStreetMap showing citizen-selected incident coordinates
                  </p>
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              {/* Location metadata bar */}
              <div className="flex items-start gap-2 text-xs text-slate-600 bg-slate-50 p-3 rounded-lg border border-slate-100">
                <svg className="w-4 h-4 text-emerald-600 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                </svg>
                <div className="flex-1">
                  <p className="font-semibold text-slate-800">{locationText}</p>
                  <p className="text-[11px] font-mono text-slate-500 mt-0.5">
                    Latitude: {report.latitude.toFixed(6)} | Longitude: {report.longitude.toFixed(6)}
                  </p>
                </div>
              </div>

              {/* Map Canvas */}
              <ReportMapView
                latitude={report.latitude}
                longitude={report.longitude}
                addressText={report.addressText}
              />
            </CardContent>
          </Card>

          {/* 4. Photographic Evidence Gallery */}
          <Card>
            <CardHeader className="mb-3">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base font-bold text-slate-900">
                  Photo Evidence ({report.attachments ? report.attachments.length : 0})
                </CardTitle>
                <span className="text-[11px] text-slate-500">
                  Click any image to expand full-size preview
                </span>
              </div>
            </CardHeader>
            <CardContent>
              <ReportPhotoGallery attachments={report.attachments || []} />
            </CardContent>
          </Card>
        </div>

        {/* Right Column (1/3 width on desktop) — Status Audit Timeline */}
        <div className="space-y-6">
          <Card>
            <CardHeader className="mb-4">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base font-bold text-slate-900">Status History</CardTitle>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => refetchHistory()}
                  className="p-1 h-7 text-slate-400 hover:text-slate-600"
                  aria-label="Refresh status history"
                >
                  <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                </Button>
              </div>
              <p className="text-xs text-slate-500 mt-0.5">
                Chronological audit record of report lifecycle transitions
              </p>
            </CardHeader>
            <CardContent>
              <ReportStatusHistoryTimeline
                history={history}
                isLoading={isHistoryLoading}
                isError={isHistoryError}
                error={historyError}
                onRetry={refetchHistory}
              />
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Authoritative report actions are shared by managers and officers. */}
      {canReview && (
        <>
          <StartReviewModal
            isOpen={isStartReviewModalOpen}
            isSubmitting={isMutating}
            onConfirm={handleConfirmStartReview}
            onCancel={() => {
              if (!isMutating) setIsStartReviewModalOpen(false);
            }}
          />

          <VerifyReportModal
            isOpen={isVerifyModalOpen}
            isSubmitting={isMutating}
            onConfirm={handleConfirmVerify}
            onCancel={() => {
              if (!isMutating) setIsVerifyModalOpen(false);
            }}
          />

          <RejectReportModal
            isOpen={isRejectModalOpen}
            isSubmitting={isMutating}
            reason={rejectReason}
            onReasonChange={setRejectReason}
            onConfirm={handleConfirmReject}
            onCancel={() => {
              if (!isMutating) setIsRejectModalOpen(false);
            }}
          />
        </>
      )}
    </div>
  );
};
