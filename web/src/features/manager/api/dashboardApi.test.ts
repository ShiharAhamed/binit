import { describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { managerDashboardApi } from './dashboardApi';

vi.mock('../../../api/axiosClient', () => ({ axiosClient: { get: vi.fn() } }));

describe('managerDashboardApi', () => {
  it('loads five counts through one authenticated API client request', async () => {
    const overview = {
      aiWorkflowsAwaitingApproval: 6, activeCollectionAssignments: 4, availableVehicles: 8,
      openOperationalIncidents: 2, unresolvedComplaints: 5,
    };
    vi.mocked(axiosClient.get).mockResolvedValueOnce({ data: overview });

    expect(await managerDashboardApi.getOverview()).toEqual(overview);
    expect(axiosClient.get).toHaveBeenCalledOnce();
    expect(axiosClient.get).toHaveBeenCalledWith('/dashboard/municipal-manager/overview');
  });

  it('loads the manager review queue through its own endpoint', async () => {
    vi.mocked(axiosClient.get).mockResolvedValueOnce({ data: [] });
    expect(await managerDashboardApi.getNeedsAttention()).toEqual([]);
    expect(axiosClient.get).toHaveBeenCalledWith('/dashboard/municipal-manager/needs-attention');
  });
});
