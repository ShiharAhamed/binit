import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { OfficerNeedsAttentionQueue } from './OfficerNeedsAttentionQueue';
import type { WasteOfficerNeedsAttentionItem } from '../types/dashboard';

const items: WasteOfficerNeedsAttentionItem[] = [
  { id: '12345678-0000-0000-0000-000000000001', itemType: 'WasteReport', reference: 'Report 12345678', createdAt: '2026-10-05T09:42:00Z', secondaryLabel: 'General', submittedByName: 'Kasun Silva', addressText: 'Rajagiriya' },
  { id: 'abcdef12-0000-0000-0000-000000000002', itemType: 'Complaint', reference: 'Complaint ABCDEF12', createdAt: '2026-10-05T09:38:00Z', secondaryLabel: 'Missed collection', submittedByName: 'Nimal Perera', addressText: null },
];

const renderQueue = (props: Partial<React.ComponentProps<typeof OfficerNeedsAttentionQueue>> = {}) =>
  render(<MemoryRouter>
    <OfficerNeedsAttentionQueue items={items} isLoading={false} isError={false} onRetry={vi.fn()} {...props} />
    <LocationPath />
  </MemoryRouter>);

const LocationPath = () => <output data-testid="current-path">{useLocation().pathname}</output>;

describe('Waste Officer needs attention queue', () => {
  it('keeps server order and links friendly references to existing detail routes', () => {
    renderQueue();
    const table = screen.getByRole('table', { name: 'Items needing initial review' });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows).toHaveLength(2);
    for (const heading of ['Type', 'Reference', 'Category', 'Submitted by', 'Location', 'Submitted'])
      expect(within(table).getByRole('columnheader', { name: heading })).toBeInTheDocument();
    expect(within(rows[0]).getByRole('link', { name: 'Report 12345678' }))
      .toHaveAttribute('href', '/officer/waste-reports/12345678-0000-0000-0000-000000000001');
    expect(within(rows[1]).getByRole('link', { name: 'Complaint ABCDEF12' }))
      .toHaveAttribute('href', '/officer/complaints/abcdef12-0000-0000-0000-000000000002');
    expect(within(rows[0]).getByRole('cell', { name: 'General' })).toBeInTheDocument();
    expect(within(rows[1]).getByRole('cell', { name: 'Missed collection' })).toBeInTheDocument();
    expect(within(rows[0]).getByRole('cell', { name: 'Kasun Silva' })).toBeInTheDocument();
    expect(within(rows[0]).getByText('Rajagiriya')).toBeInTheDocument();
    expect(within(rows[1]).getByRole('cell', { name: 'Nimal Perera' })).toBeInTheDocument();
    expect(within(rows[1]).getByRole('cell', { name: '—' })).toBeInTheDocument();
    expect(screen.queryByText('null')).not.toBeInTheDocument();
    expect(screen.queryByText(items[0].id)).not.toBeInTheDocument();
    fireEvent.click(within(rows[0]).getByRole('link', { name: 'Report 12345678' }));
    expect(screen.getByTestId('current-path')).toHaveTextContent('/officer/waste-reports/12345678-0000-0000-0000-000000000001');
  });

  it('navigates from a complaint reference using the full complaint ID', () => {
    renderQueue();
    fireEvent.click(screen.getByRole('link', { name: 'Complaint ABCDEF12' }));
    expect(screen.getByTestId('current-path')).toHaveTextContent('/officer/complaints/abcdef12-0000-0000-0000-000000000002');
  });

  it('uses manager detail routes when rendered for a Municipal Manager', () => {
    renderQueue({ role: 'manager' });
    fireEvent.click(screen.getByRole('link', { name: 'Report 12345678' }));
    expect(screen.getByTestId('current-path')).toHaveTextContent('/manager/waste-reports/12345678-0000-0000-0000-000000000001');
    fireEvent.click(screen.getByRole('link', { name: 'Complaint ABCDEF12' }));
    expect(screen.getByTestId('current-path')).toHaveTextContent('/manager/complaints/abcdef12-0000-0000-0000-000000000002');
  });

  it('shows the real empty state only after loading completes', () => {
    const view = renderQueue({ items: [], isLoading: true });
    expect(screen.getByText('Loading items needing review…')).toBeInTheDocument();
    expect(screen.queryByText('All Operational Queues Clear')).not.toBeInTheDocument();
    view.rerender(<MemoryRouter><OfficerNeedsAttentionQueue items={[]} isLoading={false} isError={false} onRetry={vi.fn()} /></MemoryRouter>);
    expect(screen.getByText('All Operational Queues Clear')).toBeInTheDocument();
    expect(screen.queryByText(/modules are connected/i)).not.toBeInTheDocument();
  });

  it('offers a retry on error without claiming the queue is clear', () => {
    const retry = vi.fn();
    renderQueue({ items: undefined, isError: true, onRetry: retry });
    expect(screen.getByRole('alert')).toHaveTextContent('Unable to load items needing attention.');
    expect(screen.queryByText('All Operational Queues Clear')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(retry).toHaveBeenCalledOnce();
  });
});
