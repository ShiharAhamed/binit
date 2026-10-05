import { describe, expect, it } from 'vitest';
import type { AgentWorkflowSummary } from '../types/agentWorkflow';
import { getTriggerReportLabel, getWorkflowStatusLabel, getWorkflowTriggerLabel, matchesWorkflowFilter, needsHumanAttention } from './workflowPresentation';

const manual: AgentWorkflowSummary = {
  id: 'workflow-1', objective: 'Plan collection.', status: 'AwaitingCollectionApproval', currentStep: 'CollectionPlanning',
  initiatedByUserId: 'staff-1', createdAt: '2026-10-03T08:00:00Z', updatedAt: null, completedAt: null, finalOutcome: null, version: 1,
};

describe('workflow presentation', () => {
  it('labels the report pause and includes it in human attention alongside existing approvals', () => {
    expect(getWorkflowStatusLabel('AwaitingReportVerification')).toBe('Awaiting Report Verification');
    expect(needsHumanAttention('AwaitingReportVerification')).toBe(true);
    expect(needsHumanAttention('AwaitingCollectionApproval')).toBe(true);
    expect(needsHumanAttention('AwaitingDispatchApproval')).toBe(true);
    expect(matchesWorkflowFilter('Planning', 'attention')).toBe(false);
    expect(matchesWorkflowFilter('Rejected', 'attention')).toBe(false);
  });

  it('treats missing historical trigger fields as manual and uses the backend report reference for citizen workflows', () => {
    expect(getWorkflowTriggerLabel(manual)).toBe('Operational Planning');
    expect(getTriggerReportLabel(manual)).toBeNull();
    const citizen: AgentWorkflowSummary = { ...manual, triggerType: 'CitizenReportSubmission', triggeringWasteReportId: 'c17add4f-79f3-4a0a-a9e0-150fde7d7827', reportReference: 'C17ADD4F' };
    expect(getWorkflowTriggerLabel(citizen)).toBe('Citizen Report Response');
    expect(getTriggerReportLabel(citizen)).toBe('Report C17ADD4F');
  });
});
