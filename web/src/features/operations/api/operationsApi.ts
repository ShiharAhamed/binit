import { axiosClient } from '../../../api/axiosClient';
import type {
  PagedResult,
  OperationalIssueSummaryDto,
  OperationalIssueDetailDto,
  OperationalIssueListParams,
  ResolveOperationalIssueRequest,
} from '../types/operations';

export const operationsApi = {
  /**
   * Retrieves paginated list of operational issues with search, filtering, and sorting.
   * Authoritative Endpoint: GET /api/v1/operational-issues
   */
  getOperationalIssues: async (
    params?: OperationalIssueListParams
  ): Promise<PagedResult<OperationalIssueSummaryDto>> => {
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
      if (params.issueType) {
        cleanParams.issueType = params.issueType;
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

    const response = await axiosClient.get<PagedResult<OperationalIssueSummaryDto>>(
      '/operational-issues',
      {
        params: cleanParams,
      }
    );
    return response.data;
  },

  /**
   * Retrieves full details of a specific operational issue by ID.
   * Authoritative Endpoint: GET /api/v1/operational-issues/{id}
   */
  getOperationalIssue: async (id: string): Promise<OperationalIssueDetailDto> => {
    const response = await axiosClient.get<OperationalIssueDetailDto>(
      `/operational-issues/${id}`
    );
    return response.data;
  },

  /**
   * Staff initiates review on a reported operational issue (Reported -> InReview).
   * Authoritative Endpoint: POST /api/v1/operational-issues/{id}/review
   */
  startReview: async (id: string): Promise<OperationalIssueDetailDto> => {
    const response = await axiosClient.post<OperationalIssueDetailDto>(
      `/operational-issues/${id}/review`
    );
    return response.data;
  },

  /**
   * Staff resolves an under-review operational issue (InReview -> Resolved) with resolution note.
   * Authoritative Endpoint: POST /api/v1/operational-issues/{id}/resolve
   */
  resolveOperationalIssue: async (
    id: string,
    resolutionNote: string
  ): Promise<OperationalIssueDetailDto> => {
    const payload: ResolveOperationalIssueRequest = { resolutionNote };
    const response = await axiosClient.post<OperationalIssueDetailDto>(
      `/operational-issues/${id}/resolve`,
      payload
    );
    return response.data;
  },
};
