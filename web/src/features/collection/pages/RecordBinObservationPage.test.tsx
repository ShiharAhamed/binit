import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { RecordBinObservationPage } from './RecordBinObservationPage';
import { RoleRoute } from '../../../routes/RoleRoute';
import { binsApi } from '../api/binsApi';
import { useAuthStore } from '../../../store/authStore';

vi.mock('../api/binsApi', () => ({ binsApi: { getBin: vi.fn(), recordObservation: vi.fn() } }));

const bin = { id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: 'Main Street, Pettah', capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [1], lastCollectedAt: null, latestObservation: null, hasActiveTask: false, activeTaskId: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null };

const renderPage = (client = new QueryClient({ defaultOptions: { queries: { retry: false } } })) => render(<QueryClientProvider client={client}><MemoryRouter initialEntries={['/officer/bins/bin-1/observations/new']}><Routes><Route path="/officer/bins/:id/observations/new" element={<RecordBinObservationPage />} /><Route path="/officer/bins/:id" element={<div>Bin detail refreshed</div>} /></Routes></MemoryRouter></QueryClientProvider>);

const completeForm = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getByLabelText('75%'));
  await user.click(screen.getByLabelText('Good'));
};

describe('RecordBinObservationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue(bin);
    useAuthStore.setState({ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
  });

  it('shows the selected bin and the contracted observation choices', async () => {
    renderPage();
    await screen.findByTestId('record-observation-page');
    expect(screen.getAllByText('BIN-COL-0042')).toHaveLength(2);
    for (const label of ['0%', '25%', '50%', '75%', '100%', 'Good', 'Damaged', 'Blocked', 'Missing']) expect(screen.getByLabelText(label)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save Observation' })).toBeInTheDocument();
  });

  it('saves through the existing API, invalidates bin queries, and returns to detail', async () => {
    const user = userEvent.setup();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    (binsApi.recordObservation as ReturnType<typeof vi.fn>).mockResolvedValue({ id: 'obs-1' });
    renderPage(client);
    await screen.findByTestId('record-observation-page');
    await completeForm(user);
    await user.click(screen.getByRole('button', { name: 'Save Observation' }));
    expect(await screen.findByText('Bin detail refreshed')).toBeInTheDocument();
    expect(binsApi.recordObservation).toHaveBeenCalledWith('bin-1', { fillLevelPercent: 75, condition: 'Good', notes: null });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['bins'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['bins', 'bin-1'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['bin-observations', 'bin-1'] });
  });

  it('cancels without submitting', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByTestId('record-observation-page');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(await screen.findByText('Bin detail refreshed')).toBeInTheDocument();
    expect(binsApi.recordObservation).not.toHaveBeenCalled();
  });

  it('keeps the page open and reports an API failure', async () => {
    const user = userEvent.setup();
    (binsApi.recordObservation as ReturnType<typeof vi.fn>).mockRejectedValue(Object.assign(new Error('Conflict'), { isAxiosError: true, response: { status: 409 } }));
    renderPage();
    await screen.findByTestId('record-observation-page');
    await completeForm(user);
    await user.click(screen.getByRole('button', { name: 'Save Observation' }));
    expect(await screen.findByText(/cannot be recorded for the bin in its current state/)).toBeInTheDocument();
    expect(screen.queryByText('Bin detail refreshed')).not.toBeInTheDocument();
  });

  it('prevents duplicate submission while saving', async () => {
    const user = userEvent.setup();
    (binsApi.recordObservation as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {}));
    renderPage();
    await screen.findByTestId('record-observation-page');
    await completeForm(user);
    const save = screen.getByRole('button', { name: 'Save Observation' });
    await user.click(save);
    expect(save).toBeDisabled();
    expect(binsApi.recordObservation).toHaveBeenCalledTimes(1);
  });

  it('is blocked for MunicipalManager by the existing officer role guard', () => {
    useAuthStore.setState({ user: { id: 'manager-1', fullName: 'Manager', email: 'manager@example.com', role: 'MunicipalManager' }, isAuthenticated: true, isLoading: false, accessToken: 'test' });
    render(<QueryClientProvider client={new QueryClient()}><MemoryRouter initialEntries={['/officer/bins/bin-1/observations/new']}><Routes><Route element={<RoleRoute allowedRoles={['WasteOfficer']} />}><Route path="/officer/bins/:id/observations/new" element={<RecordBinObservationPage />} /></Route><Route path="/unauthorized" element={<div>Unauthorized</div>} /></Routes></MemoryRouter></QueryClientProvider>);
    expect(screen.getByText('Unauthorized')).toBeInTheDocument();
  });
});
