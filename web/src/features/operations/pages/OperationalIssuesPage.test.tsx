import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { OperationalIssuesPage } from './OperationalIssuesPage';
import { operationsApi } from '../api/operationsApi';
import { useAuthStore } from '../../../store/authStore';
import type {
  PagedResult,
  OperationalIssueSummaryDto,
} from '../types/operations';

vi.mock('../api/operationsApi', () => ({
  operationsApi: {
    getOperationalIssues: vi.fn(),
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

describe('OperationalIssuesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('renders initial loading state with skeleton indicators', () => {
    (operationsApi.getOperationalIssues as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {})
    );

    renderWithClient(<OperationalIssuesPage />);

    expect(screen.getByRole('heading', { name: /operational issues/i })).toBeInTheDocument();
    expect(screen.getByTestId('operational-issues-table-loading')).toBeInTheDocument();
  });

  it('renders operational issue records with authoritative DTO data', async () => {
    const mockPaged: PagedResult<OperationalIssueSummaryDto> = {
      items: mockIssues,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    };
    (operationsApi.getOperationalIssues as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<OperationalIssuesPage />);

    await waitFor(() => {
      expect(screen.getByTestId('operational-issues-table')).toBeInTheDocument();
    });

    expect(screen.getByText('Compactor ram jammed on route 4')).toBeInTheDocument();
    expect(screen.getByText('Kamal Gunaratne')).toBeInTheDocument();
    expect(screen.getAllByText('Vehicle Problem').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Reported').length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('Fallen tree blocking narrow alleyway')).toBeInTheDocument();
    expect(screen.getByText('Saman Kumara')).toBeInTheDocument();
    expect(screen.getAllByText('Road / Access Issue').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('In Review').length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('2 Total Issues')).toBeInTheDocument();
  });

  it('applies status filter and triggers API request', async () => {
    const mockPaged: PagedResult<OperationalIssueSummaryDto> = {
      items: [mockIssues[0]],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    };
    (operationsApi.getOperationalIssues as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<OperationalIssuesPage />);

    await waitFor(() => {
      expect(screen.getByTestId('operational-issues-table')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText('Status Filter'), {
      target: { value: 'Reported' },
    });

    await waitFor(() => {
      expect(operationsApi.getOperationalIssues).toHaveBeenCalledWith(
        expect.objectContaining({
          status: 'Reported',
          page: 1,
        })
      );
    });
  });

  it('applies issueType filter and triggers API request', async () => {
    const mockPaged: PagedResult<OperationalIssueSummaryDto> = {
      items: [mockIssues[0]],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    };
    (operationsApi.getOperationalIssues as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<OperationalIssuesPage />);

    await waitFor(() => {
      expect(screen.getByTestId('operational-issues-table')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText('Issue Type Filter'), {
      target: { value: 'VehicleProblem' },
    });

    await waitFor(() => {
      expect(operationsApi.getOperationalIssues).toHaveBeenCalledWith(
        expect.objectContaining({
          issueType: 'VehicleProblem',
          page: 1,
        })
      );
    });
  });

  it('debounces search input and requests matching operational issues', async () => {
    const user = userEvent.setup();
    const mockPaged: PagedResult<OperationalIssueSummaryDto> = {
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    };
    (operationsApi.getOperationalIssues as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<OperationalIssuesPage />);

    await waitFor(() => {
      expect(screen.getByTestId('operational-issues-table-empty')).toBeInTheDocument();
    });

    const searchInput = screen.getByLabelText('Search Operational Issues');
    await user.type(searchInput, 'Hydraulic');

    await waitFor(
      () => {
        expect(operationsApi.getOperationalIssues).toHaveBeenCalledWith(
          expect.objectContaining({
            search: 'Hydraulic',
            page: 1,
          })
        );
      },
      { timeout: 1000 }
    );
  });

  it('handles server-side pagination page changes', async () => {
    const mockPagedPage1: PagedResult<OperationalIssueSummaryDto> = {
      items: mockIssues,
      page: 1,
      pageSize: 2,
      totalCount: 4,
      totalPages: 2,
    };
    (operationsApi.getOperationalIssues as ReturnType<typeof vi.fn>).mockResolvedValue(mockPagedPage1);

    renderWithClient(<OperationalIssuesPage />);

    await waitFor(() => {
      expect(screen.getByTestId('operational-issues-pagination-summary')).toBeInTheDocument();
    });

    const nextBtn = screen.getByRole('button', { name: /next/i });
    fireEvent.click(nextBtn);

    await waitFor(() => {
      expect(operationsApi.getOperationalIssues).toHaveBeenCalledWith(
        expect.objectContaining({
          page: 2,
        })
      );
    });
  });

  it('renders inline error alert with retry button when query fails', async () => {
    (operationsApi.getOperationalIssues as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Failed to connect to operations backend service.')
    );

    renderWithClient(<OperationalIssuesPage />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load operational issues')).toBeInTheDocument();
    });

    expect(
      screen.getByText('Failed to connect to operations backend service.')
    ).toBeInTheDocument();

    const retryBtn = screen.getByRole('button', { name: /retry/i });
    expect(retryBtn).toBeInTheDocument();
  });
});
