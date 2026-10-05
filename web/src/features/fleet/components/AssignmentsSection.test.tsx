import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AssignmentsSection } from './AssignmentsSection';
import type { AssignmentSummaryDto } from '../types/assignments';

const hooks = vi.hoisted(() => ({
  useAssignments: vi.fn(),
  useAssignmentDetail: vi.fn(),
}));

vi.mock('../hooks/useAssignments', () => ({
  useAssignments: hooks.useAssignments,
  useAssignmentDetail: hooks.useAssignmentDetail,
  useReorderRouteStops: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCancelAssignment: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock('./CollectionStopsMap', () => ({
  CollectionStopsMap: () => <div data-testid="stop-map">Mock Map</div>,
  isValidCollectionStopLocation: () => true,
}));

const mockAssignmentsList: AssignmentSummaryDto[] = [
  {
    id: 'assignment-1',
    assignmentNumber: 1,
    assignmentReference: 'Assignment 001',
    status: 'Assigned',
    driverId: 'driver-1',
    driverName: 'Kasun Fernando',
    vehicleId: 'vehicle-1',
    vehicleRegistrationNumber: 'WP-C34-1001',
    stopCount: 3,
    completedStopCount: 0,
    failedStopCount: 0,
    assignedAt: '2026-09-25T08:00:00Z',
  },
  {
    id: 'assignment-2',
    assignmentNumber: 2,
    assignmentReference: 'Assignment 002',
    status: 'Completed',
    driverId: 'driver-2',
    driverName: 'Nimal Perera',
    vehicleId: 'vehicle-2',
    vehicleRegistrationNumber: 'WP-C34-1002',
    stopCount: 5,
    completedStopCount: 5,
    failedStopCount: 0,
    assignedAt: '2026-09-24T08:00:00Z',
  },
];

describe('AssignmentsSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    hooks.useAssignments.mockReturnValue({
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
      data: {
        items: mockAssignmentsList,
        page: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
      },
    });
  });

  it('renders title matching the role (WasteOfficer vs MunicipalManager)', () => {
    const { rerender } = render(<AssignmentsSection role="WasteOfficer" />);
    expect(screen.getByText('Collection assignments')).toBeInTheDocument();

    rerender(<AssignmentsSection role="MunicipalManager" />);
    expect(screen.getByText('Fleet Assignments & Routes')).toBeInTheDocument();
  });

  it('filters assignments by status and requests with page reset', async () => {
    const user = userEvent.setup();
    render(<AssignmentsSection role="WasteOfficer" />);

    const select = screen.getByRole('combobox', { name: /filter assignment status/i });
    await user.selectOptions(select, 'InProgress');

    expect(hooks.useAssignments).toHaveBeenCalledWith({
      status: 'InProgress',
      page: 1,
      pageSize: 20,
    });
  });

  it('switches to AssignmentDetailPanel when View details is clicked', async () => {
    const user = userEvent.setup();
    hooks.useAssignmentDetail.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        id: 'assignment-1',
        assignmentNumber: 1,
        assignmentReference: 'Assignment 001',
        driverId: 'driver-1',
        driverName: 'Kasun Fernando',
        vehicleId: 'vehicle-1',
        vehicleRegistrationNumber: 'WP-C34-1001',
        status: 'Assigned',
        assignedAt: '2026-09-25T08:00:00Z',
        stopCount: 3,
        completedStopCount: 0,
        failedStopCount: 0,
        route: {
          id: 'route-1',
          collectionAssignmentId: 'assignment-1',
          routingMethod: 'ManualOrdered',
          routeGeometry: null,
          estimatedDistanceMeters: null,
          estimatedDurationSeconds: null,
          stops: [],
        },
        history: [],
      },
      refetch: vi.fn(),
    });

    render(<AssignmentsSection role="WasteOfficer" />);

    expect(screen.getByText('WP-C34-1001')).toBeInTheDocument();
    expect(screen.getByText('Assignment 001')).toBeInTheDocument();
    expect(screen.queryByText('assignment-1')).not.toBeInTheDocument();

    const viewDetailsButtons = screen.getAllByRole('button', { name: /view details/i });
    await user.click(viewDetailsButtons[0]);

    // Detail panel should be visible
    expect(screen.getByTestId('assignment-detail-panel')).toBeInTheDocument();
    expect(hooks.useAssignmentDetail).toHaveBeenCalledWith('assignment-1');
    expect(screen.getByRole('button', { name: /back to assignments/i })).toBeInTheDocument();

    // Clicking back returns to the table
    await user.click(screen.getByRole('button', { name: /back to assignments/i }));
    expect(screen.getByTestId('assignments-section')).toBeInTheDocument();
  });

  it('renders created assignment banner when createdAssignmentId is passed', () => {
    render(<AssignmentsSection role="WasteOfficer" createdAssignmentId="assignment-new-99" createdAssignmentReference="Assignment 099" />);

    expect(screen.getByText(/assignment created/i)).toBeInTheDocument();
    expect(screen.getByText(/assignment 099 is now listed/i)).toBeInTheDocument();
    expect(screen.queryByText('assignment-new-99')).not.toBeInTheDocument();
  });
});
