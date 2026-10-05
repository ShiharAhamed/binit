import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ReplacementTaskForm } from './ReplacementTaskForm';
import type { CollectionTaskDetailDto } from '../types/collectionTasks';

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

describe('ReplacementTaskForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders with reminders, initial original notes, and required fields', () => {
    render(
      <ReplacementTaskForm
        failedTask={mockFailedTask}
        isSubmitting={false}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByText(/submitting this form schedules a/i)).toBeInTheDocument();
    expect(screen.getByText(/TSK-20260925-0010/)).toBeInTheDocument();
    expect(screen.getByLabelText(/new scheduled collection time/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/replacement reason/i)).toBeInTheDocument();
    expect(screen.getByDisplayValue('Original gate key required')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Urgent cleanup')).toBeInTheDocument();
  });

  it('validates scheduled date must be provided and in the future', async () => {
    const user = userEvent.setup();
    const onSubmitMock = vi.fn();

    render(
      <ReplacementTaskForm
        failedTask={mockFailedTask}
        isSubmitting={false}
        onSubmit={onSubmitMock}
        onCancel={vi.fn()}
      />
    );

    const submitBtn = screen.getByRole('button', { name: /schedule replacement task/i });

    // Submit with empty date
    await user.click(submitBtn);
    expect(screen.getByText('Enter a scheduled collection date and time.')).toBeInTheDocument();

    // Type a past date
    const dateInput = screen.getByLabelText(/new scheduled collection time/i);
    await user.type(dateInput, '2020-01-01T10:00');
    await user.click(submitBtn);
    expect(screen.getByText('Scheduled time must be in the future.')).toBeInTheDocument();

    expect(onSubmitMock).not.toHaveBeenCalled();
  });

  it('enforces 5-character minimum for replacement justification reason', async () => {
    const user = userEvent.setup();
    const onSubmitMock = vi.fn();

    render(
      <ReplacementTaskForm
        failedTask={mockFailedTask}
        isSubmitting={false}
        onSubmit={onSubmitMock}
        onCancel={vi.fn()}
      />
    );

    const dateInput = screen.getByLabelText(/new scheduled collection time/i);
    await user.type(dateInput, '2099-01-01T10:00');

    const reasonInput = screen.getByLabelText(/replacement reason/i);
    await user.type(reasonInput, 'bad');

    const submitBtn = screen.getByRole('button', { name: /schedule replacement task/i });
    await user.click(submitBtn);

    expect(screen.getByText('Replacement reason must be between 5 and 500 characters.')).toBeInTheDocument();
    expect(onSubmitMock).not.toHaveBeenCalled();
  });

  it('submits valid replacement request converting local Colombo time to UTC ISO', async () => {
    const user = userEvent.setup();
    const onSubmitMock = vi.fn();

    render(
      <ReplacementTaskForm
        failedTask={mockFailedTask}
        isSubmitting={false}
        onSubmit={onSubmitMock}
        onCancel={vi.fn()}
      />
    );

    const dateInput = screen.getByLabelText(/new scheduled collection time/i);
    await user.type(dateInput, '2099-06-15T14:30');

    const reasonInput = screen.getByLabelText(/replacement reason/i);
    await user.type(reasonInput, 'Road access was cleared by municipal police.');

    const submitBtn = screen.getByRole('button', { name: /schedule replacement task/i });
    await user.click(submitBtn);

    expect(onSubmitMock).toHaveBeenCalledWith(
      expect.objectContaining({
        scheduledAt: expect.stringMatching(/^2099-06-15T/),
        replacementReason: 'Road access was cleared by municipal police.',
        handlingNotes: 'Original gate key required',
        schedulingReason: 'Urgent cleanup',
      })
    );
  });

  it('displays apiError when provided and preserves input values', () => {
    render(
      <ReplacementTaskForm
        failedTask={mockFailedTask}
        isSubmitting={false}
        apiError="The failed task still has an active assignment claim."
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByText('The failed task still has an active assignment claim.')).toBeInTheDocument();
  });

  it('calls onCancel when Cancel is clicked', async () => {
    const user = userEvent.setup();
    const onCancelMock = vi.fn();

    render(
      <ReplacementTaskForm
        failedTask={mockFailedTask}
        isSubmitting={false}
        onSubmit={vi.fn()}
        onCancel={onCancelMock}
      />
    );

    await user.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancelMock).toHaveBeenCalled();
  });
});
