import { axiosClient } from '../../../api/axiosClient';
import type { WasteOfficerDashboardOverview, WasteOfficerNeedsAttentionItem } from '../types/dashboard';

export const dashboardApi = {
  getOverview: async (): Promise<WasteOfficerDashboardOverview> => {
    const response = await axiosClient.get<WasteOfficerDashboardOverview>(
      '/dashboard/waste-officer/overview'
    );
    return response.data;
  },
  getNeedsAttention: async (): Promise<WasteOfficerNeedsAttentionItem[]> => {
    const response = await axiosClient.get<WasteOfficerNeedsAttentionItem[]>(
      '/dashboard/waste-officer/needs-attention'
    );
    return response.data;
  },
};
