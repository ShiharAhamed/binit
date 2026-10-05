import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { OperationalIssueSummaryDto } from '../types/operations';
import { OPERATIONAL_ISSUE_TYPE_LABELS } from '../types/operations';
import { OperationalIssueStatusBadge } from './OperationalIssueStatusBadge';
import { Button } from '../../../components/ui/Button';
import { useAuthStore } from '../../../store/authStore';

export interface OperationalIssuesTableProps {
  issues: OperationalIssueSummaryDto[];
  isLoading: boolean;
  isFiltered: boolean;
  onClearFilters: () => void;
  detailBasePath?: string;
}

function formatIssueRef(id: string): string {
  const cleanId = id.replace(/-/g, '');
  return `#${cleanId.slice(0, 8).toUpperCase()}`;
}

function formatDate(isoString: string): string {
  try {
    const d = new Date(isoString);
    return new Intl.DateTimeFormat('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(d);
  } catch {
    return isoString;
  }
}

export const OperationalIssuesTable: React.FC<OperationalIssuesTableProps> = ({
  issues,
  isLoading,
  isFiltered,
  onClearFilters,
  detailBasePath,
}) => {
  const navigate = useNavigate();
  const { user } = useAuthStore();
  const basePath =
    detailBasePath ||
    (user?.role === 'MunicipalManager' ? '/manager/operations' : '/officer/operations');

  // 1. Loading Skeleton State
  if (isLoading) {
    return (
      <div className="overflow-x-auto" data-testid="operational-issues-table-loading">
        <table className="w-full text-left text-xs border-collapse">
          <thead>
            <tr className="bg-slate-50/80 border-b border-slate-200/80 text-slate-500 uppercase tracking-wider font-semibold">
              <th className="py-3.5 px-4">Issue Title</th>
              <th className="py-3.5 px-4">Driver</th>
              <th className="py-3.5 px-4">Issue Type</th>
              <th className="py-3.5 px-4">Status</th>
              <th className="py-3.5 px-4">Reported</th>
              <th className="py-3.5 px-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {Array.from({ length: 6 }).map((_, idx) => (
              <tr key={idx} className="animate-pulse">
                <td className="py-3.5 px-4">
                  <div className="h-3.5 w-16 bg-slate-200 rounded mb-1.5" />
                  <div className="h-3 w-48 bg-slate-100 rounded" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-3.5 w-24 bg-slate-200 rounded" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-4 w-24 bg-slate-200 rounded-full" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-4 w-20 bg-slate-200 rounded-full" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-3 w-28 bg-slate-100 rounded" />
                </td>
                <td className="py-3.5 px-4 text-right">
                  <div className="h-7 w-20 bg-slate-200 rounded ml-auto" />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  // 2. Filtered Empty State
  if (issues.length === 0 && isFiltered) {
    return (
      <div className="py-16 px-4 text-center" data-testid="operational-issues-table-filtered-empty">
        <div
          className="w-12 h-12 rounded-full bg-amber-50 border border-amber-200/70 text-amber-600 mx-auto flex items-center justify-center mb-3.5 shadow-2xs"
          aria-hidden="true"
        >
          <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z"
            />
          </svg>
        </div>
        <p className="text-base font-bold text-slate-800">
          No operational issues match the selected filters.
        </p>
        <p className="text-xs text-slate-500 max-w-sm mx-auto mt-1 mb-4 leading-relaxed">
          Try adjusting search terms or resetting issue type and status filters to view operational issues.
        </p>
        <Button
          variant="secondary"
          size="sm"
          onClick={onClearFilters}
          className="inline-flex items-center gap-1.5"
        >
          Clear Filters
        </Button>
      </div>
    );
  }

  // 3. Unfiltered Empty State
  if (issues.length === 0) {
    return (
      <div className="py-16 px-4 text-center" data-testid="operational-issues-table-empty">
        <div
          className="w-12 h-12 rounded-full bg-emerald-50 border border-emerald-200/70 text-emerald-600 mx-auto flex items-center justify-center mb-3.5 shadow-2xs"
          aria-hidden="true"
        >
          <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
            />
          </svg>
        </div>
        <p className="text-base font-bold text-slate-800">No operational issues found.</p>
        <p className="text-xs text-slate-500 max-w-sm mx-auto mt-1 leading-relaxed">
          There are currently no driver-reported operational issues in the system.
        </p>
      </div>
    );
  }

  // 4. Data Table
  return (
    <div className="overflow-x-auto" data-testid="operational-issues-table">
      <table className="w-full text-left text-xs border-collapse">
        <thead>
          <tr className="bg-slate-50/80 border-b border-slate-200/80 text-slate-500 uppercase tracking-wider font-semibold">
            <th className="py-3.5 px-4 min-w-[200px]">Issue Title</th>
            <th className="py-3.5 px-4 min-w-[130px]">Driver</th>
            <th className="py-3.5 px-4 min-w-[140px]">Issue Type</th>
            <th className="py-3.5 px-4 min-w-[110px]">Status</th>
            <th className="py-3.5 px-4 min-w-[140px]">Reported</th>
            <th className="py-3.5 px-4 text-right min-w-[110px]">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {issues.map((issue) => {
            const hasLocation =
              typeof issue.latitude === 'number' &&
              typeof issue.longitude === 'number';
            const detailUrl = `${basePath}/${issue.id}`;

            return (
              <tr
                key={issue.id}
                onClick={() => navigate(detailUrl)}
                className="hover:bg-slate-50/70 transition-colors cursor-pointer group"
                data-testid={`operational-issue-row-${issue.id}`}
              >
                {/* 1. Title */}
                <td className="py-3.5 px-4">
                  <div className="flex items-center gap-1.5 font-mono text-[11px] font-semibold text-slate-400 group-hover:text-emerald-700 transition-colors">
                    <span>{formatIssueRef(issue.id)}</span>
                    {hasLocation && (
                      <span
                        className="inline-flex items-center text-emerald-600"
                        title="Location coordinates attached"
                        aria-label="Location coordinates attached"
                      >
                        <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                        </svg>
                      </span>
                    )}
                  </div>
                  <div className="font-medium text-slate-900 mt-0.5 max-w-sm sm:max-w-md line-clamp-1">
                    {issue.title}
                  </div>
                </td>

                {/* 2. Driver */}
                <td className="py-3.5 px-4 text-slate-700">
                  <span className="font-medium text-slate-900">
                    {issue.driverName || 'Driver'}
                  </span>
                </td>

                {/* 3. Issue Type */}
                <td className="py-3.5 px-4">
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-700 border border-slate-200">
                    {OPERATIONAL_ISSUE_TYPE_LABELS[issue.issueType] || issue.issueType}
                  </span>
                </td>

                {/* 4. Status */}
                <td className="py-3.5 px-4">
                  <OperationalIssueStatusBadge status={issue.status} />
                </td>

                {/* 5. Reported */}
                <td className="py-3.5 px-4 text-slate-500 whitespace-nowrap">
                  {formatDate(issue.createdAt)}
                </td>

                {/* 6. Action Button */}
                <td className="py-3.5 px-4 text-right whitespace-nowrap">
                  <Link
                    to={detailUrl}
                    onClick={(e) => e.stopPropagation()}
                    className="inline-flex items-center px-2.5 py-1 text-xs font-semibold text-emerald-700 bg-emerald-50/80 hover:bg-emerald-100/80 border border-emerald-200/70 rounded-md transition-colors"
                  >
                    View Details
                  </Link>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
};
