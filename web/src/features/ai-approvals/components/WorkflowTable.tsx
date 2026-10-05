import React from 'react';
import { Link } from 'react-router-dom';
import type { AgentWorkflowSummary } from '../types/agentWorkflow';
import { formatWorkflowDate, getTriggerReportLabel, getWorkflowStepLabel, getWorkflowTriggerLabel } from '../utils/workflowPresentation';
import { getWasteReportDetailPath } from '../../reporting/utils/reportReviewAuthority';
import { WorkflowStatusBadge } from './WorkflowStatusBadge';

interface WorkflowTableProps {
  workflows: AgentWorkflowSummary[];
  workflowBasePath: string;
  role?: string;
}

export const WorkflowTable: React.FC<WorkflowTableProps> = ({ workflows, workflowBasePath, role }) => (
  <div className="overflow-x-auto">
    <table className="w-full min-w-[900px] table-fixed divide-y divide-slate-200 text-left">
      <colgroup>
        <col className="w-[18%]" />
        <col className="w-[14%]" />
        <col className="w-[18%]" />
        <col className="w-[13%]" />
        <col className="w-[12%]" />
        <col className="w-[12%]" />
        <col className="w-[13%]" />
      </colgroup>
      <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
        <tr>
          <th className="px-3 py-3">Objective</th>
          <th className="px-3 py-3">Source</th>
          <th className="px-3 py-3">Status</th>
          <th className="px-3 py-3">Current stage</th>
          <th className="px-3 py-3">Created</th>
          <th className="px-3 py-3">Updated</th>
          <th className="px-3 py-3"><span className="sr-only">Open workflow</span></th>
        </tr>
      </thead>
      <tbody className="divide-y divide-slate-100 bg-white text-sm text-slate-700">
        {workflows.map((workflow) => (
          <tr key={workflow.id}>
            <td className="min-w-0 px-3 py-4 font-medium text-slate-900">
              <span className="block truncate" title={workflow.objective}>{workflow.objective}</span>
            </td>
            <td className="px-3 py-4 text-xs font-semibold text-slate-700">
              {workflow.triggeringWasteReportId && getTriggerReportLabel(workflow)
                ? <Link to={getWasteReportDetailPath(role, workflow.triggeringWasteReportId)} className="whitespace-nowrap text-emerald-700 hover:text-emerald-800">{getTriggerReportLabel(workflow)}</Link>
                : getWorkflowTriggerLabel(workflow)}
            </td>
            <td className="px-3 py-4"><WorkflowStatusBadge status={workflow.status} /></td>
            <td className="px-3 py-4">{getWorkflowStepLabel(workflow.currentStep)}</td>
            <td className="px-3 py-4 text-xs text-slate-500">{formatWorkflowDate(workflow.createdAt)}</td>
            <td className="px-3 py-4 text-xs text-slate-500">{formatWorkflowDate(workflow.updatedAt)}</td>
            <td className="px-2 py-4 text-right">
              <Link to={`${workflowBasePath}/${workflow.id}`} aria-label="Open Workflow" className="inline-block whitespace-nowrap text-sm font-semibold text-emerald-700 hover:text-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2">
                Open Workflow
              </Link>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  </div>
);
