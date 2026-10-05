import React from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/Card';
import type { AgentWorkflowDetail, AgentWorkflowStep, DispatchPlanRecommendation } from '../types/agentWorkflow';
import { getCompatibilityLabel, getFleetRouteOutput } from '../utils/workflowDetailPresentation';

const compatibilityClasses = {
  Compatible: 'border-emerald-200 bg-emerald-50 text-emerald-900',
  Unknown: 'border-amber-200 bg-amber-50 text-amber-900',
  Incompatible: 'border-rose-200 bg-rose-50 text-rose-900',
};

const DispatchPlanCard: React.FC<{ plan: DispatchPlanRecommendation; index: number }> = ({ plan, index }) => {
  const stops = [...plan.recommendedTasks].sort((left, right) => left.sequence - right.sequence || left.taskCode.localeCompare(right.taskCode));
  const compatibility = plan.compatibility;

  return <article className="rounded-xl border border-slate-200 p-4 sm:p-5">
    <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
      <div><h3 className="font-semibold text-slate-900">Dispatch Plan {index + 1}</h3><p className="mt-1 text-xs text-slate-500">Reference: {plan.planId}</p></div>
      {compatibility && <span className={`rounded-full border px-2.5 py-1 text-xs font-semibold ${compatibilityClasses[compatibility.status]}`}>{getCompatibilityLabel(compatibility.status)}</span>}
    </div>
    <p className="mt-3 text-sm leading-relaxed text-slate-600">{plan.rationale}</p>
    <div className="mt-4 grid gap-4 sm:grid-cols-2">
      <section><h4 className="text-sm font-semibold text-slate-800">Recommended Driver</h4><p className="mt-1 font-medium text-slate-900">{plan.recommendedDriver.displayName}</p><p className="mt-1 text-sm leading-relaxed text-slate-600">{plan.recommendedDriver.reason}</p></section>
      <section><h4 className="text-sm font-semibold text-slate-800">Recommended Vehicle</h4><p className="mt-1 font-medium text-slate-900">{plan.recommendedVehicle.registrationNumber}</p><p className="text-xs text-slate-500">{plan.recommendedVehicle.vehicleType}</p><p className="mt-1 text-sm leading-relaxed text-slate-600">{plan.recommendedVehicle.reason}</p></section>
    </div>
    {compatibility && compatibility.issues.length > 0 && <div role="note" className={`mt-4 rounded-lg border p-3 text-sm ${compatibilityClasses[compatibility.status]}`}><p className="font-semibold">Compatibility notes</p><ul className="mt-2 list-disc space-y-1 pl-5">{compatibility.issues.map((issue) => <li key={issue}>{issue}</li>)}</ul></div>}
    <section className="mt-5 border-t border-slate-100 pt-4" aria-labelledby={`suggested-stop-sequence-${plan.planId}`}><h4 id={`suggested-stop-sequence-${plan.planId}`} className="font-semibold text-slate-900">Suggested Stop Sequence</h4><ol className="mt-3 space-y-3">{stops.map((task) => <li key={task.taskId} className="flex gap-3"><span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-slate-100 text-xs font-bold text-slate-700">{task.sequence}</span><div><p className="font-medium text-slate-900">{task.taskCode}</p>{task.addressText && <p className="mt-0.5 text-sm text-slate-500">{task.addressText}</p>}<p className="mt-1 text-sm leading-relaxed text-slate-600">{task.reason}</p></div></li>)}</ol></section>
    {plan.warnings.length > 0 && <div role="note" className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900"><p className="font-semibold">Plan warnings</p><ul className="mt-2 list-disc space-y-1 pl-5">{plan.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul></div>}
  </article>;
};

export const FleetRouteSection: React.FC<{ workflow: AgentWorkflowDetail; step: AgentWorkflowStep | null }> = ({ workflow, step }) => {
  const output = getFleetRouteOutput(step);
  const isInProgress = workflow.status === 'FleetPlanning' && output.kind === 'missing';
  const failedBeforeCompletion = workflow.status === 'Failed' && workflow.currentStep === 'FleetPlanning' && output.kind === 'missing';

  return <Card><CardHeader><CardTitle>Fleet &amp; Route Planning</CardTitle><CardDescription>Advisory dispatch recommendations using current Scheduled CollectionTasks, available drivers, and vehicles.</CardDescription></CardHeader><CardContent>
    {isInProgress && <p role="status" className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">Fleet planning is in progress.</p>}
    {failedBeforeCompletion && <p className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">Fleet &amp; Route result unavailable because fleet planning did not complete.</p>}
    {output.kind === 'missing' && !isInProgress && !failedBeforeCompletion && <p className="text-sm text-slate-500">Fleet &amp; Route result unavailable.</p>}
    {output.kind === 'invalid' && <p className="text-sm text-slate-500">Fleet &amp; Route result is unavailable or incompatible with the current format.</p>}
    {output.kind === 'value' && <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4"><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Dispatch plans</p><p className="mt-1 font-semibold text-slate-900">{output.value.dispatchPlans.length}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Planned tasks</p><p className="mt-1 font-semibold text-slate-900">{output.value.dispatchPlans.reduce((count, plan) => count + plan.recommendedTasks.length, 0)}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Unplanned tasks</p><p className="mt-1 font-semibold text-slate-900">{output.value.unplannedTasks.length}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Warnings</p><p className="mt-1 font-semibold text-slate-900">{output.value.warnings.length}</p></div></div>
      <p className="text-sm leading-relaxed text-slate-600">{output.value.rationale}</p>
      {output.value.status === 'empty' ? <p className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">No dispatch plans were proposed for the considered Scheduled CollectionTasks.</p> : <div className="space-y-4">{output.value.dispatchPlans.map((plan, index) => <DispatchPlanCard key={plan.planId} plan={plan} index={index} />)}</div>}
      {output.value.unplannedTasks.length > 0 ? <section><h3 className="font-semibold text-slate-900">Unplanned Tasks</h3><p className="mt-1 text-sm text-slate-600">These Scheduled CollectionTasks were not included in the proposed dispatch plans.</p><ul className="mt-3 space-y-2">{output.value.unplannedTasks.map((task) => <li key={task.taskId} className="rounded-lg border border-slate-200 p-3 text-sm"><p className="font-medium text-slate-900">{task.taskCode ?? 'Scheduled task'} <span className="font-normal text-slate-500">· {task.taskId}</span></p><p className="mt-1 leading-relaxed text-slate-600">{task.reason}</p></li>)}</ul></section> : <p className="text-sm text-slate-500">All considered tasks were included in a dispatch plan.</p>}
      {output.value.warnings.length > 0 && <div role="note" className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"><p className="font-semibold">Fleet planning notices</p><ul className="mt-2 list-disc space-y-1 pl-5">{output.value.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul></div>}
      <p className="text-xs text-slate-500">Source tasks considered: {output.value.sourceTaskTotalCount}.</p>
    </div>}
  </CardContent></Card>;
};
