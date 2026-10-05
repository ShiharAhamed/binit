import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { OperationalIssuesTable } from './OperationalIssuesTable';
import type { OperationalIssueSummaryDto } from '../types/operations';

const mockIssues: OperationalIssueSummaryDto[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    driverId: 'd1',
    driverName: 'Kamal Gunaratne',
    issueType: 'VehicleProblem',
    title: 'Compactor ram jammed on route 4',
    status: 'Reported',
    latitude: 6.9271,
    longitude: 79.8612,
    createdAt: '2026-09-20T08:30:00Z',
    updatedAt: null,
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    driverId: 'd2',
    driverName: 'Saman Kumara',
    issueType: 'RoadOrAccessIssue',
    title: 'Fallen tree blocking narrow alleyway',
    status: 'InReview',
    latitude: null,
    longitude: null,
    createdAt: '2026-09-21T14:15:00Z',
    updatedAt: '2026-09-22T09:00:00Z',
  },
];

describe('OperationalIssuesTable', () => {
  it('renders loading skeleton when isLoading is true', () => {
    render(
      <MemoryRouter>
        <OperationalIssuesTable
          issues={[]}
          isLoading={true}
          isFiltered={false}
          onClearFilters={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId('operational-issues-table-loading')).toBeInTheDocument();
  });

  it('renders unfiltered empty state when no issues exist', () => {
    render(
      <MemoryRouter>
        <OperationalIssuesTable
          issues={[]}
          isLoading={false}
          isFiltered={false}
          onClearFilters={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId('operational-issues-table-empty')).toBeInTheDocument();
    expect(screen.getByText('No operational issues found.')).toBeInTheDocument();
  });

  it('renders filtered empty state with clear filters button when filters active', () => {
    const handleClear = vi.fn();
    render(
      <MemoryRouter>
        <OperationalIssuesTable
          issues={[]}
          isLoading={false}
          isFiltered={true}
          onClearFilters={handleClear}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId('operational-issues-table-filtered-empty')).toBeInTheDocument();
    expect(
      screen.getByText('No operational issues match the selected filters.')
    ).toBeInTheDocument();

    const clearBtn = screen.getByRole('button', { name: /clear filters/i });
    fireEvent.click(clearBtn);
    expect(handleClear).toHaveBeenCalledTimes(1);
  });

  it('renders operational issue rows with correct data and location indicator', () => {
    render(
      <MemoryRouter>
        <OperationalIssuesTable
          issues={mockIssues}
          isLoading={false}
          isFiltered={false}
          onClearFilters={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.getByText('Compactor ram jammed on route 4')).toBeInTheDocument();
    expect(screen.getByText('Kamal Gunaratne')).toBeInTheDocument();
    expect(screen.getAllByText('Vehicle Problem').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Reported').length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('Fallen tree blocking narrow alleyway')).toBeInTheDocument();
    expect(screen.getByText('Saman Kumara')).toBeInTheDocument();
    expect(screen.getAllByText('Road / Access Issue').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('In Review').length).toBeGreaterThanOrEqual(1);


    // Check location pin on row with coordinates
    expect(screen.getByLabelText('Location coordinates attached')).toBeInTheDocument();
  });
});
