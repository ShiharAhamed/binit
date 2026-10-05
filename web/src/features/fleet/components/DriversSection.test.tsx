import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DriversSection } from './DriversSection';

const hooks = vi.hoisted(() => ({ useDrivers: vi.fn(), useDriver: vi.fn() }));
vi.mock('../hooks/useDrivers', () => hooks);

describe('DriversSection', () => {
  beforeEach(() => {
    hooks.useDrivers.mockReturnValue({ isLoading: false, isError: false, isFetching: false, refetch: vi.fn(), data: { items: [{ id: 'driver-user-1', displayName: 'Kasun Fernando', availabilityStatus: 'OffDuty', isOccupied: true }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 } });
    hooks.useDriver.mockReturnValue({ isLoading: false, isError: false, refetch: vi.fn(), data: { id: 'driver-user-1', displayName: 'Kasun Fernando', availabilityStatus: 'OffDuty', isOccupied: true } });
  });

  it('shows Driver-controlled availability and derived occupancy without profile administration', async () => {
    const user = userEvent.setup();
    render(<DriversSection />);
    expect(screen.getAllByText('OffDuty').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Occupied')).toBeInTheDocument();
    expect(screen.queryByText(/eligib/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/licen[cs]e/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/add profile/i)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'View details' }));
    expect(screen.getAllByText('Duty availability').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('Assignment occupancy')).toBeInTheDocument();
  });
});
