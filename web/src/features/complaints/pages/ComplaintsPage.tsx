import React, { useState, useCallback } from 'react';
import { Card } from '../../../components/ui/Card';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { ComplaintsIcon } from '../../../components/ui/Icons';
import { useAuthStore } from '../../../store/authStore';
import { useComplaints } from '../hooks/useComplaints';
import {
  ComplaintsFilterBar,
  type ComplaintFilterState,
} from '../components/ComplaintsFilterBar';
import { ComplaintsTable } from '../components/ComplaintsTable';
import { ComplaintsPagination } from '../components/ComplaintsPagination';

const DEFAULT_FILTERS: ComplaintFilterState = {
  search: '',
  status: '',
  category: '',
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

export const ComplaintsPage: React.FC = () => {
  const { user } = useAuthStore();
  const [filters, setFilters] = useState<ComplaintFilterState>(DEFAULT_FILTERS);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const isManager = user?.role === 'MunicipalManager';
  const detailBasePath = isManager ? '/manager/complaints' : '/officer/complaints';

  const {
    complaints,
    totalCount,
    totalPages,
    currentPage,
    isLoading,
    isFetching,
    isError,
    errorMessage,
    refetch,
  } = useComplaints({
    page,
    pageSize,
    search: filters.search,
    status: filters.status,
    category: filters.category,
    sortBy: filters.sortBy,
    sortDirection: filters.sortDirection,
  });

  const handleFilterChange = useCallback((newFilters: Partial<ComplaintFilterState>) => {
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
    Boolean(filters.category) ||
    filters.sortBy !== 'createdAt' ||
    filters.sortDirection !== 'desc';

  return (
    <div className="space-y-6" data-testid="complaints-page">
      {/* Page Header / Breadcrumb */}
      <div className="border-b border-slate-200/80 pb-5">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <div className="flex items-center gap-1.5 text-xs text-slate-400 font-medium mb-1">
              <span>{isManager ? 'Management' : 'Operations'}</span>
              <span>/</span>
              <span className="text-slate-600 font-semibold">Complaints</span>
            </div>
            <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 flex items-center gap-2">
              <span>Complaints</span>
              <ComplaintsIcon className="w-6 h-6 text-emerald-600 shrink-0 inline-block" />
            </h1>
            <p className="mt-1 text-xs sm:text-sm text-slate-500 leading-relaxed">
              Review and resolve service complaints submitted by citizens.
            </p>
          </div>

          {/* Quick summary badge */}
          {!isLoading && !isError && (
            <div className="text-left sm:text-right shrink-0">
              <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-200/80">
                {totalCount} {totalCount === 1 ? 'Complaint' : 'Total Complaints'}
              </span>
            </div>
          )}
        </div>
      </div>

      {/* Filter and Search Bar */}
      <ComplaintsFilterBar
        filters={filters}
        onFilterChange={handleFilterChange}
        onClearFilters={handleClearFilters}
        isFetching={isFetching && !isLoading}
      />

      {/* Error state */}
      {isError && (
        <Alert variant="error" className="flex items-center justify-between gap-4">
          <div>
            <p className="font-semibold text-sm">Failed to load complaints</p>
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

      {/* Main Complaints Table Card */}
      <Card className="p-0 overflow-hidden shadow-2xs border-slate-200/80">
        <ComplaintsTable
          complaints={complaints}
          isLoading={isLoading}
          isFiltered={isFiltered}
          onClearFilters={handleClearFilters}
          detailBasePath={detailBasePath}
        />

        {/* Pagination Controls */}
        {!isLoading && !isError && (
          <ComplaintsPagination
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

export default ComplaintsPage;
