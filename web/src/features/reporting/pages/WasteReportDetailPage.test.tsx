import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { WasteReportDetailPage } from './WasteReportDetailPage';
import { reportingApi } from '../api/reportingApi';
import { AppRoutes } from '../../../routes/AppRoutes';
import { useAuthStore } from '../../../store/authStore';
import type {
  WasteReportDetailDto,
  WasteReportStatusHistoryDto,
  WasteReportStatus,
} from '../types/reporting';

vi.mock('../api/reportingApi', () => ({
  reportingApi: {
    getWasteReport: vi.fn(),
    getWasteReportHistory: vi.fn(),
    getWasteReports: vi.fn(),
    startReview: vi.fn(),
    verifyReport: vi.fn(),
    rejectReport: vi.fn(),
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

function renderWithClient(
  ui: React.ReactElement,
  initialEntries: string[] = ['/officer/waste-reports/11111111-2222-3333-4444-555555555555']
) {
  const testClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={testClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path="/officer/waste-reports/:id" element={ui} />
          <Route path="/officer/waste-reports" element={<div>Waste Reports List Page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

function renderManagerWithClient(
  ui: React.ReactElement,
  initialEntries: string[] = ['/manager/reports/11111111-2222-3333-4444-555555555555']
) {
  const testClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={testClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path="/manager/reports/:id" element={ui} />
          <Route path="/manager/reports" element={<div>Manager Reports List Page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

const mockDetail: WasteReportDetailDto = {
  id: '11111111-2222-3333-4444-555555555555',
  citizenId: 'c1',
  citizenName: 'Sunil Silva',
  description: 'Overflowing commercial waste container behind market stalls.',
  wasteType: 'Organic',
  latitude: 6.9351,
  longitude: 79.8521,
  addressText: 'Market Square, Pettah, Colombo 11',
  status: 'Submitted',
  priority: 'High',
  verifiedByUserId: null,
  verifiedByUserName: null,
  verifiedAt: null,
  attachments: [
    {
      id: 'att-1',
      wasteReportId: '11111111-2222-3333-4444-555555555555',
      fileUrl: 'https://storage.smartwaste.local/reports/photo1.jpg',
      fileType: 'image/jpeg',
      createdAt: '2026-09-17T08:30:00Z',
    },
    {
      id: 'att-2',
      wasteReportId: '11111111-2222-3333-4444-555555555555',
      fileUrl: 'https://storage.smartwaste.local/reports/photo2.jpg',
      fileType: 'image/jpeg',
      createdAt: '2026-09-17T08:31:00Z',
    },
  ],
  createdAt: '2026-09-17T08:30:00Z',
  updatedAt: '2026-09-17T09:15:00Z',
};

const mockHistory: WasteReportStatusHistoryDto[] = [
  {
    id: 'hist-1',
    wasteReportId: '11111111-2222-3333-4444-555555555555',
    fromStatus: null,
    toStatus: 'Submitted',
    changedByUserId: 'c1',
    changedByUserName: 'Sunil Silva',
    notes: 'Citizen submitted report with 2 photos',
    changedAt: '2026-09-17T08:30:00Z',
  },
  {
    id: 'hist-2',
    wasteReportId: '11111111-2222-3333-4444-555555555555',
    fromStatus: 'Submitted',
    toStatus: 'UnderReview',
    changedByUserId: 'u-officer-1',
    changedByUserName: 'Officer Perera',
    notes: 'Under field assessment review',
    changedAt: '2026-09-17T09:15:00Z',
  },
];

describe('WasteReportDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'test-officer-token',
      user: {
        id: 'u-officer-1',
        fullName: 'Officer Perera',
        email: 'officer@smartwaste.local',
        role: 'WasteOfficer',
      },
    });
  });

  it('renders initial loading state with skeleton indicators', () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {})
    );
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {})
    );

    renderWithClient(<WasteReportDetailPage />);

    expect(screen.getByTestId('report-detail-loading')).toBeInTheDocument();
  });

  it('renders waste report details including header, overview, multiline description, and coordinates', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    // Header short reference
    expect(await screen.findByText('Report 11111111')).toBeInTheDocument();
    expect(screen.getByText('Waste Report Details')).toBeInTheDocument();

    // Start Review action button is present for Submitted reports
    expect(screen.getByRole('button', { name: /start review/i })).toBeInTheDocument();

    // Overview Card
    expect(screen.getByText('Report Overview')).toBeInTheDocument();
    expect(screen.getAllByText('Organic').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Sunil Silva').length).toBeGreaterThanOrEqual(1);

    // Description Card
    expect(screen.getByText('Citizen Description')).toBeInTheDocument();
    expect(
      screen.getByText('Overflowing commercial waste container behind market stalls.')
    ).toBeInTheDocument();

    // Incident Location
    expect(screen.getByText('Market Square, Pettah, Colombo 11')).toBeInTheDocument();
    expect(screen.getByText(/Latitude: 6.935100 | Longitude: 79.852100/i)).toBeInTheDocument();
  });

  it('renders real interactive OpenStreetMap map with attribution and marker container', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    const mapRegion = await screen.findByTestId('report-map-container');
    expect(mapRegion).toBeInTheDocument();
    expect(mapRegion).toHaveAttribute('role', 'region');
    expect(mapRegion).toHaveAttribute('aria-label', 'Waste report location map');

    // OpenStreetMap attribution link
    expect(screen.getByText('OpenStreetMap')).toBeInTheDocument();
  });

  it('renders invalid coordinates fallback when latitude or longitude is invalid', async () => {
    const invalidCoordDetail: WasteReportDetailDto = {
      ...mockDetail,
      latitude: NaN,
      longitude: 200, // Invalid longitude > 180
    };
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(invalidCoordDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    expect(await screen.findByTestId('report-map-invalid')).toBeInTheDocument();
    expect(screen.getByText('Invalid Location Coordinates')).toBeInTheDocument();
  });

  it('renders photo gallery with thumbnails, counts, and triggers lightbox modal preview', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    expect(await screen.findByText('Photo Evidence (2)')).toBeInTheDocument();
    expect(screen.getByTestId('photo-thumbnail-0')).toBeInTheDocument();
    expect(screen.getByTestId('photo-thumbnail-1')).toBeInTheDocument();

    // Open lightbox by clicking the first thumbnail
    await user.click(screen.getByTestId('photo-thumbnail-0'));

    // Lightbox modal opens
    const modal = screen.getByRole('dialog', { name: /photo evidence preview/i });
    expect(modal).toBeInTheDocument();
    expect(screen.getByText('Photo 1 of 2')).toBeInTheDocument();

    // Navigate to next photo
    const nextButton = screen.getByRole('button', { name: /next photo/i });
    await user.click(nextButton);
    expect(screen.getByText('Photo 2 of 2')).toBeInTheDocument();

    // Close lightbox via close button
    const closeButton = screen.getByRole('button', { name: /close photo preview/i });
    await user.click(closeButton);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('closes lightbox preview modal on Escape key press', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    await user.click(await screen.findByTestId('photo-thumbnail-0'));
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    // Press Escape
    fireEvent.keyDown(window, { key: 'Escape', code: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('handles broken photo thumbnail gracefully via fallback card', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    const images = await screen.findAllByRole('img');
    const firstPhoto = images.find((img) =>
      img.getAttribute('src')?.includes('photo1.jpg')
    );
    expect(firstPhoto).toBeDefined();

    // Simulate image error
    fireEvent.error(firstPhoto!);

    expect(await screen.findByText('Photo unavailable')).toBeInTheDocument();
  });

  it('renders clean empty state when report has zero attached photos', async () => {
    const noPhotosDetail: WasteReportDetailDto = {
      ...mockDetail,
      attachments: [],
    };
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(noPhotosDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    expect(await screen.findByTestId('no-photo-evidence')).toBeInTheDocument();
    expect(screen.getByText('No Photo Evidence')).toBeInTheDocument();
    expect(
      screen.getByText(/the citizen did not attach photographic evidence/i)
    ).toBeInTheDocument();
  });

  it('renders status history timeline with proper initial transition formatting', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    expect(await screen.findByTestId('status-history-timeline')).toBeInTheDocument();

    // Initial transition: fromStatus is null -> renders "Report Submitted", NEVER "null → Submitted"
    expect(screen.getByText('Report Submitted')).toBeInTheDocument();
    expect(screen.queryByText(/null\s*→/i)).not.toBeInTheDocument();

    // Second transition: Submitted -> UnderReview
    expect(screen.getByTestId('history-step-1')).toBeInTheDocument();

    // Actors
    expect(screen.getByText('Officer Perera')).toBeInTheDocument();

    // Notes
    expect(screen.getByText('Under field assessment review')).toBeInTheDocument();
  });

  it('renders status history failure state with isolated retry button', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new Error('Failed to retrieve audit history')
    );

    renderWithClient(<WasteReportDetailPage />);

    expect(await screen.findByTestId('status-history-error')).toBeInTheDocument();
    expect(screen.getByText('Failed to retrieve audit history')).toBeInTheDocument();

    const retryHistoryBtn = screen.getByRole('button', { name: /retry history/i });
    expect(retryHistoryBtn).toBeInTheDocument();

    // On retry, mock success
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValueOnce(mockHistory);
    await user.click(retryHistoryBtn);

    expect(await screen.findByTestId('status-history-timeline')).toBeInTheDocument();
  });

  it('renders full error alert with Retry when main report fetch fails', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new Error('Waste report not found')
    );
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue([]);

    renderWithClient(<WasteReportDetailPage />);

    expect(await screen.findByTestId('report-detail-error')).toBeInTheDocument();
    expect(screen.getByText('Waste report not found')).toBeInTheDocument();

    // On retry, mock success
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValueOnce(mockDetail);
    const retryBtn = screen.getByRole('button', { name: /retry/i });
    await user.click(retryBtn);

    expect(await screen.findByText('Waste Report Details')).toBeInTheDocument();
  });

  it('STRICT INVARIANT: Start Review is present for Submitted, but NO other mutation buttons (Verify, Reject, Edit, Cancel, Delete, Schedule) exist', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    expect(await screen.findByRole('button', { name: /start review/i })).toBeInTheDocument();

    // Assert strictly no other mutation actions are rendered in this step
    expect(screen.queryByRole('button', { name: /verify/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /reject/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /schedule/i })).not.toBeInTheDocument();
  });

  it('renders Start Review button ONLY for Submitted status, and NOT for any other lifecycle status', async () => {
    const nonSubmittedStatuses = [
      'UnderReview',
      'Verified',
      'Rejected',
      'Scheduled',
      'InProgress',
      'Resolved',
      'Cancelled',
    ] as const;

    for (const status of nonSubmittedStatuses) {
      (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
        ...mockDetail,
        status,
      });
      (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

      const { unmount } = renderWithClient(<WasteReportDetailPage />);

      expect(await screen.findByText('Waste Report Details')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /start review/i })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('handles confirmation modal: Cancel dismisses without calling API, Escape key dismisses modal', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    const startReviewBtn = await screen.findByRole('button', { name: /start review/i });
    await user.click(startReviewBtn);

    // Modal appears
    const modal = screen.getByRole('dialog', { name: /start reviewing this report\?/i });
    expect(modal).toBeInTheDocument();
    expect(screen.getByText('This will move the report to Under Review.')).toBeInTheDocument();

    // Click Cancel
    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelBtn);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(reportingApi.startReview).not.toHaveBeenCalled();

    // Open again and test Escape key
    await user.click(startReviewBtn);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    fireEvent.keyDown(window, { key: 'Escape', code: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(reportingApi.startReview).not.toHaveBeenCalled();
  });

  it('executes Start Review on confirmation: calls API, refetches authoritative detail & history, updates status to UnderReview, and hides button', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const underReviewDetail: WasteReportDetailDto = {
      ...mockDetail,
      status: 'UnderReview',
      updatedAt: '2026-09-17T09:30:00Z',
    };
    const underReviewHistory: WasteReportStatusHistoryDto[] = [
      ...mockHistory,
      {
        id: 'hist-3',
        wasteReportId: mockDetail.id,
        fromStatus: 'Submitted',
        toStatus: 'UnderReview',
        changedByUserId: 'u-officer-1',
        changedByUserName: 'Officer Perera',
        notes: null,
        changedAt: '2026-09-17T09:30:00Z',
      },
    ];

    (reportingApi.startReview as ReturnType<typeof vi.fn>).mockResolvedValue(underReviewDetail);

    renderWithClient(<WasteReportDetailPage />);

    const startReviewBtn = await screen.findByRole('button', { name: /start review/i });
    await user.click(startReviewBtn);

    // Mock next getWasteReport and getWasteReportHistory calls for authoritative refetch
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(underReviewDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(underReviewHistory);

    const confirmBtn = screen.getByTestId('confirm-start-review-button');
    await user.click(confirmBtn);

    // Assert API called
    expect(reportingApi.startReview).toHaveBeenCalledWith(mockDetail.id);

    // Modal closed
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    // Success feedback alert
    expect(await screen.findByText('Review started successfully.')).toBeInTheDocument();

    // Start Review button is gone
    expect(screen.queryByRole('button', { name: /start review/i })).not.toBeInTheDocument();

    // Updated status badge
    expect(screen.getAllByText('Under Review').length).toBeGreaterThanOrEqual(1);
  });

  it('protects against double clicks while Start Review is in flight', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    let resolveApi: (val: any) => void;
    const pendingPromise = new Promise((resolve) => {
      resolveApi = resolve;
    });
    (reportingApi.startReview as ReturnType<typeof vi.fn>).mockReturnValue(pendingPromise);

    renderWithClient(<WasteReportDetailPage />);

    const startReviewBtn = await screen.findByRole('button', { name: /start review/i });
    await user.click(startReviewBtn);

    const confirmBtn = screen.getByTestId('confirm-start-review-button');

    // Rapid double-clicks
    await user.click(confirmBtn);
    await user.click(confirmBtn);
    await user.click(confirmBtn);

    expect(reportingApi.startReview).toHaveBeenCalledTimes(1);

    // Button shows loading spinner / disabled state
    expect(confirmBtn).toBeDisabled();

    // Resolve promise
    await act(async () => {
      resolveApi!({ ...mockDetail, status: 'UnderReview' });
    });
  });

  it('handles 409 Conflict race condition gracefully: displays friendly message and refetches latest data', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const conflictError = new Error('Conflict') as any;
    conflictError.isAxiosError = true;
    conflictError.response = {
      status: 409,
      data: {
        detail: 'Cannot start review. Report is currently UnderReview.',
      },
    };
    (reportingApi.startReview as ReturnType<typeof vi.fn>).mockRejectedValue(conflictError);

    renderWithClient(<WasteReportDetailPage />);

    const startReviewBtn = await screen.findByRole('button', { name: /start review/i });
    await user.click(startReviewBtn);

    // Updated report state when refetched
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });

    const confirmBtn = screen.getByTestId('confirm-start-review-button');
    await user.click(confirmBtn);

    expect(
      await screen.findByText(
        'This report can no longer be started for review because its status has changed.'
      )
    ).toBeInTheDocument();

    // Does not leave modal open
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('handles 403 Forbidden and 500 server errors gracefully without faking success', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const forbiddenError = new Error('Forbidden') as any;
    forbiddenError.isAxiosError = true;
    forbiddenError.response = { status: 403 };
    (reportingApi.startReview as ReturnType<typeof vi.fn>).mockRejectedValueOnce(forbiddenError);

    renderWithClient(<WasteReportDetailPage />);

    const startReviewBtn = await screen.findByRole('button', { name: /start review/i });
    await user.click(startReviewBtn);

    const confirmBtn = screen.getByTestId('confirm-start-review-button');
    await user.click(confirmBtn);

    expect(
      await screen.findByText("You don't have permission to start review for this report.")
    ).toBeInTheDocument();

    // Start Review button remains available for retry
    expect(screen.getByRole('button', { name: /start review/i })).toBeInTheDocument();

    // Test 500 error
    const serverError = new Error('Server Error') as any;
    serverError.isAxiosError = true;
    serverError.response = { status: 500 };
    (reportingApi.startReview as ReturnType<typeof vi.fn>).mockRejectedValueOnce(serverError);

    await user.click(screen.getByRole('button', { name: /start review/i }));
    await user.click(screen.getByTestId('confirm-start-review-button'));

    expect(
      await screen.findByText("Couldn't start the review. Please try again.")
    ).toBeInTheDocument();
  });

  it('renders Verify Report and Reject Report buttons ONLY for UnderReview status, and NOT for any other status', async () => {
    // 1. For UnderReview status: both Verify and Reject buttons must be rendered
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const { unmount } = renderWithClient(<WasteReportDetailPage />);

    expect(await screen.findByTestId('verify-report-button')).toBeInTheDocument();
    expect(screen.getByTestId('reject-report-button')).toBeInTheDocument();
    expect(screen.queryByTestId('start-review-button')).not.toBeInTheDocument();
    unmount();

    // 2. For all other 7 statuses: neither Verify nor Reject should exist
    const nonUnderReviewStatuses = [
      'Submitted',
      'Verified',
      'Rejected',
      'Scheduled',
      'InProgress',
      'Resolved',
      'Cancelled',
    ] as const;

    for (const status of nonUnderReviewStatuses) {
      (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
        ...mockDetail,
        status,
      });
      (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

      const view = renderWithClient(<WasteReportDetailPage />);

      expect(await screen.findByText('Waste Report Details')).toBeInTheDocument();
      expect(screen.queryByTestId('verify-report-button')).not.toBeInTheDocument();
      expect(screen.queryByTestId('reject-report-button')).not.toBeInTheDocument();
      view.unmount();
    }
  });

  it('handles Verify Report modal: Cancel dismisses without calling API, Escape key dismisses modal', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    const verifyBtn = await screen.findByTestId('verify-report-button');
    await user.click(verifyBtn);

    // Modal appears
    const modal = screen.getByRole('dialog', { name: /verify this report\?/i });
    expect(modal).toBeInTheDocument();
    expect(
      screen.getByText('Confirm that the submitted waste report has been reviewed and is valid.')
    ).toBeInTheDocument();

    // Click Cancel
    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelBtn);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(reportingApi.verifyReport).not.toHaveBeenCalled();

    // Open again and test Escape key
    await user.click(verifyBtn);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    fireEvent.keyDown(window, { key: 'Escape', code: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(reportingApi.verifyReport).not.toHaveBeenCalled();
  });

  it('executes Verify Report on confirmation: calls API, refetches authoritative detail & history, updates status to Verified, and hides action buttons', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const verifiedDetail: WasteReportDetailDto = {
      ...mockDetail,
      status: 'Verified',
      verifiedByUserId: 'u-officer-1',
      verifiedByUserName: 'Officer Perera',
      verifiedAt: '2026-09-17T11:45:00Z',
      updatedAt: '2026-09-17T11:45:00Z',
    };
    const verifiedHistory: WasteReportStatusHistoryDto[] = [
      ...mockHistory,
      {
        id: 'hist-3',
        wasteReportId: mockDetail.id,
        fromStatus: 'UnderReview',
        toStatus: 'Verified',
        changedByUserId: 'u-officer-1',
        changedByUserName: 'Officer Perera',
        notes: null,
        changedAt: '2026-09-17T11:45:00Z',
      },
    ];

    (reportingApi.verifyReport as ReturnType<typeof vi.fn>).mockResolvedValue(verifiedDetail);

    renderWithClient(<WasteReportDetailPage />);

    const verifyBtn = await screen.findByTestId('verify-report-button');
    await user.click(verifyBtn);

    // Mock next fetch calls for authoritative refetch
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(verifiedDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(verifiedHistory);

    const confirmBtn = screen.getByTestId('confirm-verify-report-button');
    await user.selectOptions(screen.getByLabelText('Verification priority'), 'High');
    await user.click(confirmBtn);

    // Assert API called
    expect(reportingApi.verifyReport).toHaveBeenCalledWith(mockDetail.id, 'High');

    // Modal closed
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    // Success feedback alert
    expect(await screen.findByText('Report verified successfully.')).toBeInTheDocument();

    // Verify & Reject buttons are gone
    expect(screen.queryByTestId('verify-report-button')).not.toBeInTheDocument();
    expect(screen.queryByTestId('reject-report-button')).not.toBeInTheDocument();

    // Updated status badge
    expect(screen.getAllByText('Verified').length).toBeGreaterThanOrEqual(1);

    // Verification metadata displayed
    expect(screen.getByText(/Verified by/i)).toBeInTheDocument();
    expect(screen.getByText(/Verified by/i)).toHaveTextContent('Officer Perera');
  });

  it('handles Reject Report modal validation: enforces 5-500 character limits, updates character counter, Cancel dismisses without calling API, Escape dismisses', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    const rejectBtn = await screen.findByTestId('reject-report-button');
    await user.click(rejectBtn);

    // Modal opens
    const modal = screen.getByRole('dialog', { name: /reject waste report/i });
    expect(modal).toBeInTheDocument();
    expect(screen.getByText(/Explain why this report is being rejected/i)).toBeInTheDocument();
    expect(screen.getByText('0 / 500')).toBeInTheDocument();

    const textarea = screen.getByTestId('reject-reason-input');
    const confirmBtn = screen.getByTestId('confirm-reject-report-button');

    // 1. Submit empty -> blocked
    await user.click(confirmBtn);
    expect(
      await screen.findByText('Please enter a reason of at least 5 characters.')
    ).toBeInTheDocument();
    expect(reportingApi.rejectReport).not.toHaveBeenCalled();

    // 2. Submit < 5 chars (e.g. 3 chars) -> blocked
    await user.type(textarea, 'abc');
    expect(screen.getByText('3 / 500')).toBeInTheDocument();
    await user.click(confirmBtn);
    expect(
      screen.getByText('Please enter a reason of at least 5 characters.')
    ).toBeInTheDocument();
    expect(reportingApi.rejectReport).not.toHaveBeenCalled();

    // 3. Submit whitespace-only -> blocked
    await user.clear(textarea);
    await user.type(textarea, '        ');
    await user.click(confirmBtn);
    expect(
      screen.getByText('Please enter a reason of at least 5 characters.')
    ).toBeInTheDocument();
    expect(reportingApi.rejectReport).not.toHaveBeenCalled();

    // 4. Cancel dismisses modal
    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelBtn);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(reportingApi.rejectReport).not.toHaveBeenCalled();

    // 5. Reopen and test Escape key dismisses modal
    await user.click(rejectBtn);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    fireEvent.keyDown(window, { key: 'Escape', code: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(reportingApi.rejectReport).not.toHaveBeenCalled();
  });

  it('executes Reject Report on confirmation: calls API with trimmed reason, refetches detail & history, updates status to Rejected, and shows reason in history notes', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const rejectedDetail: WasteReportDetailDto = {
      ...mockDetail,
      status: 'Rejected',
      updatedAt: '2026-09-17T11:50:00Z',
    };
    const rejectedHistory: WasteReportStatusHistoryDto[] = [
      ...mockHistory,
      {
        id: 'hist-3',
        wasteReportId: mockDetail.id,
        fromStatus: 'UnderReview',
        toStatus: 'Rejected',
        changedByUserId: 'u-officer-1',
        changedByUserName: 'Officer Perera',
        notes: 'Photo does not show municipal waste pile; private property.',
        changedAt: '2026-09-17T11:50:00Z',
      },
    ];

    (reportingApi.rejectReport as ReturnType<typeof vi.fn>).mockResolvedValue(rejectedDetail);

    renderWithClient(<WasteReportDetailPage />);

    const rejectBtn = await screen.findByTestId('reject-report-button');
    await user.click(rejectBtn);

    const textarea = screen.getByTestId('reject-reason-input');
    await user.type(textarea, 'Photo does not show municipal waste pile; private property.');

    // Mock next fetch calls for authoritative refetch
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(rejectedDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(rejectedHistory);

    const confirmBtn = screen.getByTestId('confirm-reject-report-button');
    await user.click(confirmBtn);

    // Assert API called with exact reason
    expect(reportingApi.rejectReport).toHaveBeenCalledWith(
      mockDetail.id,
      'Photo does not show municipal waste pile; private property.'
    );

    // Modal closed
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    // Success feedback alert
    expect(await screen.findByText('Report rejected.')).toBeInTheDocument();

    // Verify & Reject buttons are gone
    expect(screen.queryByTestId('verify-report-button')).not.toBeInTheDocument();
    expect(screen.queryByTestId('reject-report-button')).not.toBeInTheDocument();

    // Updated status badge
    expect(screen.getAllByText('Rejected').length).toBeGreaterThanOrEqual(1);

    // Rejection reason rendered in status history notes
    expect(
      screen.getByText('Photo does not show municipal waste pile; private property.')
    ).toBeInTheDocument();
  });

  it('protects against double clicks while Verify or Reject is in flight', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    let resolveVerify: (val: any) => void;
    const pendingPromise = new Promise((resolve) => {
      resolveVerify = resolve;
    });
    (reportingApi.verifyReport as ReturnType<typeof vi.fn>).mockReturnValue(pendingPromise);

    renderWithClient(<WasteReportDetailPage />);

    const verifyBtn = await screen.findByTestId('verify-report-button');
    await user.click(verifyBtn);

    await user.selectOptions(screen.getByLabelText(/verification priority/i), 'High');
    const confirmBtn = screen.getByTestId('confirm-verify-report-button');
    await user.selectOptions(screen.getByLabelText('Verification priority'), 'High');

    // Rapid double-clicks
    await user.click(confirmBtn);
    await user.click(confirmBtn);
    await user.click(confirmBtn);

    expect(reportingApi.verifyReport).toHaveBeenCalledTimes(1);
    expect(reportingApi.verifyReport).toHaveBeenCalledWith(mockDetail.id, 'High');
    expect(confirmBtn).toBeDisabled();

    // Resolve
    await act(async () => {
      resolveVerify!({ ...mockDetail, status: 'Verified' });
    });
  });

  it('handles 409 Conflict race condition for Verify Report: displays friendly message and refetches latest state', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const conflictError = new Error('Conflict') as any;
    conflictError.isAxiosError = true;
    conflictError.response = {
      status: 409,
      data: {
        detail: 'Cannot verify report. Report is currently Cancelled.',
      },
    };
    (reportingApi.verifyReport as ReturnType<typeof vi.fn>).mockRejectedValue(conflictError);

    renderWithClient(<WasteReportDetailPage />);

    const verifyBtn = await screen.findByTestId('verify-report-button');
    await user.click(verifyBtn);

    // Updated report state when refetched
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'Cancelled',
    });

    await user.selectOptions(screen.getByLabelText(/verification priority/i), 'Low');
    const confirmBtn = screen.getByTestId('confirm-verify-report-button');
    await user.selectOptions(screen.getByLabelText('Verification priority'), 'High');
    await user.click(confirmBtn);

    expect(
      await screen.findByText(
        'This report can no longer be updated because its status has changed.'
      )
    ).toBeInTheDocument();

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('handles 409 Conflict race condition for Reject Report: displays friendly message and refetches latest state', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const conflictError = new Error('Conflict') as any;
    conflictError.isAxiosError = true;
    conflictError.response = {
      status: 409,
      data: {
        detail: 'Cannot reject report. Report is currently Cancelled.',
      },
    };
    (reportingApi.rejectReport as ReturnType<typeof vi.fn>).mockRejectedValue(conflictError);

    renderWithClient(<WasteReportDetailPage />);

    const rejectBtn = await screen.findByTestId('reject-report-button');
    await user.click(rejectBtn);

    const textarea = screen.getByTestId('reject-reason-input');
    await user.type(textarea, 'Report is not in municipal area.');

    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'Cancelled',
    });

    const confirmBtn = screen.getByTestId('confirm-reject-report-button');
    await user.click(confirmBtn);

    expect(
      await screen.findByText(
        'This report can no longer be updated because its status has changed.'
      )
    ).toBeInTheDocument();

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('handles 403 Forbidden and 500 server errors on Verify Report without faking success', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    const forbiddenError = new Error('Forbidden') as any;
    forbiddenError.isAxiosError = true;
    forbiddenError.response = { status: 403 };
    (reportingApi.verifyReport as ReturnType<typeof vi.fn>).mockRejectedValueOnce(forbiddenError);

    renderWithClient(<WasteReportDetailPage />);

    const verifyBtn = await screen.findByTestId('verify-report-button');
    await user.click(verifyBtn);

    await user.selectOptions(screen.getByLabelText(/verification priority/i), 'Urgent');
    const confirmBtn = screen.getByTestId('confirm-verify-report-button');
    await user.selectOptions(screen.getByLabelText('Verification priority'), 'High');
    await user.click(confirmBtn);

    expect(
      await screen.findByText("You don't have permission to review this report.")
    ).toBeInTheDocument();

    // Verify button remains available for retry
    expect(screen.getByTestId('verify-report-button')).toBeInTheDocument();

    // Test 500 error
    const serverError = new Error('Server Error') as any;
    serverError.isAxiosError = true;
    serverError.response = { status: 500 };
    (reportingApi.verifyReport as ReturnType<typeof vi.fn>).mockRejectedValueOnce(serverError);

    await user.click(screen.getByTestId('verify-report-button'));
    await user.selectOptions(screen.getByLabelText(/verification priority/i), 'Urgent');
    await user.click(screen.getByTestId('confirm-verify-report-button'));

    expect(
      await screen.findByText("Couldn't verify this report. Please try again.")
    ).toBeInTheDocument();
  });

  it('handles 403 Forbidden and 500 server errors on Reject Report, and retains entered rejection reason when reopened', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderWithClient(<WasteReportDetailPage />);

    const rejectBtn = await screen.findByTestId('reject-report-button');
    await user.click(rejectBtn);

    const textarea = screen.getByTestId('reject-reason-input');
    await user.type(textarea, 'Preserved rejection reasoning text');

    // Simulate 500 server error
    const serverError = new Error('Server Error') as any;
    serverError.isAxiosError = true;
    serverError.response = { status: 500 };
    (reportingApi.rejectReport as ReturnType<typeof vi.fn>).mockRejectedValueOnce(serverError);

    const confirmBtn = screen.getByTestId('confirm-reject-report-button');
    await user.click(confirmBtn);

    expect(
      await screen.findByText("Couldn't reject this report. Please try again.")
    ).toBeInTheDocument();

    // Reopen modal to verify entered reason was NOT wiped
    await user.click(screen.getByTestId('reject-report-button'));
    const reopenedTextarea = screen.getByTestId('reject-reason-input');
    expect(reopenedTextarea).toHaveValue('Preserved rejection reasoning text');
  });
});

describe('WasteReportDetailPage Role Access Guards', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('allows authorized WasteOfficer to access /officer/waste-reports/:id', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

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
      <MemoryRouter initialEntries={['/officer/waste-reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByText('Waste Report Details')).toBeInTheDocument();
  });

  it('allows authorized WasteOfficer to access /officer/reports/:id alias', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

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
      <MemoryRouter initialEntries={['/officer/reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByText('Waste Report Details')).toBeInTheDocument();
  });

  it('blocks Citizen from accessing /officer/waste-reports/:id and redirects to /unauthorized', async () => {
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
      <MemoryRouter initialEntries={['/officer/waste-reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('blocks Driver from accessing /officer/waste-reports/:id and redirects to /unauthorized', async () => {
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
      <MemoryRouter initialEntries={['/officer/waste-reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('blocks MunicipalManager from accessing /officer/waste-reports/:id and redirects to /unauthorized', async () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'manager-token',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Perera',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/officer/waste-reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('allows authorized MunicipalManager to access /manager/reports/:id', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'manager-token',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Perera',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByText('Waste Report Details')).toBeInTheDocument();
  });

  it('allows authorized MunicipalManager to access /manager/waste-reports/:id alias', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'manager-token',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Perera',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/waste-reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByText('Waste Report Details')).toBeInTheDocument();
  });

  it('blocks WasteOfficer from accessing /manager/reports/:id and redirects to /unauthorized', async () => {
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
      <MemoryRouter initialEntries={['/manager/reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('blocks Citizen from accessing /manager/reports/:id and redirects to /unauthorized', async () => {
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
      <MemoryRouter initialEntries={['/manager/reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });

  it('blocks Driver from accessing /manager/reports/:id and redirects to /unauthorized', async () => {
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
      <MemoryRouter initialEntries={['/manager/reports/11111111-2222-3333-4444-555555555555']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /access denied/i })).toBeInTheDocument();
  });
});

describe('WasteReportDetailPage (MunicipalManager Review Context)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'test-manager-token',
      user: {
        id: 'u-manager-1',
        fullName: 'Manager Kavindi',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });
  });

  it('renders complete report details, citizen info, coordinates, and back link to /manager/reports', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderManagerWithClient(<WasteReportDetailPage />);

    // Short reference and title
    expect(await screen.findByText('Report 11111111')).toBeInTheDocument();
    expect(screen.getByText('Waste Report Details')).toBeInTheDocument();

    // Citizen and description
    expect(screen.getAllByText('Sunil Silva').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Overflowing commercial waste container behind market stalls.')).toBeInTheDocument();

    // Coordinates and map
    expect(screen.getByText('Market Square, Pettah, Colombo 11')).toBeInTheDocument();
    expect(screen.getByText(/Latitude: 6.935100 | Longitude: 79.852100/i)).toBeInTheDocument();
    expect(screen.getByTestId('report-map-container')).toBeInTheDocument();

    // Back link points to /manager/reports
    const backLink = screen.getByTestId('back-to-reports-link');
    expect(backLink).toHaveAttribute('href', '/manager/reports');
  });

  it('renders photo gallery and supports modal lightbox preview for MunicipalManager', async () => {
    const user = userEvent.setup();
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderManagerWithClient(<WasteReportDetailPage />);

    expect(await screen.findByText('Photo Evidence (2)')).toBeInTheDocument();
    expect(screen.getByTestId('photo-thumbnail-0')).toBeInTheDocument();
    expect(screen.getByTestId('photo-thumbnail-1')).toBeInTheDocument();

    // Open lightbox modal
    await user.click(screen.getByTestId('photo-thumbnail-0'));
    const modal = screen.getByRole('dialog', { name: /photo evidence preview/i });
    expect(modal).toBeInTheDocument();
    expect(screen.getByText('Photo 1 of 2')).toBeInTheDocument();

    // Close lightbox modal
    const closeBtn = screen.getByRole('button', { name: /close photo preview/i });
    await user.click(closeBtn);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders status history timeline with all audit records for MunicipalManager', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue(mockDetail);
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderManagerWithClient(<WasteReportDetailPage />);

    expect(await screen.findByTestId('status-history-timeline')).toBeInTheDocument();
    expect(screen.getByText('Status History')).toBeInTheDocument();
    expect(screen.getByText('Citizen submitted report with 2 photos')).toBeInTheDocument();
    expect(screen.getByText('Under field assessment review')).toBeInTheDocument();
  });

  it('offers Start Review to MunicipalManager for a Submitted report', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'Submitted',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderManagerWithClient(<WasteReportDetailPage />);

    await screen.findByText('Report 11111111');

    expect(screen.getByTestId('start-review-button')).toBeInTheDocument();
    expect(screen.queryByTestId('verify-report-button')).toBeNull();
    expect(screen.queryByTestId('reject-report-button')).toBeNull();

    // No action modals
    expect(screen.queryByTestId('start-review-modal')).toBeNull();
    expect(screen.queryByTestId('verify-report-modal')).toBeNull();
    expect(screen.queryByTestId('reject-report-modal')).toBeNull();

  });

  it('offers Verify and Reject to MunicipalManager for an UnderReview report', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
      ...mockDetail,
      status: 'UnderReview',
    });
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

    renderManagerWithClient(<WasteReportDetailPage />);

    await screen.findByText('Report 11111111');

    expect(screen.queryByTestId('start-review-button')).toBeNull();
    expect(screen.getByTestId('verify-report-button')).toBeInTheDocument();
    expect(screen.getByTestId('reject-report-button')).toBeInTheDocument();

    // Displays Under Review badge
    expect(screen.getAllByText('Under Review').length).toBeGreaterThanOrEqual(1);
  });

  const nonActionableStatuses: WasteReportStatus[] = [
    'Verified',
    'Rejected',
    'Scheduled',
    'InProgress',
    'Resolved',
    'Cancelled',
  ];

  nonActionableStatuses.forEach((status) => {
    it(`does NOT render any mutation buttons or controls for ${status} status`, async () => {
      (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockResolvedValue({
        ...mockDetail,
        status,
      });
      (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue(mockHistory);

      renderManagerWithClient(<WasteReportDetailPage />);

      await screen.findByText('Report 11111111');

      expect(screen.queryByTestId('start-review-button')).toBeNull();
      expect(screen.queryByTestId('verify-report-button')).toBeNull();
      expect(screen.queryByTestId('reject-report-button')).toBeNull();
      expect(screen.queryByTestId('start-review-modal')).toBeNull();
      expect(screen.queryByTestId('verify-report-modal')).toBeNull();
      expect(screen.queryByTestId('reject-report-modal')).toBeNull();
    });
  });

  it('renders return to list button pointing to /manager/reports on not found / error state', async () => {
    (reportingApi.getWasteReport as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Waste report not found')
    );
    (reportingApi.getWasteReportHistory as ReturnType<typeof vi.fn>).mockResolvedValue([]);

    renderManagerWithClient(<WasteReportDetailPage />);

    expect(await screen.findByTestId('report-detail-error')).toBeInTheDocument();
    const backLink = screen.getByTestId('back-to-reports-link');
    expect(backLink).toHaveAttribute('href', '/manager/reports');
  });
});
