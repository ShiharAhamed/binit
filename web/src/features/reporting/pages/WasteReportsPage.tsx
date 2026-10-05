import React, { useState, useCallback } from 'react';
import { Card } from '../../../components/ui/Card';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { LeafIcon } from '../../../components/ui/Icons';
import { useAuthStore } from '../../../store/authStore';
import { useWasteReports } from '../hooks/useWasteReports';
import {
  WasteReportsFilterBar,
  type FilterState,
} from '../components/WasteReportsFilterBar';
import { WasteReportsTable } from '../components/WasteReportsTable';
import { WasteReportsPagination } from '../components/WasteReportsPagination';

const DEFAULT_FILTERS: FilterState = {
  search: '',
  status: '',
  wasteType: '',
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

export const WasteReportsPage: React.FC = () => {
  const { user } = useAuthStore();
  const [filters, setFilters] = useState<FilterState>(DEFAULT_FILTERS);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const pageSubtitle =
    user?.role === 'MunicipalManager'
      ? 'Monitor reported waste issues and their current operational status.'
      : 'Review and manage reported waste issues across municipal zones.';

  // Query waste reports via React Query hook
  const {
    reports,
    totalCount,
    totalPages,
    currentPage,
    isLoading,
    isFetching,
    isError,
    errorMessage,
    refetch,
  } = useWasteReports({
    page,
    pageSize,
    search: filters.search,
    status: filters.status,
    wasteType: filters.wasteType,
    sortBy: filters.sortBy,
    sortDirection: filters.sortDirection,
  });

  const handleFilterChange = useCallback((newFilters: Partial<FilterState>) => {
    setFilters((prev) => ({ ...prev, ...newFilters }));
    setPage(1);
  }, []);

  const handleClearFilters = useCallback(() => {
    setFilters(DEFAULT_FILTERS);
    setPage(1);
  }, []);

  const isFiltered =
    Boolean(filters.search) ||
    Boolean(filters.status) ||
    Boolean(filters.wasteType) ||
    filters.sortBy !== 'createdAt' ||
    filters.sortDirection !== 'desc';

  return (
    <div className="space-y-6">
      {/* Page Header / Breadcrumb */}
      <div className="border-b border-slate-200/80 pb-5">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <div className="flex items-center gap-1.5 text-xs text-slate-400 font-medium mb-1">
              <span>Operations</span>
              <span>/</span>
              <span className="text-slate-600 font-semibold">Waste Reports</span>
            </div>
            <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 flex items-center gap-2">
              <span>Waste Reports</span>
              <LeafIcon className="w-6 h-6 text-emerald-600 shrink-0 inline-block" />
            </h1>
            <p className="mt-1 text-xs sm:text-sm text-slate-500 leading-relaxed">
              {pageSubtitle}
            </p>
          </div>

          {/* Quick summary metric if loaded */}
          {!isLoading && !isError && (
            <div className="text-left sm:text-right shrink-0">
              <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-200/80">
                {totalCount} {totalCount === 1 ? 'Report' : 'Total Reports'}
              </span>
            </div>
          )}
        </div>
      </div>

      {/* Top Filter & Search Area */}
      <WasteReportsFilterBar
        filters={filters}
        onFilterChange={handleFilterChange}
        onClearFilters={handleClearFilters}
        isFetching={isFetching && !isLoading}
      />

      {/* Inline Error State with Retry Button */}
      {isError && (
        <Alert variant="error" className="flex items-center justify-between gap-4">
          <div>
            <p className="font-semibold text-sm">Failed to load reports</p>
            <p className="text-xs text-red-700 mt-0.5">{errorMessage}</p>
          </div>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => refetch()}
            className="shrink-0 text-xs border-red-200 text-red-800 hover:bg-red-100"
          >
            Retry
          </Button>
        </Alert>
      )}

      {/* Main Reports Table / List Surface */}
      <Card className="p-0 overflow-hidden shadow-2xs border-slate-200/80">
        <WasteReportsTable
          reports={reports}
          isLoading={isLoading}
          isFiltered={isFiltered}
          onClearFilters={handleClearFilters}
        />

        {/* Authoritative Pagination Controls */}
        {!isLoading && !isError && (
          <WasteReportsPagination
            currentPage={currentPage}
            totalPages={totalPages}
            totalCount={totalCount}
            pageSize={pageSize}
            onPageChange={setPage}
            isLoading={isFetching}
          />
        )}
      </Card>
    </div>
  );
};

export default WasteReportsPage;
