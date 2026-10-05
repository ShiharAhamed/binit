import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  ComplaintsFilterBar,
  type ComplaintFilterState,
} from './ComplaintsFilterBar';

const defaultFilters: ComplaintFilterState = {
  search: '',
  status: '',
  category: '',
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

describe('ComplaintsFilterBar', () => {
  it('renders all filter controls properly', () => {
    render(
      <ComplaintsFilterBar
        filters={defaultFilters}
        onFilterChange={vi.fn()}
        onClearFilters={vi.fn()}
      />
    );

    expect(screen.getByLabelText('Search Complaints')).toBeInTheDocument();
    expect(screen.getByLabelText('Status Filter')).toBeInTheDocument();
    expect(screen.getByLabelText('Category Filter')).toBeInTheDocument();
    expect(screen.getByLabelText('Sort Order')).toBeInTheDocument();
  });

  it('triggers onFilterChange when status dropdown changes', () => {
    const onFilterChange = vi.fn();
    render(
      <ComplaintsFilterBar
        filters={defaultFilters}
        onFilterChange={onFilterChange}
        onClearFilters={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText('Status Filter'), {
      target: { value: 'InReview' },
    });

    expect(onFilterChange).toHaveBeenCalledWith({ status: 'InReview' });
  });

  it('triggers onFilterChange when category dropdown changes', () => {
    const onFilterChange = vi.fn();
    render(
      <ComplaintsFilterBar
        filters={defaultFilters}
        onFilterChange={onFilterChange}
        onClearFilters={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText('Category Filter'), {
      target: { value: 'MissedCollection' },
    });

    expect(onFilterChange).toHaveBeenCalledWith({ category: 'MissedCollection' });
  });

  it('triggers onFilterChange when sort dropdown changes', () => {
    const onFilterChange = vi.fn();
    render(
      <ComplaintsFilterBar
        filters={defaultFilters}
        onFilterChange={onFilterChange}
        onClearFilters={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText('Sort Order'), {
      target: { value: 'createdAt:asc' },
    });

    expect(onFilterChange).toHaveBeenCalledWith({
      sortBy: 'createdAt',
      sortDirection: 'asc',
    });
  });

  it('debounces search input changes', async () => {
    const user = userEvent.setup();
    const onFilterChange = vi.fn();
    render(
      <ComplaintsFilterBar
        filters={defaultFilters}
        onFilterChange={onFilterChange}
        onClearFilters={vi.fn()}
      />
    );

    const searchInput = screen.getByLabelText('Search Complaints');
    await user.type(searchInput, 'Main Street');

    await waitFor(
      () => {
        expect(onFilterChange).toHaveBeenCalledWith({ search: 'Main Street' });
      },
      { timeout: 1000 }
    );
  });

  it('renders Clear Filters button when filters are active and calls onClearFilters', () => {
    const onClearFilters = vi.fn();
    render(
      <ComplaintsFilterBar
        filters={{
          ...defaultFilters,
          status: 'Submitted',
        }}
        onFilterChange={vi.fn()}
        onClearFilters={onClearFilters}
      />
    );

    const clearButton = screen.getByRole('button', { name: /clear filters/i });
    expect(clearButton).toBeInTheDocument();

    fireEvent.click(clearButton);
    expect(onClearFilters).toHaveBeenCalled();
  });
});
