import React, { useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { complaintsApi } from '../api/complaintsApi';
import { useComplaintDetail } from '../hooks/useComplaintDetail';
import { ComplaintStatusBadge } from '../components/ComplaintStatusBadge';
import { ComplaintLocationMap } from '../components/ComplaintLocationMap';
import { ResolveComplaintModal } from '../components/ResolveComplaintModal';
import { Card, CardHeader, CardTitle, CardContent } from '../../../components/ui/Card';
import { Button } from '../../../components/ui/Button';
import { Alert } from '../../../components/ui/Alert';
import { useAuthStore } from '../../../store/authStore';
import { COMPLAINT_CATEGORY_LABELS } from '../types/complaints';

function formatComplaintRef(id: string): string {
  const cleanId = id.replace(/-/g, '');
  return `#${cleanId.slice(0, 8).toUpperCase()}`;
}

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

export const ComplaintDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuthStore();

  const isManager = user?.role === 'MunicipalManager';
  const listPath = isManager ? '/manager/complaints' : '/officer/complaints';

  const [isResolveModalOpen, setIsResolveModalOpen] = useState(false);
  const [isMutating, setIsMutating] = useState(false);
  const [actionFeedback, setActionFeedback] = useState<{
    type: 'success' | 'error';
    message: string;
  } | null>(null);

  const {
    complaint,
    isLoading,
    isError,
    error,
    errorMessage,
    refetch,
  } = useComplaintDetail(id);

  const handleStartReview = async () => {
    if (!complaint || isMutating) return;

    setIsMutating(true);
    setActionFeedback(null);

    try {
      await complaintsApi.startReview(complaint.id);
      setActionFeedback({
        type: 'success',
        message: 'Review started successfully. Complaint status is now Under Review.',
      });

      queryClient.invalidateQueries({ queryKey: ['complaints'] });
      queryClient.invalidateQueries({ queryKey: ['complaint', complaint.id] });
      await refetch();
    } catch (err: unknown) {
      let userFacingMessage = "Couldn't start review for this complaint. Please try again.";

      if (axios.isAxiosError(err)) {
        const statusCode = err.response?.status;
        if (statusCode === 409) {
          userFacingMessage =
            'This complaint can no longer be started for review because its status has changed.';
          await refetch();
        } else if (statusCode === 403) {
          userFacingMessage = "You don't have permission to review this complaint.";
        } else if (statusCode === 404) {
          userFacingMessage = 'This complaint could not be found.';
          await refetch();
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

  const handleConfirmResolve = async (resolutionNote: string) => {
    if (!complaint || isMutating) return;

    setIsMutating(true);
    setActionFeedback(null);

    try {
      await complaintsApi.resolveComplaint(complaint.id, resolutionNote);
      setIsResolveModalOpen(false);
      setActionFeedback({
        type: 'success',
        message: 'Complaint resolved successfully.',
      });

      queryClient.invalidateQueries({ queryKey: ['complaints'] });
      queryClient.invalidateQueries({ queryKey: ['complaint', complaint.id] });
      await refetch();
    } catch (err: unknown) {
      setIsResolveModalOpen(false);
      let userFacingMessage = "Couldn't resolve this complaint. Please try again.";

      if (axios.isAxiosError(err)) {
        const statusCode = err.response?.status;
        if (statusCode === 409) {
          userFacingMessage =
            'This complaint can no longer be resolved because its status has changed.';
          await refetch();
        } else if (statusCode === 403) {
          userFacingMessage = "You don't have permission to resolve this complaint.";
        } else if (statusCode === 404) {
          userFacingMessage = 'This complaint could not be found.';
          await refetch();
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

  // 1. Loading Skeleton State
  if (isLoading) {
    return (
      <div className="space-y-6 animate-pulse" data-testid="complaint-detail-loading">
        <div className="flex items-center justify-between">
          <div className="h-6 w-48 bg-slate-200 rounded" />
          <div className="h-9 w-24 bg-slate-200 rounded" />
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-6">
            <div className="h-44 bg-slate-200 rounded-xl" />
            <div className="h-32 bg-slate-200 rounded-xl" />
            <div className="h-72 bg-slate-200 rounded-xl" />
          </div>
          <div className="space-y-6">
            <div className="h-64 bg-slate-200 rounded-xl" />
          </div>
        </div>
      </div>
    );
  }

  // 2. Error State
  if (isError || !complaint) {
    return (
      <div className="space-y-6" data-testid="complaint-detail-error">
        <div className="flex items-center gap-2">
          <Link
            to={listPath}
            data-testid="back-to-complaints-link"
            className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            Back to Complaints
          </Link>
        </div>

        <Alert variant="error" title="Failed to load complaint details">
          <p className="mt-1">
            {errorMessage || error?.message || 'The requested complaint could not be found or failed to load.'}
          </p>
          <div className="mt-4 flex gap-3">
            <Button variant="secondary" size="sm" onClick={() => refetch()}>
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

  const shortRef = formatComplaintRef(complaint.id);

  return (
    <div className="space-y-6 pb-12" data-testid="complaint-detail-page">
      {/* Back Link & Navigation Bar */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <Link
          to={listPath}
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800 transition-colors"
          data-testid="back-to-complaints-link"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          Back to Complaints
        </Link>

        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => refetch()}
            className="inline-flex items-center gap-1.5 text-xs"
            aria-label="Refresh complaint details"
          >
            <svg className="w-3.5 h-3.5 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
              />
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
              <ComplaintStatusBadge status={complaint.status} />
              <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-700 border border-slate-200">
                {COMPLAINT_CATEGORY_LABELS[complaint.category] || complaint.category}
              </span>
            </div>
            <h1 className="text-xl sm:text-2xl font-bold text-slate-900 mt-2">
              {complaint.subject}
            </h1>
            <p className="text-xs text-slate-500 mt-1 font-mono">
              Complaint ID: {complaint.id}
            </p>
          </div>

          {/* Quick Header Status Badge */}
          <div className="flex items-center sm:justify-end">
            <span className="text-xs text-slate-500">
              Submitted on <strong className="font-semibold text-slate-700">{formatDate(complaint.createdAt)}</strong>
            </span>
          </div>
        </div>
      </div>

      {/* Two-Column Responsive Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left Column (2/3 width on desktop) */}
        <div className="lg:col-span-2 space-y-6">
          {/* 1. Complaint Overview & Citizen Details */}
          <Card>
            <CardHeader className="mb-4">
              <CardTitle className="text-base font-bold text-slate-900">Complaint Overview</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs">
                <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100">
                  <dt className="text-slate-500 font-medium">Category</dt>
                  <dd className="mt-1 text-sm font-semibold text-slate-900">
                    {COMPLAINT_CATEGORY_LABELS[complaint.category] || complaint.category}
                  </dd>
                </div>

                <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100">
                  <dt className="text-slate-500 font-medium">Citizen</dt>
                  <dd className="mt-1 text-sm font-semibold text-slate-900">
                    {complaint.citizenName || 'Citizen'}
                  </dd>
                </div>

                <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100">
                  <dt className="text-slate-500 font-medium">Date Submitted</dt>
                  <dd className="mt-1 text-sm font-medium text-slate-800">
                    {formatDate(complaint.createdAt)}
                  </dd>
                </div>

                <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100">
                  <dt className="text-slate-500 font-medium">Last Updated</dt>
                  <dd className="mt-1 text-sm font-medium text-slate-800">
                    {complaint.updatedAt ? formatDate(complaint.updatedAt) : 'None'}
                  </dd>
                </div>
              </dl>
            </CardContent>
          </Card>

          {/* 2. Citizen Description */}
          <Card>
            <CardHeader className="mb-3">
              <CardTitle className="text-base font-bold text-slate-900">Description</CardTitle>
            </CardHeader>
            <CardContent>
              <div
                className="bg-slate-50/70 rounded-lg p-4 border border-slate-100 text-xs sm:text-sm text-slate-800 leading-relaxed whitespace-pre-wrap break-words"
                data-testid="complaint-description"
              >
                {complaint.description}
              </div>
            </CardContent>
          </Card>

          {/* 3. Reported Location Map (Leaflet / OSM) */}
          <Card>
            <CardHeader className="mb-4">
              <CardTitle className="text-base font-bold text-slate-900">Reported Location</CardTitle>
              <p className="text-xs text-slate-500 mt-0.5">
                Optional incident coordinates provided by citizen on submission
              </p>
            </CardHeader>
            <CardContent>
              <ComplaintLocationMap
                latitude={complaint.latitude}
                longitude={complaint.longitude}
                locationDescription={complaint.locationDescription}
                subject={complaint.subject}
              />
            </CardContent>
          </Card>

          {/* 4. Resolution Section (if resolved or resolution note present) */}
          {complaint.status === 'Resolved' && (
            <Card className="border-emerald-200/80 bg-emerald-50/20 shadow-2xs">
              <CardHeader className="mb-3">
                <div className="flex items-center gap-2">
                  <div className="w-7 h-7 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center">
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                  </div>
                  <CardTitle className="text-base font-bold text-slate-900">
                    Resolution Details
                  </CardTitle>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="bg-white rounded-lg p-4 border border-emerald-200/70 shadow-2xs">
                  <p className="text-xs font-semibold text-slate-700 mb-1.5">Resolution Note</p>
                  <p
                    className="text-xs sm:text-sm text-slate-800 leading-relaxed whitespace-pre-wrap break-words"
                    data-testid="resolution-note-display"
                  >
                    {complaint.resolutionNote || 'No resolution note recorded.'}
                  </p>
                </div>

                <dl className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
                  {complaint.resolvedAt && (
                    <div className="bg-white p-3 rounded-lg border border-slate-200/70">
                      <dt className="text-slate-500 font-medium">Resolved Date</dt>
                      <dd className="mt-0.5 text-xs font-semibold text-slate-900" data-testid="resolved-date">
                        {formatDate(complaint.resolvedAt)}
                      </dd>
                    </div>
                  )}
                  {complaint.resolvedByUserName && (
                    <div className="bg-white p-3 rounded-lg border border-slate-200/70">
                      <dt className="text-slate-500 font-medium">Resolved By</dt>
                      <dd className="mt-0.5 text-xs font-semibold text-slate-900" data-testid="resolved-by">
                        {complaint.resolvedByUserName}
                      </dd>
                    </div>
                  )}
                </dl>
              </CardContent>
            </Card>
          )}
        </div>

        {/* Right Column (1/3 width on desktop) — Lifecycle Actions */}
        <div className="space-y-6">
          <Card>
            <CardHeader className="mb-4">
              <CardTitle className="text-base font-bold text-slate-900">Complaint Actions</CardTitle>
              <p className="text-xs text-slate-500 mt-0.5">
                Staff workflow actions based on current complaint status
              </p>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* 1. Submitted State -> Start Review */}
              {complaint.status === 'Submitted' && (
                <div className="space-y-3" data-testid="submitted-actions">
                  <p className="text-xs text-slate-600 leading-relaxed">
                    This complaint is pending staff review. Start review to transition it to Under Review.
                  </p>
                  <Button
                    type="button"
                    variant="primary"
                    size="sm"
                    disabled={isMutating}
                    isLoading={isMutating}
                    onClick={handleStartReview}
                    className="w-full inline-flex items-center justify-center gap-2 text-xs font-semibold shadow-xs"
                    data-testid="start-review-button"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"
                      />
                    </svg>
                    <span>Start Review</span>
                  </Button>
                </div>
              )}

              {/* 2. InReview State -> Resolve Complaint */}
              {complaint.status === 'InReview' && (
                <div className="space-y-3" data-testid="in-review-actions">
                  <p className="text-xs text-slate-600 leading-relaxed">
                    This complaint is currently under review. Resolve it once remedial actions have been taken.
                  </p>
                  <Button
                    type="button"
                    variant="primary"
                    size="sm"
                    disabled={isMutating}
                    onClick={() => {
                      setActionFeedback(null);
                      setIsResolveModalOpen(true);
                    }}
                    className="w-full inline-flex items-center justify-center gap-2 text-xs font-semibold shadow-xs"
                    data-testid="resolve-complaint-button"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                    <span>Resolve Complaint</span>
                  </Button>
                </div>
              )}

              {/* 3. Resolved State -> Read-only */}
              {complaint.status === 'Resolved' && (
                <div
                  className="bg-emerald-50 border border-emerald-200/80 rounded-lg p-3.5 text-center"
                  data-testid="resolved-readonly-state"
                >
                  <div className="w-8 h-8 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center mx-auto mb-2">
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                  </div>
                  <p className="text-xs font-bold text-emerald-900">Complaint Resolved</p>
                  <p className="text-[11px] text-emerald-700 mt-0.5">
                    This complaint has been resolved and is now in read-only terminal state.
                  </p>
                </div>
              )}

              {/* Information Notice */}
              <div className="pt-3 border-t border-slate-100 text-[11px] text-slate-500 space-y-1">
                <p className="font-semibold text-slate-700">Complaint Lifecycle</p>
                <p>Submitted → In Review → Resolved</p>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Modal for Resolving Complaint */}
      <ResolveComplaintModal
        isOpen={isResolveModalOpen}
        isSubmitting={isMutating}
        onConfirm={handleConfirmResolve}
        onCancel={() => {
          if (!isMutating) setIsResolveModalOpen(false);
        }}
      />
    </div>
  );
};

export default ComplaintDetailPage;
