import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { useAuthStore } from '../../../store/authStore';
import { agentWorkflowApi } from '../api/agentWorkflowApi';
import { reportingApi } from '../../reporting/api/reportingApi';
import type { WasteReportDetailDto } from '../../reporting/types/reporting';
import type { AgentWorkflowDetail, JsonValue } from '../types/agentWorkflow';
import { AiWorkflowDetailPage } from './AiWorkflowDetailPage';

vi.mock('../api/agentWorkflowApi', () => ({ agentWorkflowApi: { getWorkflow: vi.fn(), approveCollectionPlan: vi.fn(), requestCollectionRevision: vi.fn(), rejectCollectionPlan: vi.fn(), executeCollectionPlan: vi.fn(), approveDispatchPlan: vi.fn(), requestDispatchRevision: vi.fn(), rejectDispatchPlan: vi.fn(), executeDispatchPlan: vi.fn() } }));
vi.mock('../../reporting/api/reportingApi', () => ({ reportingApi: { getWasteReport: vi.fn(), startReview: vi.fn(), verifyReport: vi.fn(), rejectReport: vi.fn() } }));

const plannerOutput: JsonValue = { objective: 'Prepare a shared collection operation.', summary: 'Delegate verified waste needs to planning specialists.', steps: [{ stepId: 'c1', specialist: 'WasteAnalysis', objective: 'Assess verified reports.', dependsOn: [], sequence: 1 }, { stepId: 'c2', specialist: 'CollectionPlanning', objective: 'Create proposed collection groups.', dependsOn: ['c1'], sequence: 2 }, { stepId: 'c3', specialist: 'FleetRoute', objective: 'Recommend routes after approval.', dependsOn: ['c2'], sequence: 3 }, { stepId: 'c4', specialist: 'ValidationOperations', objective: 'Validate operational feasibility.', dependsOn: ['c3'], sequence: 4 }], warnings: ['Review isolated hazardous material separately.'], agentName: 'shared_planner_agent', modelName: null, advisoryOnly: true, status: 'completed' };
const analysisOutput: JsonValue = { objective: 'Assess reports.', analyses: [{ reportId: 'report-123456789', categoryAssessment: 'Organic waste accumulation', recommendedPriority: 'High', operationalConcerns: ['Restricted vehicle access'], recommendedHandling: 'Use a compact collection vehicle.', confidence: 'High', rationale: 'Verified report and fill condition indicate prompt handling.' }, { reportId: 'report-2', categoryAssessment: 'Mixed waste', recommendedPriority: 'Medium', operationalConcerns: [], recommendedHandling: 'Add to the next normal collection cycle.', confidence: 'Medium', rationale: 'No immediate service risk recorded.' }], sourcePage: 1, sourcePageSize: 20, sourceTotalCount: 2, agentName: 'waste_analysis_agent', modelName: null, status: 'completed' };
const planningOutput: JsonValue = { objective: 'Create proposed groups.', candidateGroups: [{ groupId: 'group-north', attentionOrder: 1, needReferences: [{ needId: 'need-1', targetType: 'Report', collectionReason: 'VerifiedReport', urgency: 'High' }], proposedSchedule: { scheduledAt: '2026-10-01T08:00:00Z', schedulingReason: 'High urgency and compatible coverage.' }, rationale: 'Nearby urgent needs can be collected together.', wasteHandlingConsiderations: ['Use sealed containers.'], warnings: [] }], separateHandling: [{ needReference: { needId: 'need-hazard', targetType: 'Bin', collectionReason: 'FullOrBlockedBin', urgency: 'Urgent' }, attentionOrder: null, proposedSchedule: null, rationale: 'Requires specialist hazardous-waste handling.' }], deferredNeeds: [{ needReference: { needId: 'need-deferred', targetType: 'Report', collectionReason: 'RoutineCollection', urgency: 'Low' }, attentionOrder: null, proposedSchedule: null, rationale: 'Defer until the next routine cycle.' }], warnings: ['Current telemetry was unavailable for one bin.'], sourcePage: 1, sourcePageSize: 20, sourceTotalCount: 3, sourceTotalPages: 1, retrievedPages: [1], isCompleteSnapshot: true, agentName: 'collection_planning_agent', modelName: null, advisoryOnly: true, status: 'completed' };
const fleetRouteOutput: JsonValue = { objective: 'Recommend advisory dispatch plans.', dispatchPlans: [{ planId: 'plan-1', recommendedDriver: { driverId: 'driver-1', displayName: 'Asha Driver', reason: 'Available for this collection window.' }, recommendedVehicle: { vehicleId: 'vehicle-1', registrationNumber: 'WP-CAB-1234', vehicleType: 'CompactorTruck', reason: 'Suitable capacity and waste handling.' }, recommendedTasks: [{ taskId: 'task-2', taskCode: 'TSK-102', sequence: 2, addressText: 'Pettah Market, Colombo', reason: 'Follow the first collection stop.' }, { taskId: 'task-1', taskCode: 'TSK-101', sequence: 1, addressText: 'Fort Railway Station, Colombo', reason: 'Earliest scheduled collection.' }], compatibility: { status: 'Unknown', requiresAcknowledgement: true, issues: ['A manual capacity check is required before dispatch approval.'] }, rationale: 'The driver and vehicle are advisory recommendations for the first service area.', warnings: ['Traffic conditions remain advisory.'] }, { planId: 'plan-2', recommendedDriver: { driverId: 'driver-2', displayName: 'Bimal Driver', reason: 'Available for the second service area.' }, recommendedVehicle: { vehicleId: 'vehicle-2', registrationNumber: 'WP-CAB-5678', vehicleType: 'CollectionVan', reason: 'Suitable for the planned task volume.' }, recommendedTasks: [{ taskId: 'task-3', taskCode: 'TSK-103', sequence: 1, addressText: null, reason: 'Independent scheduled collection.' }], compatibility: { status: 'Compatible', requiresAcknowledgement: false, issues: [] }, rationale: 'A separate advisory plan covers the remaining service area.', warnings: [] }], unplannedTasks: [{ taskId: 'task-4', taskCode: 'TSK-104', reason: 'Deferred while vehicle capacity is reviewed.' }], warnings: ['Fleet availability is a persisted planning snapshot.'], rationale: 'Two advisory dispatch plans were proposed from the current task snapshot.', sourceTaskPage: 1, sourceTaskPageSize: 20, sourceTaskTotalCount: 4, sourceTaskTotalPages: 1, agentName: 'fleet_route_agent', modelName: null, advisoryOnly: true, status: 'completed' };
const validationOutput: JsonValue = { objective: 'Validate advisory dispatch plans.', validationOutcome: 'ReadyForHumanReview', planReviews: [{ planId: 'plan-1', outcome: 'ReadyForHumanReview', requiresAcknowledgement: true, findings: [{ code: 'COMPATIBILITY_UNKNOWN', severity: 'Warning', message: 'Capacity confirmation is required before approval.', relatedTaskIds: ['task-1'], relatedDriverId: null, relatedVehicleId: 'vehicle-1' }], summary: 'The first dispatch plan is reviewable with acknowledgement.' }, { planId: 'plan-2', outcome: 'ReadyForHumanReview', requiresAcknowledgement: false, findings: [], summary: 'The second dispatch plan is operationally reviewable.' }], unplannedTaskFindings: [{ code: 'UNPLANNED_CAPACITY', severity: 'Info', message: 'The deferred task remains visible for a later decision.', relatedTaskIds: ['task-4'], relatedDriverId: null, relatedVehicleId: null }], requiresAcknowledgement: true, warnings: ['Review the compatibility caution with the dispatcher.'], summary: 'Operational validation completed against the persisted dispatch proposal.', agentName: 'validation_operations_agent', modelName: null, advisoryOnly: true, status: 'completed' };

const baseDetail: AgentWorkflowDetail = { id: 'workflow-1', objective: 'Prepare a shared collection operation.', status: 'AwaitingCollectionApproval', currentStep: 'CollectionPlanning', initiatedByUserId: 'user-1', createdAt: '2026-09-30T08:00:00Z', updatedAt: '2026-09-30T09:00:00Z', completedAt: null, finalOutcome: null, version: 4, steps: [
  { id: 'planner', sequence: 1, stepType: 'SharedPlanning', agentName: 'shared_planner_agent', status: 'Completed', input: null, output: plannerOutput, validation: null, errorMessage: null, startedAt: '2026-09-30T08:01:00Z', completedAt: '2026-09-30T08:02:00Z' },
  { id: 'c1', sequence: 2, stepType: 'WasteAnalysis', agentName: 'waste_analysis_agent', status: 'Completed', input: null, output: analysisOutput, validation: null, errorMessage: null, startedAt: '2026-09-30T08:02:00Z', completedAt: '2026-09-30T08:03:00Z' },
  { id: 'c2', sequence: 3, stepType: 'CollectionPlanning', agentName: 'collection_planning_agent', status: 'Completed', input: null, output: planningOutput, validation: null, errorMessage: null, startedAt: '2026-09-30T08:03:00Z', completedAt: '2026-09-30T08:04:00Z' },
], transitions: [], approvals: [], executionResults: [] };

const createDetail = (overrides: Partial<AgentWorkflowDetail> = {}): AgentWorkflowDetail => ({ ...baseDetail, ...overrides, steps: overrides.steps ?? baseDetail.steps, transitions: overrides.transitions ?? baseDetail.transitions, approvals: overrides.approvals ?? baseDetail.approvals, executionResults: overrides.executionResults ?? baseDetail.executionResults });
const triggerReportId = 'c17add4f-79f3-4a0a-a9e0-150fde7d7827';
const reportAnalysisOutput: JsonValue = { ...analysisOutput as Record<string, JsonValue>, analyses: [{ ...(analysisOutput as { analyses: Array<Record<string, JsonValue>> }).analyses[0], reportId: triggerReportId }], sourceTotalCount: 1 };
const reportSteps = () => [baseDetail.steps[0], { ...baseDetail.steps[1], output: reportAnalysisOutput }];
const reportWorkflow = (overrides: Partial<AgentWorkflowDetail> = {}): AgentWorkflowDetail => createDetail({ triggerType: 'CitizenReportSubmission', triggeringWasteReportId: triggerReportId, reportReference: 'C17ADD4F', status: 'AwaitingReportVerification', currentStep: 'WasteAnalysis', steps: reportSteps(), ...overrides });
const reportDetail = (status: WasteReportDetailDto['status']): WasteReportDetailDto => ({ id: triggerReportId, reportReference: 'C17ADD4F', citizenId: 'citizen-1', citizenName: 'Citizen', description: 'Waste near the park.', wasteType: 'General', latitude: 6.9, longitude: 79.8, addressText: 'Park Road', status, priority: null, verifiedByUserId: null, verifiedByUserName: null, verifiedAt: null, attachments: [], createdAt: '2026-10-03T08:00:00Z', updatedAt: null });
const withSecondHalf = (fleetOutput: JsonValue | null = fleetRouteOutput, validationOutputValue: JsonValue | null = validationOutput, validationStatus: 'Completed' | 'Failed' = 'Completed'): AgentWorkflowDetail['steps'] => [...baseDetail.steps,
  { id: 'c3', sequence: 4, stepType: 'FleetPlanning', agentName: 'fleet_route_agent', status: 'Completed', input: null, output: fleetOutput, validation: null, errorMessage: null, startedAt: '2026-09-30T08:04:00Z', completedAt: '2026-09-30T08:05:00Z' },
  { id: 'c4', sequence: 5, stepType: 'OperationalValidation', agentName: 'validation_operations_agent', status: validationStatus, input: fleetOutput, output: validationOutputValue, validation: null, errorMessage: validationStatus === 'Failed' ? 'Operational validation could not complete.' : null, startedAt: '2026-09-30T08:05:00Z', completedAt: validationStatus === 'Completed' ? '2026-09-30T08:06:00Z' : null },
];

const renderDetail = (role = 'WasteOfficer') => {
  useAuthStore.setState({ user: { id: 'user-1', fullName: 'Workflow User', email: 'workflow@example.com', role }, isAuthenticated: true, isLoading: false, accessToken: 'token' });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const base = role === 'MunicipalManager' ? '/manager/ai-approvals' : '/officer/ai-approvals';
  return render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[`${base}/workflow-1`]}><Routes><Route path={`${base}/:workflowId`} element={<AiWorkflowDetailPage />} /></Routes></MemoryRouter></QueryClientProvider>);
};

describe('AiWorkflowDetailPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses report references and authoritative bin codes in C1/C2 while keeping IDs in persisted output', async () => {
    const reportId = 'c17add4f-79f3-4a0a-a9e0-150fde7d7827';
    const binId = 'a3711544-f9a1-4a59-b89e-df4fa9407f03';
    const steps = baseDetail.steps.map((step) => step.stepType === 'WasteAnalysis'
      ? { ...step, output: JSON.parse(JSON.stringify(analysisOutput).replace('report-123456789', reportId)) as JsonValue }
      : step.stepType === 'CollectionPlanning'
        ? { ...step, output: JSON.parse(JSON.stringify(planningOutput).replace('need-1', reportId).replace('need-hazard', binId)) as JsonValue }
        : step);
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ steps, binCodes: { [binId]: 'BIN-COL-0042' } }));
    renderDetail();
    expect(await screen.findByText('Report C17ADD4F')).toBeInTheDocument();
    expect(screen.getByText(/BIN-COL-0042/)).toBeInTheDocument();
    expect(screen.queryByText(binId)).not.toBeInTheDocument();
  });

  it('renders the persisted overview, timeline, planner, C1, and proposed C2 plan without review actions', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail());
    renderDetail('MunicipalManager');
    expect(await screen.findByText('Delegate verified waste needs to planning specialists.')).toBeInTheDocument();
    expect(screen.getByText('Fleet & Route')).toBeInTheDocument();
    expect(screen.getByText('Advisory Priority: High')).toBeInTheDocument();
    expect(screen.getByText('Restricted vehicle access')).toBeInTheDocument();
    expect(screen.getByText('Current telemetry was unavailable for one bin.')).toBeInTheDocument();
    expect(screen.getByText('Complete source snapshot')).toBeInTheDocument();
    expect(screen.getByText('Collection Plan Human Review')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /back to ai approvals/i })).toHaveAttribute('href', '/manager/ai-approvals');
    expect(screen.getByLabelText('Collection Approval: Current')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve Collection Plan' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Request Revision' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reject Collection Plan' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Execute Approved Collection Plan' })).not.toBeInTheDocument();
  });

  it('loads only the triggering report and moves from Start Review to direct Verify with a human-selected priority', async () => {
    const user = userEvent.setup();
    let currentReport = reportDetail('Submitted');
    let currentWorkflow = reportWorkflow();
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockImplementation(() => Promise.resolve(currentWorkflow));
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockImplementation(() => Promise.resolve(currentReport));
    (reportingApi.startReview as ReturnType<typeof vi.fn>).mockImplementation(() => { currentReport = reportDetail('UnderReview'); return Promise.resolve(currentReport); });
    (reportingApi.verifyReport as ReturnType<typeof vi.fn>).mockImplementation(() => { currentReport = reportDetail('Verified'); currentWorkflow = reportWorkflow({ status: 'Planning', currentStep: 'CollectionPlanning' }); return Promise.resolve(currentReport); });
    renderDetail('MunicipalManager');
    expect(await screen.findByRole('button', { name: 'Start Review' })).toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'Report review actions' })).getByRole('button', { name: 'Start Review' })).toBeInTheDocument();
    expect(reportingApi.getWasteReport).toHaveBeenCalledWith(triggerReportId);
    expect(screen.getByRole('link', { name: 'Open Report C17ADD4F details' })).toHaveAttribute('href', `/manager/reports/${triggerReportId}`);
    expect(screen.getByText('AI Advisory Analysis')).toBeInTheDocument();
    expect(screen.getByLabelText('Report Verification: Current')).toBeInTheDocument();
    expect(screen.getByText('Advisory Priority: High')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Verify Report' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Start Review' }));
    await user.click(screen.getByTestId('confirm-start-review-button'));
    await waitFor(() => expect(reportingApi.startReview).toHaveBeenCalledWith(triggerReportId));
    await user.click(await screen.findByRole('button', { name: 'Verify Report' }));
    const reviewActions = screen.getByRole('group', { name: 'Report review actions' });
    expect(within(reviewActions).getByRole('button', { name: 'Verify Report' })).toBeInTheDocument();
    expect(within(reviewActions).getByRole('button', { name: 'Reject Report' })).toBeInTheDocument();
    expect(screen.getByText(/AI advisory recommendation:/)).toHaveTextContent('High');
    expect(screen.getByLabelText('Verification priority')).toHaveValue('');
    expect(screen.getByTestId('confirm-verify-report-button')).toBeDisabled();
    await user.selectOptions(screen.getByLabelText('Verification priority'), 'Urgent');
    await user.click(screen.getByTestId('confirm-verify-report-button'));
    await waitFor(() => expect(reportingApi.verifyReport).toHaveBeenCalledWith(triggerReportId, 'Urgent'));
    expect(await screen.findByRole('status', { name: '' })).toHaveTextContent('Report verified. Collection planning is being prepared.');
    expect(screen.queryByRole('button', { name: 'Verify Report' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve Collection Plan' })).not.toBeInTheDocument();
    currentWorkflow = reportWorkflow({ status: 'AwaitingCollectionApproval', currentStep: 'CollectionPlanning', steps: baseDetail.steps });
    await user.click(screen.getByRole('button', { name: 'Refresh report and workflow' }));
    expect(await screen.findByRole('button', { name: 'Approve Collection Plan' })).toBeInTheDocument();
  });

  it('lets WasteOfficer reject the report through the existing modal and shows the terminal workflow', async () => {
    const user = userEvent.setup();
    let currentReport = reportDetail('UnderReview');
    let currentWorkflow = reportWorkflow();
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockImplementation(() => Promise.resolve(currentWorkflow));
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockImplementation(() => Promise.resolve(currentReport));
    (reportingApi.rejectReport as ReturnType<typeof vi.fn>).mockImplementation(() => { currentReport = reportDetail('Rejected'); currentWorkflow = reportWorkflow({ status: 'Rejected' }); return Promise.resolve(currentReport); });
    renderDetail('WasteOfficer');
    await user.click(await screen.findByRole('button', { name: 'Reject Report' }));
    await user.type(screen.getByTestId('reject-reason-input'), 'Location cannot be verified.');
    await user.click(screen.getByTestId('confirm-reject-report-button'));
    await waitFor(() => expect(reportingApi.rejectReport).toHaveBeenCalledWith(triggerReportId, 'Location cannot be verified.'));
    expect(await screen.findByText('Planning stopped. This workflow is closed.')).toBeInTheDocument();
    expect(screen.getByLabelText('Report Verification: Rejected')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Verify Report' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve Collection Plan' })).not.toBeInTheDocument();
  });

  it.each(['Citizen', 'Driver'])('does not offer report decisions to %s', async (role) => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(reportWorkflow());
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(reportDetail('UnderReview'));
    renderDetail(role);
    expect(await screen.findByText('Under Review')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Verify Report' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject Report' })).not.toBeInTheDocument();
  });

  it('does not treat a mismatched C1 analysis as verification guidance or fetch a report for manual workflows', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(reportWorkflow({ steps: [baseDetail.steps[0], baseDetail.steps[1]] })).mockResolvedValue(createDetail());
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(reportDetail('UnderReview'));
    const first = renderDetail();
    expect(await screen.findByText(/saved analysis does not match the triggering report/)).toBeInTheDocument();
    expect(screen.queryByText('Advisory Priority: High')).not.toBeInTheDocument();
    first.unmount();
    renderDetail();
    expect(await screen.findByText('Collection Plan Human Review')).toBeInTheDocument();
    expect(reportingApi.getWasteReport).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['Created', 'None', 'Submitted'],
    ['Planning', 'SharedPlanning', 'UnderReview'],
    ['Failed', 'WasteAnalysis', 'UnderReview'],
    ['Rejected', 'WasteAnalysis', 'Cancelled'],
  ] as const)('keeps report decisions hidden for %s workflow with %s report', async (status, currentStep, reportStatus) => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(reportWorkflow({ status, currentStep }));
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(reportDetail(reportStatus));
    renderDetail('MunicipalManager');
    expect(await screen.findByText('Triggering citizen report')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start Review' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Verify Report' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject Report' })).not.toBeInTheDocument();
    if (status === 'Failed') expect(screen.getByText(/AI analysis or planning could not be completed/)).toBeInTheDocument();
    if (status === 'Rejected') expect(screen.getByText('Planning stopped. This workflow is closed.')).toBeInTheDocument();
  });

  it('keeps workflow detail available when report retrieval fails', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(reportWorkflow());
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('offline'));
    renderDetail();
    expect(await screen.findByText('Triggering citizen report')).toBeInTheDocument();
    expect(await screen.findByText(/Report status is unavailable/)).toBeInTheDocument();
    expect(screen.getByText('AI Advisory Analysis')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start Review' })).not.toBeInTheDocument();
  });

  it.each([
    ['FleetPlanning', 'FleetPlanning', 'Fleet Planning: Current'],
    ['Completed', 'AssignmentExecution', 'Assignment Creation: Completed'],
    ['Failed', 'WasteAnalysis', 'Waste Analysis: Failed'],
    ['Rejected', 'CollectionPlanning', 'Collection Approval: Rejected'],
    ['CollectionNeedsRevision', 'CollectionPlanning', 'Collection Approval: Needs Revision'],
  ] as const)('derives timeline state for %s workflows', async (status, currentStep, timelineLabel) => {
    const steps = status === 'Failed' ? baseDetail.steps.map((step) => step.stepType === 'WasteAnalysis' ? { ...step, status: 'Failed' as const, output: null, errorMessage: 'The analysis could not complete.' } : step) : baseDetail.steps;
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ status, currentStep, steps }));
    renderDetail();
    expect(await screen.findByLabelText(timelineLabel)).toBeInTheDocument();
    if (status === 'Failed') expect(screen.getByRole('alert')).toHaveTextContent('could not complete');
  });

  it('renders C1 empty and C2 partial snapshot states distinctly', async () => {
    const steps = baseDetail.steps.map((step) => {
      if (step.stepType === 'WasteAnalysis') return { ...step, output: { ...analysisOutput as Record<string, JsonValue>, analyses: [], status: 'empty' } as JsonValue };
      if (step.stepType === 'CollectionPlanning') return { ...step, output: { ...planningOutput as Record<string, JsonValue>, isCompleteSnapshot: false, status: 'partial' } as JsonValue };
      return step;
    });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ steps }));
    renderDetail();
    expect(await screen.findByText('No verified waste reports were available for analysis.')).toBeInTheDocument();
    expect(screen.getByText('Partial source snapshot')).toBeInTheDocument();
    expect(screen.getByText(/used only part of the authoritative source snapshot/i)).toBeInTheDocument();
  });

  it('renders a missing planner result and an empty C2 result without treating either as a failure', async () => {
    const steps = baseDetail.steps.map((step) => step.stepType === 'SharedPlanning' ? { ...step, output: null } : step.stepType === 'CollectionPlanning' ? { ...step, output: { ...planningOutput as Record<string, JsonValue>, candidateGroups: [], status: 'empty' } as JsonValue } : step);
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ steps }));
    renderDetail();
    expect(await screen.findByText('Planner result unavailable.')).toBeInTheDocument();
    expect(screen.getByText('No collection needs were available for planning.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('keeps valid sections visible when a legacy planner output is malformed and another output is missing', async () => {
    const steps = baseDetail.steps.map((step) => step.stepType === 'SharedPlanning' ? { ...step, output: '{not-valid-json' } : step.stepType === 'WasteAnalysis' ? { ...step, output: null } : step);
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ steps }));
    renderDetail();
    expect(await screen.findByText('Result unavailable or incompatible with the current format.')).toBeInTheDocument();
    expect(screen.getByText('Waste Analysis result unavailable.')).toBeInTheDocument();
    expect(screen.getByText('Proposed collection groups')).toBeInTheDocument();
  });

  it('renders loading and unavailable states through the existing detail GET client', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockReturnValueOnce(new Promise(() => undefined));
    const firstRender = renderDetail();
    expect(screen.getByText('Loading workflow…')).toBeInTheDocument();
    firstRender.unmount();
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('missing'));
    renderDetail();
    expect(await screen.findByText('Workflow unavailable')).toBeInTheDocument();
  });

  it('approves once with the current version and then renders the separate execution action', async () => {
    const user = userEvent.setup();
    const approved = createDetail({ status: 'CollectionApproved', currentStep: 'ScheduledTaskCreation', version: 5 });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(createDetail()).mockResolvedValue(approved);
    (agentWorkflowApi.approveCollectionPlan as ReturnType<typeof vi.fn>).mockResolvedValue(approved);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Approve Collection Plan' }));
    await user.click(screen.getAllByRole('button', { name: 'Approve Collection Plan' })[1]);
    await waitFor(() => expect(agentWorkflowApi.approveCollectionPlan).toHaveBeenCalledWith('workflow-1', { expectedVersion: 4 }));
    expect(agentWorkflowApi.executeCollectionPlan).not.toHaveBeenCalled();
    expect(await screen.findByRole('button', { name: 'Execute Approved Collection Plan' })).toBeInTheDocument();
  });

  it('sends an optional approval note without coupling approval to execution', async () => {
    const user = userEvent.setup();
    const approved = createDetail({ status: 'CollectionApproved', currentStep: 'ScheduledTaskCreation', version: 5 });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(createDetail()).mockResolvedValue(approved);
    (agentWorkflowApi.approveCollectionPlan as ReturnType<typeof vi.fn>).mockResolvedValue(approved);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Approve Collection Plan' }));
    await user.type(screen.getByLabelText('Approval note (optional)'), 'Reviewed against the current service needs.');
    await user.click(screen.getAllByRole('button', { name: 'Approve Collection Plan' })[1]);
    await waitFor(() => expect(agentWorkflowApi.approveCollectionPlan).toHaveBeenCalledWith('workflow-1', { expectedVersion: 4, reason: 'Reviewed against the current service needs.' }));
    expect(agentWorkflowApi.executeCollectionPlan).not.toHaveBeenCalled();
  });

  it('prevents a duplicate approval while the authoritative request is pending', async () => {
    const user = userEvent.setup();
    let resolveApproval: (value: AgentWorkflowDetail) => void = () => undefined;
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail());
    (agentWorkflowApi.approveCollectionPlan as ReturnType<typeof vi.fn>).mockReturnValue(new Promise<AgentWorkflowDetail>((resolve) => { resolveApproval = resolve; }));
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Approve Collection Plan' }));
    await user.click(screen.getAllByRole('button', { name: 'Approve Collection Plan' })[1]);
    expect(agentWorkflowApi.approveCollectionPlan).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'Loading Approving...' })).toBeDisabled();
    resolveApproval(createDetail({ status: 'CollectionApproved', version: 5, currentStep: 'ScheduledTaskCreation' }));
  });

  it('requires a 5 to 500 character reason for revision and sends the exact request', async () => {
    const user = userEvent.setup();
    const revised = createDetail({ status: 'CollectionNeedsRevision', version: 5 });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(createDetail()).mockResolvedValue(revised);
    (agentWorkflowApi.requestCollectionRevision as ReturnType<typeof vi.fn>).mockResolvedValue(revised);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Request Revision' }));
    await user.type(screen.getByLabelText('Reason'), 'bad');
    await user.click(screen.getAllByRole('button', { name: 'Request Revision' })[1]);
    expect(screen.getByRole('alert')).toHaveTextContent('at least 5 characters');
    await user.clear(screen.getByLabelText('Reason'));
    await user.type(screen.getByLabelText('Reason'), 'Please separate the urgent hazardous collection need.');
    await user.click(screen.getAllByRole('button', { name: 'Request Revision' })[1]);
    await waitFor(() => expect(agentWorkflowApi.requestCollectionRevision).toHaveBeenCalledWith('workflow-1', { expectedVersion: 4, reason: 'Please separate the urgent hazardous collection need.' }));
    expect(await screen.findByText(/revision was requested/i)).toBeInTheDocument();
    expect(agentWorkflowApi.executeCollectionPlan).not.toHaveBeenCalled();
  });

  it('rejects with a required reason and removes all collection actions', async () => {
    const user = userEvent.setup();
    const rejected = createDetail({ status: 'Rejected', version: 5 });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(createDetail()).mockResolvedValue(rejected);
    (agentWorkflowApi.rejectCollectionPlan as ReturnType<typeof vi.fn>).mockResolvedValue(rejected);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Reject Collection Plan' }));
    await user.type(screen.getByLabelText('Reason'), 'The plan cannot safely proceed.');
    await user.click(screen.getAllByRole('button', { name: 'Reject Collection Plan' })[1]);
    await waitFor(() => expect(agentWorkflowApi.rejectCollectionPlan).toHaveBeenCalledWith('workflow-1', { expectedVersion: 4, reason: 'The plan cannot safely proceed.' }));
    expect(await screen.findByText(/was rejected and is closed/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /approve|request revision|execute approved/i })).not.toBeInTheDocument();
  });

  it('does not present an incomplete C2 snapshot as approval-ready', async () => {
    const incompleteSteps = baseDetail.steps.map((step) => step.stepType === 'CollectionPlanning' ? { ...step, output: { ...planningOutput as Record<string, JsonValue>, isCompleteSnapshot: false } as JsonValue } : step);
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ steps: incompleteSteps }));
    renderDetail();
    expect(await screen.findByText('Collection plan is not approval-ready.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /approve|request revision|reject collection/i })).not.toBeInTheDocument();
  });

  it('executes an approved plan once with the post-approval version and renders the authoritative dispatch outcome', async () => {
    const user = userEvent.setup();
    const approved = createDetail({ status: 'CollectionApproved', currentStep: 'ScheduledTaskCreation', version: 5 });
    const dispatched = createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'OperationalValidation', version: 6 });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(approved).mockResolvedValue(dispatched);
    (agentWorkflowApi.executeCollectionPlan as ReturnType<typeof vi.fn>).mockResolvedValue(dispatched);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Execute Approved Collection Plan' }));
    await user.click(screen.getByRole('button', { name: 'Execute Approved Plan' }));
    await waitFor(() => expect(agentWorkflowApi.executeCollectionPlan).toHaveBeenCalledWith('workflow-1', { expectedVersion: 5 }));
    expect((await screen.findAllByText('Awaiting Dispatch Approval')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Execute Approved Collection Plan' })).not.toBeInTheDocument();
    expect(screen.queryByText(/driver recommendation|suggested stop sequence/i)).not.toBeInTheDocument();
  });

  it('handles a 409 conflict by refreshing without retrying the stale action', async () => {
    const user = userEvent.setup();
    const conflict = { isAxiosError: true, response: { status: 409 } };
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail());
    (agentWorkflowApi.approveCollectionPlan as ReturnType<typeof vi.fn>).mockRejectedValue(conflict);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Approve Collection Plan' }));
    await user.click(screen.getAllByRole('button', { name: 'Approve Collection Plan' })[1]);
    expect(await screen.findByRole('alert')).toHaveTextContent('changed while you were reviewing');
    expect(agentWorkflowApi.approveCollectionPlan).toHaveBeenCalledTimes(1);
    expect(agentWorkflowApi.executeCollectionPlan).not.toHaveBeenCalled();
  });

  it('treats an unknown collection-execution outcome as unconfirmed and never retries automatically', async () => {
    const user = userEvent.setup();
    const approved = createDetail({ status: 'CollectionApproved', currentStep: 'ScheduledTaskCreation', version: 5 });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(approved);
    (agentWorkflowApi.executeCollectionPlan as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('network unavailable'));
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Execute Approved Collection Plan' }));
    await user.click(screen.getByRole('button', { name: 'Execute Approved Plan' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('could not be confirmed');
    expect(agentWorkflowApi.executeCollectionPlan).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(agentWorkflowApi.getWorkflow).toHaveBeenCalledTimes(2));
    expect(screen.getByRole('button', { name: 'Execute Approved Collection Plan' })).toBeInTheDocument();
  });

  it('renders persisted C3/C4 dispatch plans, validation, and ready-for-review dispatch actions', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', version: 6, steps: withSecondHalf() }));
    renderDetail();
    expect(await screen.findByText('Fleet & Route Planning')).toBeInTheDocument();
    expect(screen.getAllByText('Dispatch Plan 1').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Dispatch Plan 2').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Recommended Driver')).toHaveLength(2);
    expect(screen.getAllByText('Recommended Vehicle')).toHaveLength(2);
    expect(screen.getAllByText('Suggested Stop Sequence')).toHaveLength(2);
    const firstPlan = screen.getAllByText('Dispatch Plan 1')[0].closest('article');
    expect(firstPlan?.textContent?.indexOf('TSK-101')).toBeLessThan(firstPlan?.textContent?.indexOf('TSK-102') ?? 0);
    expect(screen.getByText('Unplanned Tasks')).toBeInTheDocument();
    expect(screen.getByText('TSK-104')).toBeInTheDocument();
    expect(screen.getByText('Compatibility unknown')).toBeInTheDocument();
    expect(screen.getAllByText('Operational Validation').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Ready for Human Review').length).toBeGreaterThan(0);
    expect(screen.getByText('Warning acknowledgement will be required before dispatch approval.')).toBeInTheDocument();
    expect(screen.getAllByText('Dispatch proposal is ready for human review.')).toHaveLength(2);
    expect(screen.queryByText(/optimal route|optimized route|fastest route|shortest route/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve Dispatch' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Request Dispatch Revision' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reject Dispatch' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Execute Approved Dispatch Plan' })).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(agentWorkflowApi.approveDispatchPlan).not.toHaveBeenCalled();
    expect(agentWorkflowApi.requestDispatchRevision).not.toHaveBeenCalled();
    expect(agentWorkflowApi.rejectDispatchPlan).not.toHaveBeenCalled();
    expect(agentWorkflowApi.executeDispatchPlan).not.toHaveBeenCalled();
  });

  it('requires explicit warning acknowledgement and approves dispatch without automatic execution', async () => {
    const user = userEvent.setup();
    const approved = createDetail({ status: 'DispatchApproved', currentStep: 'DispatchApproval', version: 7, steps: withSecondHalf(), approvals: [{ id: 'dispatch-approval-1', workflowStepId: 'c4', approvalStage: 'FleetDispatch', decision: 'Approved', decisionReason: 'Reviewed current operational warnings.', decisionPayload: { acknowledgeWarnings: true }, decidedByUserId: 'user-1', decidedAt: '2026-10-01T08:00:00Z' }] });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', version: 6, steps: withSecondHalf() })).mockResolvedValue(approved);
    (agentWorkflowApi.approveDispatchPlan as ReturnType<typeof vi.fn>).mockResolvedValue(approved);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Approve Dispatch' }));
    const submit = screen.getAllByRole('button', { name: 'Approve Dispatch' })[1];
    expect(submit).toBeDisabled();
    await user.click(screen.getByRole('checkbox', { name: /acknowledge the operational warnings/i }));
    await user.type(screen.getByLabelText('Approval note (optional)'), 'Reviewed current operational warnings.');
    await user.click(submit);
    await waitFor(() => expect(agentWorkflowApi.approveDispatchPlan).toHaveBeenCalledWith('workflow-1', { expectedVersion: 6, acknowledgeWarnings: true, reason: 'Reviewed current operational warnings.' }));
    expect(agentWorkflowApi.executeDispatchPlan).not.toHaveBeenCalled();
    expect(await screen.findByRole('button', { name: 'Execute Approved Dispatch Plan' })).toBeInTheDocument();
    expect(screen.getByText(/Warning acknowledgement: acknowledged/i)).toBeInTheDocument();
  });

  it('sends false acknowledgement when C4 does not require one', async () => {
    const user = userEvent.setup();
    const noAck = { ...validationOutput as Record<string, JsonValue>, requiresAcknowledgement: false } as JsonValue;
    const approved = createDetail({ status: 'DispatchApproved', currentStep: 'DispatchApproval', version: 7, steps: withSecondHalf(fleetRouteOutput, noAck) });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', version: 6, steps: withSecondHalf(fleetRouteOutput, noAck) })).mockResolvedValue(approved);
    (agentWorkflowApi.approveDispatchPlan as ReturnType<typeof vi.fn>).mockResolvedValue(approved);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Approve Dispatch' }));
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: 'Approve Dispatch' })[1]);
    await waitFor(() => expect(agentWorkflowApi.approveDispatchPlan).toHaveBeenCalledWith('workflow-1', { expectedVersion: 6, acknowledgeWarnings: false }));
  });

  it('resets dispatch warning acknowledgement when its dialog is cancelled', async () => {
    const user = userEvent.setup();
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', version: 6, steps: withSecondHalf() }));
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Approve Dispatch' }));
    const acknowledgement = screen.getByRole('checkbox', { name: /acknowledge the operational warnings/i });
    await user.click(acknowledgement);
    expect(acknowledgement).toBeChecked();
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await user.click(screen.getByRole('button', { name: 'Approve Dispatch' }));
    expect(screen.getByRole('checkbox', { name: /acknowledge the operational warnings/i })).not.toBeChecked();
    expect(screen.getAllByRole('button', { name: 'Approve Dispatch' })[1]).toBeDisabled();
  });

  it('validates dispatch revision and rejection reasons and calls only the selected endpoint', async () => {
    const user = userEvent.setup();
    const ready = createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', version: 6, steps: withSecondHalf() });
    const revised = createDetail({ status: 'DispatchNeedsRevision', currentStep: 'OperationalValidation', version: 7, steps: withSecondHalf() });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(ready).mockResolvedValue(revised);
    (agentWorkflowApi.requestDispatchRevision as ReturnType<typeof vi.fn>).mockResolvedValue(revised);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Request Dispatch Revision' }));
    await user.type(screen.getByLabelText('Reason'), 'bad');
    await user.click(screen.getAllByRole('button', { name: 'Request Dispatch Revision' })[1]);
    expect(screen.getByRole('alert')).toHaveTextContent('at least 5 characters');
    await user.clear(screen.getByLabelText('Reason'));
    await user.type(screen.getByLabelText('Reason'), 'Use a compatible vehicle for the first plan.');
    await user.click(screen.getAllByRole('button', { name: 'Request Dispatch Revision' })[1]);
    await waitFor(() => expect(agentWorkflowApi.requestDispatchRevision).toHaveBeenCalledWith('workflow-1', { expectedVersion: 6, reason: 'Use a compatible vehicle for the first plan.' }));
    expect(agentWorkflowApi.rejectDispatchPlan).not.toHaveBeenCalled();
    expect(agentWorkflowApi.executeDispatchPlan).not.toHaveBeenCalled();
  });

  it('executes only an approved dispatch with the current version and renders assignment completion', async () => {
    const user = userEvent.setup();
    const approved = createDetail({ status: 'DispatchApproved', currentStep: 'DispatchApproval', version: 7, steps: withSecondHalf() });
    const completed = createDetail({ status: 'Completed', currentStep: 'AssignmentExecution', version: 9, steps: withSecondHalf(), executionResults: [{ id: 'execution-1', workflowStepId: 'assignment-step', executionType: 'CollectionAssignment', status: 'Succeeded', result: null, errorMessage: null, executedAt: '2026-10-01T09:00:00Z' }] });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(approved).mockResolvedValue(completed);
    (agentWorkflowApi.executeDispatchPlan as ReturnType<typeof vi.fn>).mockResolvedValue(completed);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Execute Approved Dispatch Plan' }));
    await user.click(screen.getAllByRole('button', { name: 'Execute Approved Dispatch Plan' })[1]);
    await waitFor(() => expect(agentWorkflowApi.executeDispatchPlan).toHaveBeenCalledWith('workflow-1', { expectedVersion: 7 }));
    expect(agentWorkflowApi.approveDispatchPlan).not.toHaveBeenCalled();
    expect((await screen.findAllByText('Workflow Completed')).length).toBeGreaterThan(0);
    expect(screen.getByText(/CollectionAssignment execution: Succeeded/i)).toBeInTheDocument();
  });

  it('handles dispatch conflicts and unknown execution outcomes by refreshing without retries', async () => {
    const user = userEvent.setup();
    const ready = createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', version: 6, steps: withSecondHalf() });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(ready);
    (agentWorkflowApi.approveDispatchPlan as ReturnType<typeof vi.fn>).mockRejectedValue({ isAxiosError: true, response: { status: 409 } });
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Approve Dispatch' }));
    await user.click(screen.getByRole('checkbox', { name: /acknowledge the operational warnings/i }));
    await user.click(screen.getAllByRole('button', { name: 'Approve Dispatch' })[1]);
    expect(await screen.findByRole('alert')).toHaveTextContent('changed while you were reviewing');
    expect(agentWorkflowApi.approveDispatchPlan).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(agentWorkflowApi.getWorkflow).toHaveBeenCalledTimes(2));
  });

  it('rejects dispatch with a required reason and leaves no execution action', async () => {
    const user = userEvent.setup();
    const ready = createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', version: 6, steps: withSecondHalf() });
    const rejected = createDetail({ status: 'Rejected', currentStep: 'OperationalValidation', version: 7, steps: withSecondHalf() });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(ready).mockResolvedValue(rejected);
    (agentWorkflowApi.rejectDispatchPlan as ReturnType<typeof vi.fn>).mockResolvedValue(rejected);
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Reject Dispatch' }));
    await user.type(screen.getByLabelText('Reason'), 'The fleet capacity risk remains unresolved.');
    await user.click(screen.getAllByRole('button', { name: 'Reject Dispatch' })[1]);
    await waitFor(() => expect(agentWorkflowApi.rejectDispatchPlan).toHaveBeenCalledWith('workflow-1', { expectedVersion: 6, reason: 'The fleet capacity risk remains unresolved.' }));
    expect(await screen.findByText(/no CollectionAssignments will be created/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Execute Approved Dispatch Plan' })).not.toBeInTheDocument();
  });

  it('treats an unknown dispatch execution outcome as unconfirmed and never retries automatically', async () => {
    const user = userEvent.setup();
    const approved = createDetail({ status: 'DispatchApproved', currentStep: 'DispatchApproval', version: 7, steps: withSecondHalf() });
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(approved);
    (agentWorkflowApi.executeDispatchPlan as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('network unavailable'));
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Execute Approved Dispatch Plan' }));
    await user.click(screen.getAllByRole('button', { name: 'Execute Approved Dispatch Plan' })[1]);
    expect(await screen.findByRole('alert')).toHaveTextContent('execution response could not be confirmed');
    expect(agentWorkflowApi.executeDispatchPlan).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(agentWorkflowApi.getWorkflow).toHaveBeenCalledTimes(2));
  });

  it('keeps the same C3/C4 visualization on the Manager detail route', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', steps: withSecondHalf() }));
    renderDetail('MunicipalManager');
    expect(await screen.findByText('Fleet & Route Planning')).toBeInTheDocument();
    expect(screen.getAllByText('Operational Validation').length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: /back to ai approvals/i })).toHaveAttribute('href', '/manager/ai-approvals');
  });

  it('renders valid empty C3 output without a fake dispatch plan', async () => {
    const emptyFleet = { ...fleetRouteOutput as Record<string, JsonValue>, dispatchPlans: [], unplannedTasks: [], warnings: [], status: 'empty' } as JsonValue;
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', steps: withSecondHalf(emptyFleet) }));
    renderDetail();
    expect(await screen.findByText('No dispatch plans were proposed for the considered Scheduled CollectionTasks.')).toBeInTheDocument();
    expect(screen.queryByText('Dispatch Plan 1')).not.toBeInTheDocument();
    expect(screen.getByText('All considered tasks were included in a dispatch plan.')).toBeInTheDocument();
  });

  it('renders C3 and C4 in-progress states without treating persisted output as an error', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(createDetail({ status: 'FleetPlanning', currentStep: 'FleetPlanning' })).mockResolvedValueOnce(createDetail({ status: 'OperationalValidation', currentStep: 'OperationalValidation', steps: withSecondHalf(fleetRouteOutput, null) }));
    const firstRender = renderDetail();
    expect(await screen.findByText('Fleet planning is in progress.')).toBeInTheDocument();
    firstRender.unmount();
    renderDetail();
    expect((await screen.findAllByText('Suggested Stop Sequence')).length).toBeGreaterThan(0);
    expect(screen.getByText('Operational validation is in progress.')).toBeInTheDocument();
  });

  it('renders Needs Revision and incompatible findings as a valid C4 outcome without acknowledgement input', async () => {
    const needsRevision = { ...validationOutput as Record<string, JsonValue>, validationOutcome: 'NeedsRevision', requiresAcknowledgement: false, planReviews: [{ planId: 'plan-1', outcome: 'NeedsRevision', requiresAcknowledgement: false, findings: [{ code: 'VEHICLE_INCOMPATIBLE', severity: 'Error', message: 'The proposed vehicle cannot handle this task.', relatedTaskIds: ['task-1'], relatedDriverId: null, relatedVehicleId: 'vehicle-1' }], summary: 'Revise the first dispatch proposal.' }], warnings: [] } as JsonValue;
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ status: 'DispatchNeedsRevision', currentStep: 'OperationalValidation', steps: withSecondHalf(fleetRouteOutput, needsRevision) }));
    renderDetail();
    expect(await screen.findAllByText('Needs Revision')).not.toHaveLength(0);
    expect(screen.getByText('The proposed vehicle cannot handle this task.')).toBeInTheDocument();
    expect(screen.getByText('Dispatch proposal requires revision before approval.')).toBeInTheDocument();
    expect(screen.queryByText('Warning acknowledgement will be required before dispatch approval.')).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
  });

  it('keeps valid C3 visible when C4 is malformed or failed', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', steps: withSecondHalf(fleetRouteOutput, '{not-valid-json') })).mockResolvedValueOnce(createDetail({ status: 'Failed', currentStep: 'OperationalValidation', steps: withSecondHalf(fleetRouteOutput, null, 'Failed') }));
    const firstRender = renderDetail();
    expect(await screen.findByText('Asha Driver')).toBeInTheDocument();
    expect(screen.getByText('Operational Validation result is unavailable or incompatible with the current format.')).toBeInTheDocument();
    firstRender.unmount();
    renderDetail();
    expect(await screen.findByText('Asha Driver')).toBeInTheDocument();
    expect(screen.getByText('Operational Validation result unavailable because validation did not complete.')).toBeInTheDocument();
  });

  it('renders a malformed C3 result safely and retains the rest of the workflow detail', async () => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ status: 'AwaitingDispatchApproval', currentStep: 'DispatchApproval', steps: withSecondHalf('{not-valid-json') }));
    renderDetail();
    expect(await screen.findByText('Fleet & Route result is unavailable or incompatible with the current format.')).toBeInTheDocument();
    expect(screen.getByText('Collection Plan Human Review')).toBeInTheDocument();
    expect(screen.getAllByText('Operational Validation').length).toBeGreaterThan(0);
  });

  it.each([
    ['DispatchNeedsRevision', 'OperationalValidation', 'Dispatch proposal requires revision before approval.'],
    ['Completed', 'AssignmentExecution', 'Agentic workflow planning and approved assignment creation are recorded as complete. Driver execution remains separate.'],
  ] as const)('keeps historical C3/C4 read-only for %s workflows', async (status, currentStep, callout) => {
    (agentWorkflowApi.getWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(createDetail({ status, currentStep, steps: withSecondHalf() }));
    renderDetail();
    expect(await screen.findByText('Asha Driver')).toBeInTheDocument();
    expect(screen.getByText(callout)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /approve dispatch|request dispatch revision|reject dispatch|execute dispatch plan/i })).not.toBeInTheDocument();
  });
});
