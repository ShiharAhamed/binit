import React, { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert } from '../../../components/ui/Alert';
import { BinsIcon } from '../../../components/ui/Icons';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { BinFilterBar, type BinFilterState } from '../components/BinFilterBar';
import { BinPagination } from '../components/BinPagination';
import { BinsTable } from '../components/BinsTable';
import { useBins } from '../hooks/useBins';

const defaultFilters: BinFilterState = { search: '', status: '', wasteType: '', condition: '', minFillLevel: '' };

export const BinsPage: React.FC = () => {
  const [filters, setFilters] = useState<BinFilterState>(defaultFilters);
  const [page, setPage] = useState(1);
  const { bins, totalCount, totalPages, currentPage, pageSize, isLoading, isFetching, isError, errorMessage, refetch } = useBins({ ...filters, page, pageSize: 20 });

  const onFilterChange = useCallback((changes: Partial<BinFilterState>) => { setFilters((previous) => ({ ...previous, ...changes })); setPage(1); }, []);
  const clearFilters = useCallback(() => { setFilters(defaultFilters); setPage(1); }, []);
  const isFiltered = Boolean(filters.search || filters.status || filters.wasteType || filters.condition) || filters.minFillLevel !== '';

  return <div className="space-y-6">
    <div className="border-b border-slate-200/80 pb-5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div><div className="flex gap-1.5 text-xs text-slate-400 font-medium mb-1"><span>Operations</span><span>/</span><span className="text-slate-600 font-semibold">Bin Management</span></div><h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 flex items-center gap-2">Bin Management <BinsIcon className="w-6 h-6 text-emerald-600" /></h1><p className="mt-1 text-xs sm:text-sm text-slate-500">Review registered municipal roadside bins and their latest recorded operating observations.</p></div>
      <div className="flex items-center gap-3">{!isLoading && !isError && <span className="inline-flex self-start sm:self-auto px-3 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-200/80">{totalCount} {totalCount === 1 ? 'Bin' : 'Total Bins'}</span>}<Link to="/officer/bins/register" className="inline-flex items-center justify-center px-3 py-1.5 rounded-md text-xs font-semibold bg-emerald-600 text-white hover:bg-emerald-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2">Register Bin</Link></div>
    </div>
    <BinFilterBar filters={filters} onFilterChange={onFilterChange} onClearFilters={clearFilters} isFetching={isFetching && !isLoading} />
    {isError && <Alert variant="error" className="flex items-center justify-between gap-4"><div><p className="font-semibold text-sm">Failed to load bins</p><p className="text-xs text-red-700 mt-0.5">{errorMessage}</p></div><Button variant="secondary" size="sm" onClick={() => refetch()} className="shrink-0 text-xs border-red-200 text-red-800 hover:bg-red-100">Retry</Button></Alert>}
    <Card className="p-0! overflow-hidden shadow-2xs border-slate-200/80"><BinsTable bins={bins} isLoading={isLoading} isFiltered={isFiltered} onClearFilters={clearFilters} />{!isLoading && !isError && <BinPagination currentPage={currentPage} totalPages={totalPages} totalCount={totalCount} pageSize={pageSize} onPageChange={setPage} isLoading={isFetching} />}</Card>
  </div>;
};

export default BinsPage;
