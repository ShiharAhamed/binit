import React, { useState } from 'react';
import axios from 'axios';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { Button } from '../../../components/ui/Button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { reportingApi } from '../../reporting/api/reportingApi';
import { StartReviewModal } from '../../reporting/components/StartReviewModal';
import { VerifyReportModal } from '../../reporting/components/VerifyReportModal';
import { RejectReportModal } from '../../reporting/components/RejectReportModal';
import { WasteReportStatusBadge } from '../../reporting/components/WasteReportStatusBadge';
import type { WasteReportPriority } from '../../reporting/types/reporting';
import { canReviewWasteReports, getWasteReportDetailPath } from '../../reporting/utils/reportReviewAuthority';
import { useAuthStore } from '../../../store/authStore';
import { agentWorkflowApi } from '../api/agentWorkflowApi';
import { WorkflowStatusBadge } from '../components/WorkflowStatusBadge';
import { WorkflowTimeline } from '../components/WorkflowTimeline';
import { SharedPlannerSection } from '../components/SharedPlannerSection';
import { WasteAnalysisSection } from '../components/WasteAnalysisSection';
import { CollectionPlanningSection } from '../components/CollectionPlanningSection';
import { CollectionPlanReviewPanel } from '../components/CollectionPlanReviewPanel';
import { FleetRouteSection } from '../components/FleetRouteSection';
import { OperationalValidationSection } from '../components/OperationalValidationSection';
import { DispatchReviewPanel } from '../components/DispatchReviewPanel';
import { formatWorkflowDate, getAiApprovalsBasePath, getTriggerReportLabel, getWorkflowStepLabel, getWorkflowTriggerLabel, isReportTriggeredWorkflow } from '../utils/workflowPresentation';
import { findLatestWorkflowStep, getFleetRouteOutput, getWasteAnalysisOutput, getWorkflowFailureSummary } from '../utils/workflowDetailPresentation';

type ReportAction = 'start' | 'verify' | 'reject';

const reportActionError = (error: unknown): string => {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 409) return 'The report or workflow changed. The latest state has been loaded; review it before trying again.';
    if (error.response?.status === 403) return 'You do not have permission to review this report.';
    if (error.response?.status === 404) return 'The report could not be found.';
    if (error.response?.status === 401) return 'Your session has expired. Please sign in again.';
  }
  return 'The report decision could not be confirmed. Refresh the report and workflow before trying again.';
};

/** Shared, read-only persisted-workflow detail for equal-authority Manager and WasteOfficer roles. */
export const AiWorkflowDetailPage: React.FC = () => {
  const { workflowId } = useParams<{ workflowId: string }>();
  const { user } = useAuthStore();
  const queryClient = useQueryClient();
  const basePath = getAiApprovalsBasePath(user?.role);
  const workflowQuery = useQuery({ queryKey: ['agent-workflow', workflowId], queryFn: () => agentWorkflowApi.getWorkflow(workflowId ?? ''), enabled: Boolean(workflowId) });
  const reportId = workflowQuery.data && isReportTriggeredWorkflow(workflowQuery.data) ? workflowQuery.data.triggeringWasteReportId : null;
  const reportQuery = useQuery({ queryKey: ['waste-report', reportId], queryFn: () => reportingApi.getWasteReport(reportId!), enabled: Boolean(reportId), retry: false });
  const [openAction, setOpenAction] = useState<ReportAction | null>(null);
  const [isMutating, setIsMutating] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [actionFeedback, setActionFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const submitReportAction = async (action: ReportAction, priority?: WasteReportPriority) => {
    if (!reportId || isMutating) return;
    setIsMutating(true);
    setActionFeedback(null);
    try {
      if (action === 'start') await reportingApi.startReview(reportId);
      else if (action === 'verify') {
        if (!priority) return;
        await reportingApi.verifyReport(reportId, priority);
      } else await reportingApi.rejectReport(reportId, rejectReason.trim());
      setOpenAction(null);
      if (action === 'reject') setRejectReason('');
      setActionFeedback({ type: 'success', message: action === 'start' ? 'Review started.' : action === 'verify' ? 'Report verified. Collection planning is being prepared.' : 'Report rejected. Planning has stopped.' });
      await queryClient.invalidateQueries({ queryKey: ['waste-reports'] });
    } catch (error) {
      setOpenAction(null);
      setActionFeedback({ type: 'error', message: reportActionError(error) });
    } finally {
      await Promise.all([reportQuery.refetch(), workflowQuery.refetch()]);
      setIsMutating(false);
    }
  };
  if (!workflowId) return <Card><CardTitle>Workflow unavailable</CardTitle><CardDescription>A workflow identifier is required to open this page.</CardDescription><Link to={basePath} className="mt-4 inline-block text-sm font-semibold text-emerald-700">Back to AI Approvals</Link></Card>;
  if (workflowQuery.isLoading) return <div role="status" className="flex min-h-56 items-center justify-center gap-3 text-sm text-slate-500"><LoadingSpinner size="md" label="Loading workflow" />Loading workflow…</div>;
  if (workflowQuery.isError || !workflowQuery.data) return <Card><CardTitle>Workflow unavailable</CardTitle><CardDescription>We could not load this workflow. It may not exist or you may no longer have access.</CardDescription><div className="mt-5 flex gap-3"><Link to={basePath}><Button type="button" variant="secondary">Back to AI Approvals</Button></Link><Button type="button" variant="secondary" onClick={() => workflowQuery.refetch()}>Try again</Button></div></Card>;
  const workflow = workflowQuery.data;
  const plannerStep = findLatestWorkflowStep(workflow, 'SharedPlanning');
  const wasteAnalysisStep = findLatestWorkflowStep(workflow, 'WasteAnalysis');
  const wasteAnalysis = getWasteAnalysisOutput(wasteAnalysisStep);
  const matchingAnalysis = reportId && wasteAnalysis.kind === 'value' && wasteAnalysis.value.analyses.length === 1 && wasteAnalysis.value.analyses[0].reportId.toLowerCase() === reportId.toLowerCase() ? wasteAnalysis.value.analyses[0] : null;
  const report = reportQuery.data?.id.toLowerCase() === reportId?.toLowerCase() ? reportQuery.data : null;
  const canDecideReport = canReviewWasteReports(user?.role) && workflow.status === 'AwaitingReportVerification' && Boolean(report) && !reportQuery.isError && !reportQuery.isFetching && wasteAnalysisStep?.status === 'Completed' && Boolean(matchingAnalysis);
  const collectionPlanningStep = findLatestWorkflowStep(workflow, 'CollectionPlanning');
  const fleetPlanningStep = findLatestWorkflowStep(workflow, 'FleetPlanning');
  const operationalValidationStep = findLatestWorkflowStep(workflow, 'OperationalValidation');
  const fleetRouteOutput = getFleetRouteOutput(fleetPlanningStep);
  const hasSecondHalf = Boolean(fleetPlanningStep || operationalValidationStep) || ['FleetPlanning', 'OperationalValidation', 'AwaitingDispatchApproval', 'DispatchNeedsRevision', 'DispatchApproved', 'ExecutingAssignments', 'Completed'].includes(workflow.status) || (workflow.status === 'Failed' && ['FleetPlanning', 'OperationalValidation'].includes(workflow.currentStep));
  const showOperationalValidation = Boolean(operationalValidationStep) || fleetRouteOutput.kind === 'value' || ['OperationalValidation', 'AwaitingDispatchApproval', 'DispatchNeedsRevision', 'DispatchApproved', 'ExecutingAssignments', 'Completed'].includes(workflow.status) || (workflow.status === 'Failed' && workflow.currentStep === 'OperationalValidation');
  const secondHalfCallout = workflow.status === 'AwaitingDispatchApproval' ? 'Dispatch proposal is ready for human review.' : workflow.status === 'DispatchNeedsRevision' ? 'Dispatch proposal requires revision before approval.' : workflow.status === 'DispatchApproved' ? 'Dispatch approval is recorded. CollectionAssignment execution remains a separate explicit action.' : workflow.status === 'Completed' ? 'Agentic workflow planning and approved assignment creation are recorded as complete. Driver execution remains separate.' : null;
  const failureSummary = getWorkflowFailureSummary(workflow);

  return <div className="space-y-8"><Link to={basePath} className="inline-flex items-center gap-1.5 text-sm font-semibold text-emerald-700 hover:text-emerald-800">← Back to AI Approvals</Link>
    <div className="border-b border-slate-200/80 pb-5"><p className="text-xs font-medium text-slate-400">Agentic workflow</p><div className="mt-1 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">Workflow details</h1><p className="mt-2 max-w-3xl text-sm leading-relaxed text-slate-600">{workflow.objective}</p><p className="mt-3 text-xs text-slate-400">Reference: {workflow.id}</p></div><WorkflowStatusBadge status={workflow.status} /></div></div>
    {workflow.status === 'AwaitingCollectionApproval' && <div role="note" className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"><p className="font-semibold">Collection plan is ready for human review.</p><p className="mt-1">Review actions are available after the persisted collection-plan detail below.</p></div>}
    {isReportTriggeredWorkflow(workflow) && <Card><CardHeader><CardTitle>Triggering citizen report</CardTitle><CardDescription>Report review is decided through the authoritative WasteReport lifecycle.</CardDescription></CardHeader><CardContent className="space-y-3 text-sm"><p><span className="font-semibold">Trigger source:</span> {getWorkflowTriggerLabel(workflow)}</p><p><span className="font-semibold">Report:</span> {getTriggerReportLabel(workflow) ?? 'Report reference unavailable'}</p><p><span className="font-semibold">Workflow status:</span> {workflow.status === 'AwaitingReportVerification' ? 'Awaiting Report Verification' : workflow.status}</p><p><span className="font-semibold">Current step:</span> {getWorkflowStepLabel(workflow.currentStep)}</p>{reportId && <Link className="inline-block font-semibold text-emerald-700 hover:text-emerald-800" to={getWasteReportDetailPath(user?.role, reportId)}>Open {getTriggerReportLabel(workflow) ?? 'waste report'} details</Link>}{!reportId && <p role="alert" className="text-rose-700">This workflow has no triggering report ID.</p>}{reportId && reportQuery.isLoading && <p role="status">Loading report status…</p>}{reportId && reportQuery.isError && <p role="alert" className="text-rose-700">Report status is unavailable. The report may be missing or access may have changed. <Button type="button" variant="secondary" size="sm" onClick={() => reportQuery.refetch()}>Retry report</Button></p>}{reportId && reportQuery.data && !report && <p role="alert" className="text-rose-700">The returned report does not match this workflow.</p>}{report && <p className="flex items-center gap-2"><span className="font-semibold">Report status:</span><WasteReportStatusBadge status={report.status} /></p>}{actionFeedback && <p role={actionFeedback.type === 'error' ? 'alert' : 'status'} className={actionFeedback.type === 'error' ? 'text-rose-700' : 'text-emerald-700'}>{actionFeedback.message}</p>}{workflow.status === 'AwaitingReportVerification' && <p className="text-amber-800">AI analysis is complete. An authorized officer must review the report.</p>}{['Created', 'Planning'].includes(workflow.status) && report?.status !== 'Verified' && <p>AI analysis in progress. Report verification actions will be available after analysis finishes.</p>}{workflow.status === 'Planning' && workflow.currentStep === 'CollectionPlanning' && report?.status === 'Verified' && <p>Report verified. Collection planning is being prepared. Refresh to check for the collection approval stage.</p>}{workflow.status === 'Failed' && <p>AI analysis or planning could not be completed. The waste report remains in its own reporting state.</p>}{workflow.status === 'Rejected' && <p>Planning stopped. This workflow is closed.</p>}{canDecideReport && (report?.status === 'Submitted' || report?.status === 'UnderReview') && (
        <div role="group" aria-label="Report review actions" className="flex flex-wrap items-center gap-2">
          {report?.status === 'Submitted' && <Button type="button" disabled={isMutating} onClick={() => setOpenAction('start')}>Start Review</Button>}
          {report?.status === 'UnderReview' && <><Button type="button" disabled={isMutating} onClick={() => setOpenAction('verify')}>Verify Report</Button><Button type="button" variant="danger" disabled={isMutating} onClick={() => setOpenAction('reject')}>Reject Report</Button></>}
        </div>
      )}<Button type="button" variant="secondary" size="sm" disabled={isMutating} onClick={() => { setActionFeedback(null); reportQuery.refetch(); workflowQuery.refetch(); }}>Refresh report and workflow</Button></CardContent></Card>}
    {failureSummary && <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">{failureSummary}</div>}
    <Card><CardHeader><CardTitle>Workflow overview</CardTitle><CardDescription>Persisted ASP.NET workflow state.</CardDescription></CardHeader><CardContent className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4"><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Current stage</p><p className="mt-1 font-medium text-slate-900">{getWorkflowStepLabel(workflow.currentStep)}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Workflow version</p><p className="mt-1 font-medium text-slate-900">{workflow.version}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Created</p><p className="mt-1 font-medium text-slate-900">{formatWorkflowDate(workflow.createdAt)}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Last updated</p><p className="mt-1 font-medium text-slate-900">{formatWorkflowDate(workflow.updatedAt)}</p></div></CardContent></Card>
    <Card><CardContent><WorkflowTimeline workflow={workflow} /></CardContent></Card>
    <SharedPlannerSection step={plannerStep} />
    <WasteAnalysisSection step={wasteAnalysisStep} reportId={reportId} />
    <CollectionPlanningSection step={collectionPlanningStep} binCodes={workflow.binCodes} />
    <CollectionPlanReviewPanel workflow={workflow} collectionPlanningStep={collectionPlanningStep} />
    {!hasSecondHalf && ['CollectionApproved', 'CreatingScheduledTasks'].includes(workflow.status) && <p role="note" className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">Fleet planning will become available after the approved collection plan is executed.</p>}
    {hasSecondHalf && <>
      {secondHalfCallout && <div role="note" className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">{secondHalfCallout}</div>}
      <FleetRouteSection workflow={workflow} step={fleetPlanningStep} />
      {showOperationalValidation && <OperationalValidationSection workflow={workflow} step={operationalValidationStep} fleetRoute={fleetRouteOutput.kind === 'value' ? fleetRouteOutput.value : null} />}
      <DispatchReviewPanel key={`${workflow.id}-${workflow.version}`} workflow={workflow} fleetPlanningStep={fleetPlanningStep} operationalValidationStep={operationalValidationStep} />
    </>}
    {reportId && <><StartReviewModal isOpen={openAction === 'start'} isSubmitting={isMutating} onConfirm={() => submitReportAction('start')} onCancel={() => setOpenAction(null)} /><VerifyReportModal isOpen={openAction === 'verify'} isSubmitting={isMutating} aiRecommendedPriority={matchingAnalysis?.recommendedPriority} onConfirm={(priority) => submitReportAction('verify', priority)} onCancel={() => setOpenAction(null)} /><RejectReportModal isOpen={openAction === 'reject'} isSubmitting={isMutating} reason={rejectReason} onReasonChange={setRejectReason} onConfirm={() => submitReportAction('reject')} onCancel={() => setOpenAction(null)} /></>}
  </div>;
};

export default AiWorkflowDetailPage;
