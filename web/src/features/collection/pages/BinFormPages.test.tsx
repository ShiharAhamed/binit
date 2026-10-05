import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { EditBinPage, RegisterBinPage } from './BinFormPages';
import { binsApi } from '../api/binsApi';

vi.mock('../api/binsApi', () => ({ binsApi: { createBin: vi.fn(), updateBin: vi.fn(), getBin: vi.fn() } }));
vi.mock('../components/BinLocationMap', () => ({
  isValidBinLocation: (latitude: unknown, longitude: unknown) => typeof latitude === 'number' && Number.isFinite(latitude) && latitude >= -90 && latitude <= 90 && typeof longitude === 'number' && Number.isFinite(longitude) && longitude >= -180 && longitude <= 180,
  BinLocationPicker: ({ latitude, longitude, onSelect }: { latitude?: number; longitude?: number; onSelect: (location: { latitude: number; longitude: number }) => void }) => <div data-testid="bin-location-picker" data-latitude={latitude} data-longitude={longitude}><button type="button" onClick={() => onSelect({ latitude: 6.9312, longitude: 79.8504 })}>Select map location</button></div>,
}));

const detail = { id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: 'Main Street, Pettah', capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [1, 4], lastCollectedAt: null, latestObservation: null, hasActiveTask: false, activeTaskId: null, createdAt: '2026-09-01T00:00:00Z', updatedAt: null };

const renderRoute = (path: string, element: React.ReactNode) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={[path]}><Routes><Route path="/officer/bins/register" element={<RegisterBinPage />} /><Route path="/officer/bins/:id/edit" element={<EditBinPage />} /><Route path="/officer/bins/:id" element={element} /></Routes></MemoryRouter></QueryClientProvider>);

const completeCreateForm = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(screen.getByLabelText(/Bin code/), 'BIN-COL-0043');
  await user.click(screen.getByRole('button', { name: 'Select map location' }));
  await user.clear(screen.getByLabelText('Capacity in liters *'));
  await user.type(screen.getByLabelText('Capacity in liters *'), '1100');
  await user.click(screen.getByLabelText('General'));
  await user.click(screen.getByLabelText('Mon'));
};

describe('Bin registration and editing pages', () => {
  beforeEach(() => vi.clearAllMocks());

  it('submits the create request and navigates using the returned bin id', async () => {
    const user = userEvent.setup();
    (binsApi.createBin as ReturnType<typeof vi.fn>).mockResolvedValue({ ...detail, id: 'created-bin' });
    renderRoute('/officer/bins/register', <div>Created bin details</div>);
    await completeCreateForm(user);
    await user.click(screen.getByRole('button', { name: 'Register Bin' }));
    expect(await screen.findByText('Created bin details')).toBeInTheDocument();
    expect(binsApi.createBin).toHaveBeenCalledWith({ binCode: 'BIN-COL-0043', latitude: 6.9312, longitude: 79.8504, addressText: null, capacityLiters: 1100, acceptedWasteTypes: ['General'], collectionWeekdays: [1] });
  });

  it('prepopulates edit fields and sends only the supported update payload', async () => {
    const user = userEvent.setup();
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue(detail);
    (binsApi.updateBin as ReturnType<typeof vi.fn>).mockResolvedValue(detail);
    renderRoute('/officer/bins/bin-1/edit', <div>Updated bin details</div>);
    expect(await screen.findByDisplayValue('BIN-COL-0042')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Main Street, Pettah')).toBeInTheDocument();
    expect(screen.getByTestId('bin-location-picker')).toHaveAttribute('data-latitude', '6.9271');
    expect(screen.getByTestId('bin-location-picker')).toHaveAttribute('data-longitude', '79.8612');
    await user.clear(screen.getByLabelText('Capacity in liters *'));
    await user.type(screen.getByLabelText('Capacity in liters *'), '1100');
    await user.click(screen.getByRole('button', { name: 'Save Changes' }));
    expect(await screen.findByText('Updated bin details')).toBeInTheDocument();
    expect(binsApi.updateBin).toHaveBeenCalledWith('bin-1', { latitude: 6.9271, longitude: 79.8612, addressText: 'Main Street, Pettah', capacityLiters: 1100, acceptedWasteTypes: ['General'], collectionWeekdays: [1, 4] });
  });

  it('uses a newly selected map location in the edit request', async () => {
    const user = userEvent.setup();
    (binsApi.getBin as ReturnType<typeof vi.fn>).mockResolvedValue(detail);
    (binsApi.updateBin as ReturnType<typeof vi.fn>).mockResolvedValue(detail);
    renderRoute('/officer/bins/bin-1/edit', <div>Updated bin details</div>);
    await screen.findByDisplayValue('BIN-COL-0042');
    await user.click(screen.getByRole('button', { name: 'Select map location' }));
    await user.click(screen.getByRole('button', { name: 'Save Changes' }));
    expect(binsApi.updateBin).toHaveBeenCalledWith('bin-1', expect.objectContaining({ latitude: 6.9312, longitude: 79.8504 }));
  });

  it('shows a duplicate bin-code error without discarding the form', async () => {
    const user = userEvent.setup();
    const error = Object.assign(new Error('Conflict'), { isAxiosError: true, response: { status: 409 } });
    (binsApi.createBin as ReturnType<typeof vi.fn>).mockRejectedValue(error);
    renderRoute('/officer/bins/register', <div />);
    await completeCreateForm(user);
    await user.click(screen.getByRole('button', { name: 'Register Bin' }));
    expect(await screen.findByText(/That bin code is already registered/)).toBeInTheDocument();
    expect(screen.getByDisplayValue('BIN-COL-0043')).toBeInTheDocument();
  });
});
