import { useQuery } from '@tanstack/react-query';
import { reportingApi } from '../api/reportingApi';
import type { WasteReportDetailDto } from '../types/reporting';

export function useWasteReportDetail(id: string | undefined) {
  const query = useQuery<WasteReportDetailDto, Error>({
    queryKey: ['waste-report', id],
    queryFn: () => {
      if (!id) {
        throw new Error('Report ID is required');
      }
      return reportingApi.getWasteReport(id);
    },
    enabled: Boolean(id),
    staleTime: 30000,
  });

  return {
    report: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
  };
}
