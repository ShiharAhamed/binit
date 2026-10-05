import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ObservationHistory } from './ObservationHistory';

const observations = [
  { id: 'obs-old', wasteBinId: 'bin-1', fillLevelPercent: 25 as const, condition: 'Good' as const, notes: null, recordedByUserId: 'u-1', recordedByUserName: 'Officer A', recordedAt: '2026-09-20T08:00:00Z' },
  { id: 'obs-new', wasteBinId: 'bin-1', fillLevelPercent: 75 as const, condition: 'Blocked' as const, notes: 'Access obstructed', recordedByUserId: 'u-2', recordedByUserName: 'Officer B', recordedAt: '2026-09-21T09:00:00Z' },
];

describe('ObservationHistory', () => {
  it('renders the most recent human-recorded observation first and paginates', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    render(<ObservationHistory observations={observations} isLoading={false} isError={false} errorMessage={null} totalCount={12} currentPage={2} totalPages={3} onPageChange={onPageChange} onRetry={vi.fn()} />);
    const records = screen.getAllByText(/Observed/);
    expect(records[0]).toHaveTextContent('Observed 75%');
    expect(screen.getByText('Access obstructed')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Next' }));
    expect(onPageChange).toHaveBeenCalledWith(3);
  });

  it('shows an honest empty state', () => {
    render(<ObservationHistory observations={[]} isLoading={false} isError={false} errorMessage={null} totalCount={0} currentPage={1} totalPages={1} onPageChange={vi.fn()} onRetry={vi.fn()} />);
    expect(screen.getByText('No observations recorded')).toBeInTheDocument();
  });
});
