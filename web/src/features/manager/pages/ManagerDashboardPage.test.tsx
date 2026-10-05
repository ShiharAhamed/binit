import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AppRoutes } from '../../../routes/AppRoutes';
import { useAuthStore } from '../../../store/authStore';
import { authApi } from '../../../api/authApi';

// Mock authApi for login testing
vi.mock('../../../api/authApi', () => ({
  authApi: {
    login: vi.fn(),
  },
}));

vi.mock('../../../features/ai-approvals/api/agentWorkflowApi', () => ({
  agentWorkflowApi: {
    listWorkflows: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }),
  },
}));

vi.mock('../api/dashboardApi', () => ({
  managerDashboardApi: {
    getNeedsAttention: vi.fn().mockResolvedValue([]),
    getOverview: vi.fn().mockResolvedValue({
      aiWorkflowsAwaitingApproval: 6,
      activeCollectionAssignments: 4,
      availableVehicles: 8,
      openOperationalIncidents: 2,
      unresolvedComplaints: 5,
    }),
  },
}));

describe('MunicipalManager Dashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('renders Municipal Manager Dashboard and greeting for authorized MunicipalManager', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Dashboard title and greeting
    expect(screen.getAllByText('Municipal Manager Dashboard').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/welcome back,/i)).toBeInTheDocument();
    expect(screen.getAllByText('Kavindi Silva').length).toBeGreaterThanOrEqual(1);
  });

  it('renders all Management Overview KPI cards', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Verify all 5 management overview cards
    expect(screen.getByText('AI Workflows Awaiting Approval')).toBeInTheDocument();
    expect(screen.getByText('Active Collection Assignments')).toBeInTheDocument();
    expect(screen.getByText('Available Vehicles')).toBeInTheDocument();
    expect(screen.getByText('Open Operational Incidents')).toBeInTheDocument();
    expect(screen.getByText('Unresolved Complaints')).toBeInTheDocument();

  });

  it('renders AI Workflows Awaiting Approval as the first overview card in DOM order', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    const aiApprovalCard = screen.getByText('AI Workflows Awaiting Approval');
    const fleetCard = screen.getByText('Active Collection Assignments');
    expect(aiApprovalCard.compareDocumentPosition(fleetCard)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING
    );
  });

  it('renders Quick Actions with links to municipal management modules', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: 'Quick Actions' })).toBeInTheDocument();

    expect(screen.getByRole('link', { name: /review ai approvals/i })).toHaveAttribute(
      'href',
      '/manager/ai-approvals'
    );
    expect(screen.getByRole('link', { name: /view fleet & routes/i })).toHaveAttribute(
      'href',
      '/manager/fleet'
    );
    expect(screen.getByRole('link', { name: /monitor operations/i })).toHaveAttribute(
      'href',
      '/manager/operations'
    );
    expect(screen.queryByRole('link', { name: /analytics/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /audit logs/i })).not.toBeInTheDocument();
  });

  it('renders sidebar navigation links matching the manager specification', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    const nav = screen.getByRole('navigation', { name: /main navigation/i });
    expect(nav).toBeInTheDocument();

    // Verify links in sidebar
    expect(screen.getAllByRole('link', { name: /dashboard/i }).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByRole('link', { name: /waste reports/i }).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByRole('link', { name: /waste reports/i })).toHaveAttribute(
      'href',
      '/manager/reports'
    );
    expect(screen.getAllByRole('link', { name: /ai approvals/i }).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByRole('link', { name: /fleet & routes/i }).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByRole('link', { name: /operations/i }).length).toBeGreaterThanOrEqual(1);
    expect(nav.querySelector('a[href="/manager/analytics"]')).not.toBeInTheDocument();
    expect(nav.querySelector('a[href="/manager/audit"]')).not.toBeInTheDocument();

    // Sidebar should NOT contain WasteOfficer items
    expect(screen.queryByRole('link', { name: /bin management/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /collection schedules/i })).not.toBeInTheDocument();
  });

  it('renders the real Needs Attention empty state', async () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: 'Needs Attention' })).toBeInTheDocument();
    expect(await screen.findByText('All Review Queues Clear')).toBeInTheDocument();
    expect(screen.getByText('No waste reports or complaints are currently awaiting initial review.')).toBeInTheDocument();
  });

  it('allows MunicipalManager to logout via top-right header account menu', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Open account dropdown
    const accountTrigger = screen.getByRole('button', { name: /user account menu/i });
    expect(accountTrigger).toHaveAttribute('aria-expanded', 'false');
    await user.click(accountTrigger);
    expect(accountTrigger).toHaveAttribute('aria-expanded', 'true');

    // Displays role badge
    expect(screen.getAllByText('Municipal Manager').length).toBeGreaterThanOrEqual(1);

    // Click logout
    const logoutBtn = screen.getByRole('menuitem', { name: /logout/i });
    await user.click(logoutBtn);

    const storeState = useAuthStore.getState();
    expect(storeState.isAuthenticated).toBe(false);
    expect(storeState.user).toBeNull();
  });

  it('routes MunicipalManager directly to /manager/dashboard upon successful login', async () => {
    const user = userEvent.setup();
    const mockManagerResponse = {
      accessToken: 'jwt-manager-token',
      expiresAt: '2026-09-09T18:00:00Z',
      user: {
        id: 'manager-789',
        fullName: 'Anura Bandara',
        email: 'anura.manager@example.com',
        role: 'MunicipalManager',
      },
    };

    vi.mocked(authApi.login).mockResolvedValueOnce(mockManagerResponse);

    render(
      <MemoryRouter initialEntries={['/login']}>
        <AppRoutes />
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/email address/i), 'anura.manager@example.com');
    await user.type(screen.getByLabelText(/password/i), 'Manager123!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(authApi.login).toHaveBeenCalledWith({
        email: 'anura.manager@example.com',
        password: 'Manager123!',
        clientType: 'web',
      });
    });

    // Directly renders Municipal Manager Dashboard
    expect(await screen.findByText(/welcome back,/i)).toBeInTheDocument();
    expect(screen.getAllByText('Anura Bandara').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Municipal Manager Dashboard').length).toBeGreaterThanOrEqual(1);

    // Generic HomePage elements must NOT be displayed
    expect(screen.queryByText('Smart Waste Management System')).not.toBeInTheDocument();
  });

  it('redirects authenticated MunicipalManager visiting / directly to /manager/dashboard', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getAllByText('Municipal Manager Dashboard').length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByText('Smart Waste Management System')).not.toBeInTheDocument();
  });

  it('redirects already-authenticated MunicipalManager visiting /login directly to /manager/dashboard', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/login']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getAllByText('Municipal Manager Dashboard').length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByRole('button', { name: /sign in/i })).not.toBeInTheDocument();
  });

  it('redirects WasteOfficer accessing /manager/dashboard to /unauthorized', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-officer',
      user: {
        id: 'officer-1',
        fullName: 'Nimal Perera',
        email: 'officer@smartwaste.local',
        role: 'WasteOfficer',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByText('Access Denied')).toBeInTheDocument();
    expect(screen.queryByText('Municipal Manager Dashboard')).not.toBeInTheDocument();
  });

  it('redirects MunicipalManager accessing /officer/dashboard to /unauthorized', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByText('Access Denied')).toBeInTheDocument();
    expect(screen.queryByText('Waste Officer Dashboard')).not.toBeInTheDocument();
  });

  it('renders the shared AI Approvals dashboard for MunicipalManager', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/ai-approvals']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: 'AI Approvals' })).toBeInTheDocument();
    expect(screen.getByText('Monitor Agentic AI workflows and manage collection operations.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New End-to-End Collection Operation' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'AI Approvals' })).toHaveAttribute('href', '/manager/ai-approvals');
  });

  it('shows not found for removed manager placeholder subroutes', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    const managerSubroutes = [
      '/manager/analytics',
      '/manager/audit',
    ];

    managerSubroutes.forEach((route) => {
      const { unmount } = render(
        <MemoryRouter initialEntries={[route]}>
          <AppRoutes />
        </MemoryRouter>
      );

      expect(screen.getByRole('heading', { name: 'Page Not Found' })).toBeInTheDocument();
      unmount();
    });
  });

  it('wires Open Operational Incidents OverviewCard to /manager/reports', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-manager',
      user: {
        id: 'manager-1',
        fullName: 'Kavindi Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    const incidentsCard = screen.getByRole('link', { name: /open operational incidents/i });
    expect(incidentsCard).toHaveAttribute('href', '/manager/reports');
  });
});
