import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AssignmentDetailPanel } from './AssignmentDetailPanel';
import type { AssignmentDetailDto } from '../types/assignments';

const hooks = vi.hoisted(() => ({
  useAssignmentDetail: vi.fn(),
  useReorderRouteStops: vi.fn(),
  useCancelAssignment: vi.fn(),
}));

vi.mock('../hooks/useAssignments', () => ({
  useAssignmentDetail: hooks.useAssignmentDetail,
  useReorderRouteStops: hooks.useReorderRouteStops,
  useCancelAssignment: hooks.useCancelAssignment,
}));

vi.mock('./CollectionStopsMap', () => ({
  CollectionStopsMap: ({
    stops,
    selectedStopId,
  }: {
    stops: Array<{ id: string; sequence: number; latitude: number | null }>;
    selectedStopId: string | null;
  }) => (
    <div data-testid="stop-map" data-selected-stop={selectedStopId}>
      {stops.map((s) => (
        <span key={s.id}>{`${s.id}:${s.sequence}:${s.latitude ?? 'none'}`}</span>
      ))}
    </div>
  ),
  isValidCollectionStopLocation: (latitude: unknown, longitude: unknown) =>
    typeof latitude === 'number' &&
    Number.isFinite(latitude) &&
    latitude >= -90 &&
    latitude <= 90 &&
    typeof longitude === 'number' &&
    Number.isFinite(longitude) &&
    longitude >= -180 &&
    longitude <= 180,
}));

const mockDetail: AssignmentDetailDto = {
  id: 'assignment-1',
  assignmentNumber: 1,
  assignmentReference: 'Assignment 001',
  driverId: 'driver-1',
  driverName: 'Kasun Fernando',
  vehicleId: 'vehicle-1',
  vehicleRegistrationNumber: 'WP-C34-1001',
  status: 'Assigned',
  assignedAt: '2026-09-25T08:00:00Z',
  stopCount: 2,
  completedStopCount: 0,
  failedStopCount: 0,
  route: {
    id: 'route-1',
    collectionAssignmentId: 'assignment-1',
    routingMethod: 'ManualOrdered',
    routeGeometry: null,
    estimatedDistanceMeters: null,
    estimatedDurationSeconds: null,
    stops: [
      {
        id: 'stop-1',
        sequence: 1,
        status: 'Pending',
        failureReason: null,
        completedAt: null,
        failedAt: null,
        history: [],
        task: {
          id: 'task-1',
          taskCode: 'TSK-20260925-0001',
          targetType: 'Report',
          wasteReportId: 'rep-1',
          wasteBinId: null,
          collectionReason: 'VerifiedReport',
          status: 'Assigned',
          scheduledAt: '2026-09-25T08:00:00Z',
          addressText: '12 Main St, Colombo',
          latitude: 6.93,
          longitude: 79.85,
        },
      },
      {
        id: 'stop-2',
        sequence: 2,
        status: 'Pending',
        failureReason: null,
        completedAt: null,
        failedAt: null,
        history: [],
        task: {
          id: 'task-2',
          taskCode: 'TSK-20260925-0002',
          targetType: 'Bin',
          wasteReportId: null,
          wasteBinId: 'bin-1',
          collectionReason: 'FullOrBlockedBin',
          status: 'Assigned',
          scheduledAt: '2026-09-25T09:00:00Z',
          addressText: '45 Galle Rd, Colombo',
          latitude: null, // missing coordinate
          longitude: null,
        },
      },
    ],
  },
  history: [
    {
      id: 'history-1',
      fromStatus: null,
      toStatus: 'Assigned',
      changedAt: '2026-09-25T08:00:00Z',
      changedByUserId: 'user-1',
      notes: 'Initial dispatch by officer.',
    },
  ],
};

describe('AssignmentDetailPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    hooks.useReorderRouteStops.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useCancelAssignment.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
  });

  it('renders loading state when fetching assignment details', () => {
    hooks.useAssignmentDetail.mockReturnValue({
      isLoading: true,
      isError: false,
      data: null,
    });

    render(<AssignmentDetailPanel assignmentId="assignment-1" role="WasteOfficer" onClose={vi.fn()} />);
    expect(screen.getByText(/loading collection assignment details/i)).toBeInTheDocument();
  });

  it('renders error alert when loading fails', () => {
    hooks.useAssignmentDetail.mockReturnValue({
      isLoading: false,
      isError: true,
      error: new Error('Network timeout'),
      data: null,
      refetch: vi.fn(),
    });

    render(<AssignmentDetailPanel assignmentId="assignment-1" role="WasteOfficer" onClose={vi.fn()} />);
    expect(screen.getByText('Network timeout')).toBeInTheDocument();
  });

  it('displays full assignment details, metrics, stops, and audit timeline', () => {
    hooks.useAssignmentDetail.mockReturnValue({
      isLoading: false,
      isError: false,
      data: mockDetail,
      refetch: vi.fn(),
    });

    render(<AssignmentDetailPanel assignmentId="assignment-1" role="WasteOfficer" onClose={vi.fn()} />);

    expect(screen.getByText('Assignment 001')).toBeInTheDocument();
    expect(screen.queryByText('assignment-1')).not.toBeInTheDocument();
    expect(hooks.useAssignmentDetail).toHaveBeenCalledWith('assignment-1');
    expect(screen.getAllByText('Assigned').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Kasun Fernando')).toBeInTheDocument();
    expect(screen.getByText('WP-C34-1001')).toBeInTheDocument();
    expect(screen.getByText('TSK-20260925-0001')).toBeInTheDocument();
    expect(screen.getByText('TSK-20260925-0002')).toBeInTheDocument();

    // Map test id
    expect(screen.getByTestId('stop-map')).toBeInTheDocument();
    // Missing coordinates indicator
    expect(screen.getByText(/1 stop lacks coordinates/i)).toBeInTheDocument();
    expect(screen.getByText(/coordinates unavailable for map pin/i)).toBeInTheDocument();

    // History
    expect(screen.getByText(/transitioned to/i)).toBeInTheDocument();
    expect(screen.getByText('Initial dispatch by officer.')).toBeInTheDocument();
  });

  it('allows WasteOfficer to reorder and cancel unstarted assignments', async () => {
    const user = userEvent.setup();
    hooks.useAssignmentDetail.mockReturnValue({
      isLoading: false,
      isError: false,
      data: mockDetail,
      refetch: vi.fn(),
    });

    render(<AssignmentDetailPanel assignmentId="assignment-1" role="WasteOfficer" onClose={vi.fn()} />);

    const reorderButton = screen.getByRole('button', { name: /reorder route/i });
    const cancelButton = screen.getByRole('button', { name: /cancel assignment/i });

    expect(reorderButton).toBeInTheDocument();
    expect(cancelButton).toBeInTheDocument();

    // Clicking reorder opens the RouteStopsEditor
    await user.click(reorderButton);
    expect(screen.getByText(/adjust the planned stop sequence for this unstarted assignment/i)).toBeInTheDocument();
  });

  it('allows MunicipalManager to cancel unstarted assignments but NOT reorder them', () => {
    hooks.useAssignmentDetail.mockReturnValue({
      isLoading: false,
      isError: false,
      data: mockDetail,
      refetch: vi.fn(),
    });

    render(<AssignmentDetailPanel assignmentId="assignment-1" role="MunicipalManager" onClose={vi.fn()} />);

    expect(screen.queryByRole('button', { name: /reorder route/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel assignment/i })).toBeInTheDocument();
  });

  it('hides both Reorder and Cancel buttons when assignment is InProgress', () => {
    const inProgressDetail = { ...mockDetail, status: 'InProgress' as const };
    hooks.useAssignmentDetail.mockReturnValue({
      isLoading: false,
      isError: false,
      data: inProgressDetail,
      refetch: vi.fn(),
    });

    render(<AssignmentDetailPanel assignmentId="assignment-1" role="WasteOfficer" onClose={vi.fn()} />);

    expect(screen.queryByRole('button', { name: /reorder route/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel assignment/i })).not.toBeInTheDocument();
  });
});
