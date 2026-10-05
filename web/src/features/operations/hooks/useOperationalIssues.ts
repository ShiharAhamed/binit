import { useQuery, keepPreviousData } from '@tanstack/react-query';
import axios from 'axios';
import { operationsApi } from '../api/operationsApi';
import type {
  OperationalIssueListParams,
  PagedResult,
  OperationalIssueSummaryDto,
} from '../types/operations';

export function useOperationalIssues(params: OperationalIssueListParams) {
  const query = useQuery<PagedResult<OperationalIssueSummaryDto>, Error>({
    queryKey: ['operational-issues', params],
    queryFn: () => operationsApi.getOperationalIssues(params),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 30, // 30 seconds
  });

  let errorMessage: string | null = null;
  if (query.error) {
    if (axios.isAxiosError(query.error)) {
      errorMessage =
        query.error.response?.data?.detail ||
        query.error.response?.data?.title ||
        'Unable to load operational issues. Please verify network connection and try again.';
    } else {
      errorMessage =
        query.error.message ||
        'An unexpected error occurred while loading operational issues.';
    }
  }

  return {
    ...query,
    issues: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    totalPages: query.data?.totalPages ?? 1,
    currentPage: query.data?.page ?? params.page ?? 1,
    pageSize: query.data?.pageSize ?? params.pageSize ?? 20,
    errorMessage,
  };
}
