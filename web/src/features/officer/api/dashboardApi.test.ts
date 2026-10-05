import { describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { dashboardApi } from './dashboardApi';

vi.mock('../../../api/axiosClient', () => ({ axiosClient: { get: vi.fn() } }));

describe('dashboardApi', () => {
  it('loads the five counts with one authenticated API client request', async () => {
    const overview = {
      reportsAwaitingReview: 4, activeBins: 12, scheduledCollections: 7,
      openCollectionTasks: 5, openComplaints: 3,
    };
    vi.mocked(axiosClient.get).mockResolvedValueOnce({ data: overview });

    expect(await dashboardApi.getOverview()).toEqual(overview);
    expect(axiosClient.get).toHaveBeenCalledOnce();
    expect(axiosClient.get).toHaveBeenCalledWith('/dashboard/waste-officer/overview');
  });

  it('loads the initial-review queue through the dashboard endpoint', async () => {
    vi.mocked(axiosClient.get).mockResolvedValueOnce({ data: [] });
    expect(await dashboardApi.getNeedsAttention()).toEqual([]);
    expect(axiosClient.get).toHaveBeenCalledWith('/dashboard/waste-officer/needs-attention');
  });
});
