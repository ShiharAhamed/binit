import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteStopsEditor } from './RouteStopsEditor';
import type { RouteStopReadDto } from '../types/assignments';

const mutateAsyncMock = vi.fn();
vi.mock('../hooks/useAssignments', () => ({
  useReorderRouteStops: () => ({
    mutateAsync: mutateAsyncMock,
    isPending: false,
  }),
}));

const mockStops: RouteStopReadDto[] = [
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
      latitude: 6.91,
      longitude: 79.86,
    },
  },
];

describe('RouteStopsEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders stops in order, with Up disabled for the first and Down disabled for the last', () => {
    render(
      <RouteStopsEditor
        assignmentId="assignment-1"
        initialStops={mockStops}
        onSaved={vi.fn()}
        onDiscard={vi.fn()}
      />
    );

    expect(screen.getByText('TSK-20260925-0001')).toBeInTheDocument();
    expect(screen.getByText('TSK-20260925-0002')).toBeInTheDocument();

    const upButtons = screen.getAllByRole('button', { name: /up/i });
    const downButtons = screen.getAllByRole('button', { name: /down/i });

    expect(upButtons[0]).toBeDisabled();
    expect(downButtons[0]).toBeEnabled();

    expect(upButtons[1]).toBeEnabled();
    expect(downButtons[1]).toBeDisabled();
  });

  it('moves stop down and notifies onOrderPreviewChange with contiguous sequence', async () => {
    const user = userEvent.setup();
    const previewChangeMock = vi.fn();

    render(
      <RouteStopsEditor
        assignmentId="assignment-1"
        initialStops={mockStops}
        onSaved={vi.fn()}
        onDiscard={vi.fn()}
        onOrderPreviewChange={previewChangeMock}
      />
    );

    const downButtons = screen.getAllByRole('button', { name: /down/i });
    await user.click(downButtons[0]);

    expect(previewChangeMock).toHaveBeenCalledWith([
      expect.objectContaining({ id: 'stop-2', sequence: 1 }),
      expect.objectContaining({ id: 'stop-1', sequence: 2 }),
    ]);
  });

  it('saves the reordered route and invokes onSaved', async () => {
    const user = userEvent.setup();
    const onSavedMock = vi.fn();
    mutateAsyncMock.mockResolvedValueOnce({ id: 'route-1', stops: [] });

    render(
      <RouteStopsEditor
        assignmentId="assignment-1"
        initialStops={mockStops}
        onSaved={onSavedMock}
        onDiscard={vi.fn()}
      />
    );

    // Reorder stop 1 down
    const downButtons = screen.getAllByRole('button', { name: /down/i });
    await user.click(downButtons[0]);

    // Save
    await user.click(screen.getByRole('button', { name: /save route order/i }));

    expect(mutateAsyncMock).toHaveBeenCalledWith({
      id: 'assignment-1',
      request: {
        stops: [
          { routeStopId: 'stop-2', sequence: 1 },
          { routeStopId: 'stop-1', sequence: 2 },
        ],
      },
    });
    expect(onSavedMock).toHaveBeenCalled();
  });

  it('calls onDiscard when discard button is clicked', async () => {
    const user = userEvent.setup();
    const onDiscardMock = vi.fn();

    render(
      <RouteStopsEditor
        assignmentId="assignment-1"
        initialStops={mockStops}
        onSaved={vi.fn()}
        onDiscard={onDiscardMock}
      />
    );

    await user.click(screen.getByRole('button', { name: /discard changes/i }));
    expect(onDiscardMock).toHaveBeenCalled();
  });
});
