import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DispatchRoutesPage } from './DispatchRoutesPage';

const hooks = vi.hoisted(() => ({
  useAvailableAssignmentTasks: vi.fn(),
  useAssignments: vi.fn(),
  useAssignmentDetail: vi.fn(),
}));

vi.mock('../hooks/useAvailableAssignmentTasks', () => ({ useAvailableAssignmentTasks: hooks.useAvailableAssignmentTasks }));
vi.mock('../hooks/useAssignments', () => ({
  useAssignments: hooks.useAssignments,
  useAssignmentDetail: hooks.useAssignmentDetail,
  useReorderRouteStops: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCancelAssignment: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock('../components/CollectionStopsMap', () => ({
  CollectionStopsMap: () => <div data-testid="stop-map">Mock Map</div>,
  isValidCollectionStopLocation: () => true,
}));

describe('DispatchRoutesPage', () => {
  beforeEach(() => {
    hooks.useAvailableAssignmentTasks.mockReturnValue({
      isLoading: false, isError: false, isFetching: false, refetch: vi.fn(),
      data: { items: [{ id: 'task-1', taskCode: 'TSK-20260925-0001', targetType: 'Report', wasteReportId: 'report-1', wasteBinId: null, collectionReason: 'VerifiedReport', status: 'Scheduled', scheduledAt: '2026-09-26T05:30:00Z', addressText: 'Main Street, Pettah', latitude: 6.93, longitude: 79.85 }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
    });
    hooks.useAssignments.mockReturnValue({
      isLoading: false, isError: false, isFetching: false, refetch: vi.fn(),
      data: { items: [{ id: 'assignment-1', assignmentNumber: 1, assignmentReference: 'Assignment 001', status: 'Assigned', driverId: 'driver-1', driverName: 'Kasun Fernando', vehicleId: 'vehicle-1', vehicleRegistrationNumber: 'WP-C34-1001', stopCount: 2, completedStopCount: 0, failedStopCount: 0, assignedAt: '2026-09-25T05:30:00Z' }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
    });
    hooks.useAssignmentDetail.mockReturnValue({
      isLoading: false, isError: false, isFetching: false, refetch: vi.fn(),
      data: {
        id: 'assignment-1', assignmentNumber: 1, assignmentReference: 'Assignment 001', status: 'Assigned', driverId: 'driver-1', driverName: 'Kasun Fernando', vehicleId: 'vehicle-1', vehicleRegistrationNumber: 'WP-C34-1001', stopCount: 2, completedStopCount: 0, failedStopCount: 0, assignedAt: '2026-09-25T05:30:00Z',
        route: { id: 'route-1', assignmentId: 'assignment-1', routingMethod: 'ManualOrdered', stops: [] },
        history: [],
      },
    });
  });

  it('renders available task data with an API-safe page size and switches to assignment summaries', async () => {
    const user = userEvent.setup();
    render(<DispatchRoutesPage />);

    expect(hooks.useAvailableAssignmentTasks).toHaveBeenCalledWith({ page: 1, pageSize: 20 });
    expect(screen.getByText('TSK-20260925-0001')).toBeInTheDocument();
    expect(screen.getByText('Waste report')).toBeInTheDocument();
    expect(screen.getByText('Main Street, Pettah')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Assignments' }));

    expect(screen.getByText('Assignment 001')).toBeInTheDocument();
    expect(screen.queryByText('assignment-1')).not.toBeInTheDocument();
    expect(screen.getByText('Kasun Fernando')).toBeInTheDocument();
    expect(screen.getAllByText('Assigned').length).toBeGreaterThanOrEqual(1);
  });

  it('renders helpful empty content and allows the officer to begin a manual dispatch', () => {
    hooks.useAvailableAssignmentTasks.mockReturnValue({ isLoading: false, isError: false, isFetching: false, refetch: vi.fn(), data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } });

    render(<DispatchRoutesPage />);

    expect(screen.getByText('No tasks are awaiting dispatch')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create assignment/i })).toBeInTheDocument();
  });

  it('opens assignment details from the assignments list and shows officer actions', async () => {
    const user = userEvent.setup();
    render(<DispatchRoutesPage />);

    await user.click(screen.getByRole('button', { name: 'Assignments' }));
    await user.click(screen.getByRole('button', { name: 'View details' }));

    expect(screen.getByTestId('assignment-detail-panel')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reorder route/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel assignment/i })).toBeInTheDocument();
  });
});
