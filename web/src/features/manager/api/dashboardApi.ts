import { axiosClient } from '../../../api/axiosClient';
import type { MunicipalManagerDashboardOverview } from '../types/dashboard';
import type { WasteOfficerNeedsAttentionItem } from '../../officer/types/dashboard';

export const managerDashboardApi = {
  getOverview: async (): Promise<MunicipalManagerDashboardOverview> => {
    const response = await axiosClient.get<MunicipalManagerDashboardOverview>(
      '/dashboard/municipal-manager/overview'
    );
    return response.data;
  },
  getNeedsAttention: async (): Promise<WasteOfficerNeedsAttentionItem[]> => {
    const response = await axiosClient.get<WasteOfficerNeedsAttentionItem[]>(
      '/dashboard/municipal-manager/needs-attention'
    );
    return response.data;
  },
};
