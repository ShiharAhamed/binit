import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { useAuthStore } from '../../../store/authStore';
import { agentWorkflowApi } from '../api/agentWorkflowApi';
import { AiApprovalsPage } from './AiApprovalsPage';
import type { AgentWorkflowSummary, PagedAgentWorkflows } from '../types/agentWorkflow';

vi.mock('../api/agentWorkflowApi', () => ({
  agentWorkflowApi: {
    listWorkflows: vi.fn(),
    createWorkflow: vi.fn(),
    startWorkflow: vi.fn(),
  },
}));

const workflows: AgentWorkflowSummary[] = [
  { id: 'attention', objective: 'Prepare urgent collection coverage.', status: 'AwaitingCollectionApproval', currentStep: 'CollectionPlanning', initiatedByUserId: 'user-1', createdAt: '2026-09-30T08:00:00Z', updatedAt: '2026-09-30T09:00:00Z', completedAt: null, finalOutcome: null, version: 2 },
  { id: 'complete', objective: 'Complete completed operation.', status: 'Completed', currentStep: 'AssignmentExecution', initiatedByUserId: 'user-1', createdAt: '2026-09-29T08:00:00Z', updatedAt: null, completedAt: '2026-09-29T10:00:00Z', finalOutcome: 'Done', version: 5 },
];

const pageResult = (items = workflows): PagedAgentWorkflows => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 });

const setRole = (role: string) => useAuthStore.setState({ user: { id: 'user-1', fullName: 'Workflow User', email: 'workflow@example.com', role }, isAuthenticated: true, isLoading: false, accessToken: 'token' });

const renderDashboard = (role = 'WasteOfficer') => {
  setRole(role);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const basePath = role === 'MunicipalManager' ? '/manager/ai-approvals' : '/officer/ai-approvals';
  return render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[basePath]}><Routes><Route path={basePath} element={<AiApprovalsPage />} /><Route path={`${basePath}/:workflowId`} element={<p>Workflow destination</p>} /></Routes></MemoryRouter></QueryClientProvider>);
};

describe('AiApprovalsPage dashboard and initiation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (agentWorkflowApi.listWorkflows as ReturnType<typeof vi.fn>).mockResolvedValue(pageResult());
  });

  it('renders workflow summary, rows, human-readable statuses, and open links', async () => {
    renderDashboard();
    expect(await screen.findByText('Prepare urgent collection coverage.')).toBeInTheDocument();
    expect(screen.getByText('Awaiting Collection Approval')).toBeInTheDocument();
    expect(screen.getAllByText('Workflow Completed')).toHaveLength(2);
    expect(screen.getByText('Assignment Execution')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: 'Open Workflow' })[0]).toHaveAttribute('href', '/officer/ai-approvals/attention');
  });

  it('shows report verification as human attention with a friendly report link and keeps planning out of that filter', async () => {
    const reportId = 'c17add4f-79f3-4a0a-a9e0-150fde7d7827';
    const reportWorkflow: AgentWorkflowSummary = { ...workflows[0], id: 'citizen-report', triggerType: 'CitizenReportSubmission', triggeringWasteReportId: reportId, reportReference: 'C17ADD4F', status: 'AwaitingReportVerification', currentStep: 'WasteAnalysis' };
    const processingWorkflow: AgentWorkflowSummary = { ...reportWorkflow, id: 'processing-report', status: 'Planning', currentStep: 'CollectionPlanning' };
    (agentWorkflowApi.listWorkflows as ReturnType<typeof vi.fn>).mockResolvedValue(pageResult([reportWorkflow, processingWorkflow, workflows[0], { ...workflows[0], id: 'dispatch', status: 'AwaitingDispatchApproval' }, workflows[1]]));
    renderDashboard('MunicipalManager');
    const reportLinks = await screen.findAllByRole('link', { name: 'Report C17ADD4F' });
    expect(reportLinks).toHaveLength(2);
    const sourceCell = within(reportLinks[0].closest('tr')!).getAllByRole('cell')[1];
    expect(sourceCell).toHaveTextContent('Report C17ADD4F');
    expect(sourceCell).not.toHaveTextContent('Citizen Report Response');
    expect(sourceCell).not.toHaveTextContent(reportId);
    expect(reportLinks[0]).toHaveAttribute('href', `/manager/reports/${reportId}`);
    expect(within(reportLinks[0].closest('tr')!).getByRole('link', { name: 'Open Workflow' })).toHaveAttribute('href', '/manager/ai-approvals/citizen-report');
    expect(screen.getByText('Awaiting Report Verification')).toBeInTheDocument();
    await userEvent.setup().selectOptions(screen.getByLabelText('Workflow status filter'), 'attention');
    expect(screen.getByText('Awaiting Report Verification')).toBeInTheDocument();
    expect(screen.getByText('Awaiting Collection Approval')).toBeInTheDocument();
    expect(screen.getByText('Awaiting Dispatch Approval')).toBeInTheDocument();
    expect(screen.queryByText('Planning')).not.toBeInTheDocument();
    expect(screen.queryByText('Workflow Completed')).toBeInTheDocument(); // Summary remains visible.
    expect(screen.getAllByText('Operational Planning')).toHaveLength(2);
  });

  it('keeps a long objective available as a tooltip while the table action stays compact', async () => {
    const objective = 'Analyze the submitted waste report and continue end-to-end collection planning using current authoritative data after staff verification.';
    (agentWorkflowApi.listWorkflows as ReturnType<typeof vi.fn>).mockResolvedValue(pageResult([{ ...workflows[0], objective }]));
    renderDashboard();
    const objectiveText = await screen.findByTitle(objective);
    expect(objectiveText).toHaveTextContent(objective);
    expect(within(objectiveText.closest('tr')!).getByRole('link', { name: 'Open Workflow' })).toHaveTextContent('Open');
  });

  it('renders loading followed by an empty state', async () => {
    let resolveList: (result: PagedAgentWorkflows) => void = () => undefined;
    (agentWorkflowApi.listWorkflows as ReturnType<typeof vi.fn>).mockReturnValueOnce(new Promise<PagedAgentWorkflows>((resolve) => { resolveList = resolve; }));
    renderDashboard();
    expect(screen.getByText('Loading workflows…')).toBeInTheDocument();
    resolveList(pageResult([]));
    expect(await screen.findByText('No AI workflows yet.')).toBeInTheDocument();
  });

  it('renders a list-load failure', async () => {
    (agentWorkflowApi.listWorkflows as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('offline'));
    renderDashboard();
    expect(await screen.findByText('Unable to load AI workflows.')).toBeInTheDocument();
  });

  it('filters the current server page locally', async () => {
    (agentWorkflowApi.listWorkflows as ReturnType<typeof vi.fn>).mockResolvedValueOnce(pageResult());
    renderDashboard();
    await screen.findByText('Prepare urgent collection coverage.');
    await userEvent.setup().selectOptions(screen.getByLabelText('Workflow status filter'), 'completed');
    expect(screen.getByText('Complete completed operation.')).toBeInTheDocument();
    expect(screen.queryByText('Prepare urgent collection coverage.')).not.toBeInTheDocument();
  });

  it('requests the next server page when pagination advances', async () => {
    const secondPageWorkflow = { ...workflows[0], id: 'page-two', objective: 'Second page collection operation.' };
    (agentWorkflowApi.listWorkflows as ReturnType<typeof vi.fn>).mockImplementation(({ page }: { page: number }) => Promise.resolve({
      items: page === 2 ? [secondPageWorkflow] : workflows,
      page,
      pageSize: 20,
      totalCount: 21,
      totalPages: 2,
    }));
    const user = userEvent.setup();
    renderDashboard();
    expect(await screen.findByText('Prepare urgent collection coverage.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Next' }));

    await waitFor(() => expect(agentWorkflowApi.listWorkflows).toHaveBeenLastCalledWith({ page: 2, pageSize: 20 }));
    expect(await screen.findByText('Second page collection operation.')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
  });

  it('validates objective and starts a created workflow through the role-correct route', async () => {
    const user = userEvent.setup();
    const created = { ...workflows[0], id: 'created-workflow', status: 'Created' as const, currentStep: 'None' as const, version: 1 };
    const started = { ...created, status: 'AwaitingCollectionApproval' as const, currentStep: 'CollectionPlanning' as const, version: 2 };
    (agentWorkflowApi.createWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(created);
    (agentWorkflowApi.startWorkflow as ReturnType<typeof vi.fn>).mockResolvedValue(started);
    renderDashboard('MunicipalManager');
    await screen.findByText('Prepare urgent collection coverage.');
    await user.click(screen.getByRole('button', { name: 'New End-to-End Collection Operation' }));
    const input = screen.getByLabelText('Objective');
    await user.clear(input);
    await user.type(input, 'bad');
    await user.click(screen.getByRole('button', { name: 'Start Operation' }));
    expect(screen.getByRole('alert')).toHaveTextContent('at least 5 characters');
    await user.clear(input);
    await user.type(input, 'Prepare approved municipal collection coverage.');
    await user.click(screen.getByRole('button', { name: 'Start Operation' }));
    await waitFor(() => expect(agentWorkflowApi.createWorkflow).toHaveBeenCalledWith({ objective: 'Prepare approved municipal collection coverage.' }));
    expect(agentWorkflowApi.startWorkflow).toHaveBeenCalledWith('created-workflow');
    expect(await screen.findByText('Workflow destination')).toBeInTheDocument();
  });

  it('does not start on create failure and preserves a created workflow when start fails', async () => {
    const user = userEvent.setup();
    (agentWorkflowApi.createWorkflow as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('create failed'));
    renderDashboard();
    await screen.findByText('Prepare urgent collection coverage.');
    await user.click(screen.getByRole('button', { name: 'New End-to-End Collection Operation' }));
    await user.click(screen.getByRole('button', { name: 'Start Operation' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to start the collection operation');
    expect(agentWorkflowApi.startWorkflow).not.toHaveBeenCalled();

    const created = { ...workflows[0], id: 'preserved-workflow', status: 'Created' as const, currentStep: 'None' as const, version: 1 };
    (agentWorkflowApi.createWorkflow as ReturnType<typeof vi.fn>).mockResolvedValueOnce(created);
    (agentWorkflowApi.startWorkflow as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('start failed'));
    await user.click(screen.getByRole('button', { name: 'Start Operation' }));
    expect(await screen.findByRole('link', { name: 'Open the created workflow.' })).toHaveAttribute('href', '/officer/ai-approvals/preserved-workflow');
    expect(agentWorkflowApi.createWorkflow).toHaveBeenCalledTimes(2);
    expect(agentWorkflowApi.startWorkflow).toHaveBeenCalledTimes(1);
  });
});
