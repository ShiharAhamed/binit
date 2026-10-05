import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ComplaintsPage } from './ComplaintsPage';
import { complaintsApi } from '../api/complaintsApi';
import { useAuthStore } from '../../../store/authStore';
import type {
  PagedResult,
  ComplaintSummaryDto,
} from '../types/complaints';

vi.mock('../api/complaintsApi', () => ({
  complaintsApi: {
    getComplaints: vi.fn(),
  },
}));

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
    },
  });
}

function renderWithClient(ui: React.ReactElement) {
  const testClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={testClient}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>
  );
}

const mockComplaints: ComplaintSummaryDto[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    citizenId: 'c1',
    citizenName: 'Sunil Silva',
    category: 'MissedCollection',
    subject: 'Dumpster on 5th Lane was skipped',
    status: 'Submitted',
    latitude: 6.9271,
    longitude: 79.8612,
    createdAt: '2026-09-20T08:30:00Z',
    updatedAt: null,
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    citizenId: 'c2',
    citizenName: 'Anura Kumara',
    category: 'DelayedService',
    subject: 'Recycling pickup was delayed by two days',
    status: 'InReview',
    latitude: null,
    longitude: null,
    createdAt: '2026-09-21T14:15:00Z',
    updatedAt: '2026-09-22T09:00:00Z',
  },
];

describe('ComplaintsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('renders initial loading state with skeleton indicators', () => {
    (complaintsApi.getComplaints as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {})
    );

    renderWithClient(<ComplaintsPage />);

    expect(screen.getByRole('heading', { name: /complaints/i })).toBeInTheDocument();
    expect(screen.getByTestId('complaints-table-loading')).toBeInTheDocument();
  });

  it('renders complaint records with authoritative DTO data', async () => {
    const mockPaged: PagedResult<ComplaintSummaryDto> = {
      items: mockComplaints,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    };
    (complaintsApi.getComplaints as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<ComplaintsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('complaints-table')).toBeInTheDocument();
    });

    expect(screen.getByText('Dumpster on 5th Lane was skipped')).toBeInTheDocument();
    expect(screen.getByText('Sunil Silva')).toBeInTheDocument();
    expect(screen.getAllByText('Missed Collection').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Submitted').length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('Recycling pickup was delayed by two days')).toBeInTheDocument();
    expect(screen.getByText('Anura Kumara')).toBeInTheDocument();
    expect(screen.getAllByText('Delayed Service').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('In Review').length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('2 Total Complaints')).toBeInTheDocument();
  });

  it('applies status filter and triggers API request', async () => {
    const mockPaged: PagedResult<ComplaintSummaryDto> = {
      items: [mockComplaints[0]],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    };
    (complaintsApi.getComplaints as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<ComplaintsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('complaints-table')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText('Status Filter'), {
      target: { value: 'Submitted' },
    });

    await waitFor(() => {
      expect(complaintsApi.getComplaints).toHaveBeenCalledWith(
        expect.objectContaining({
          status: 'Submitted',
          page: 1,
        })
      );
    });
  });

  it('applies category filter and triggers API request', async () => {
    const mockPaged: PagedResult<ComplaintSummaryDto> = {
      items: [mockComplaints[0]],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    };
    (complaintsApi.getComplaints as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<ComplaintsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('complaints-table')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText('Category Filter'), {
      target: { value: 'MissedCollection' },
    });

    await waitFor(() => {
      expect(complaintsApi.getComplaints).toHaveBeenCalledWith(
        expect.objectContaining({
          category: 'MissedCollection',
          page: 1,
        })
      );
    });
  });

  it('debounces search input and requests matching complaints', async () => {
    const user = userEvent.setup();
    const mockPaged: PagedResult<ComplaintSummaryDto> = {
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    };
    (complaintsApi.getComplaints as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<ComplaintsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('complaints-table-empty')).toBeInTheDocument();
    });

    const searchInput = screen.getByLabelText('Search Complaints');
    await user.type(searchInput, 'Recycling');

    await waitFor(
      () => {
        expect(complaintsApi.getComplaints).toHaveBeenCalledWith(
          expect.objectContaining({
            search: 'Recycling',
            page: 1,
          })
        );
      },
      { timeout: 1000 }
    );
  });

  it('handles server-side pagination page changes', async () => {
    const mockPagedPage1: PagedResult<ComplaintSummaryDto> = {
      items: mockComplaints,
      page: 1,
      pageSize: 2,
      totalCount: 4,
      totalPages: 2,
    };
    (complaintsApi.getComplaints as ReturnType<typeof vi.fn>).mockResolvedValue(mockPagedPage1);

    renderWithClient(<ComplaintsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('complaints-pagination-summary')).toBeInTheDocument();
    });

    const nextBtn = screen.getByRole('button', { name: /next/i });
    fireEvent.click(nextBtn);

    await waitFor(() => {
      expect(complaintsApi.getComplaints).toHaveBeenCalledWith(
        expect.objectContaining({
          page: 2,
        })
      );
    });
  });

  it('renders inline error alert with retry button when query fails', async () => {
    (complaintsApi.getComplaints as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Failed to connect to complaint backend service.')
    );

    renderWithClient(<ComplaintsPage />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load complaints')).toBeInTheDocument();
    });

    expect(
      screen.getByText('Failed to connect to complaint backend service.')
    ).toBeInTheDocument();

    const retryBtn = screen.getByRole('button', { name: /retry/i });
    expect(retryBtn).toBeInTheDocument();
  });
});
