import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { BinDetailPage } from './BinDetailPage';
import { binsApi } from '../api/binsApi';
import { useAuthStore } from '../../../store/authStore';

vi.mock('../api/binsApi', () => ({ binsApi: { getBin: vi.fn(), getObservations: vi.fn(), recordObservation: vi.fn(), deactivateBin: vi.fn() } }));
vi.mock('../components/BinLocationMap', () => ({
  isValidBinLocation: (latitude: unknown, longitude: unknown) => typeof latitude === 'number' && Number.isFinite(latitude) && latitude >= -90 && latitude <= 90 && typeof longitude === 'number' && Number.isFinite(longitude) && longitude >= -180 && longitude <= 180,
  BinLocationMap: ({ latitude, longitude }: { latitude?: number; longitude?: number }) => <div data-testid="bin-location-map" data-latitude={latitude} data-longitude={longitude}>Read-only location map</div>,
}));

const renderDetail = (client = new QueryClient({ defaultOptions: { queries: { retry: false } } })) => render(<QueryClientProvider client={client}><MemoryRouter initialEntries={['/officer/bins/bin-1']}><Routes><Route path="/officer/bins/:id" element={<BinDetailPage />} /></Routes></MemoryRouter></QueryClientProvider>);

describe('BinDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({ user: null, isAuthenticated: false, isLoading: false, accessToken: null });
    (binsApi.getObservations as ReturnType<typeof vi.fn>).mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
  });
  it('shows a loading state while retrieving the detail DTO', () => {
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {}));
    renderDetail();
    expect(screen.getByTestId('bin-detail-loading')).toBeInTheDocument();
  });

  it('displays authoritative detail fields and links back to the bin list', async () => {
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: 'Main Street, Pettah', capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General', 'Recyclable'], collectionWeekdays: [1, 4], lastCollectedAt: null, latestObservation: { id: 'obs-1', fillLevelPercent: 75, condition: 'Good', notes: 'Routine morning check', recordedByUserId: 'officer-1', recordedByUserName: 'Officer Silva', recordedAt: '2026-09-21T08:30:00Z' }, hasActiveTask: false, activeTaskId: null, createdAt: '2026-09-01T10:00:00Z', updatedAt: null });
    renderDetail();
    expect(await screen.findByText('BIN-COL-0042')).toBeInTheDocument();
    expect(screen.getByText('Main Street, Pettah')).toBeInTheDocument();
    expect(screen.getByText('Monday, Thursday')).toBeInTheDocument();
    expect(screen.getByText('Routine morning check')).toBeInTheDocument();
    expect(screen.getByTestId('bin-location-map')).toHaveAttribute('data-latitude', '6.9271');
    expect(screen.getByTestId('bin-location-map')).toHaveAttribute('data-longitude', '79.8612');
    expect(screen.getByTestId('back-to-bins-link')).toHaveAttribute('href', '/officer/bins');
  });

  it('shows a not-found state for a 404 response', async () => {
    const error = Object.assign(new Error('Not found'), { isAxiosError: true, response: { status: 404, data: { detail: 'Bin not found.' } } });
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockRejectedValue(error);
    renderDetail();
    expect(await screen.findByText('Bin not found')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });

  it('links WasteOfficer to the dedicated observation page without rendering the old inline form', async () => {
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: 'Main Street', capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [], lastCollectedAt: null, latestObservation: null, hasActiveTask: false, activeTaskId: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null });
    renderDetail();
    expect(await screen.findByRole('link', { name: 'Record Observation' })).toHaveAttribute('href', '/officer/bins/bin-1/observations/new');
    expect(screen.queryByTestId('record-observation-panel')).not.toBeInTheDocument();
    expect(screen.getByTestId('observation-history-empty')).toBeInTheDocument();
  });

  it('does not show recording controls to MunicipalManager', async () => {
    useAuthStore.setState({ user: { id: 'manager-1', fullName: 'Manager', email: 'manager@example.com', role: 'MunicipalManager' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: null, capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [], lastCollectedAt: null, latestObservation: null, hasActiveTask: false, activeTaskId: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null });
    renderDetail();
    await screen.findByText('BIN-COL-0042');
    expect(screen.queryByRole('button', { name: 'Record Observation' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Deactivate Bin' })).not.toBeInTheDocument();
  });

  it('shows a confirmation flow only to WasteOfficer for an active bin and submits the contracted request', async () => {
    const user = (await import('@testing-library/user-event')).default.setup();
    const activeBin = { id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: 'Main Street', capacityLiters: 660, administrativeStatus: 'Active' as const, acceptedWasteTypes: ['General'], collectionWeekdays: [], lastCollectedAt: null, latestObservation: null, hasActiveTask: false, activeTaskId: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null };
    const inactiveBin = { ...activeBin, administrativeStatus: 'OutOfService' as const };
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValueOnce(activeBin).mockResolvedValue(inactiveBin);
    (binsApi.deactivateBin as ReturnType<typeof vi.fn>).mockResolvedValue(inactiveBin);
    renderDetail(client);
    await user.click(await screen.findByRole('button', { name: 'Deactivate Bin' }));
    expect(screen.getByRole('dialog', { name: /Deactivate bin BIN-COL-0042/ })).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Deactivation status'), 'OutOfService');
    await user.type(screen.getByLabelText(/Reason/), 'Depot repair');
    await user.click(screen.getByRole('button', { name: 'Confirm Deactivation' }));
    expect(await screen.findByText('Out of service')).toBeInTheDocument();
    expect(binsApi.deactivateBin).toHaveBeenCalledWith('bin-1', { targetStatus: 'OutOfService', reason: 'Depot repair' });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['bins'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['bins', 'bin-1'] });
    expect(screen.queryByRole('button', { name: 'Deactivate Bin' })).not.toBeInTheDocument();
  });

  it('cancels deactivation without calling the API and has no action for inactive bins', async () => {
    const user = (await import('@testing-library/user-event')).default.setup();
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: null, capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [], lastCollectedAt: null, latestObservation: null, hasActiveTask: false, activeTaskId: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null });
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Deactivate Bin' }));
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(binsApi.deactivateBin).not.toHaveBeenCalled();
  });

  it('keeps the active status and shows the backend active-task conflict', async () => {
    const user = (await import('@testing-library/user-event')).default.setup();
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: null, capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [], lastCollectedAt: null, latestObservation: null, hasActiveTask: true, activeTaskId: 'task-1', createdAt: '2026-09-01T00:00:00Z', updatedAt: null });
    (binsApi.deactivateBin as ReturnType<typeof vi.fn>).mockRejectedValue(Object.assign(new Error('Conflict'), { isAxiosError: true, response: { status: 409, data: { detail: 'Cannot deactivate a waste bin with an active collection task.' } } }));
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Deactivate Bin' }));
    await user.selectOptions(screen.getByLabelText('Deactivation status'), 'Retired');
    await user.click(screen.getByRole('button', { name: 'Confirm Deactivation' }));
    expect(await screen.findByText('Cannot deactivate a waste bin with an active collection task.')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Deactivate Bin' })).toBeInTheDocument();
  });

  it('prevents duplicate deactivation submissions while the request is pending', async () => {
    const user = (await import('@testing-library/user-event')).default.setup();
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: null, capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [], lastCollectedAt: null, latestObservation: null, hasActiveTask: false, activeTaskId: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null });
    (binsApi.deactivateBin as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {}));
    renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Deactivate Bin' }));
    await user.selectOptions(screen.getByLabelText('Deactivation status'), 'OutOfService');
    await user.click(screen.getByRole('button', { name: 'Confirm Deactivation' }));
    expect(screen.getByTestId('confirm-deactivate-bin-button')).toBeDisabled();
    expect(binsApi.deactivateBin).toHaveBeenCalledTimes(1);
  });
});
