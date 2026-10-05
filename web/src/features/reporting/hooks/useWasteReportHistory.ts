import { useQuery } from '@tanstack/react-query';
import { reportingApi } from '../api/reportingApi';
import type { WasteReportStatusHistoryDto } from '../types/reporting';

export function useWasteReportHistory(id: string | undefined) {
  const query = useQuery<WasteReportStatusHistoryDto[], Error>({
    queryKey: ['waste-report-history', id],
    queryFn: () => {
      if (!id) {
        throw new Error('Report ID is required');
      }
      return reportingApi.getWasteReportHistory(id);
    },
    enabled: Boolean(id),
    staleTime: 30000,
  });

  return {
    history: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
  };
}
