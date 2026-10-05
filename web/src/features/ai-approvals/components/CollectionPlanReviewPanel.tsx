import React, { useState } from 'react';
import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '../../../components/ui/Button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/Card';
import { agentWorkflowApi } from '../api/agentWorkflowApi';
import type { AgentWorkflowDetail, AgentWorkflowStep } from '../types/agentWorkflow';
import { getCollectionPlanningOutput } from '../utils/workflowDetailPresentation';
import { getWorkflowStatusLabel } from '../utils/workflowPresentation';

type Decision = 'approve' | 'revision' | 'reject' | 'execute';

const decisionCopy: Record<Decision, { title: string; description: string; submit: string; requiresReason: boolean; optionalReason?: boolean; danger?: boolean }> = {
  approve: { title: 'Approve Collection Plan?', description: 'Approve this proposed collection plan for authoritative Scheduled Collection Task creation? Execution remains a separate action.', submit: 'Approve Collection Plan', requiresReason: false, optionalReason: true },
  revision: { title: 'Request Collection Plan Revision', description: 'Describe the changes required. The current plan will not be executed.', submit: 'Request Revision', requiresReason: true },
  reject: { title: 'Reject Collection Plan?', description: 'This workflow will be rejected and the proposed plan will not be executed.', submit: 'Reject Collection Plan', requiresReason: true, danger: true },
  execute: { title: 'Execute Approved Collection Plan?', description: 'This will create approved collection tasks and continue fleet planning.', submit: 'Execute Approved Plan', requiresReason: false },
};

const getErrorMessage = (error: unknown): string => {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 409) return 'This workflow changed while you were reviewing it. The latest state has been loaded.';
    const payload = error.response?.data;
    if (payload && typeof payload === 'object') {
      const detail = (payload as Record<string, unknown>).detail;
      const title = (payload as Record<string, unknown>).title;
      if (typeof detail === 'string') return detail;
      if (typeof title === 'string') return title;
    }
    if (error.response?.status === 403) return 'You are not permitted to perform this workflow action.';
  }
  return 'The workflow action could not be confirmed. The latest workflow state has been requested; do not retry blindly.';
};

const postExecutionSummary = (workflow: AgentWorkflowDetail): string | null => {
  const execution = [...workflow.executionResults].filter((result) => result.executionType === 'CollectionTaskCreation').sort((left, right) => right.executedAt.localeCompare(left.executedAt))[0];
  return execution ? `Scheduled task creation: ${execution.status}.` : null;
};

export const CollectionPlanReviewPanel: React.FC<{ workflow: AgentWorkflowDetail; collectionPlanningStep: AgentWorkflowStep | null }> = ({ workflow, collectionPlanningStep }) => {
  const queryClient = useQueryClient();
  const [decision, setDecision] = useState<Decision | null>(null);
  const [reason, setReason] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const c2Output = getCollectionPlanningOutput(collectionPlanningStep);
  const isApprovalReady = c2Output.kind === 'value' && c2Output.value.status === 'completed' && c2Output.value.isCompleteSnapshot;

  const refreshWorkflow = async (updated?: AgentWorkflowDetail) => {
    if (updated) queryClient.setQueryData(['agent-workflow', workflow.id], updated);
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['agent-workflow', workflow.id] }),
      queryClient.invalidateQueries({ queryKey: ['agent-workflows'] }),
    ]);
  };

  const actionMutation = useMutation({
    mutationFn: async ({ action, note }: { action: Decision; note: string }) => {
      if (action === 'approve') {
        const request = note ? { expectedVersion: workflow.version, reason: note } : { expectedVersion: workflow.version };
        return agentWorkflowApi.approveCollectionPlan(workflow.id, request);
      }
      if (action === 'revision') return agentWorkflowApi.requestCollectionRevision(workflow.id, { expectedVersion: workflow.version, reason: note });
      if (action === 'reject') return agentWorkflowApi.rejectCollectionPlan(workflow.id, { expectedVersion: workflow.version, reason: note });
      return agentWorkflowApi.executeCollectionPlan(workflow.id, { expectedVersion: workflow.version });
    },
    onSuccess: async (updated) => {
      setActionError(null);
      setDecision(null);
      setReason('');
      await refreshWorkflow(updated);
    },
    onError: async (error) => {
      setActionError(getErrorMessage(error));
      setDecision(null);
      setReason('');
      await refreshWorkflow();
    },
  });

  const openDecision = (nextDecision: Decision) => {
    if (actionMutation.isPending) return;
    setValidationError(null);
    setReason('');
    setDecision(nextDecision);
  };

  const submitDecision = (event: React.FormEvent) => {
    event.preventDefault();
    if (!decision || actionMutation.isPending) return;
    const note = reason.trim();
    if (decisionCopy[decision].requiresReason && note.length < 5) {
      setValidationError('Reason must be at least 5 characters.');
      return;
    }
    if (note.length > 500) {
      setValidationError('Reason cannot exceed 500 characters.');
      return;
    }
    setValidationError(null);
    actionMutation.mutate({ action: decision, note });
  };

  const executionSummary = postExecutionSummary(workflow);
  const pendingAction = actionMutation.isPending ? actionMutation.variables?.action : null;
  const hasAdvancedPastCollection = ['CreatingScheduledTasks', 'FleetPlanning', 'OperationalValidation', 'AwaitingDispatchApproval', 'DispatchNeedsRevision', 'DispatchApproved', 'ExecutingAssignments', 'Completed', 'Failed'].includes(workflow.status);

  return <Card><CardHeader><CardTitle>Collection Plan Human Review</CardTitle><CardDescription>Human review and authoritative execution use the persisted C2 proposal. ASP.NET remains the final authority.</CardDescription></CardHeader><CardContent className="space-y-4">
    {actionError && <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">{actionError}</div>}
    {c2Output.kind === 'value' && <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4"><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Candidate groups</p><p className="mt-1 font-semibold text-slate-900">{c2Output.value.candidateGroups.length}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Separate handling</p><p className="mt-1 font-semibold text-slate-900">{c2Output.value.separateHandling.length}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Deferred needs</p><p className="mt-1 font-semibold text-slate-900">{c2Output.value.deferredNeeds.length}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Planning notices</p><p className="mt-1 font-semibold text-slate-900">{c2Output.value.warnings.length}</p></div></div>}
    {workflow.status === 'AwaitingCollectionApproval' && !isApprovalReady && <div role="note" className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"><p className="font-semibold">Collection plan is not approval-ready.</p><p className="mt-1">The persisted C2 result must be completed and based on a complete source snapshot before it can be approved.</p></div>}
    {workflow.status === 'AwaitingCollectionApproval' && isApprovalReady && <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900"><p className="font-semibold">Collection plan is ready for human review.</p><p className="mt-1">Approving records the human decision only. Execution remains an explicit separate action.</p><div className="mt-4 flex flex-col gap-2 sm:flex-row sm:flex-wrap"><Button type="button" onClick={() => openDecision('approve')} disabled={Boolean(pendingAction)} isLoading={pendingAction === 'approve'}>{pendingAction === 'approve' ? 'Approving...' : 'Approve Collection Plan'}</Button><Button type="button" variant="secondary" onClick={() => openDecision('revision')} disabled={Boolean(pendingAction)} isLoading={pendingAction === 'revision'}>{pendingAction === 'revision' ? 'Requesting revision...' : 'Request Revision'}</Button><Button type="button" variant="danger" onClick={() => openDecision('reject')} disabled={Boolean(pendingAction)} isLoading={pendingAction === 'reject'}>{pendingAction === 'reject' ? 'Rejecting...' : 'Reject Collection Plan'}</Button></div></div>}
    {workflow.status === 'CollectionApproved' && <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900"><p className="font-semibold">Collection plan approved.</p><p className="mt-1">This will create the approved Scheduled Collection Tasks using current authoritative backend data and continue the Agentic workflow to fleet planning.</p><Button type="button" className="mt-4" onClick={() => openDecision('execute')} disabled={Boolean(pendingAction)} isLoading={pendingAction === 'execute'}>{pendingAction === 'execute' ? 'Creating scheduled tasks and continuing fleet planning...' : 'Execute Approved Collection Plan'}</Button></div>}
    {workflow.status === 'CollectionNeedsRevision' && <p className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">Collection plan revision was requested. The current plan will not be executed.</p>}
    {workflow.status === 'Rejected' && <p className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">This collection plan was rejected and is closed.</p>}
    {hasAdvancedPastCollection && <p className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">Collection review is complete. Current workflow status: <span className="font-semibold">{getWorkflowStatusLabel(workflow.status)}</span>.</p>}
    {executionSummary && <p className="text-sm text-slate-600">{executionSummary}</p>}
    {decision && <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4" role="dialog" aria-modal="true" aria-labelledby="collection-decision-title"><div className="w-full max-w-lg rounded-xl border border-slate-200 bg-white p-5 shadow-xl sm:p-6"><h2 id="collection-decision-title" className="text-lg font-bold text-slate-900">{decisionCopy[decision].title}</h2><p className="mt-2 text-sm leading-relaxed text-slate-600">{decisionCopy[decision].description}</p><form className="mt-5 space-y-4" onSubmit={submitDecision}>{(decisionCopy[decision].requiresReason || decisionCopy[decision].optionalReason) && <><label className="block text-sm font-semibold text-slate-800" htmlFor="collection-decision-reason">{decisionCopy[decision].optionalReason ? 'Approval note (optional)' : 'Reason'}</label><textarea id="collection-decision-reason" value={reason} onChange={(event) => setReason(event.target.value)} disabled={actionMutation.isPending} maxLength={500} rows={5} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-emerald-500 focus:outline-none focus:ring-2 focus:ring-emerald-500" /><p className="text-right text-xs text-slate-500">{reason.length}/500</p></>}{validationError && <p role="alert" className="text-sm text-rose-800">{validationError}</p>}{decision === 'execute' && actionMutation.isPending && <p role="status" className="rounded-lg border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-900">Creating scheduled tasks and continuing fleet planning...</p>}<div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end"><Button type="button" variant="secondary" disabled={actionMutation.isPending} onClick={() => setDecision(null)}>Cancel</Button><Button type="submit" variant={decisionCopy[decision].danger ? 'danger' : 'primary'} disabled={actionMutation.isPending} isLoading={actionMutation.isPending}>{decisionCopy[decision].submit}</Button></div></form></div></div>}
  </CardContent></Card>;
};
