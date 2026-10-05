import { useQuery, keepPreviousData } from '@tanstack/react-query';
import axios from 'axios';
import { reportingApi } from '../api/reportingApi';
import type { WasteReportListParams, PagedResult, WasteReportSummaryDto } from '../types/reporting';

export function useWasteReports(params: WasteReportListParams) {
  const query = useQuery<PagedResult<WasteReportSummaryDto>, Error>({
    queryKey: ['waste-reports', params],
    queryFn: () => reportingApi.getWasteReports(params),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 30, // 30 seconds
  });

  let errorMessage: string | null = null;
  if (query.error) {
    if (axios.isAxiosError(query.error)) {
      errorMessage =
        query.error.response?.data?.detail ||
        query.error.response?.data?.title ||
        'Unable to load waste reports. Please verify network connection and try again.';
    } else {
      errorMessage = query.error.message || 'An unexpected error occurred while loading reports.';
    }
  }

  return {
    ...query,
    reports: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    totalPages: query.data?.totalPages ?? 1,
    currentPage: query.data?.page ?? params.page ?? 1,
    pageSize: query.data?.pageSize ?? params.pageSize ?? 20,
    errorMessage,
  };
}
