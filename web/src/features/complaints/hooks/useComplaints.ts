import { useQuery, keepPreviousData } from '@tanstack/react-query';
import axios from 'axios';
import { complaintsApi } from '../api/complaintsApi';
import type { ComplaintListParams, PagedResult, ComplaintSummaryDto } from '../types/complaints';

export function useComplaints(params: ComplaintListParams) {
  const query = useQuery<PagedResult<ComplaintSummaryDto>, Error>({
    queryKey: ['complaints', params],
    queryFn: () => complaintsApi.getComplaints(params),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 30, // 30 seconds
  });

  let errorMessage: string | null = null;
  if (query.error) {
    if (axios.isAxiosError(query.error)) {
      errorMessage =
        query.error.response?.data?.detail ||
        query.error.response?.data?.title ||
        'Unable to load complaints. Please verify network connection and try again.';
    } else {
      errorMessage = query.error.message || 'An unexpected error occurred while loading complaints.';
    }
  }

  return {
    ...query,
    complaints: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    totalPages: query.data?.totalPages ?? 1,
    currentPage: query.data?.page ?? params.page ?? 1,
    pageSize: query.data?.pageSize ?? params.pageSize ?? 20,
    errorMessage,
  };
}
