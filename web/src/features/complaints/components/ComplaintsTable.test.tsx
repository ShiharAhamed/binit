import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ComplaintsTable } from './ComplaintsTable';
import type { ComplaintSummaryDto } from '../types/complaints';
import { useAuthStore } from '../../../store/authStore';

const mockComplaints: ComplaintSummaryDto[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    citizenId: 'c1',
    citizenName: 'Kamal Perera',
    category: 'MissedCollection',
    subject: 'Missed Tuesday recycling pickup on Flower Rd',
    status: 'Submitted',
    latitude: 6.9271,
    longitude: 79.8612,
    createdAt: '2026-09-20T08:30:00Z',
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    citizenId: 'c2',
    citizenName: 'Nimal Jayasuriya',
    category: 'PoorService',
    subject: 'Spilled waste on pavement during bin emptying',
    status: 'InReview',
    latitude: null,
    longitude: null,
    createdAt: '2026-09-21T10:00:00Z',
  },
];

describe('ComplaintsTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('renders loading skeleton when isLoading is true', () => {
    render(
      <MemoryRouter>
        <ComplaintsTable
          complaints={[]}
          isLoading={true}
          isFiltered={false}
          onClearFilters={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId('complaints-table-loading')).toBeInTheDocument();
  });

  it('renders unfiltered empty state when complaints list is empty', () => {
    render(
      <MemoryRouter>
        <ComplaintsTable
          complaints={[]}
          isLoading={false}
          isFiltered={false}
          onClearFilters={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId('complaints-table-empty')).toBeInTheDocument();
    expect(screen.getByText('No complaints found.')).toBeInTheDocument();
  });

  it('renders filtered empty state with clear filters button when isFiltered is true', () => {
    const onClearFilters = vi.fn();
    render(
      <MemoryRouter>
        <ComplaintsTable
          complaints={[]}
          isLoading={false}
          isFiltered={true}
          onClearFilters={onClearFilters}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId('complaints-table-filtered-empty')).toBeInTheDocument();
    expect(
      screen.getByText('No complaints match the current filters.')
    ).toBeInTheDocument();

    const clearBtn = screen.getByRole('button', { name: /clear filters/i });
    fireEvent.click(clearBtn);
    expect(onClearFilters).toHaveBeenCalled();
  });

  it('renders complaint records with subjects, categories, status badges, and citizen names', () => {
    render(
      <MemoryRouter>
        <ComplaintsTable
          complaints={mockComplaints}
          isLoading={false}
          isFiltered={false}
          onClearFilters={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId('complaints-table')).toBeInTheDocument();
    expect(screen.getByText('Missed Tuesday recycling pickup on Flower Rd')).toBeInTheDocument();
    expect(screen.getByText('Kamal Perera')).toBeInTheDocument();
    expect(screen.getByText('Missed Collection')).toBeInTheDocument();
    expect(screen.getAllByText('Submitted').length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('Spilled waste on pavement during bin emptying')).toBeInTheDocument();
    expect(screen.getByText('Nimal Jayasuriya')).toBeInTheDocument();
    expect(screen.getByText('Poor Service')).toBeInTheDocument();
    expect(screen.getByText('In Review')).toBeInTheDocument();
  });

  it('displays location indicator icon when coordinates are present', () => {
    render(
      <MemoryRouter>
        <ComplaintsTable
          complaints={mockComplaints}
          isLoading={false}
          isFiltered={false}
          onClearFilters={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.getByTitle('Location coordinates attached')).toBeInTheDocument();
  });
});
