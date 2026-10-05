import React, { useState } from 'react';
import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '../../../components/ui/Button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/Card';
import { agentWorkflowApi } from '../api/agentWorkflowApi';
import type { AgentWorkflowDetail, AgentWorkflowStep } from '../types/agentWorkflow';
import { getFleetRouteOutput, getOperationalValidationOutput } from '../utils/workflowDetailPresentation';
import { formatWorkflowDate, getWorkflowStatusLabel } from '../utils/workflowPresentation';

type Decision = 'approve' | 'revision' | 'reject' | 'execute';

const copy: Record<Decision, { title: string; description: string; submit: string; requiresReason: boolean; optionalReason?: boolean; danger?: boolean }> = {
  approve: { title: 'Approve Dispatch Plan?', description: 'Approve this persisted dispatch proposal for separate authoritative CollectionAssignment creation. Execution remains a separate action.', submit: 'Approve Dispatch', requiresReason: false, optionalReason: true },
  revision: { title: 'Request Dispatch Revision', description: 'Describe the changes required. The current dispatch proposal will not be executed.', submit: 'Request Dispatch Revision', requiresReason: true },
  reject: { title: 'Reject Dispatch Plan?', description: 'This workflow will be rejected and no CollectionAssignments will be created from this proposal.', submit: 'Reject Dispatch', requiresReason: true, danger: true },
  execute: { title: 'Execute Approved Dispatch Plan?', description: 'ASP.NET will freshly validate authoritative task, driver, vehicle, and compatibility state before creating CollectionAssignments.', submit: 'Execute Approved Dispatch Plan', requiresReason: false },
};

const errorMessage = (error: unknown, execution: boolean): string => {
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
  return execution
    ? 'The execution response could not be confirmed. The latest workflow state has been refreshed.'
    : 'The dispatch approval response could not be confirmed. The latest workflow state has been refreshed; do not retry blindly.';
};

export const DispatchReviewPanel: React.FC<{ workflow: AgentWorkflowDetail; fleetPlanningStep: AgentWorkflowStep | null; operationalValidationStep: AgentWorkflowStep | null }> = ({ workflow, fleetPlanningStep, operationalValidationStep }) => {
  const queryClient = useQueryClient();
  const [decision, setDecision] = useState<Decision | null>(null);
  const [reason, setReason] = useState('');
  const [acknowledged, setAcknowledged] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const fleet = getFleetRouteOutput(fleetPlanningStep);
  const validation = getOperationalValidationOutput(operationalValidationStep);
  const readyForReview = validation.kind === 'value' && validation.value.validationOutcome === 'ReadyForHumanReview';
  const requiresAcknowledgement = validation.kind === 'value' && validation.value.requiresAcknowledgement;
  const plannedTaskCount = fleet.kind === 'value' ? fleet.value.dispatchPlans.reduce((count, plan) => count + plan.recommendedTasks.length, 0) : null;
  const approval = [...workflow.approvals].filter((item) => item.approvalStage === 'FleetDispatch').sort((left, right) => right.decidedAt.localeCompare(left.decidedAt))[0];
  const assignmentExecution = [...workflow.executionResults].filter((item) => item.executionType === 'CollectionAssignment').sort((left, right) => right.executedAt.localeCompare(left.executedAt))[0];
  const recordedAcknowledgement = approval?.decisionPayload && typeof approval.decisionPayload === 'object' && !Array.isArray(approval.decisionPayload) && typeof approval.decisionPayload.acknowledgeWarnings === 'boolean'
    ? approval.decisionPayload.acknowledgeWarnings
    : null;

  const refreshWorkflow = async (updated?: AgentWorkflowDetail) => {
    if (updated) queryClient.setQueryData(['agent-workflow', workflow.id], updated);
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['agent-workflow', workflow.id] }),
      queryClient.invalidateQueries({ queryKey: ['agent-workflows'] }),
    ]);
  };

  const mutation = useMutation({
    mutationFn: async ({ action, note, ack }: { action: Decision; note: string; ack: boolean }) => {
      if (action === 'approve') return agentWorkflowApi.approveDispatchPlan(workflow.id, { expectedVersion: workflow.version, acknowledgeWarnings: ack, ...(note ? { reason: note } : {}) });
      if (action === 'revision') return agentWorkflowApi.requestDispatchRevision(workflow.id, { expectedVersion: workflow.version, reason: note });
      if (action === 'reject') return agentWorkflowApi.rejectDispatchPlan(workflow.id, { expectedVersion: workflow.version, reason: note });
      return agentWorkflowApi.executeDispatchPlan(workflow.id, { expectedVersion: workflow.version });
    },
    onSuccess: async (updated) => {
      setActionError(null);
      setDecision(null);
      setReason('');
      setAcknowledged(false);
      await refreshWorkflow(updated);
    },
    onError: async (error, variables) => {
      setActionError(errorMessage(error, variables.action === 'execute'));
      setDecision(null);
      setReason('');
      setAcknowledged(false);
      await refreshWorkflow();
    },
  });

  const open = (next: Decision) => {
    if (mutation.isPending) return;
    setValidationError(null);
    setReason('');
    setAcknowledged(false);
    setDecision(next);
  };

  const close = () => {
    if (mutation.isPending) return;
    setDecision(null);
    setReason('');
    setAcknowledged(false);
    setValidationError(null);
  };

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    if (!decision || mutation.isPending) return;
    const note = reason.trim();
    if (copy[decision].requiresReason && note.length < 5) {
      setValidationError('Reason must be at least 5 characters.');
      return;
    }
    if (note.length > 500) {
      setValidationError('Reason cannot exceed 500 characters.');
      return;
    }
    if (decision === 'approve' && requiresAcknowledgement && !acknowledged) {
      setValidationError('Warning acknowledgement is required before dispatch approval.');
      return;
    }
    setValidationError(null);
    mutation.mutate({ action: decision, note, ack: decision === 'approve' ? acknowledged : false });
  };

  const pendingAction = mutation.isPending ? mutation.variables?.action : null;
  const actionButtons = workflow.status === 'AwaitingDispatchApproval' && readyForReview;

  return <Card><CardHeader><CardTitle>Dispatch Human Review</CardTitle><CardDescription>Review the persisted C3 fleet proposal and C4 operational validation. ASP.NET remains the authority for approval and CollectionAssignment creation.</CardDescription></CardHeader><CardContent className="space-y-4">
    {actionError && <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">{actionError}</div>}
    <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4"><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Dispatch plans</p><p className="mt-1 font-semibold text-slate-900">{fleet.kind === 'value' ? fleet.value.dispatchPlans.length : '—'}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Planned tasks</p><p className="mt-1 font-semibold text-slate-900">{plannedTaskCount ?? '—'}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Unplanned tasks</p><p className="mt-1 font-semibold text-slate-900">{fleet.kind === 'value' ? fleet.value.unplannedTasks.length : '—'}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">C4 outcome</p><p className="mt-1 font-semibold text-slate-900">{validation.kind === 'value' ? validation.value.validationOutcome ?? 'No proposal' : 'Unavailable'}</p></div></div>
    {validation.kind === 'value' && <p className="text-sm text-slate-600">Warnings: {validation.value.warnings.length}. Acknowledgement: {validation.value.requiresAcknowledgement ? 'required before approval' : 'not required'}.</p>}
    {actionButtons && <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900"><p className="font-semibold">Dispatch proposal is ready for human review.</p><p className="mt-1">Approval records a human decision only. Execute is available only after approval and remains separate.</p><div className="mt-4 flex flex-col gap-2 sm:flex-row sm:flex-wrap"><Button type="button" onClick={() => open('approve')} disabled={Boolean(pendingAction)} isLoading={pendingAction === 'approve'}>{pendingAction === 'approve' ? 'Approving dispatch...' : 'Approve Dispatch'}</Button><Button type="button" variant="secondary" onClick={() => open('revision')} disabled={Boolean(pendingAction)} isLoading={pendingAction === 'revision'}>{pendingAction === 'revision' ? 'Requesting revision...' : 'Request Dispatch Revision'}</Button><Button type="button" variant="danger" onClick={() => open('reject')} disabled={Boolean(pendingAction)} isLoading={pendingAction === 'reject'}>{pendingAction === 'reject' ? 'Rejecting dispatch...' : 'Reject Dispatch'}</Button></div></div>}
    {workflow.status === 'AwaitingDispatchApproval' && !readyForReview && <p role="note" className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">Dispatch approval is unavailable until the persisted C4 result is ReadyForHumanReview.</p>}
    {workflow.status === 'DispatchNeedsRevision' && <p className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">Dispatch proposal requires revision before approval. No approval or execution action is available.</p>}
    {workflow.status === 'DispatchApproved' && <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900"><p className="font-semibold">Dispatch approval is recorded.</p><p className="mt-1">ASP.NET will freshly validate authoritative state before creating CollectionAssignments.</p><Button type="button" className="mt-4" onClick={() => open('execute')} disabled={Boolean(pendingAction)} isLoading={pendingAction === 'execute'}>{pendingAction === 'execute' ? 'Creating collection assignments and validating current fleet state...' : 'Execute Approved Dispatch Plan'}</Button></div>}
    {workflow.status === 'ExecutingAssignments' && <p role="status" className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">Creating approved collection assignments...</p>}
    {workflow.status === 'Completed' && <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900"><p className="font-semibold">Workflow Completed</p><p className="mt-1">Dispatch planning and approved CollectionAssignment creation completed successfully. Driver route execution continues through the Driver application.</p></div>}
    {workflow.status === 'Rejected' && <p className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">This dispatch proposal was rejected. No CollectionAssignments will be created from this proposal.</p>}
    {workflow.status === 'Failed' && <p className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">Dispatch review is unavailable because this workflow failed.</p>}
    {['FleetPlanning', 'OperationalValidation'].includes(workflow.status) && <p className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">Dispatch review will be available after operational validation. Current workflow status: <span className="font-semibold">{getWorkflowStatusLabel(workflow.status)}</span>.</p>}
    {approval && <p className="text-sm text-slate-600">Dispatch approval audit: {approval.decision}{approval.decisionReason ? ` — ${approval.decisionReason}` : ''}. Warning acknowledgement: {recordedAcknowledgement === null ? 'not recorded' : recordedAcknowledgement ? 'acknowledged' : 'not required'}. Recorded by <span className="break-all text-xs text-slate-500">{approval.decidedByUserId}</span> on {formatWorkflowDate(approval.decidedAt)}.</p>}
    {assignmentExecution && <p className="text-sm text-slate-600">CollectionAssignment execution: {assignmentExecution.status}. Executed {formatWorkflowDate(assignmentExecution.executedAt)}.</p>}
    {decision && <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4" role="dialog" aria-modal="true" aria-labelledby="dispatch-decision-title"><div className="w-full max-w-lg rounded-xl border border-slate-200 bg-white p-5 shadow-xl sm:p-6"><h2 id="dispatch-decision-title" className="text-lg font-bold text-slate-900">{copy[decision].title}</h2><p className="mt-2 text-sm leading-relaxed text-slate-600">{copy[decision].description}</p><form className="mt-5 space-y-4" onSubmit={submit}>{(copy[decision].requiresReason || copy[decision].optionalReason) && <><label className="block text-sm font-semibold text-slate-800" htmlFor="dispatch-decision-reason">{copy[decision].optionalReason ? 'Approval note (optional)' : 'Reason'}</label><textarea id="dispatch-decision-reason" value={reason} onChange={(event) => setReason(event.target.value)} disabled={mutation.isPending} maxLength={500} rows={5} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-emerald-500 focus:outline-none focus:ring-2 focus:ring-emerald-500" /><p className="text-right text-xs text-slate-500">{reason.length}/500</p></>}{decision === 'approve' && requiresAcknowledgement && <label className="flex gap-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900"><input type="checkbox" checked={acknowledged} onChange={(event) => setAcknowledged(event.target.checked)} disabled={mutation.isPending} /><span>I acknowledge the operational warnings recorded in C4.</span></label>}{validationError && <p role="alert" className="text-sm text-rose-800">{validationError}</p>}{decision === 'execute' && mutation.isPending && <p role="status" className="rounded-lg border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-900">Creating collection assignments and validating current fleet state...</p>}<div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end"><Button type="button" variant="secondary" disabled={mutation.isPending} onClick={close}>Cancel</Button><Button type="submit" variant={copy[decision].danger ? 'danger' : 'primary'} disabled={mutation.isPending || (decision === 'approve' && requiresAcknowledgement && !acknowledged)} isLoading={mutation.isPending}>{copy[decision].submit}</Button></div></form></div></div>}
  </CardContent></Card>;
};
