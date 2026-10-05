import { useQuery } from '@tanstack/react-query';
import axios from 'axios';
import { complaintsApi } from '../api/complaintsApi';
import type { ComplaintDetailDto } from '../types/complaints';

export function useComplaintDetail(id: string | undefined) {
  const query = useQuery<ComplaintDetailDto, Error>({
    queryKey: ['complaint', id],
    queryFn: () => {
      if (!id) {
        throw new Error('Complaint ID is required');
      }
      return complaintsApi.getComplaint(id);
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
        'Unable to load complaint details. Please try again.';
    } else {
      errorMessage = query.error.message || 'An unexpected error occurred.';
    }
  }

  return {
    complaint: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    errorMessage,
    refetch: query.refetch,
  };
}
