import React from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/Card';
import type { AgentWorkflowStep } from '../types/agentWorkflow';
import { getSharedPlannerOutput, getSpecialistLabel } from '../utils/workflowDetailPresentation';

export const SharedPlannerSection: React.FC<{ step: AgentWorkflowStep | null }> = ({ step }) => {
  const output = getSharedPlannerOutput(step);
  return <Card><CardHeader><CardTitle>Shared Planner</CardTitle><CardDescription>How the objective was delegated across the specialist workflow.</CardDescription></CardHeader><CardContent>
    {output.kind === 'missing' && <p className="text-sm text-slate-500">Planner result unavailable.</p>}
    {output.kind === 'invalid' && <p className="text-sm text-slate-500">Result unavailable or incompatible with the current format.</p>}
    {output.kind === 'value' && <div className="space-y-5"><p className="text-sm leading-relaxed text-slate-700">{output.value.summary}</p><ol className="space-y-3">{[...output.value.steps].sort((left, right) => (left.sequence ?? Number.MAX_SAFE_INTEGER) - (right.sequence ?? Number.MAX_SAFE_INTEGER)).map((plannerStep, index) => <li key={plannerStep.stepId} className="rounded-lg border border-slate-200 p-4"><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Step {plannerStep.sequence ?? index + 1}</p><h3 className="mt-1 font-semibold text-slate-900">{getSpecialistLabel(plannerStep.specialist)}</h3><p className="mt-1 text-sm text-slate-600">{plannerStep.objective}</p></li>)}</ol>{output.value.warnings.length > 0 && <div role="note" className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"><p className="font-semibold">Planner notices</p><ul className="mt-2 list-disc space-y-1 pl-5">{output.value.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul></div>}</div>}
  </CardContent></Card>;
};
