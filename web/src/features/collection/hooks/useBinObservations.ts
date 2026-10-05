import axios from 'axios';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { binsApi } from '../api/binsApi';
import type { BinObservationDto, ObservationListParams, PagedResult } from '../types/bins';

export function useBinObservations(id: string | undefined, params: ObservationListParams) {
  const query = useQuery<PagedResult<BinObservationDto>, Error>({
    queryKey: ['bin-observations', id, params],
    queryFn: () => binsApi.getObservations(id!, params),
    enabled: Boolean(id),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });
  const errorMessage = query.error
    ? axios.isAxiosError(query.error)
      ? query.error.response?.data?.detail || query.error.response?.data?.title || 'Unable to load observation history.'
      : query.error.message || 'Unable to load observation history.'
    : null;
  return { ...query, observations: query.data?.items ?? [], totalCount: query.data?.totalCount ?? 0, totalPages: query.data?.totalPages ?? 1, currentPage: query.data?.page ?? params.page ?? 1, pageSize: query.data?.pageSize ?? params.pageSize ?? 20, errorMessage };
}
