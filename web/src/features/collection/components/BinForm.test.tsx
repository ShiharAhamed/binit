import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BinForm } from './BinForm';

vi.mock('./BinLocationMap', () => ({
  isValidBinLocation: (latitude: unknown, longitude: unknown) => typeof latitude === 'number' && Number.isFinite(latitude) && latitude >= -90 && latitude <= 90 && typeof longitude === 'number' && Number.isFinite(longitude) && longitude >= -180 && longitude <= 180,
  BinLocationPicker: ({ onSelect }: { onSelect: (location: { latitude: number; longitude: number }) => void }) => <button type="button" onClick={() => onSelect({ latitude: 6.9271, longitude: 79.8612 })}>Select map location</button>,
}));

const initialValues = { binCode: '', latitude: undefined, longitude: undefined, addressText: '', capacityLiters: 0, acceptedWasteTypes: [], collectionWeekdays: [] };

describe('BinForm', () => {
  it('shows contracted client validation before submission', async () => {
    const user = userEvent.setup();
    render(<BinForm mode="create" initialValues={initialValues} isSubmitting={false} onCancel={() => {}} onSubmit={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Register Bin' }));
    expect(await screen.findByText('Bin code is required.')).toBeInTheDocument();
    expect(screen.getByText('Select at least one accepted waste type.')).toBeInTheDocument();
  });

  it('requires a location selected from the map before registration', async () => {
    const user = userEvent.setup();
    render(<BinForm mode="create" initialValues={initialValues} isSubmitting={false} onCancel={() => {}} onSubmit={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Register Bin' }));
    expect(await screen.findByText('Select a valid location on the map.')).toBeInTheDocument();
  });
});
