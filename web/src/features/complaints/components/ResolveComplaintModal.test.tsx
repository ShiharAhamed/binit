import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ResolveComplaintModal } from './ResolveComplaintModal';

describe('ResolveComplaintModal', () => {
  it('does not render when isOpen is false', () => {
    render(
      <ResolveComplaintModal
        isOpen={false}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders correctly when isOpen is true', () => {
    render(
      <ResolveComplaintModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /resolve complaint/i })).toBeInTheDocument();
    expect(
      screen.getByText('Add a resolution note that will be visible to the citizen.')
    ).toBeInTheDocument();
    expect(screen.getByTestId('resolution-note-input')).toBeInTheDocument();
    expect(screen.getByTestId('confirm-resolve-complaint-button')).toBeInTheDocument();
  });

  it('validates empty resolution note on submit', async () => {
    const onConfirm = vi.fn();
    render(
      <ResolveComplaintModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
      />
    );

    fireEvent.click(screen.getByTestId('confirm-resolve-complaint-button'));

    expect(
      screen.getByText('Resolution note is required.')
    ).toBeInTheDocument();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('validates resolution note shorter than 5 characters', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    render(
      <ResolveComplaintModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
      />
    );

    const input = screen.getByTestId('resolution-note-input');
    await user.type(input, 'Done');
    fireEvent.click(screen.getByTestId('confirm-resolve-complaint-button'));

    expect(
      screen.getByText('Resolution note must be at least 5 characters long.')
    ).toBeInTheDocument();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('submits valid trimmed resolution note', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    render(
      <ResolveComplaintModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
      />
    );

    const input = screen.getByTestId('resolution-note-input');
    await user.type(input, '  Missed collection completed by crew on morning route.  ');
    fireEvent.click(screen.getByTestId('confirm-resolve-complaint-button'));

    expect(onConfirm).toHaveBeenCalledWith(
      'Missed collection completed by crew on morning route.'
    );
  });

  it('displays character counter', async () => {
    const user = userEvent.setup();
    render(
      <ResolveComplaintModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    const counter = screen.getByTestId('char-counter');
    expect(counter).toHaveTextContent('0 / 1000');

    const input = screen.getByTestId('resolution-note-input');
    await user.type(input, 'Testing note');

    expect(counter).toHaveTextContent('12 / 1000');
  });

  it('calls onCancel when Cancel button is clicked', () => {
    const onCancel = vi.fn();
    render(
      <ResolveComplaintModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={onCancel}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalled();
  });

  it('disables actions when isSubmitting is true', () => {
    render(
      <ResolveComplaintModal
        isOpen={true}
        isSubmitting={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByTestId('resolution-note-input')).toBeDisabled();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeDisabled();
    expect(screen.getByTestId('confirm-resolve-complaint-button')).toBeDisabled();
  });
});
