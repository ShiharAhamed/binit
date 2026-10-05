import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ObservationForm } from './ObservationForm';

describe('ObservationForm', () => {
  it('validates required selections and submits only the supported observation payload', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<ObservationForm isSubmitting={false} onSubmit={onSubmit} onCancel={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Record Observation' }));
    expect(await screen.findByText('Select an observed fill level.')).toBeInTheDocument();
    await user.click(screen.getByLabelText('75%'));
    await user.click(screen.getByLabelText('Blocked'));
    await user.type(screen.getByLabelText(/Notes/), 'Access obstructed');
    await user.click(screen.getByRole('button', { name: 'Record Observation' }));
    expect(onSubmit).toHaveBeenCalledWith({ fillLevelPercent: 75, condition: 'Blocked', notes: 'Access obstructed' });
  });
});
