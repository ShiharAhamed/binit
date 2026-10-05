import type { AgentWorkflowStatus, AgentWorkflowSummary, WorkflowStepType } from '../types/agentWorkflow';
import { reportLabel } from '../../../utils/displayReferences';

const workflowStatusLabels: Record<AgentWorkflowStatus, string> = {
  Created: 'Created',
  Planning: 'Planning',
  AwaitingReportVerification: 'Awaiting Report Verification',
  AwaitingCollectionApproval: 'Awaiting Collection Approval',
  CollectionNeedsRevision: 'Collection Needs Revision',
  CollectionApproved: 'Collection Approved',
  CreatingScheduledTasks: 'Creating Scheduled Tasks',
  FleetPlanning: 'Fleet Planning',
  OperationalValidation: 'Operational Validation',
  AwaitingDispatchApproval: 'Awaiting Dispatch Approval',
  DispatchNeedsRevision: 'Dispatch Needs Revision',
  DispatchApproved: 'Dispatch Approved',
  ExecutingAssignments: 'Executing Assignments',
  Completed: 'Workflow Completed',
  Rejected: 'Rejected',
  Failed: 'Failed',
};

const workflowStepLabels: Record<WorkflowStepType, string> = {
  None: 'Not started',
  SharedPlanning: 'Shared Planning',
  WasteAnalysis: 'Waste Analysis',
  CollectionPlanning: 'Collection Planning',
  ScheduledTaskCreation: 'Scheduled Task Creation',
  FleetPlanning: 'Fleet Planning',
  OperationalValidation: 'Operational Validation',
  DispatchApproval: 'Dispatch Approval',
  AssignmentExecution: 'Assignment Execution',
};

export const getWorkflowStatusLabel = (status: AgentWorkflowStatus): string => workflowStatusLabels[status];
export const getWorkflowStepLabel = (step: WorkflowStepType): string => workflowStepLabels[step];

export const needsHumanAttention = (status: AgentWorkflowStatus): boolean =>
  status === 'AwaitingReportVerification' || status === 'AwaitingCollectionApproval' || status === 'AwaitingDispatchApproval';

export const isReportTriggeredWorkflow = (workflow: AgentWorkflowSummary): boolean =>
  workflow.triggerType === 'CitizenReportSubmission';

export const getWorkflowTriggerLabel = (workflow: AgentWorkflowSummary): string =>
  isReportTriggeredWorkflow(workflow) ? 'Citizen Report Response' : 'Operational Planning';

export const getTriggerReportLabel = (workflow: AgentWorkflowSummary): string | null =>
  isReportTriggeredWorkflow(workflow) && workflow.triggeringWasteReportId
    ? reportLabel(workflow.triggeringWasteReportId, workflow.reportReference)
    : null;

export const isWorkflowInProgress = (status: AgentWorkflowStatus): boolean => [
  'Created',
  'Planning',
  'CollectionApproved',
  'CreatingScheduledTasks',
  'FleetPlanning',
  'OperationalValidation',
  'DispatchApproved',
  'ExecutingAssignments',
].includes(status);

export type WorkflowDisplayFilter = 'all' | 'attention' | 'inProgress' | 'completed' | 'terminal';

export const matchesWorkflowFilter = (status: AgentWorkflowStatus, filter: WorkflowDisplayFilter): boolean => {
  if (filter === 'all') return true;
  if (filter === 'attention') return needsHumanAttention(status);
  if (filter === 'inProgress') return isWorkflowInProgress(status);
  if (filter === 'completed') return status === 'Completed';
  return status === 'Failed' || status === 'Rejected' || status === 'CollectionNeedsRevision' || status === 'DispatchNeedsRevision';
};

export const getAiApprovalsBasePath = (role?: string): string =>
  role === 'MunicipalManager' ? '/manager/ai-approvals' : '/officer/ai-approvals';

export const formatWorkflowDate = (value: string | null): string => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
};
