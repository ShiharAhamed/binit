import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ManagerFleetRoutesPage } from './ManagerFleetRoutesPage';

const hooks = vi.hoisted(() => ({
  useVehicles: vi.fn(),
  useVehicle: vi.fn(),
  useAssignments: vi.fn(),
}));

vi.mock('../hooks/useVehicles', () => ({
  useVehicles: hooks.useVehicles,
  useVehicle: hooks.useVehicle,
  useVehicleMutations: () => ({
    create: { isPending: false, mutateAsync: vi.fn() },
    update: { isPending: false, mutateAsync: vi.fn() },
    status: { mutate: vi.fn() },
    error: () => 'Vehicle request failed.',
  }),
}));

vi.mock('../hooks/useAssignments', () => ({
  useAssignments: hooks.useAssignments,
  useAssignmentDetail: vi.fn(),
  useReorderRouteStops: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCancelAssignment: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

describe('ManagerFleetRoutesPage', () => {
  beforeEach(() => {
    hooks.useVehicles.mockReturnValue({
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
      data: {
        items: [{ id: 'vehicle-1', registrationNumber: 'WP-C34-1001', vehicleType: 'Compactor', operationalStatus: 'Available', supportedWasteTypes: ['General'], isOccupied: true }],
        page: 1, pageSize: 20, totalCount: 1, totalPages: 1,
      },
    });
    hooks.useVehicle.mockReturnValue({
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
      data: { id: 'vehicle-1', registrationNumber: 'WP-C34-1001', vehicleType: 'Compactor', capacityLiters: 8500, operationalStatus: 'Available', supportedWasteTypes: ['General'], isOccupied: true, notes: null, currentAssignmentId: 'assignment-1', createdAt: '2026-01-01T00:00:00Z', updatedAt: null },
    });
    hooks.useAssignments.mockReturnValue({
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
      data: {
        items: [{ id: 'assignment-1', status: 'Assigned', driverId: 'driver-1', driverName: 'Kasun Fernando', vehicleId: 'vehicle-1', vehicleRegistrationNumber: 'WP-C34-1001', stopCount: 2, completedStopCount: 0, failedStopCount: 0, assignedAt: '2026-09-25T05:30:00Z' }],
        page: 1, pageSize: 20, totalCount: 1, totalPages: 1,
      },
    });
  });

  it('displays operational status separately from assignment occupancy and fetches detail on demand', async () => {
    const user = userEvent.setup();
    render(<ManagerFleetRoutesPage />);

    expect(screen.getAllByText('Available').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('Occupied')).toBeInTheDocument();
    expect(screen.getByText(/manage municipal vehicles and review collection driver readiness/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'View details' }));
    expect(screen.getByText(/8,500/)).toBeInTheDocument();
    expect(screen.getByText('Assignment occupancy')).toBeInTheDocument();
    expect(screen.getAllByText('Occupied').length).toBeGreaterThanOrEqual(2);
  });

  it('navigates to Assignments & Routes and displays municipal assignment oversight', async () => {
    const user = userEvent.setup();
    render(<ManagerFleetRoutesPage />);

    const assignmentsTab = screen.getByRole('button', { name: 'Assignments & Routes' });
    expect(assignmentsTab).toBeInTheDocument();

    await user.click(assignmentsTab);

    expect(screen.getByText('Fleet Assignments & Routes')).toBeInTheDocument();
    expect(screen.getByText('WP-C34-1001')).toBeInTheDocument();
    expect(screen.getByText('Kasun Fernando')).toBeInTheDocument();
  });
});
