import { axiosClient } from '../../../api/axiosClient';
import type {
  PagedResult,
  WasteReportSummaryDto,
  WasteReportListParams,
  WasteReportDetailDto,
  WasteReportStatusHistoryDto,
  RejectWasteReportRequest,
  VerifyWasteReportRequest,
} from '../types/reporting';

export const reportingApi = {
  /**
   * Retrieves paginated list of waste reports with search, filter, and sorting.
   * Authoritative Endpoint: GET /api/v1/waste-reports
   */
  getWasteReports: async (
    params?: WasteReportListParams
  ): Promise<PagedResult<WasteReportSummaryDto>> => {
    const cleanParams: Record<string, unknown> = {};

    if (params) {
      if (params.page !== undefined && params.page > 0) {
        cleanParams.page = params.page;
      }
      if (params.pageSize !== undefined && params.pageSize > 0) {
        cleanParams.pageSize = params.pageSize;
      }
      if (params.status) {
        cleanParams.status = params.status;
      }
      if (params.wasteType) {
        cleanParams.wasteType = params.wasteType;
      }
      if (params.search && params.search.trim()) {
        cleanParams.search = params.search.trim();
      }
      if (params.sortBy) {
        cleanParams.sortBy = params.sortBy;
      }
      if (params.sortDirection) {
        cleanParams.sortDirection = params.sortDirection;
      }
      if (params.fromDate) {
        cleanParams.fromDate = params.fromDate;
      }
      if (params.toDate) {
        cleanParams.toDate = params.toDate;
      }
    }

    const response = await axiosClient.get<PagedResult<WasteReportSummaryDto>>('/waste-reports', {
      params: cleanParams,
    });
    return response.data;
  },

  /**
   * Retrieves full details of a specific waste report by ID.
   * Authoritative Endpoint: GET /api/v1/waste-reports/{id}
   */
  getWasteReport: async (id: string): Promise<WasteReportDetailDto> => {
    const response = await axiosClient.get<WasteReportDetailDto>(`/waste-reports/${id}`);
    return response.data;
  },

  /**
   * Retrieves chronological status transition audit trail for a waste report.
   * Authoritative Endpoint: GET /api/v1/waste-reports/{id}/history
   */
  getWasteReportHistory: async (id: string): Promise<WasteReportStatusHistoryDto[]> => {
    const response = await axiosClient.get<WasteReportStatusHistoryDto[]>(`/waste-reports/${id}/history`);
    return response.data;
  },

  /**
   * Waste Officer initiates review on a submitted report (Submitted -> UnderReview).
   * Authoritative Endpoint: POST /api/v1/waste-reports/{id}/start-review
   */
  startReview: async (id: string): Promise<WasteReportDetailDto> => {
    const response = await axiosClient.post<WasteReportDetailDto>(`/waste-reports/${id}/start-review`);
    return response.data;
  },

  /**
   * Waste Officer verifies an under-review report (UnderReview -> Verified).
   * Authoritative Endpoint: POST /api/v1/waste-reports/{id}/verify
   */
  verifyReport: async (id: string, priority: VerifyWasteReportRequest['priority']): Promise<WasteReportDetailDto> => {
    const response = await axiosClient.post<WasteReportDetailDto>(`/waste-reports/${id}/verify`, { priority });
    return response.data;
  },

  /**
   * Waste Officer rejects an under-review report (UnderReview -> Rejected) with reason.
   * Authoritative Endpoint: POST /api/v1/waste-reports/{id}/reject
   */
  rejectReport: async (id: string, reason: string): Promise<WasteReportDetailDto> => {
    const payload: RejectWasteReportRequest = { reason };
    const response = await axiosClient.post<WasteReportDetailDto>(
      `/waste-reports/${id}/reject`,
      payload
    );
    return response.data;
  },
};
