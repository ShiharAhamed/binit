import { axiosClient } from '../../../api/axiosClient';
import type {
  PagedResult,
  ComplaintSummaryDto,
  ComplaintDetailDto,
  ComplaintListParams,
  ResolveComplaintRequest,
} from '../types/complaints';

export const complaintsApi = {
  /**
   * Retrieves paginated list of complaints with search, filtering, and sorting.
   * Authoritative Endpoint: GET /api/v1/complaints
   */
  getComplaints: async (
    params?: ComplaintListParams
  ): Promise<PagedResult<ComplaintSummaryDto>> => {
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
      if (params.category) {
        cleanParams.category = params.category;
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
    }

    const response = await axiosClient.get<PagedResult<ComplaintSummaryDto>>('/complaints', {
      params: cleanParams,
    });
    return response.data;
  },

  /**
   * Retrieves full details of a specific complaint by ID.
   * Authoritative Endpoint: GET /api/v1/complaints/{id}
   */
  getComplaint: async (id: string): Promise<ComplaintDetailDto> => {
    const response = await axiosClient.get<ComplaintDetailDto>(`/complaints/${id}`);
    return response.data;
  },

  /**
   * Staff initiates review on a submitted complaint (Submitted -> InReview).
   * Authoritative Endpoint: POST /api/v1/complaints/{id}/review
   */
  startReview: async (id: string): Promise<ComplaintDetailDto> => {
    const response = await axiosClient.post<ComplaintDetailDto>(`/complaints/${id}/review`);
    return response.data;
  },

  /**
   * Staff resolves an under-review complaint (InReview -> Resolved) with resolution note.
   * Authoritative Endpoint: POST /api/v1/complaints/{id}/resolve
   */
  resolveComplaint: async (
    id: string,
    resolutionNote: string
  ): Promise<ComplaintDetailDto> => {
    const payload: ResolveComplaintRequest = { resolutionNote };
    const response = await axiosClient.post<ComplaintDetailDto>(
      `/complaints/${id}/resolve`,
      payload
    );
    return response.data;
  },
};
