import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ComplaintStatusBadge } from './ComplaintStatusBadge';

describe('ComplaintStatusBadge', () => {
  it('renders Submitted status correctly with amber styling', () => {
    render(<ComplaintStatusBadge status="Submitted" />);
    const badge = screen.getByTestId('complaint-status-badge');
    expect(badge).toHaveTextContent('Submitted');
    expect(badge).toHaveAttribute('data-status', 'Submitted');
    expect(badge.className).toContain('bg-amber-50');
    expect(badge.className).toContain('text-amber-800');
  });

  it('renders InReview status correctly as "In Review" with blue styling', () => {
    render(<ComplaintStatusBadge status="InReview" />);
    const badge = screen.getByTestId('complaint-status-badge');
    expect(badge).toHaveTextContent('In Review');
    expect(badge).toHaveAttribute('data-status', 'InReview');
    expect(badge.className).toContain('bg-blue-50');
    expect(badge.className).toContain('text-blue-800');
  });

  it('renders Resolved status correctly with emerald styling', () => {
    render(<ComplaintStatusBadge status="Resolved" />);
    const badge = screen.getByTestId('complaint-status-badge');
    expect(badge).toHaveTextContent('Resolved');
    expect(badge).toHaveAttribute('data-status', 'Resolved');
    expect(badge.className).toContain('bg-emerald-50');
    expect(badge.className).toContain('text-emerald-800');
  });
});
