import React from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ComplaintDetailPage } from './ComplaintDetailPage';
import { complaintsApi } from '../api/complaintsApi';
import { useAuthStore } from '../../../store/authStore';
import type { ComplaintDetailDto } from '../types/complaints';

vi.mock('leaflet', () => ({
  default: {
    divIcon: vi.fn((options) => ({ options })),
  },
}));

vi.mock('react-leaflet', () => ({
  MapContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="leaflet-map-container">{children}</div>
  ),
  TileLayer: () => <div data-testid="tile-layer" />,
  Marker: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="map-marker">{children}</div>
  ),
  Popup: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="map-popup">{children}</div>
  ),
}));

vi.mock('../api/complaintsApi', () => ({
  complaintsApi: {
    getComplaint: vi.fn(),
    startReview: vi.fn(),
    resolveComplaint: vi.fn(),
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
  initialEntries: string[] = ['/officer/complaints/11111111-1111-1111-1111-111111111111']
) {
  const testClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={testClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path="/officer/complaints/:id" element={ui} />
          <Route path="/officer/complaints" element={<div>Complaints List Page</div>} />
          <Route path="/manager/complaints/:id" element={ui} />
          <Route path="/manager/complaints" element={<div>Manager Complaints List Page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

const mockSubmittedComplaint: ComplaintDetailDto = {
  id: '11111111-1111-1111-1111-111111111111',
  citizenId: 'c1',
  citizenName: 'Sunil Silva',
  category: 'MissedCollection',
  subject: 'Dumpster on 5th Lane was skipped',
  description: 'The green waste bin outside house 42 was left uncollected this morning.',
  latitude: 6.9271,
  longitude: 79.8612,
  locationDescription: 'In front of gate 42',
  status: 'Submitted',
  resolutionNote: null,
  resolvedAt: null,
  resolvedByUserId: null,
  resolvedByUserName: null,
  createdAt: '2026-09-20T08:30:00Z',
  updatedAt: null,
};

const mockInReviewComplaint: ComplaintDetailDto = {
  ...mockSubmittedComplaint,
  id: '22222222-2222-2222-2222-222222222222',
  status: 'InReview',
  latitude: null,
  longitude: null,
  locationDescription: null,
};

const mockResolvedComplaint: ComplaintDetailDto = {
  ...mockSubmittedComplaint,
  id: '33333333-3333-3333-3333-333333333333',
  status: 'Resolved',
  resolutionNote: 'Driver route reassigned and missed organic bin emptied on afternoon pickup.',
  resolvedAt: '2026-09-21T15:00:00Z',
  resolvedByUserId: 'u1',
  resolvedByUserName: 'Officer Bandara',
};

describe('ComplaintDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('renders loading skeleton state', () => {
    (complaintsApi.getComplaint as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {})
    );

    renderWithClient(<ComplaintDetailPage />);

    expect(screen.getByTestId('complaint-detail-loading')).toBeInTheDocument();
  });

  it('renders error alert when complaint query fails', async () => {
    (complaintsApi.getComplaint as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Complaint not found')
    );

    renderWithClient(<ComplaintDetailPage />);

    await waitFor(() => {
      expect(screen.getByTestId('complaint-detail-error')).toBeInTheDocument();
    });

    expect(screen.getByText('Failed to load complaint details')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('renders full complaint details with OpenStreetMap when coordinates are present', async () => {
    (complaintsApi.getComplaint as ReturnType<typeof vi.fn>).mockResolvedValue(
      mockSubmittedComplaint
    );

    renderWithClient(<ComplaintDetailPage />);

    await waitFor(() => {
      expect(screen.getByTestId('complaint-detail-page')).toBeInTheDocument();
    });

    expect(
      screen.getByRole('heading', { name: 'Dumpster on 5th Lane was skipped' })
    ).toBeInTheDocument();
    expect(screen.getByText('Sunil Silva')).toBeInTheDocument();
    expect(screen.getAllByText('Missed Collection').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByTestId('complaint-status-badge')).toHaveTextContent('Submitted');
    expect(
      screen.getByText('The green waste bin outside house 42 was left uncollected this morning.')
    ).toBeInTheDocument();

    // Map container & location note
    expect(screen.getByTestId('complaint-map-container')).toBeInTheDocument();
    expect(screen.getAllByText('In front of gate 42').length).toBeGreaterThanOrEqual(1);
  });

  it('renders clean no-location placeholder when coordinates are absent', async () => {
    (complaintsApi.getComplaint as ReturnType<typeof vi.fn>).mockResolvedValue(
      mockInReviewComplaint
    );

    renderWithClient(
      <ComplaintDetailPage />,
      ['/officer/complaints/22222222-2222-2222-2222-222222222222']
    );

    await waitFor(() => {
      expect(screen.getByTestId('complaint-detail-page')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('complaint-map-container')).not.toBeInTheDocument();
    expect(screen.getByTestId('complaint-no-location')).toBeInTheDocument();
    expect(
      screen.getByText('No location was provided with this complaint.')
    ).toBeInTheDocument();
  });

  describe('Lifecycle Action: Submitted -> Start Review', () => {
    it('shows Start Review button for Submitted complaint and triggers mutation on click', async () => {
      (complaintsApi.getComplaint as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockSubmittedComplaint
      );
      (complaintsApi.startReview as ReturnType<typeof vi.fn>).mockResolvedValue({
        ...mockSubmittedComplaint,
        status: 'InReview',
      });

      renderWithClient(<ComplaintDetailPage />);

      await waitFor(() => {
        expect(screen.getByTestId('start-review-button')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('start-review-button'));

      await waitFor(() => {
        expect(complaintsApi.startReview).toHaveBeenCalledWith(
          '11111111-1111-1111-1111-111111111111'
        );
      });
    });
  });

  describe('Lifecycle Action: InReview -> Resolve Complaint', () => {
    it('shows Resolve Complaint button for InReview complaint, opens modal, and confirms resolution', async () => {
      const user = userEvent.setup();
      (complaintsApi.getComplaint as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockInReviewComplaint
      );
      (complaintsApi.resolveComplaint as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockResolvedComplaint
      );

      renderWithClient(
        <ComplaintDetailPage />,
        ['/officer/complaints/22222222-2222-2222-2222-222222222222']
      );

      await waitFor(() => {
        expect(screen.getByTestId('resolve-complaint-button')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('resolve-complaint-button'));

      expect(screen.getByRole('dialog')).toBeInTheDocument();

      const input = screen.getByTestId('resolution-note-input');
      await user.type(
        input,
        'Driver route reassigned and missed organic bin emptied on afternoon pickup.'
      );

      fireEvent.click(screen.getByTestId('confirm-resolve-complaint-button'));

      await waitFor(() => {
        expect(complaintsApi.resolveComplaint).toHaveBeenCalledWith(
          '22222222-2222-2222-2222-222222222222',
          'Driver route reassigned and missed organic bin emptied on afternoon pickup.'
        );
      });
    });
  });

  describe('Lifecycle: Resolved State', () => {
    it('renders read-only state for Resolved complaint with resolution details and no mutation controls', async () => {
      (complaintsApi.getComplaint as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockResolvedComplaint
      );

      renderWithClient(
        <ComplaintDetailPage />,
        ['/officer/complaints/33333333-3333-3333-3333-333333333333']
      );

      await waitFor(() => {
        expect(screen.getByTestId('complaint-detail-page')).toBeInTheDocument();
      });

      expect(screen.getByTestId('resolved-readonly-state')).toBeInTheDocument();
      expect(screen.getByText('Complaint Resolved')).toBeInTheDocument();
      expect(screen.getByTestId('resolution-note-display')).toHaveTextContent(
        'Driver route reassigned and missed organic bin emptied on afternoon pickup.'
      );
      expect(screen.getByTestId('resolved-by')).toHaveTextContent('Officer Bandara');

      // No mutation buttons should exist
      expect(screen.queryByTestId('start-review-button')).not.toBeInTheDocument();
      expect(screen.queryByTestId('resolve-complaint-button')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /reopen/i })).not.toBeInTheDocument();
    });
  });

  describe('Role-based navigation', () => {
    it('sets back link to /manager/complaints when logged in as MunicipalManager', async () => {
      useAuthStore.setState({
        user: {
          id: 'mgr-1',
          fullName: 'Manager Silva',
          email: 'manager@smartwaste.local',
          role: 'MunicipalManager',
          mustChangePassword: false,
        },
        accessToken: 'test-token',
        isAuthenticated: true,
        isLoading: false,
      });

      (complaintsApi.getComplaint as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockSubmittedComplaint
      );

      renderWithClient(
        <ComplaintDetailPage />,
        ['/manager/complaints/11111111-1111-1111-1111-111111111111']
      );

      await waitFor(() => {
        expect(screen.getByTestId('back-to-complaints-link')).toBeInTheDocument();
      });

      expect(screen.getByTestId('back-to-complaints-link')).toHaveAttribute(
        'href',
        '/manager/complaints'
      );
    });
  });
});
