import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppRoutes } from '../../../routes/AppRoutes';
import { useAuthStore } from '../../../store/authStore';

vi.mock('../api/agentWorkflowApi', () => ({
  agentWorkflowApi: {
    listWorkflows: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }),
    getWorkflow: vi.fn().mockResolvedValue({
      id: 'workflow-1',
      objective: 'Prepare a shared collection operation.',
      status: 'AwaitingDispatchApproval',
      currentStep: 'OperationalValidation',
      initiatedByUserId: 'user-1',
      createdAt: '2026-09-30T08:00:00Z',
      updatedAt: '2026-09-30T09:00:00Z',
      completedAt: null,
      finalOutcome: null,
      version: 4,
      steps: [],
      transitions: [],
      approvals: [],
      executionResults: [],
    }),
  },
}));

const setAuthenticatedRole = (role: string) => useAuthStore.setState({
  isAuthenticated: true,
  isLoading: false,
  accessToken: 'token',
  user: { id: 'user-1', fullName: 'Workflow User', email: 'workflow@example.com', role },
});

describe('AI Approvals role routes', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it.each([
    ['MunicipalManager', '/manager/ai-approvals'],
    ['WasteOfficer', '/officer/ai-approvals'],
  ])('renders the shared dashboard for %s at %s', (role, route) => {
    setAuthenticatedRole(role);
    render(<MemoryRouter initialEntries={[route]}><AppRoutes /></MemoryRouter>);

    expect(screen.getByRole('heading', { name: 'AI Approvals' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New End-to-End Collection Operation' })).toBeInTheDocument();
  });

  it.each([
    ['MunicipalManager', '/manager/ai-approvals/workflow-1', '/manager/ai-approvals'],
    ['WasteOfficer', '/officer/ai-approvals/workflow-1', '/officer/ai-approvals'],
  ])('renders the shared detail shell for %s at its role-correct route', async (role, route, parentRoute) => {
    setAuthenticatedRole(role);
    render(<MemoryRouter initialEntries={[route]}><AppRoutes /></MemoryRouter>);

    expect(await screen.findByText('Prepare a shared collection operation.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /back to ai approvals/i })).toHaveAttribute('href', parentRoute);
  });

  it.each([
    ['Citizen', '/manager/ai-approvals'],
    ['Driver', '/officer/ai-approvals'],
  ])('denies %s access to %s', (role, route) => {
    setAuthenticatedRole(role);
    render(<MemoryRouter initialEntries={[route]}><AppRoutes /></MemoryRouter>);

    expect(screen.getByText('Access Denied')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'AI Approvals' })).not.toBeInTheDocument();
  });
});
