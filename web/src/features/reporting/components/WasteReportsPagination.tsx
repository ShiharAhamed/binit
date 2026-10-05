import React from 'react';
import { Button } from '../../../components/ui/Button';

export interface WasteReportsPaginationProps {
  currentPage: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (newPage: number) => void;
  isLoading?: boolean;
}

export const WasteReportsPagination: React.FC<WasteReportsPaginationProps> = ({
  currentPage,
  totalPages,
  totalCount,
  pageSize,
  onPageChange,
  isLoading = false,
}) => {
  if (totalCount === 0) {
    return null;
  }

  const startRecord = Math.min((currentPage - 1) * pageSize + 1, totalCount);
  const endRecord = Math.min(currentPage * pageSize, totalCount);

  return (
    <div
      className="p-4 border-t border-slate-200/80 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 text-xs text-slate-500 bg-white"
      data-testid="pagination-summary"
    >
      <div>
        <span>
          Showing <span className="font-semibold text-slate-800">{startRecord}</span> to{' '}
          <span className="font-semibold text-slate-800">{endRecord}</span> of{' '}
          <span className="font-semibold text-slate-800">{totalCount}</span> reports
        </span>
      </div>

      <div className="flex items-center gap-2.5 self-end sm:self-auto">
        <Button
          variant="secondary"
          size="sm"
          disabled={currentPage <= 1 || isLoading}
          onClick={() => onPageChange(currentPage - 1)}
          aria-label="Previous page"
        >
          Previous
        </Button>

        <span className="font-semibold text-slate-700 px-1">
          Page {currentPage} of {Math.max(1, totalPages)}
        </span>

        <Button
          variant="secondary"
          size="sm"
          disabled={currentPage >= totalPages || isLoading}
          onClick={() => onPageChange(currentPage + 1)}
          aria-label="Next page"
        >
          Next
        </Button>
      </div>
    </div>
  );
};
