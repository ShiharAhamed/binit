import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { CollectionNeedsPage } from './CollectionNeedsPage';
import { collectionNeedsApi } from '../api/collectionNeedsApi';
import { collectionTasksApi } from '../api/collectionTasksApi';
import { useAuthStore } from '../../../store/authStore';

vi.mock('../api/collectionNeedsApi', () => ({ collectionNeedsApi: { getCollectionNeeds: vi.fn() } }));
vi.mock('../api/collectionTasksApi', () => ({ collectionTasksApi: { createManualTask: vi.fn() } }));
const renderPage = () => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter><CollectionNeedsPage /></MemoryRouter></QueryClientProvider>);
const response = { items: [
  { id: 'report-1', targetType: 'Report' as const, collectionReason: 'VerifiedReport' as const, title: 'Verified Report: Pettah', latitude: 6.93, longitude: 79.85, addressText: 'Pettah Market', wasteTypes: ['General'], urgency: 'High', triggerDate: '2026-09-20T10:00:00Z', attachmentCount: 2, binDetails: null },
  { id: 'bin-1', targetType: 'Bin' as const, collectionReason: 'FullOrBlockedBin' as const, title: 'BIN-COL-0042 (100% Full)', latitude: 6.92, longitude: 79.86, addressText: 'Main Street', wasteTypes: ['General', 'Recyclable'], urgency: 'Urgent', triggerDate: '2026-09-21T10:00:00Z', attachmentCount: 0, binDetails: { binCode: 'BIN-COL-0042', capacityLiters: 660, latestFillLevelPercent: 100, latestCondition: 'Blocked' as const, observationAgeHours: 1.2 } },
], page: 1, pageSize: 20, totalCount: 22, totalPages: 2 };

describe('CollectionNeedsPage', () => {
  beforeEach(() => { vi.clearAllMocks(); useAuthStore.setState({ user: { id: 'manager-1', fullName: 'Manager', email: 'manager@example.com', role: 'MunicipalManager' }, isAuthenticated: true, isLoading: false, accessToken: 'test' }); });
  it('renders distinct report and bin needs with existing detail navigation', async () => {
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue(response);
    renderPage();
    expect(await screen.findByText('Verified Report: Pettah')).toBeInTheDocument();
    expect(screen.getByText('BIN-COL-0042')).toBeInTheDocument();
    expect(screen.getByTestId('need-detail-report-1')).toHaveAttribute('href', '/officer/waste-reports/report-1');
    expect(screen.getByTestId('need-detail-bin-1')).toHaveAttribute('href', '/officer/bins/bin-1');
  });
  it('passes filters and pagination to the server query', async () => {
    const user = userEvent.setup();
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue(response);
    renderPage();
    await screen.findByText('BIN-COL-0042');
    await user.selectOptions(screen.getByLabelText('Target type'), 'Bin');
    await waitFor(() => expect(collectionNeedsApi.getCollectionNeeds).toHaveBeenLastCalledWith(expect.objectContaining({ targetType: 'Bin', page: 1, pageSize: 20 })));
    await user.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => expect(collectionNeedsApi.getCollectionNeeds).toHaveBeenLastCalledWith(expect.objectContaining({ targetType: 'Bin', page: 2, pageSize: 20 })));
  });
  it('renders empty, filtered-empty, loading, and error states', async () => {
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
    renderPage();
    expect(await screen.findByText('No collection needs at this time.')).toBeInTheDocument();
  });
  it('shows a filtered empty state after applying a server-side filter', async () => {
    const user = userEvent.setup();
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
    renderPage();
    await screen.findByText('No collection needs at this time.');
    await user.selectOptions(screen.getByLabelText('Target type'), 'Bin');
    expect(await screen.findByText('No collection needs match the current filters.')).toBeInTheDocument();
  });
  it('shows a loading state while the collection needs request is pending', () => {
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {}));
    renderPage();
    expect(screen.getByTestId('collection-needs-loading')).toBeInTheDocument();
  });
  it('renders a retryable API error', async () => {
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Backend unavailable'));
    renderPage();
    expect(await screen.findByText('Failed to load collection needs')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
  it('shows schedule controls only to WasteOfficer and submits a selected bin target in UTC', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue(response);
    (collectionTasksApi.createManualTask as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'task-1', taskCode: 'TSK-20990101-0001' });
    renderPage();
    await user.click(await screen.findByTestId('schedule-need-bin-1'));
    await user.type(screen.getByLabelText(/Scheduled collection time/), '2099-01-01T10:00');
    await user.click(screen.getByRole('button', { name: 'Schedule Task' }));
    await waitFor(() => expect(collectionTasksApi.createManualTask).toHaveBeenCalledWith(expect.objectContaining({ wasteReportId: null, wasteBinId: 'bin-1', collectionReason: 'FullOrBlockedBin', scheduledAt: '2099-01-01T04:30:00.000Z' })));
    expect(await screen.findByText(/TSK-20990101-0001 was scheduled successfully/)).toBeInTheDocument();
    await waitFor(() => expect(collectionNeedsApi.getCollectionNeeds).toHaveBeenCalledTimes(2));
  });
  it('keeps MunicipalManager queue access read-only', async () => {
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue(response);
    renderPage();
    await screen.findByText('BIN-COL-0042');
    expect(screen.queryByRole('button', { name: 'Schedule Collection' })).not.toBeInTheDocument();
  });
  it('shows safe validation and conflict messages without closing the form', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue(response);
    (collectionTasksApi.createManualTask as ReturnType<typeof vi.fn>).mockRejectedValue({ isAxiosError: true, response: { status: 409, data: { detail: 'An active collection task already exists for this target.' } } });
    renderPage();
    await user.click(await screen.findByTestId('schedule-need-bin-1'));
    await user.type(screen.getByLabelText(/Scheduled collection time/), '2099-01-01T10:00');
    await user.click(screen.getByRole('button', { name: 'Schedule Task' }));
    expect(await screen.findByText('An active collection task already exists for this target.')).toBeInTheDocument();
    expect(screen.getByTestId('manual-task-form')).toBeInTheDocument();
  });
  it('shows a backend validation message without discarding the selected target', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue(response);
    (collectionTasksApi.createManualTask as ReturnType<typeof vi.fn>).mockRejectedValue({ isAxiosError: true, response: { status: 400, data: { detail: 'Scheduled time cannot be in the past.' } } });
    renderPage();
    await user.click(await screen.findByTestId('schedule-need-bin-1'));
    await user.type(screen.getByLabelText(/Scheduled collection time/), '2099-01-01T10:00');
    await user.click(screen.getByRole('button', { name: 'Schedule Task' }));
    expect(await screen.findByText('Scheduled time cannot be in the past.')).toBeInTheDocument();
    expect(screen.getAllByText('BIN-COL-0042')).toHaveLength(2);
  });
  it('opens the new task detail route after successful manual scheduling', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (collectionNeedsApi.getCollectionNeeds as ReturnType<typeof vi.fn>).mockResolvedValue(response);
    (collectionTasksApi.createManualTask as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'task-99', taskCode: 'TSK-99' });
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={['/officer/schedules']}><Routes><Route path="/officer/schedules" element={<CollectionNeedsPage />} /><Route path="/officer/tasks/:id" element={<p>Task detail opened</p>} /></Routes></MemoryRouter></QueryClientProvider>);
    await user.click(await screen.findByTestId('schedule-need-bin-1'));
    await user.type(screen.getByLabelText(/Scheduled collection time/), '2099-01-01T10:00');
    await user.click(screen.getByRole('button', { name: 'Schedule Task' }));
    expect(await screen.findByText('Task detail opened')).toBeInTheDocument();
  });
});
