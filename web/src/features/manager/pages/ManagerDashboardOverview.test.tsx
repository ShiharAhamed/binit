import { beforeEach, describe, expect, it, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ManagerDashboardPage } from './ManagerDashboardPage';
import { managerDashboardApi } from '../api/dashboardApi';
import { useAuthStore } from '../../../store/authStore';
import type { WasteOfficerNeedsAttentionItem } from '../../officer/types/dashboard';

vi.mock('../api/dashboardApi', () => ({ managerDashboardApi: { getOverview: vi.fn(), getNeedsAttention: vi.fn().mockResolvedValue([]) } }));

const renderDashboard = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter><ManagerDashboardPage /></MemoryRouter>
    </QueryClientProvider>
  );
};

const expectCardValue = (label: string, value: string) => {
  const cardText = screen.getByText(label).parentElement;
  expect(cardText).not.toBeNull();
  expect(within(cardText!).getByText(value)).toBeInTheDocument();
};

describe('Municipal Manager management overview', () => {
  const overview = {
    aiWorkflowsAwaitingApproval: 6, activeCollectionAssignments: 4, availableVehicles: 8,
    openOperationalIncidents: 2, unresolvedComplaints: 5,
  };
  const queue: WasteOfficerNeedsAttentionItem[] = Array.from({ length: 5 }, (_, index) => ({
    id: `12345678-0000-0000-0000-00000000000${index}`,
    itemType: index % 2 === 0 ? 'WasteReport' : 'Complaint',
    reference: index % 2 === 0 ? `Report 1234567${index}` : `Complaint 1234567${index}`,
    createdAt: `2026-10-05T09:0${index}:00Z`,
    secondaryLabel: index % 2 === 0 ? 'General' : 'Missed collection',
    submittedByName: `Citizen ${index}`,
    addressText: index === 1 ? null : `Location ${index}`,
  }));
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'manager-token',
      user: { id: 'manager-1', fullName: 'Municipal Manager', email: 'manager@example.test', role: 'MunicipalManager' },
    });
  });

  it('maps one overview response to the five correct cards', async () => {
    vi.mocked(managerDashboardApi.getOverview).mockResolvedValue({
      aiWorkflowsAwaitingApproval: 6, activeCollectionAssignments: 4, availableVehicles: 8,
      openOperationalIncidents: 2, unresolvedComplaints: 5,
    });
    renderDashboard();
    await waitFor(() => expectCardValue('AI Workflows Awaiting Approval', '6'));
    expectCardValue('Active Collection Assignments', '4');
    expectCardValue('Available Vehicles', '8');
    expectCardValue('Open Operational Incidents', '2');
    expectCardValue('Unresolved Complaints', '5');
    expect(managerDashboardApi.getOverview).toHaveBeenCalledTimes(1);
  });

  it('shows authoritative zeroes after a successful response', async () => {
    vi.mocked(managerDashboardApi.getOverview).mockResolvedValue({
      aiWorkflowsAwaitingApproval: 0, activeCollectionAssignments: 0, availableVehicles: 0,
      openOperationalIncidents: 0, unresolvedComplaints: 0,
    });
    renderDashboard();
    await waitFor(() => expectCardValue('AI Workflows Awaiting Approval', '0'));
    for (const label of ['Active Collection Assignments', 'Available Vehicles', 'Open Operational Incidents', 'Unresolved Complaints'])
      expectCardValue(label, '0');
    expect(screen.queryByText('—')).not.toBeInTheDocument();
  });

  it('keeps unknown counts as dashes during initial loading', () => {
    vi.mocked(managerDashboardApi.getOverview).mockReturnValue(new Promise(() => {}));
    renderDashboard();
    for (const label of ['AI Workflows Awaiting Approval', 'Active Collection Assignments', 'Available Vehicles', 'Open Operational Incidents', 'Unresolved Complaints'])
      expectCardValue(label, '—');
    expect(screen.getByText('Quick Actions')).toBeInTheDocument();
  });

  it('keeps actions usable and retries an error without fabricated counts', async () => {
    vi.mocked(managerDashboardApi.getOverview)
      .mockRejectedValueOnce(new Error('Unavailable'))
      .mockResolvedValueOnce({
        aiWorkflowsAwaitingApproval: 1, activeCollectionAssignments: 2, availableVehicles: 3,
        openOperationalIncidents: 4, unresolvedComplaints: 5,
      });
    renderDashboard();
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load management overview.');
    expectCardValue('AI Workflows Awaiting Approval', '—');
    expect(screen.getByRole('link', { name: /review ai approvals/i })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expectCardValue('AI Workflows Awaiting Approval', '1'));
    expect(managerDashboardApi.getOverview).toHaveBeenCalledTimes(2);
  });

  it('shows five enriched review items in server order with manager detail links', async () => {
    vi.mocked(managerDashboardApi.getOverview).mockResolvedValue(overview);
    vi.mocked(managerDashboardApi.getNeedsAttention).mockResolvedValue(queue);
    renderDashboard();
    const table = await screen.findByRole('table', { name: 'Items needing initial review' });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows).toHaveLength(5);
    expect(within(rows[0]).getByRole('link', { name: 'Report 12345670' }))
      .toHaveAttribute('href', `/manager/waste-reports/${queue[0].id}`);
    expect(within(rows[1]).getByRole('link', { name: 'Complaint 12345671' }))
      .toHaveAttribute('href', `/manager/complaints/${queue[1].id}`);
    expect(within(rows[0]).getByRole('cell', { name: 'Citizen 0' })).toBeInTheDocument();
    expect(within(rows[0]).getByText('Location 0')).toBeInTheDocument();
    expect(within(rows[1]).queryByText('Location 1')).not.toBeInTheDocument();
    expect(within(rows[3]).getByText('Location 3')).toBeInTheDocument();
    expect(screen.queryByText('All Review Queues Clear')).not.toBeInTheDocument();
  });

  it('does not show an all-clear state while the manager queue is loading', () => {
    vi.mocked(managerDashboardApi.getOverview).mockResolvedValue(overview);
    vi.mocked(managerDashboardApi.getNeedsAttention).mockReturnValue(new Promise(() => {}));
    renderDashboard();
    expect(screen.getByText('Loading items needing review…')).toBeInTheDocument();
    expect(screen.queryByText('All Review Queues Clear')).not.toBeInTheDocument();
  });

  it('keeps overview and actions available when the manager queue fails', async () => {
    vi.mocked(managerDashboardApi.getOverview).mockResolvedValue(overview);
    vi.mocked(managerDashboardApi.getNeedsAttention)
      .mockRejectedValueOnce(new Error('Unavailable'))
      .mockResolvedValueOnce(queue);
    renderDashboard();
    expect(await screen.findByText('Unable to load items needing attention.')).toBeInTheDocument();
    expectCardValue('AI Workflows Awaiting Approval', '6');
    expect(screen.getByRole('heading', { name: 'Quick Actions' })).toBeInTheDocument();
    expect(screen.queryByText('All Review Queues Clear')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByRole('table', { name: 'Items needing initial review' })).toBeInTheDocument();
  });
});
