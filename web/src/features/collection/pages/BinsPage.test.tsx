import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { BinsPage } from './BinsPage';
import { binsApi } from '../api/binsApi';

vi.mock('../api/binsApi', () => ({ binsApi: { getBins: vi.fn() } }));

const renderPage = () => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter><BinsPage /></MemoryRouter></QueryClientProvider>);

describe('BinsPage', () => {
  it('renders actual bin summary fields and an honest absent-observation state', async () => {
    (binsApi.getBins as ReturnType<typeof vi.fn>).mockResolvedValue({ items: [{ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: 'Main Street, Pettah', capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General', 'Recyclable'], collectionWeekdays: [1, 4], latestFillLevelPercent: null, latestCondition: null, latestObservationAt: null, hasActiveTask: false, lastCollectedAt: null, createdAt: '2026-09-01T10:00:00Z' }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 });
    renderPage();
    expect(await screen.findByText('BIN-COL-0042')).toBeInTheDocument();
    expect(screen.getByText('Main Street, Pettah')).toBeInTheDocument();
    expect(screen.getByText('No observation')).toBeInTheDocument();
    expect(screen.queryByText(/Latest: No observation recorded/)).not.toBeInTheDocument();
  });

  it('shows retryable failure state', async () => {
    (binsApi.getBins as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Backend unavailable'));
    renderPage();
    expect(await screen.findByText('Failed to load bins')).toBeInTheDocument();
    expect(screen.getByText('Backend unavailable')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
});
