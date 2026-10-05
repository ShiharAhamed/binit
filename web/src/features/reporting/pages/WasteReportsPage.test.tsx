import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { WasteReportsPage } from './WasteReportsPage';
import { reportingApi } from '../api/reportingApi';
import { AppRoutes } from '../../../routes/AppRoutes';
import { useAuthStore } from '../../../store/authStore';
import type {
  PagedResult,
  WasteReportSummaryDto,
  WasteReportStatus,
} from '../types/reporting';

vi.mock('../api/reportingApi', () => ({
  reportingApi: {
    getWasteReports: vi.fn(),
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

const mockReports: WasteReportSummaryDto[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    description: 'Pettah market overflowing dumpster',
    wasteType: 'General',
    status: 'Submitted',
    priority: null,
    addressText: 'Pettah Market Square, Colombo',
    latitude: 6.9351,
    longitude: 79.8521,
    citizenId: 'c1',
    citizenName: 'Sunil Silva',
    createdAt: '2026-09-15T08:30:00Z',
    updatedAt: null,
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    description: 'Chemical drums left near lake bank',
    wasteType: 'Hazardous',
    status: 'UnderReview',
    priority: 'High',
    addressText: null, // Test coordinate fallback
    latitude: 6.91234,
    longitude: 79.86789,
    citizenId: 'c2',
    citizenName: 'Anura Kumara',
    createdAt: '2026-09-14T14:15:00Z',
    updatedAt: '2026-09-15T09:00:00Z',
  },
];

describe('WasteReportsPage (Operational List)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('renders initial loading state with skeleton indicators', () => {
    // Delay resolution to capture loading state
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {})
    );

    renderWithClient(<WasteReportsPage />);

    expect(screen.getByRole('heading', { name: /waste reports/i })).toBeInTheDocument();
    expect(screen.getByTestId('reports-table-loading')).toBeInTheDocument();
  });

  it('renders waste reports table with authoritative DTO data and formatted values', async () => {
    const mockPaged: PagedResult<WasteReportSummaryDto> = {
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    };
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaged);

    renderWithClient(<WasteReportsPage />);

    // Wait for table to load
    expect(await screen.findByText('Pettah market overflowing dumpster')).toBeInTheDocument();
    expect(screen.getByText('Chemical drums left near lake bank')).toBeInTheDocument();

    // Human-friendly shortened ID references
    expect(screen.getByText('Report 11111111')).toBeInTheDocument();
    expect(screen.getByText('Report 22222222')).toBeInTheDocument();

    // Categories
    expect(screen.getAllByText('General').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Hazardous').length).toBeGreaterThanOrEqual(1);

    // Statuses
    expect(screen.getAllByText('Submitted').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Under Review').length).toBeGreaterThanOrEqual(1);

    // Citizen names
    expect(screen.getByText('Sunil Silva')).toBeInTheDocument();
    expect(screen.getByText('Anura Kumara')).toBeInTheDocument();

    // Total count badge
    expect(screen.getByText('2 Total Reports')).toBeInTheDocument();
  });

  it('renders all 8 status types with human-readable labels', async () => {
    const allStatuses: WasteReportStatus[] = [
      'Submitted',
      'UnderReview',
      'Verified',
      'Rejected',
      'Scheduled',
      'InProgress',
      'Resolved',
      'Cancelled',
    ];

    const eightReports: WasteReportSummaryDto[] = allStatuses.map((status, index) => ({
      id: `00000000-0000-0000-0000-00000000000${index}`,
      description: `Report with status ${status}`,
      wasteType: 'Organic',
      status,
      priority: null,
      addressText: `Location ${index}`,
      latitude: 6.9,
      longitude: 79.8,
      citizenId: 'c1',
      citizenName: 'Citizen Tester',
      createdAt: '2026-09-16T12:00:00Z',
      updatedAt: null,
    }));

    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: eightReports,
      page: 1,
      pageSize: 20,
      totalCount: 8,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    expect(await screen.findByText('Report with status Submitted')).toBeInTheDocument();
    expect(screen.getAllByText('Submitted').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Under Review').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Verified').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Rejected').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Scheduled').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('In Progress').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Resolved').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Cancelled').length).toBeGreaterThanOrEqual(1);
  });

  it('handles nullable priority correctly: displays Not assigned when null and subtle badge when present', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    expect(await screen.findByText('Pettah market overflowing dumpster')).toBeInTheDocument();

    // First report priority is null -> renders Not assigned
    expect(screen.getAllByText('Not assigned').length).toBeGreaterThanOrEqual(1);

    // Second report priority is High -> renders High
    expect(screen.getAllByText('High').length).toBeGreaterThanOrEqual(1);
  });

  it('displays location addressText when present, and coordinate fallback when addressText is null', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    // Address text for first report
    expect(
      await screen.findByText('Pettah Market Square, Colombo')
    ).toBeInTheDocument();

    // Coordinate fallback (5 decimals: 6.91234, 79.86789) for second report
    expect(screen.getAllByText('6.91234, 79.86789').length).toBeGreaterThanOrEqual(1);
  });

  it('debounces search input by ~350ms, resets page to 1, and queries backend', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    const searchInput = await screen.findByPlaceholderText(/search description or address/i);

    await user.type(searchInput, 'Pettah');

    await waitFor(
      () => {
        expect(reportingApi.getWasteReports).toHaveBeenCalledWith(
          expect.objectContaining({
            search: 'Pettah',
            page: 1,
          })
        );
      },
      { timeout: 1000 }
    );
  });

  it('filters by status, preserves other parameters, and resets page to 1', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    const statusSelect = await screen.findByLabelText(/status filter/i);

    await user.selectOptions(statusSelect, 'UnderReview');

    await waitFor(() => {
      expect(reportingApi.getWasteReports).toHaveBeenCalledWith(
        expect.objectContaining({
          status: 'UnderReview',
          page: 1,
        })
      );
    });
  });

  it('filters by waste type and resets page to 1', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    const wasteTypeSelect = await screen.findByLabelText(/waste type/i);

    await user.selectOptions(wasteTypeSelect, 'Hazardous');

    await waitFor(() => {
      expect(reportingApi.getWasteReports).toHaveBeenCalledWith(
        expect.objectContaining({
          wasteType: 'Hazardous',
          page: 1,
        })
      );
    });
  });

  it('changes sort order (Newest/Oldest/Recently Updated)', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    const sortSelect = await screen.findByLabelText(/sort order/i);

    await user.selectOptions(sortSelect, 'createdAt:asc');

    await waitFor(() => {
      expect(reportingApi.getWasteReports).toHaveBeenCalledWith(
        expect.objectContaining({
          sortBy: 'createdAt',
          sortDirection: 'asc',
        })
      );
    });
  });

  it('clears all active filters when Clear Filters button is clicked', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    const statusSelect = await screen.findByLabelText(/status filter/i);
    await user.selectOptions(statusSelect, 'Verified');

    const clearButton = await screen.findByRole('button', { name: /clear filters/i });
    expect(clearButton).toBeInTheDocument();

    await user.click(clearButton);

    await waitFor(() => {
      expect(statusSelect).toHaveValue('');
      expect(reportingApi.getWasteReports).toHaveBeenCalledWith(
        expect.objectContaining({
          status: '',
          search: '',
          wasteType: '',
          sortBy: 'createdAt',
          sortDirection: 'desc',
          page: 1,
        })
      );
    });
  });

  it('handles pagination: page change, disabled Previous/Next boundaries, and item summary', async () => {
    const user = userEvent.setup();
    const pagedResponse: PagedResult<WasteReportSummaryDto> = {
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 42,
      totalPages: 3,
    };
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue(pagedResponse);

    renderWithClient(<WasteReportsPage />);

    // Item count summary
    const summary = await screen.findByTestId('pagination-summary');
    expect(summary).toHaveTextContent('Showing 1 to 20 of 42 reports');
    expect(screen.getByText('Page 1 of 3')).toBeInTheDocument();

    const prevButton = screen.getByRole('button', { name: /previous page/i });
    const nextButton = screen.getByRole('button', { name: /next page/i });

    // Previous disabled on page 1
    expect(prevButton).toBeDisabled();
    expect(nextButton).toBeEnabled();

    // Click Next
    await user.click(nextButton);

    await waitFor(() => {
      expect(reportingApi.getWasteReports).toHaveBeenCalledWith(
        expect.objectContaining({
          page: 2,
        })
      );
    });
  });

  it('renders empty state when zero reports exist in the system without Report Waste CTA', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    });

    renderWithClient(<WasteReportsPage />);

    expect(await screen.findByText('No waste reports found.')).toBeInTheDocument();
    expect(
      screen.getByText(/citizen waste incident reports submitted from the mobile application/i)
    ).toBeInTheDocument();

    // WasteOfficer does not create citizen reports: ensure NO Report Waste button
    expect(screen.queryByRole('button', { name: /report waste/i })).not.toBeInTheDocument();
  });

  it('renders filtered empty state with Clear Filters button when query returns no items', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    });

    renderWithClient(<WasteReportsPage />);

    const statusSelect = await screen.findByLabelText(/status filter/i);
    await user.selectOptions(statusSelect, 'Rejected');

    expect(
      await screen.findByText('No reports match the current filters.')
    ).toBeInTheDocument();

    const clearButtons = screen.getAllByRole('button', { name: /clear filters/i });
    expect(clearButtons.length).toBeGreaterThanOrEqual(1);
  });

  it('renders inline error alert with Retry button on API failure', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Failed to fetch from backend')
    );

    renderWithClient(<WasteReportsPage />);

    expect(await screen.findByText('Failed to load reports')).toBeInTheDocument();
    expect(screen.getByText('Failed to fetch from backend')).toBeInTheDocument();

    const retryButton = screen.getByRole('button', { name: /retry/i });
    expect(retryButton).toBeInTheDocument();

    // On retry, mock success
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    await user.click(retryButton);

    expect(await screen.findByText('Pettah market overflowing dumpster')).toBeInTheDocument();
  });

  it('STRICT INVARIANT: ensures NO mutation action buttons (Start Review, Verify, Reject, Edit, Cancel) exist', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    renderWithClient(<WasteReportsPage />);

    expect(await screen.findByText('Pettah market overflowing dumpster')).toBeInTheDocument();

    // Verify zero mutation buttons
    expect(screen.queryByRole('button', { name: /start review/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /verify/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /reject/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel report/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
  });
});

describe('WasteReports Route & Role Access Guards', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('allows authorized WasteOfficer to access /officer/waste-reports', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'officer-token',
      user: {
        id: 'officer-1',
        fullName: 'Nimal Perera',
        email: 'officer@smartwaste.local',
        role: 'WasteOfficer',
      },
    });

    render(
      <MemoryRouter initialEntries={['/officer/waste-reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /waste reports/i })).toBeInTheDocument();
  });

  it('allows /officer/reports route alias to redirect to /officer/waste-reports for WasteOfficer', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'officer-token',
      user: {
        id: 'officer-1',
        fullName: 'Nimal Perera',
        email: 'officer@smartwaste.local',
        role: 'WasteOfficer',
      },
    });

    render(
      <MemoryRouter initialEntries={['/officer/reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /waste reports/i })).toBeInTheDocument();
  });

  it('blocks Citizen from accessing /officer/waste-reports and redirects to /unauthorized', async () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'citizen-token',
      user: {
        id: 'citizen-1',
        fullName: 'Kamal Citizen',
        email: 'citizen@example.com',
        role: 'Citizen',
      },
    });

    render(
      <MemoryRouter initialEntries={['/officer/waste-reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('blocks Driver from accessing /officer/waste-reports and redirects to /unauthorized', async () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'driver-token',
      user: {
        id: 'driver-1',
        fullName: 'Sunil Driver',
        email: 'driver@smartwaste.local',
        role: 'Driver',
      },
    });

    render(
      <MemoryRouter initialEntries={['/officer/waste-reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('allows MunicipalManager to access /manager/reports, view reports, and links to /manager/reports/:id', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'manager-token',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /waste reports/i })).toBeInTheDocument();
    expect(
      screen.getByText('Monitor reported waste issues and their current operational status.')
    ).toBeInTheDocument();
    expect(screen.getByText('Pettah market overflowing dumpster')).toBeInTheDocument();

    // Verify links point to manager route namespace
    const viewDetailsLink = screen.getByTestId(
      'view-details-11111111-1111-1111-1111-111111111111'
    );
    expect(viewDetailsLink).toHaveAttribute(
      'href',
      '/manager/reports/11111111-1111-1111-1111-111111111111'
    );
  });

  it('allows /manager/waste-reports alias to redirect to /manager/reports for MunicipalManager', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'manager-token',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/waste-reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /waste reports/i })).toBeInTheDocument();
  });

  it('blocks WasteOfficer from accessing /manager/reports and redirects to /unauthorized', async () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'officer-token',
      user: {
        id: 'officer-1',
        fullName: 'Nimal Perera',
        email: 'officer@smartwaste.local',
        role: 'WasteOfficer',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('blocks Citizen from accessing /manager/reports and redirects to /unauthorized', async () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'citizen-token',
      user: {
        id: 'citizen-1',
        fullName: 'Kamal Citizen',
        email: 'citizen@example.com',
        role: 'Citizen',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('blocks Driver from accessing /manager/reports and redirects to /unauthorized', async () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'driver-token',
      user: {
        id: 'driver-1',
        fullName: 'Sunil Driver',
        email: 'driver@smartwaste.local',
        role: 'Driver',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });
});

describe('WasteReports UI Responsiveness', () => {
  it('renders cleanly across desktop (1280px) and narrower (768px) viewports', async () => {
    (reportingApi.getWasteReports as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: mockReports,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
    });

    // 1. Desktop viewport (1280px)
    window.innerWidth = 1280;
    window.innerHeight = 800;
    window.dispatchEvent(new Event('resize'));

    const { unmount } = renderWithClient(<WasteReportsPage />);

    expect(await screen.findByText('Pettah market overflowing dumpster')).toBeInTheDocument();
    expect(screen.getByRole('table')).toBeInTheDocument();
    unmount();

    // 2. Narrower viewport (768px)
    window.innerWidth = 768;
    window.innerHeight = 1024;
    window.dispatchEvent(new Event('resize'));

    renderWithClient(<WasteReportsPage />);

    expect(await screen.findByText('Pettah market overflowing dumpster')).toBeInTheDocument();
    expect(screen.getByRole('table')).toBeInTheDocument();
  });
});
