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
  dashboardApi: {
    getNeedsAttention: vi.fn().mockResolvedValue([]),
    getOverview: vi.fn().mockResolvedValue({
      reportsAwaitingReview: 4,
      activeBins: 12,
      scheduledCollections: 7,
      openCollectionTasks: 5,
      openComplaints: 3,
    }),
  },
}));

describe('WasteOfficer Dashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('renders Waste Officer Dashboard and user greeting for authorized WasteOfficer', () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Dashboard title and greeting
    expect(screen.getAllByText('Waste Officer Dashboard').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/welcome back,/i)).toBeInTheDocument();
    expect(screen.getAllByText('Nimal Perera').length).toBeGreaterThanOrEqual(1);
  });

  it('renders Quick Actions section with links to operational modules', () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByText('Quick Actions')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /review waste reports/i })).toHaveAttribute(
      'href',
      '/officer/waste-reports'
    );
    expect(screen.getByRole('link', { name: /manage bins/i })).toHaveAttribute(
      'href',
      '/officer/bins'
    );
    expect(screen.getByRole('link', { name: /view collection schedules/i })).toHaveAttribute(
      'href',
      '/officer/schedules'
    );
    expect(screen.getByRole('link', { name: /view collection tasks/i })).toHaveAttribute(
      'href',
      '/officer/tasks'
    );
    expect(screen.getByRole('link', { name: /review complaints/i })).toHaveAttribute(
      'href',
      '/officer/complaints'
    );
  });

  it('renders Needs Attention section with a real empty state', async () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByText('Needs Attention')).toBeInTheDocument();
    expect(await screen.findByText('All Operational Queues Clear')).toBeInTheDocument();
    expect(screen.getByText('No waste reports or complaints are currently awaiting review.')).toBeInTheDocument();
  });

  it('does not render academic Component 1/2/3/4 badges or labels', () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.queryByText(/component \d/i)).not.toBeInTheDocument();
  });

  it('renders Operational Overview section before Quick Actions in DOM order', () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    const overviewHeading = screen.getByRole('heading', { name: /operational overview/i });
    const quickActionsHeading = screen.getByRole('heading', { name: /quick actions/i });
    expect(overviewHeading.compareDocumentPosition(quickActionsHeading)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING
    );
  });

  it('does not render removed sections or Municipal Operations Active status badge', () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.queryByText('Operational Management Modules')).not.toBeInTheDocument();
    expect(screen.queryByText('Standard Operational Sequence')).not.toBeInTheDocument();
    expect(screen.queryByText('Municipal Operations Active')).not.toBeInTheDocument();
  });

  it('renders all 5 operational management modules', () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Verify all 5 module titles in quick access cards and sidebar
    expect(screen.getAllByText('Waste Reports').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Bin Management').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Collection Needs').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Collection Tasks').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Complaints').length).toBeGreaterThanOrEqual(1);
  });

  it('verifies quick-access module links target correct routes', () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Verify module links in both sidebar navigation and module cards
    const verifyLinks = (namePattern: RegExp, expectedHref: string) => {
      const links = screen.getAllByRole('link', { name: namePattern });
      expect(links.length).toBeGreaterThanOrEqual(1);
      links.forEach((link) => {
        expect(link).toHaveAttribute('href', expectedHref);
      });
    };

    verifyLinks(/waste reports/i, '/officer/waste-reports');
    verifyLinks(/bin management/i, '/officer/bins');
    verifyLinks(/collection schedules/i, '/officer/schedules');
    verifyLinks(/collection tasks/i, '/officer/tasks');
    verifyLinks(/complaints/i, '/officer/complaints');
  });

  it('renders all operational overview metric cards', () => {
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Verify presence of all 5 overview cards
    expect(screen.getByText('Reports Awaiting Review')).toBeInTheDocument();
    expect(screen.getByText('Active Bins')).toBeInTheDocument();
    expect(screen.getByText('Scheduled Collections')).toBeInTheDocument();
    expect(screen.getByText('Open Collection Tasks')).toBeInTheDocument();
    expect(screen.getByText('Open Complaints')).toBeInTheDocument();

  });

  it('redirects non-WasteOfficer (e.g. Citizen) to /unauthorized when accessing /officer/dashboard', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-citizen',
      user: {
        id: 'citizen-1',
        fullName: 'Kamal Silva',
        email: 'kamal@example.com',
        role: 'Citizen',
      },
    });

    render(
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Access Denied page rendered
    expect(screen.getByText('Access Denied')).toBeInTheDocument();
    expect(screen.queryByText('Waste Officer Dashboard')).not.toBeInTheDocument();
  });

  it('routes WasteOfficer directly to /officer/dashboard upon successful login without landing on HomePage', async () => {
    const user = userEvent.setup();
    const mockOfficerResponse = {
      accessToken: 'jwt-officer-token',
      expiresAt: '2026-09-09T18:00:00Z',
      user: {
        id: 'officer-456',
        fullName: 'Sunil Perera',
        email: 'officer@example.com',
        role: 'WasteOfficer',
      },
    };

    vi.mocked(authApi.login).mockResolvedValueOnce(mockOfficerResponse);

    render(
      <MemoryRouter initialEntries={['/login']}>
        <AppRoutes />
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/email address/i), 'officer@example.com');
    await user.type(screen.getByLabelText(/password/i), 'Officer123!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(authApi.login).toHaveBeenCalledWith({
        email: 'officer@example.com',
        password: 'Officer123!',
        clientType: 'web',
      });
    });

    // Directly renders Waste Officer Dashboard
    expect(await screen.findByText(/welcome back,/i)).toBeInTheDocument();
    expect(screen.getAllByText('Sunil Perera').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Waste Officer Dashboard').length).toBeGreaterThanOrEqual(1);

    // Generic HomePage elements must NOT be displayed
    expect(screen.queryByText('Smart Waste Management System')).not.toBeInTheDocument();
    expect(screen.queryByText(/open dashboard/i)).not.toBeInTheDocument();

    const storeState = useAuthStore.getState();
    expect(storeState.isAuthenticated).toBe(true);
    expect(storeState.user?.role).toBe('WasteOfficer');
  });

  it('redirects authenticated WasteOfficer visiting / directly to /officer/dashboard without showing HomePage', () => {
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
      <MemoryRouter initialEntries={['/']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Landed on officer dashboard
    expect(screen.getAllByText('Waste Officer Dashboard').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/welcome back,/i)).toBeInTheDocument();
    expect(screen.getAllByText('Nimal Perera').length).toBeGreaterThanOrEqual(1);

    // Generic Home page title is NOT in the document
    expect(screen.queryByText('Smart Waste Management System')).not.toBeInTheDocument();
  });

  it('redirects already-authenticated WasteOfficer visiting /login directly to /officer/dashboard', () => {
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
      <MemoryRouter initialEntries={['/login']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Landed directly on officer dashboard
    expect(screen.getAllByText('Waste Officer Dashboard').length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByRole('button', { name: /sign in/i })).not.toBeInTheDocument();
  });

  it('redirects unauthenticated user visiting /officer/dashboard to /login', () => {
    useAuthStore.setState({
      isAuthenticated: false,
      isLoading: false,
      accessToken: null,
      user: null,
    });

    render(
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Landed on login page
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByText('Waste Officer Dashboard')).not.toBeInTheDocument();
  });

  it('renders generic HomePage for authenticated Citizen visiting /', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token-citizen',
      user: {
        id: 'citizen-1',
        fullName: 'Kamal Silva',
        email: 'citizen@smartwaste.local',
        role: 'Citizen',
      },
    });

    render(
      <MemoryRouter initialEntries={['/']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Generic Home page displayed for Citizen fallback
    expect(screen.getAllByText('Smart Waste Management System').length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByText('Waste Officer Dashboard')).not.toBeInTheDocument();
  });

  it('preserves /officer/dashboard during session restoration without redirecting to /', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: true, // initial startup loading
      accessToken: 'token-officer',
      user: null,
    });

    render(
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Shows verifying session spinner instead of redirecting or showing HomePage
    expect(screen.getAllByText(/verifying session/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByText('Smart Waste Management System')).not.toBeInTheDocument();
  });

  it('does not contain logout in sidebar and allows WasteOfficer to logout from header account dropdown', async () => {
    const user = userEvent.setup();
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
      <MemoryRouter initialEntries={['/officer/dashboard']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Sidebar navigation should not have a logout button
    const nav = screen.getByRole('navigation', { name: /main navigation/i });
    expect(nav).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /logout/i })).not.toBeInTheDocument();

    // Open account dropdown in header
    const accountTrigger = screen.getByRole('button', { name: /user account menu/i });
    expect(accountTrigger).toHaveAttribute('aria-expanded', 'false');
    await user.click(accountTrigger);
    expect(accountTrigger).toHaveAttribute('aria-expanded', 'true');

    // Logout button appears in the dropdown menu
    const logoutButton = screen.getByRole('menuitem', { name: /logout/i });
    expect(logoutButton).toBeInTheDocument();

    await user.click(logoutButton);

    const storeState = useAuthStore.getState();
    expect(storeState.isAuthenticated).toBe(false);
    expect(storeState.user).toBeNull();
  });

  it('routes WasteOfficer AI Approvals navigation to the shared dashboard', () => {
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
      <MemoryRouter initialEntries={['/officer/ai-approvals']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: 'AI Approvals' })).toBeInTheDocument();
    expect(screen.getByText('Monitor Agentic AI workflows and manage collection operations.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New End-to-End Collection Operation' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'AI Approvals' })).toHaveAttribute('href', '/officer/ai-approvals');
  });

  it('ensures /officer/complaints opens the real Complaints operational page', async () => {
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
      <MemoryRouter initialEntries={['/officer/complaints']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /complaints/i })).toBeInTheDocument();
    expect(
      screen.getByText(/review and resolve service complaints submitted by citizens/i)
    ).toBeInTheDocument();
  });

  it('ensures /officer/waste-reports opens the real Waste Reports operational page', async () => {
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
      <MemoryRouter initialEntries={['/officer/waste-reports']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /waste reports/i })).toBeInTheDocument();
    expect(
      screen.getByText(/review and manage reported waste issues across municipal zones/i)
    ).toBeInTheDocument();
  });
});
