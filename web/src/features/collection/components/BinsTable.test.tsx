import { beforeEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { BinsTable } from './BinsTable';
import { useAuthStore } from '../../../store/authStore';
import type { WasteBinSummaryDto } from '../types/bins';

const bin889: WasteBinSummaryDto = {
  id: 'bin-889', binCode: 'BIN-889', latitude: 6.90688, longitude: 79.89413,
  addressText: 'Rajagiriya', capacityLiters: 50, administrativeStatus: 'Active',
  acceptedWasteTypes: ['General', 'Recyclable'], collectionWeekdays: [],
  latestFillLevelPercent: null, latestCondition: null, latestObservationAt: null,
  hasActiveTask: false, lastCollectedAt: null, createdAt: '2026-09-01T00:00:00Z',
};

describe('BinsTable navigation', () => {
  beforeEach(() => useAuthStore.setState({ user: { id: 'officer', fullName: 'Officer', email: 'officer@example.com', role: 'WasteOfficer' }, isAuthenticated: true, isLoading: false, accessToken: 'test' }));
  it('links each bin code to its officer detail route', () => {
    render(<MemoryRouter><BinsTable isLoading={false} isFiltered={false} onClearFilters={() => {}} bins={[{ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: null, capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [], latestFillLevelPercent: null, latestCondition: null, latestObservationAt: null, hasActiveTask: false, lastCollectedAt: null, createdAt: '2026-09-01T00:00:00Z' }]} /></MemoryRouter>);
    expect(screen.getByTestId('bin-details-bin-1')).toHaveAttribute('href', '/officer/bins/bin-1');
    expect(screen.getByTestId('manage-bin-bin-1')).toHaveTextContent('Manage Bin');
  });
  it('keeps the list row compact without capacity, coordinates, or duplicate empty observation text', () => {
    render(<MemoryRouter><BinsTable isLoading={false} isFiltered={false} onClearFilters={() => {}} bins={[bin889]} /></MemoryRouter>);
    const row = within(screen.getByTestId('bin-row-bin-889'));
    expect(row.getByText('BIN-889')).toBeInTheDocument();
    expect(row.queryByText('50 L')).not.toBeInTheDocument();
    expect(row.getByText('Rajagiriya')).toBeInTheDocument();
    expect(row.queryByText(/6\.90688|79\.89413/)).not.toBeInTheDocument();
    expect(row.getByText('No observation')).toBeInTheDocument();
    expect(row.queryByText(/Latest: No observation recorded/)).not.toBeInTheDocument();
    expect(row.getByText('Active')).toBeInTheDocument();
    expect(row.getByText('General')).toBeInTheDocument();
    expect(row.getByText('Recyclable')).toBeInTheDocument();
    expect(row.getByText('No active task')).toBeInTheDocument();
    expect(row.getByText('Manage Bin')).toHaveAttribute('href', '/officer/bins/bin-889');
    expect(row.getByText('Manage Bin')).toHaveClass('whitespace-nowrap');
  });
  it('uses an address-only fallback and retains concise real observation and task details', () => {
    const observedBin: WasteBinSummaryDto = {
      ...bin889, addressText: ' ', acceptedWasteTypes: ['General', 'Organic', 'Recyclable', 'Hazardous', 'Bulky', 'Other'],
      latestFillLevelPercent: 75, latestCondition: 'Good', latestObservationAt: '2026-10-04T05:00:00Z', hasActiveTask: true,
    };
    render(<MemoryRouter><BinsTable isLoading={false} isFiltered={false} onClearFilters={() => {}} bins={[observedBin]} /></MemoryRouter>);
    const row = within(screen.getByTestId('bin-row-bin-889'));
    expect(row.getByText('No address')).toBeInTheDocument();
    expect(row.queryByText(/6\.90688|79\.89413/)).not.toBeInTheDocument();
    expect(row.getByText(/75%/)).toBeInTheDocument();
    expect(row.getByText(/Good/)).toBeInTheDocument();
    expect(row.getByText(/4 Oct 2026/)).toBeInTheDocument();
    expect(row.getByText('Active task')).toBeInTheDocument();
    for (const type of observedBin.acceptedWasteTypes) expect(row.getByText(type)).toBeInTheDocument();
  });
  it('uses the read-only label for MunicipalManager', () => { useAuthStore.setState({ user: { id: 'manager', fullName: 'Manager', email: 'manager@example.com', role: 'MunicipalManager' }, isAuthenticated: true, isLoading: false, accessToken: 'test' }); render(<MemoryRouter><BinsTable isLoading={false} isFiltered={false} onClearFilters={() => {}} bins={[{ id: 'bin-1', binCode: 'BIN-COL-0042', latitude: 6.9271, longitude: 79.8612, addressText: null, capacityLiters: 660, administrativeStatus: 'Active', acceptedWasteTypes: ['General'], collectionWeekdays: [], latestFillLevelPercent: null, latestCondition: null, latestObservationAt: null, hasActiveTask: false, lastCollectedAt: null, createdAt: '2026-09-01T00:00:00Z' }]} /></MemoryRouter>); expect(screen.getByTestId('manage-bin-bin-1')).toHaveTextContent('View Details'); expect(screen.getByTestId('manage-bin-bin-1')).toHaveAttribute('href', '/officer/bins/bin-1'); });
});
