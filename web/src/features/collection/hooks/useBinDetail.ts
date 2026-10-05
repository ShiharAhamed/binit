import axios from 'axios';
import { useQuery } from '@tanstack/react-query';
import { binsApi } from '../api/binsApi';
import type { WasteBinDetailDto } from '../types/bins';

export function useBinDetail(id: string | undefined) {
  const query = useQuery<WasteBinDetailDto, Error>({
    queryKey: ['bins', id],
    queryFn: () => binsApi.getBin(id!),
    enabled: Boolean(id),
    staleTime: 30_000,
  });

  const statusCode = axios.isAxiosError(query.error) ? query.error.response?.status : undefined;
  let errorMessage: string | null = null;
  if (query.error) {
    if (axios.isAxiosError(query.error)) {
      errorMessage = query.error.response?.data?.detail || query.error.response?.data?.title ||
        'Unable to load this bin. Please verify the network connection and try again.';
    } else {
      errorMessage = query.error.message || 'An unexpected error occurred while loading this bin.';
    }
  }

  return { ...query, bin: query.data, errorMessage, isNotFound: statusCode === 404 || !id };
}

