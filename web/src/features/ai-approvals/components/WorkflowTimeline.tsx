import React from 'react';
import type { AgentWorkflowDetail } from '../types/agentWorkflow';
import { getWorkflowTimeline } from '../utils/workflowDetailPresentation';

const stateClasses = {
  Completed: 'border-emerald-300 bg-emerald-50 text-emerald-800',
  Current: 'border-emerald-500 bg-emerald-50 text-emerald-900',
  Pending: 'border-slate-200 bg-slate-50 text-slate-500',
  'Needs Revision': 'border-amber-300 bg-amber-50 text-amber-800',
  Failed: 'border-rose-300 bg-rose-50 text-rose-800',
  Rejected: 'border-rose-300 bg-rose-50 text-rose-800',
};

export const WorkflowTimeline: React.FC<{ workflow: AgentWorkflowDetail }> = ({ workflow }) => (
  <section aria-labelledby="workflow-progress-title">
    <h2 id="workflow-progress-title" className="text-xl font-bold text-slate-900">Workflow progress</h2>
    <p className="mt-1 text-sm text-slate-500">Persisted workflow progress across AI, human review, and backend execution stages.</p>
    <ol className="mt-5 space-y-3 border-l-2 border-slate-200 pl-5">
      {getWorkflowTimeline(workflow).map((stage) => (
        <li key={stage.id} className="relative" aria-label={`${stage.label}: ${stage.state}`}>
          <span className="absolute -left-[1.82rem] top-5 h-3 w-3 rounded-full border-2 border-white bg-slate-300" aria-hidden="true" />
          <div className={`rounded-lg border px-4 py-3 ${stateClasses[stage.state]}`}>
            <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between"><span className="font-semibold">{stage.label}</span><span className="text-xs font-semibold uppercase tracking-wide">{stage.state}</span></div>
            <p className="mt-1 text-xs opacity-80">{stage.responsibility}</p>
          </div>
        </li>
      ))}
    </ol>
  </section>
);
