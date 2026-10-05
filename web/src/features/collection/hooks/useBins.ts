import axios from 'axios';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { binsApi } from '../api/binsApi';
import type { PagedResult, WasteBinListParams, WasteBinSummaryDto } from '../types/bins';

export function useBins(params: WasteBinListParams) {
  const query = useQuery<PagedResult<WasteBinSummaryDto>, Error>({
    queryKey: ['bins', params],
    queryFn: () => binsApi.getBins(params),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });

  let errorMessage: string | null = null;
  if (query.error) {
    if (axios.isAxiosError(query.error)) {
      errorMessage =
        query.error.response?.data?.detail ||
        query.error.response?.data?.title ||
        'Unable to load bins. Please verify the network connection and try again.';
    } else {
      errorMessage = query.error.message || 'An unexpected error occurred while loading bins.';
    }
  }

  return {
    ...query,
    bins: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    totalPages: query.data?.totalPages ?? 1,
    currentPage: query.data?.page ?? params.page ?? 1,
    pageSize: query.data?.pageSize ?? params.pageSize ?? 20,
    errorMessage,
  };
}

