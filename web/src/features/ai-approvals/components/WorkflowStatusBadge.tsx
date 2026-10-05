import React from 'react';
import type { AgentWorkflowStatus } from '../types/agentWorkflow';
import { getWorkflowStatusLabel, needsHumanAttention } from '../utils/workflowPresentation';

interface WorkflowStatusBadgeProps {
  status: AgentWorkflowStatus;
}

export const WorkflowStatusBadge: React.FC<WorkflowStatusBadgeProps> = ({ status }) => {
  const tone = needsHumanAttention(status)
    ? 'bg-amber-50 text-amber-800 border-amber-200'
    : status === 'Completed'
      ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
      : status === 'Failed' || status === 'Rejected'
        ? 'bg-rose-50 text-rose-800 border-rose-200'
        : 'bg-slate-50 text-slate-700 border-slate-200';

  return (
    <span className={`inline-flex items-center rounded-full border px-2.5 py-1 text-[11px] font-semibold ${tone}`}>
      {getWorkflowStatusLabel(status)}
    </span>
  );
};
