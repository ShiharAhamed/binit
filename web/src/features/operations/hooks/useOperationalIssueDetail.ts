import { useQuery } from '@tanstack/react-query';
import axios from 'axios';
import { operationsApi } from '../api/operationsApi';
import type { OperationalIssueDetailDto } from '../types/operations';

export function useOperationalIssueDetail(id: string | undefined) {
  const query = useQuery<OperationalIssueDetailDto, Error>({
    queryKey: ['operational-issue', id],
    queryFn: () => {
      if (!id) {
        throw new Error('Operational issue ID is required');
      }
      return operationsApi.getOperationalIssue(id);
    },
    enabled: Boolean(id),
    staleTime: 30000,
  });

  let errorMessage: string | null = null;
  if (query.error) {
    if (axios.isAxiosError(query.error)) {
      errorMessage =
        query.error.response?.data?.detail ||
        query.error.response?.data?.title ||
        'Unable to load operational issue details. Please try again.';
    } else {
      errorMessage = query.error.message || 'An unexpected error occurred.';
    }
  }

  return {
    issue: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    errorMessage,
    refetch: query.refetch,
  };
}
