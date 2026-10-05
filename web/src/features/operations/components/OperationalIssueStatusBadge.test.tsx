import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { OperationalIssueStatusBadge } from './OperationalIssueStatusBadge';

describe('OperationalIssueStatusBadge', () => {
  it('renders Reported status with amber styles', () => {
    render(<OperationalIssueStatusBadge status="Reported" />);
    const badge = screen.getByTestId('operational-issue-status-badge');
    expect(badge).toHaveTextContent('Reported');
    expect(badge.className).toContain('text-amber-800');
  });

  it('renders InReview status with blue styles', () => {
    render(<OperationalIssueStatusBadge status="InReview" />);
    const badge = screen.getByTestId('operational-issue-status-badge');
    expect(badge).toHaveTextContent('In Review');
    expect(badge.className).toContain('text-blue-800');
  });

  it('renders Resolved status with emerald styles', () => {
    render(<OperationalIssueStatusBadge status="Resolved" />);
    const badge = screen.getByTestId('operational-issue-status-badge');
    expect(badge).toHaveTextContent('Resolved');
    expect(badge.className).toContain('text-emerald-800');
  });
});
