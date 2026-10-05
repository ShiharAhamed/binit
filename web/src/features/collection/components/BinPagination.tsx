import React from 'react';
import { Button } from '../../../components/ui/Button';

interface BinPaginationProps {
  currentPage: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  isLoading?: boolean;
}

export const BinPagination: React.FC<BinPaginationProps> = ({ currentPage, totalPages, totalCount, pageSize, onPageChange, isLoading = false }) => {
  if (totalCount === 0) return null;
  const first = Math.min((currentPage - 1) * pageSize + 1, totalCount);
  const last = Math.min(currentPage * pageSize, totalCount);
  return <div className="p-4 border-t border-slate-200/80 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 text-xs text-slate-500"><span>Showing <strong className="text-slate-800">{first}</strong> to <strong className="text-slate-800">{last}</strong> of <strong className="text-slate-800">{totalCount}</strong> bins</span><div className="flex items-center gap-2 self-end sm:self-auto"><Button variant="secondary" size="sm" disabled={currentPage <= 1 || isLoading} onClick={() => onPageChange(currentPage - 1)}>Previous</Button><span className="font-semibold text-slate-700 px-1">Page {currentPage} of {Math.max(totalPages, 1)}</span><Button variant="secondary" size="sm" disabled={currentPage >= totalPages || isLoading} onClick={() => onPageChange(currentPage + 1)}>Next</Button></div></div>;
};

