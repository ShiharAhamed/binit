import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ResolveOperationalIssueModal } from './ResolveOperationalIssueModal';

describe('ResolveOperationalIssueModal', () => {
  it('does not render when isOpen is false', () => {
    const { container } = render(
      <ResolveOperationalIssueModal
        isOpen={false}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(container).toBeEmptyDOMElement();
  });

  it('renders modal dialog when isOpen is true', () => {
    render(
      <ResolveOperationalIssueModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Resolve Operational Issue')).toBeInTheDocument();
    expect(
      screen.getByText('Add a resolution note that will be visible to the driver.')
    ).toBeInTheDocument();
  });

  it('validates empty resolution note on submit', async () => {
    render(
      <ResolveOperationalIssueModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    const submitBtn = screen.getByTestId('confirm-resolve-operational-issue-button');
    fireEvent.click(submitBtn);

    expect(screen.getByText('Resolution note is required.')).toBeInTheDocument();
  });

  it('validates note shorter than 5 characters', async () => {
    const user = userEvent.setup();
    render(
      <ResolveOperationalIssueModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    const input = screen.getByTestId('resolution-note-input');
    await user.type(input, 'Done');

    const submitBtn = screen.getByTestId('confirm-resolve-operational-issue-button');
    fireEvent.click(submitBtn);

    expect(
      screen.getByText('Resolution note must be at least 5 characters long.')
    ).toBeInTheDocument();
  });

  it('submits valid resolution note and calls onConfirm with trimmed string', async () => {
    const user = userEvent.setup();
    const handleConfirm = vi.fn();
    render(
      <ResolveOperationalIssueModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={handleConfirm}
        onCancel={vi.fn()}
      />
    );

    const input = screen.getByTestId('resolution-note-input');
    await user.type(input, '  Replacement vehicle dispatched to complete route.  ');

    const submitBtn = screen.getByTestId('confirm-resolve-operational-issue-button');
    fireEvent.click(submitBtn);

    expect(handleConfirm).toHaveBeenCalledWith(
      'Replacement vehicle dispatched to complete route.'
    );
  });

  it('calls onCancel when Cancel button is clicked', () => {
    const handleCancel = vi.fn();
    render(
      <ResolveOperationalIssueModal
        isOpen={true}
        isSubmitting={false}
        onConfirm={vi.fn()}
        onCancel={handleCancel}
      />
    );

    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    fireEvent.click(cancelBtn);

    expect(handleCancel).toHaveBeenCalledTimes(1);
  });
});
