import React, { useEffect, useState } from 'react';
import {
  OPERATIONAL_ISSUE_STATUS_LABELS,
  OPERATIONAL_ISSUE_TYPE_LABELS,
  type OperationalIssueStatus,
  type OperationalIssueType,
} from '../types/operations';
import { Card } from '../../../components/ui/Card';
import { Button } from '../../../components/ui/Button';

export interface OperationalIssueFilterState {
  search: string;
  status: OperationalIssueStatus | '';
  issueType: OperationalIssueType | '';
  sortBy: 'createdAt' | 'updatedAt';
  sortDirection: 'asc' | 'desc';
}

export interface OperationalIssuesFilterBarProps {
  filters: OperationalIssueFilterState;
  onFilterChange: (newFilters: Partial<OperationalIssueFilterState>) => void;
  onClearFilters: () => void;
  isFetching?: boolean;
}

const ALL_STATUSES: OperationalIssueStatus[] = [
  'Reported',
  'InReview',
  'Resolved',
];

const ALL_ISSUE_TYPES: OperationalIssueType[] = [
  'VehicleProblem',
  'RoadOrAccessIssue',
  'EquipmentProblem',
  'SafetyConcern',
  'OperationalDelay',
  'Other',
];

export const OperationalIssuesFilterBar: React.FC<OperationalIssuesFilterBarProps> = ({
  filters,
  onFilterChange,
  onClearFilters,
  isFetching = false,
}) => {
  const [localSearch, setLocalSearch] = useState(filters.search);
  const [prevSearch, setPrevSearch] = useState(filters.search);

  // Synchronize local search when external filters change (e.g. on clear)
  if (filters.search !== prevSearch) {
    setPrevSearch(filters.search);
    setLocalSearch(filters.search);
  }

  // Debounce search changes by 350ms
  useEffect(() => {
    const handler = setTimeout(() => {
      if (localSearch !== filters.search) {
        onFilterChange({ search: localSearch });
      }
    }, 350);

    return () => clearTimeout(handler);
  }, [localSearch, filters.search, onFilterChange]);

  const isFiltered =
    Boolean(filters.search) ||
    Boolean(filters.status) ||
    Boolean(filters.issueType) ||
    filters.sortBy !== 'createdAt' ||
    filters.sortDirection !== 'desc';

  const sortValue = `${filters.sortBy}:${filters.sortDirection}`;

  const handleSortChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const [sortBy, sortDirection] = e.target.value.split(':') as [
      'createdAt' | 'updatedAt',
      'asc' | 'desc',
    ];
    onFilterChange({ sortBy, sortDirection });
  };

  return (
    <Card className="p-4 sm:p-5 shadow-2xs border-slate-200/80">
      <div className="flex flex-col gap-3.5">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          {/* 1. Search */}
          <div>
            <label
              htmlFor="operations-search"
              className="block text-xs font-semibold text-slate-700 mb-1"
            >
              Search Operational Issues
            </label>
            <div className="relative">
              <input
                id="operations-search"
                type="text"
                value={localSearch}
                onChange={(e) => setLocalSearch(e.target.value)}
                placeholder="Search title or description..."
                className="w-full pl-8 pr-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
              />
              <svg
                className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
                />
              </svg>
            </div>
          </div>

          {/* 2. Status Filter */}
          <div>
            <label
              htmlFor="operations-status-filter"
              className="block text-xs font-semibold text-slate-700 mb-1"
            >
              Status Filter
            </label>
            <select
              id="operations-status-filter"
              value={filters.status}
              onChange={(e) =>
                onFilterChange({ status: e.target.value as OperationalIssueStatus | '' })
              }
              className="w-full px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option value="">All Statuses</option>
              {ALL_STATUSES.map((st) => (
                <option key={st} value={st}>
                  {OPERATIONAL_ISSUE_STATUS_LABELS[st]}
                </option>
              ))}
            </select>
          </div>

          {/* 3. Issue Type Filter */}
          <div>
            <label
              htmlFor="operations-type-filter"
              className="block text-xs font-semibold text-slate-700 mb-1"
            >
              Issue Type Filter
            </label>
            <select
              id="operations-type-filter"
              value={filters.issueType}
              onChange={(e) =>
                onFilterChange({ issueType: e.target.value as OperationalIssueType | '' })
              }
              className="w-full px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option value="">All Issue Types</option>
              {ALL_ISSUE_TYPES.map((type) => (
                <option key={type} value={type}>
                  {OPERATIONAL_ISSUE_TYPE_LABELS[type]}
                </option>
              ))}
            </select>
          </div>

          {/* 4. Sort Selector */}
          <div>
            <label
              htmlFor="operations-sort-filter"
              className="block text-xs font-semibold text-slate-700 mb-1"
            >
              Sort Order
            </label>
            <select
              id="operations-sort-filter"
              value={sortValue}
              onChange={handleSortChange}
              className="w-full px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option value="createdAt:desc">Newest First</option>
              <option value="createdAt:asc">Oldest First</option>
              <option value="updatedAt:desc">Recently Updated</option>
            </select>
          </div>
        </div>

        {/* Action / Feedback strip */}
        <div className="flex items-center justify-between pt-2 border-t border-slate-100 text-xs">
          <div className="flex items-center gap-2">
            {isFetching && (
              <span className="inline-flex items-center gap-1.5 text-slate-400 font-medium text-[11px]">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
                Updating queue...
              </span>
            )}
          </div>

          {isFiltered && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onClearFilters}
              className="text-xs text-emerald-700 hover:text-emerald-800 hover:bg-emerald-50/60 font-semibold"
            >
              Clear Filters
            </Button>
          )}
        </div>
      </div>
    </Card>
  );
};
