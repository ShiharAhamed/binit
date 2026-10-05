import React from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { OperationalIssueDetailPage } from './OperationalIssueDetailPage';
import { operationsApi } from '../api/operationsApi';
import { useAuthStore } from '../../../store/authStore';
import type { OperationalIssueDetailDto } from '../types/operations';

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

vi.mock('../api/operationsApi', () => ({
  operationsApi: {
    getOperationalIssue: vi.fn(),
    startReview: vi.fn(),
    resolveOperationalIssue: vi.fn(),
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
  initialEntries: string[] = ['/officer/operations/11111111-1111-1111-1111-111111111111']
) {
  const testClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={testClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path="/officer/operations/:id" element={ui} />
          <Route path="/officer/operations" element={<div>Operations List Page</div>} />
          <Route path="/manager/operations/:id" element={ui} />
          <Route path="/manager/operations" element={<div>Manager Operations List Page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

const mockReportedIssue: OperationalIssueDetailDto = {
  id: '11111111-1111-1111-1111-111111111111',
  driverId: 'd1',
  driverName: 'Kamal Gunaratne',
  issueType: 'VehicleProblem',
  title: 'Compactor ram jammed on route 4',
  description: 'The hydraulic compactor plate jammed midway through collection on Havelock Road.',
  latitude: 6.9271,
  longitude: 79.8612,
  locationDescription: 'Opposite Havelock City Gate 2',
  status: 'Reported',
  resolutionNote: null,
  resolvedAt: null,
  resolvedByUserId: null,
  resolvedByUserName: null,
  createdAt: '2026-09-20T08:30:00Z',
  updatedAt: null,
};

const mockInReviewIssue: OperationalIssueDetailDto = {
  ...mockReportedIssue,
  id: '22222222-2222-2222-2222-222222222222',
  status: 'InReview',
  latitude: null,
  longitude: null,
  locationDescription: null,
};

const mockResolvedIssue: OperationalIssueDetailDto = {
  ...mockReportedIssue,
  id: '33333333-3333-3333-3333-333333333333',
  status: 'Resolved',
  resolutionNote: 'Mechanic team dispatched on-site; cleared hydraulic obstruction and tested compaction cycle.',
  resolvedAt: '2026-09-21T15:00:00Z',
  resolvedByUserId: 'u1',
  resolvedByUserName: 'Officer Bandara',
};

describe('OperationalIssueDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('renders loading skeleton state', () => {
    (operationsApi.getOperationalIssue as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {})
    );

    renderWithClient(<OperationalIssueDetailPage />);

    expect(screen.getByTestId('operational-issue-detail-loading')).toBeInTheDocument();
  });

  it('renders error alert when operational issue query fails', async () => {
    (operationsApi.getOperationalIssue as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Operational issue not found')
    );

    renderWithClient(<OperationalIssueDetailPage />);

    await waitFor(() => {
      expect(screen.getByTestId('operational-issue-detail-error')).toBeInTheDocument();
    });

    expect(screen.getByText('Failed to load operational issue details')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('renders full operational issue details with OpenStreetMap when coordinates are present', async () => {
    (operationsApi.getOperationalIssue as ReturnType<typeof vi.fn>).mockResolvedValue(
      mockReportedIssue
    );

    renderWithClient(<OperationalIssueDetailPage />);

    await waitFor(() => {
      expect(screen.getByTestId('operational-issue-detail-page')).toBeInTheDocument();
    });

    expect(
      screen.getByRole('heading', { name: 'Compactor ram jammed on route 4' })
    ).toBeInTheDocument();
    expect(screen.getByText('Kamal Gunaratne')).toBeInTheDocument();
    expect(screen.getAllByText('Vehicle Problem').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByTestId('operational-issue-status-badge')).toHaveTextContent('Reported');
    expect(
      screen.getByText('The hydraulic compactor plate jammed midway through collection on Havelock Road.')
    ).toBeInTheDocument();

    // Map container & location note
    expect(screen.getByTestId('operational-issue-map-container')).toBeInTheDocument();
    expect(screen.getAllByText('Opposite Havelock City Gate 2').length).toBeGreaterThanOrEqual(1);
  });

  it('renders clean no-location placeholder when coordinates are absent', async () => {
    (operationsApi.getOperationalIssue as ReturnType<typeof vi.fn>).mockResolvedValue(
      mockInReviewIssue
    );

    renderWithClient(
      <OperationalIssueDetailPage />,
      ['/officer/operations/22222222-2222-2222-2222-222222222222']
    );

    await waitFor(() => {
      expect(screen.getByTestId('operational-issue-detail-page')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('operational-issue-map-container')).not.toBeInTheDocument();
    expect(screen.getByTestId('operational-issue-no-location')).toBeInTheDocument();
    expect(
      screen.getByText('No location was provided for this issue.')
    ).toBeInTheDocument();
  });

  describe('Lifecycle Action: Reported -> Start Review', () => {
    it('shows Start Review button for Reported issue and triggers mutation on click', async () => {
      (operationsApi.getOperationalIssue as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockReportedIssue
      );
      (operationsApi.startReview as ReturnType<typeof vi.fn>).mockResolvedValue({
        ...mockReportedIssue,
        status: 'InReview',
      });

      renderWithClient(<OperationalIssueDetailPage />);

      await waitFor(() => {
        expect(screen.getByTestId('start-review-button')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('start-review-button'));

      await waitFor(() => {
        expect(operationsApi.startReview).toHaveBeenCalledWith(
          '11111111-1111-1111-1111-111111111111'
        );
      });
    });
  });

  describe('Lifecycle Action: InReview -> Resolve Issue', () => {
    it('shows Resolve Issue button for InReview issue, opens modal, and confirms resolution', async () => {
      const user = userEvent.setup();
      (operationsApi.getOperationalIssue as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockInReviewIssue
      );
      (operationsApi.resolveOperationalIssue as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockResolvedIssue
      );

      renderWithClient(
        <OperationalIssueDetailPage />,
        ['/officer/operations/22222222-2222-2222-2222-222222222222']
      );

      await waitFor(() => {
        expect(screen.getByTestId('resolve-operational-issue-button')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('resolve-operational-issue-button'));

      expect(screen.getByRole('dialog')).toBeInTheDocument();

      const input = screen.getByTestId('resolution-note-input');
      await user.type(
        input,
        'Mechanic team dispatched on-site; cleared hydraulic obstruction and tested compaction cycle.'
      );

      fireEvent.click(screen.getByTestId('confirm-resolve-operational-issue-button'));

      await waitFor(() => {
        expect(operationsApi.resolveOperationalIssue).toHaveBeenCalledWith(
          '22222222-2222-2222-2222-222222222222',
          'Mechanic team dispatched on-site; cleared hydraulic obstruction and tested compaction cycle.'
        );
      });
    });
  });

  describe('Lifecycle: Resolved State', () => {
    it('renders read-only state for Resolved issue with resolution details and no mutation controls', async () => {
      (operationsApi.getOperationalIssue as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockResolvedIssue
      );

      renderWithClient(
        <OperationalIssueDetailPage />,
        ['/officer/operations/33333333-3333-3333-3333-333333333333']
      );

      await waitFor(() => {
        expect(screen.getByTestId('operational-issue-detail-page')).toBeInTheDocument();
      });

      expect(screen.getByTestId('resolved-readonly-state')).toBeInTheDocument();
      expect(screen.getByText('Issue Resolved')).toBeInTheDocument();
      expect(screen.getByTestId('resolution-note-display')).toHaveTextContent(
        'Mechanic team dispatched on-site; cleared hydraulic obstruction and tested compaction cycle.'
      );
      expect(screen.getByTestId('resolved-by')).toHaveTextContent('Officer Bandara');

      // No mutation buttons should exist
      expect(screen.queryByTestId('start-review-button')).not.toBeInTheDocument();
      expect(screen.queryByTestId('resolve-operational-issue-button')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /reopen/i })).not.toBeInTheDocument();
    });
  });

  describe('Role-based navigation', () => {
    it('sets back link to /manager/operations when logged in as MunicipalManager', async () => {
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

      (operationsApi.getOperationalIssue as ReturnType<typeof vi.fn>).mockResolvedValue(
        mockReportedIssue
      );

      renderWithClient(
        <OperationalIssueDetailPage />,
        ['/manager/operations/11111111-1111-1111-1111-111111111111']
      );

      await waitFor(() => {
        expect(screen.getByTestId('back-to-operations-link')).toBeInTheDocument();
      });

      expect(screen.getByTestId('back-to-operations-link')).toHaveAttribute(
        'href',
        '/manager/operations'
      );
    });
  });
});
