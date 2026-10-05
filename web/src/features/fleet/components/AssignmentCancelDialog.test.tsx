import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AssignmentCancelDialog } from './AssignmentCancelDialog';
import type { AssignmentDetailDto } from '../types/assignments';

const mutateAsyncMock = vi.fn();
vi.mock('../hooks/useAssignments', () => ({
  useCancelAssignment: () => ({
    mutateAsync: mutateAsyncMock,
    isPending: false,
  }),
}));

const mockAssignment: AssignmentDetailDto = {
  id: 'assignment-1',
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
    stops: [],
  },
  history: [],
};

describe('AssignmentCancelDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders nothing when isOpen is false', () => {
    const { container } = render(
      <AssignmentCancelDialog
        assignment={mockAssignment}
        isOpen={false}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );
    expect(container.firstChild).toBeNull();
  });

  it('shows non-eligible alert when assignment is not in Assigned status', () => {
    const inProgressAssignment = { ...mockAssignment, status: 'InProgress' as const };
    render(
      <AssignmentCancelDialog
        assignment={inProgressAssignment}
        isOpen={true}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.getByText(/assignment cannot be cancelled/i)).toBeInTheDocument();
    expect(screen.getByText('InProgress')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /confirm cancellation/i })).not.toBeInTheDocument();
  });

  it('enforces 5-character minimum cancellation justification', async () => {
    const user = userEvent.setup();
    render(
      <AssignmentCancelDialog
        assignment={mockAssignment}
        isOpen={true}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const textarea = screen.getByRole('textbox', { name: /cancellation reason/i });
    const confirmButton = screen.getByRole('button', { name: /confirm cancellation/i });

    // Empty click
    await user.click(confirmButton);
    expect(screen.getByText(/cancellation reason is required/i)).toBeInTheDocument();

    // Less than 5 characters
    await user.type(textarea, 'bad');
    await user.click(confirmButton);
    expect(screen.getByText(/cancellation reason must be at least 5 characters/i)).toBeInTheDocument();

    expect(mutateAsyncMock).not.toHaveBeenCalled();
  });

  it('submits cancellation when valid reason is provided', async () => {
    const user = userEvent.setup();
    const onSuccessMock = vi.fn();
    const onCloseMock = vi.fn();
    const updatedAssignment = { ...mockAssignment, status: 'Cancelled' as const };
    mutateAsyncMock.mockResolvedValueOnce(updatedAssignment);

    render(
      <AssignmentCancelDialog
        assignment={mockAssignment}
        isOpen={true}
        onClose={onCloseMock}
        onSuccess={onSuccessMock}
      />
    );

    const textarea = screen.getByRole('textbox', { name: /cancellation reason/i });
    await user.type(textarea, 'Driver reported severe illness before departure.');

    const confirmButton = screen.getByRole('button', { name: /confirm cancellation/i });
    await user.click(confirmButton);

    expect(mutateAsyncMock).toHaveBeenCalledWith({
      id: 'assignment-1',
      request: { reason: 'Driver reported severe illness before departure.' },
    });
    expect(onSuccessMock).toHaveBeenCalledWith(updatedAssignment);
    expect(onCloseMock).toHaveBeenCalled();
  });
});
