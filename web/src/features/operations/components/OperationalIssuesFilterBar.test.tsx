import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import {
  OperationalIssuesFilterBar,
  type OperationalIssueFilterState,
} from './OperationalIssuesFilterBar';

const defaultFilters: OperationalIssueFilterState = {
  search: '',
  status: '',
  issueType: '',
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

describe('OperationalIssuesFilterBar', () => {
  it('renders all filter controls and options', () => {
    render(
      <OperationalIssuesFilterBar
        filters={defaultFilters}
        onFilterChange={vi.fn()}
        onClearFilters={vi.fn()}
      />
    );

    expect(screen.getByLabelText('Search Operational Issues')).toBeInTheDocument();
    expect(screen.getByLabelText('Status Filter')).toBeInTheDocument();
    expect(screen.getByLabelText('Issue Type Filter')).toBeInTheDocument();
    expect(screen.getByLabelText('Sort Order')).toBeInTheDocument();

    expect(screen.getByText('All Statuses')).toBeInTheDocument();
    expect(screen.getByText('All Issue Types')).toBeInTheDocument();
    expect(screen.getByText('Vehicle Problem')).toBeInTheDocument();
    expect(screen.getByText('Road / Access Issue')).toBeInTheDocument();
    expect(screen.getByText('Equipment Problem')).toBeInTheDocument();
    expect(screen.getByText('Safety Concern')).toBeInTheDocument();
    expect(screen.getByText('Operational Delay')).toBeInTheDocument();
  });

  it('calls onFilterChange when status dropdown changes', () => {
    const handleFilterChange = vi.fn();
    render(
      <OperationalIssuesFilterBar
        filters={defaultFilters}
        onFilterChange={handleFilterChange}
        onClearFilters={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText('Status Filter'), {
      target: { value: 'Reported' },
    });

    expect(handleFilterChange).toHaveBeenCalledWith({ status: 'Reported' });
  });

  it('calls onFilterChange when issueType dropdown changes', () => {
    const handleFilterChange = vi.fn();
    render(
      <OperationalIssuesFilterBar
        filters={defaultFilters}
        onFilterChange={handleFilterChange}
        onClearFilters={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText('Issue Type Filter'), {
      target: { value: 'SafetyConcern' },
    });

    expect(handleFilterChange).toHaveBeenCalledWith({ issueType: 'SafetyConcern' });
  });

  it('shows Clear Filters button when filters are active and calls onClearFilters on click', () => {
    const handleClearFilters = vi.fn();
    render(
      <OperationalIssuesFilterBar
        filters={{
          ...defaultFilters,
          status: 'Reported',
          issueType: 'VehicleProblem',
        }}
        onFilterChange={vi.fn()}
        onClearFilters={handleClearFilters}
      />
    );

    const clearBtn = screen.getByRole('button', { name: /clear filters/i });
    expect(clearBtn).toBeInTheDocument();

    fireEvent.click(clearBtn);
    expect(handleClearFilters).toHaveBeenCalledTimes(1);
  });
});
