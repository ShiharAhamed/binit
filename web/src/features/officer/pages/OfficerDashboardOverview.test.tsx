import { beforeEach, describe, expect, it, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { OfficerDashboardPage } from './OfficerDashboardPage';
import { dashboardApi } from '../api/dashboardApi';
import { useAuthStore } from '../../../store/authStore';

vi.mock('../api/dashboardApi', () => ({ dashboardApi: { getOverview: vi.fn(), getNeedsAttention: vi.fn().mockResolvedValue([]) } }));

const renderDashboard = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter><OfficerDashboardPage /></MemoryRouter>
    </QueryClientProvider>
  );
};

const expectCardValue = (label: string, value: string) => {
  const cardText = screen.getByText(label).parentElement;
  expect(cardText).not.toBeNull();
  expect(within(cardText!).getByText(value)).toBeInTheDocument();
};

describe('Waste Officer operational overview', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'officer-token',
      user: { id: 'officer-1', fullName: 'Waste Officer', email: 'officer@example.test', role: 'WasteOfficer' },
    });
  });

  it('maps one overview response to the five correct cards', async () => {
    vi.mocked(dashboardApi.getOverview).mockResolvedValue({
      reportsAwaitingReview: 4, activeBins: 12, scheduledCollections: 7,
      openCollectionTasks: 5, openComplaints: 3,
    });
    renderDashboard();
    await waitFor(() => expectCardValue('Reports Awaiting Review', '4'));
    expectCardValue('Active Bins', '12');
    expectCardValue('Scheduled Collections', '7');
    expectCardValue('Open Collection Tasks', '5');
    expectCardValue('Open Complaints', '3');
    expect(dashboardApi.getOverview).toHaveBeenCalledTimes(1);
  });

  it('shows authoritative zeroes after a successful response', async () => {
    vi.mocked(dashboardApi.getOverview).mockResolvedValue({
      reportsAwaitingReview: 0, activeBins: 0, scheduledCollections: 0,
      openCollectionTasks: 0, openComplaints: 0,
    });
    renderDashboard();
    await waitFor(() => expectCardValue('Reports Awaiting Review', '0'));
    for (const label of ['Active Bins', 'Scheduled Collections', 'Open Collection Tasks', 'Open Complaints'])
      expectCardValue(label, '0');
    expect(screen.queryByText('—')).not.toBeInTheDocument();
  });

  it('keeps unknown counts as dashes during initial loading', () => {
    vi.mocked(dashboardApi.getOverview).mockReturnValue(new Promise(() => {}));
    renderDashboard();
    for (const label of ['Reports Awaiting Review', 'Active Bins', 'Scheduled Collections', 'Open Collection Tasks', 'Open Complaints'])
      expectCardValue(label, '—');
    expect(screen.getByText('Quick Actions')).toBeInTheDocument();
  });

  it('keeps actions usable and retries a failed overview without fabricating zeroes', async () => {
    vi.mocked(dashboardApi.getOverview)
      .mockRejectedValueOnce(new Error('Unavailable'))
      .mockResolvedValueOnce({
        reportsAwaitingReview: 1, activeBins: 2, scheduledCollections: 3,
        openCollectionTasks: 4, openComplaints: 5,
      });
    renderDashboard();
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load operational overview.');
    expectCardValue('Reports Awaiting Review', '—');
    expect(screen.getByRole('link', { name: /review waste reports/i })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expectCardValue('Reports Awaiting Review', '1'));
    expect(dashboardApi.getOverview).toHaveBeenCalledTimes(2);
  });

  it('keeps overview and quick actions available when the review queue fails', async () => {
    vi.mocked(dashboardApi.getOverview).mockResolvedValue({
      reportsAwaitingReview: 2, activeBins: 3, scheduledCollections: 4,
      openCollectionTasks: 5, openComplaints: 1,
    });
    vi.mocked(dashboardApi.getNeedsAttention).mockRejectedValueOnce(new Error('Unavailable'));
    renderDashboard();
    expect(await screen.findByText('Unable to load items needing attention.')).toBeInTheDocument();
    expectCardValue('Reports Awaiting Review', '2');
    expect(screen.getByText('Quick Actions')).toBeInTheDocument();
    expect(screen.queryByText('All Operational Queues Clear')).not.toBeInTheDocument();
  });
});
