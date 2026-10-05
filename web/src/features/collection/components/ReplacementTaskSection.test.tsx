import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ReplacementTaskSection } from './ReplacementTaskSection';
import type {
  CollectionTaskDetailDto,
  CollectionTaskStatusHistoryDto,
} from '../types/collectionTasks';

const mutateAsyncMock = vi.fn();
vi.mock('../hooks/useCreateReplacementTask', () => ({
  useCreateReplacementTask: () => ({
    mutateAsync: mutateAsyncMock,
    isPending: false,
  }),
}));

const mockFailedTask: CollectionTaskDetailDto = {
  id: 'task-failed-1',
  taskCode: 'TSK-20260925-0010',
  targetType: 'Report',
  wasteReportId: 'report-1',
  wasteBinId: null,
  collectionReason: 'VerifiedReport',
  status: 'Failed',
  scheduledAt: '2026-09-25T05:00:00Z',
  handlingNotes: 'Original gate key required',
  schedulingReason: 'Urgent cleanup',
  createdByUserId: 'user-1',
  createdByUserName: 'Officer Silva',
  creationMethod: 'Manual',
  createdAt: '2026-09-24T05:00:00Z',
  updatedAt: '2026-09-25T06:30:00Z',
  targetSummary: {
    identifier: 'RPT-20260924-0001',
    latitude: 6.93,
    longitude: 79.85,
    addressText: '12 Main Street, Pettah',
    capacityLiters: null,
    wasteTypes: ['General'],
    latestFillLevelPercent: null,
  },
};

const mockStatusHistory: CollectionTaskStatusHistoryDto[] = [
  {
    id: 'hist-1',
    fromStatus: null,
    toStatus: 'Scheduled',
    changedByUserId: 'user-1',
    changedByUserName: 'Officer Silva',
    notes: 'Task created.',
    changedAt: '2026-09-24T05:00:00Z',
  },
  {
    id: 'hist-2',
    fromStatus: 'Scheduled',
    toStatus: 'Assigned',
    changedByUserId: 'user-1',
    changedByUserName: 'Officer Silva',
    notes: 'Assigned to Kasun.',
    changedAt: '2026-09-25T04:00:00Z',
  },
  {
    id: 'hist-3',
    fromStatus: 'InProgress',
    toStatus: 'Failed',
    changedByUserId: 'driver-1',
    changedByUserName: 'Driver Kasun',
    notes: 'Flood waters blocked entrance road completely.',
    changedAt: '2026-09-25T06:30:00Z',
  },
];

describe('ReplacementTaskSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('displays recorded failure reason and timestamps from status history', () => {
    render(
      <MemoryRouter>
        <ReplacementTaskSection
          task={mockFailedTask}
          statusHistory={mockStatusHistory}
          userRole="WasteOfficer"
        />
      </MemoryRouter>
    );

    expect(screen.getByText('Failed Task Review & Replacement')).toBeInTheDocument();
    expect(
      screen.getByText('Flood waters blocked entrance road completely.')
    ).toBeInTheDocument();
    expect(screen.getByText(/by Driver Kasun/)).toBeInTheDocument();
    expect(
      screen.getByText(/the original failed task and its history will remain permanently recorded/i)
    ).toBeInTheDocument();
  });

  it('renders Schedule Replacement button only for WasteOfficer', () => {
    const { rerender } = render(
      <MemoryRouter>
        <ReplacementTaskSection
          task={mockFailedTask}
          statusHistory={mockStatusHistory}
          userRole="WasteOfficer"
        />
      </MemoryRouter>
    );

    expect(screen.getByRole('button', { name: /schedule replacement/i })).toBeInTheDocument();

    rerender(
      <MemoryRouter>
        <ReplacementTaskSection
          task={mockFailedTask}
          statusHistory={mockStatusHistory}
          userRole="MunicipalManager"
        />
      </MemoryRouter>
    );

    expect(screen.queryByRole('button', { name: /schedule replacement/i })).not.toBeInTheDocument();
    expect(
      screen.getByText(/only waste officers are authorized to schedule replacements/i)
    ).toBeInTheDocument();
  });

  it('opens form, submits replacement, and displays confirmed new Scheduled task code', async () => {
    const user = userEvent.setup();
    const createdNewTask: CollectionTaskDetailDto = {
      ...mockFailedTask,
      id: 'task-new-99',
      taskCode: 'TSK-20260925-0099',
      status: 'Scheduled',
      scheduledAt: '2099-06-01T10:00:00Z',
    };
    mutateAsyncMock.mockResolvedValueOnce(createdNewTask);

    render(
      <MemoryRouter>
        <ReplacementTaskSection
          task={mockFailedTask}
          statusHistory={mockStatusHistory}
          userRole="WasteOfficer"
        />
      </MemoryRouter>
    );

    // Open form
    await user.click(screen.getByRole('button', { name: /schedule replacement/i }));
    expect(screen.getByLabelText(/new scheduled collection time/i)).toBeInTheDocument();

    // Fill form
    await user.type(screen.getByLabelText(/new scheduled collection time/i), '2099-06-01T15:30');
    await user.type(screen.getByLabelText(/replacement reason/i), 'Water level receded, safe for truck.');

    // Submit
    await user.click(screen.getByRole('button', { name: /schedule replacement task/i }));

    expect(mutateAsyncMock).toHaveBeenCalledWith(
      expect.objectContaining({
        replacementReason: 'Water level receded, safe for truck.',
      })
    );

    // Verify success banner and new task code
    expect(await screen.findByText('Replacement task scheduled successfully')).toBeInTheDocument();
    expect(screen.getByText('TSK-20260925-0099')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /view replacement task/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /back to collection tasks/i })).toBeInTheDocument();
  });

  it('displays readable backend conflict error without clearing form on failure', async () => {
    const user = userEvent.setup();
    mutateAsyncMock.mockRejectedValueOnce({
      isAxiosError: true,
      response: {
        status: 409,
        data: { detail: 'The failed task still has an active assignment claim.' },
      },
    });

    render(
      <MemoryRouter>
        <ReplacementTaskSection
          task={mockFailedTask}
          statusHistory={mockStatusHistory}
          userRole="WasteOfficer"
        />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /schedule replacement/i }));
    await user.type(screen.getByLabelText(/new scheduled collection time/i), '2099-06-01T15:30');
    await user.type(screen.getByLabelText(/replacement reason/i), 'Water level receded, safe for truck.');

    await user.click(screen.getByRole('button', { name: /schedule replacement task/i }));

    expect(
      await screen.findByText('The failed task still has an active assignment claim.')
    ).toBeInTheDocument();

    // Form inputs remain preserved!
    expect(screen.getByDisplayValue('Water level receded, safe for truck.')).toBeInTheDocument();
  });
});
