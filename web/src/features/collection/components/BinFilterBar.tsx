import React, { useEffect, useState } from 'react';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import {
  BIN_CONDITION_LABELS,
  BIN_STATUS_LABELS,
  type BinAdministrativeStatus,
  type BinCondition,
  type WasteBinListParams,
  type WasteType,
} from '../types/bins';

export type BinFilterState = Pick<
  WasteBinListParams,
  'status' | 'wasteType' | 'condition' | 'minFillLevel' | 'search'
>;

const statuses: BinAdministrativeStatus[] = ['Active', 'OutOfService', 'Retired'];
const conditions: BinCondition[] = ['Good', 'Damaged', 'Blocked', 'Missing'];
const wasteTypes: WasteType[] = ['General', 'Organic', 'Recyclable', 'Hazardous', 'Bulky', 'Other'];
const fillLevels = [0, 25, 50, 75, 100] as const;

interface BinFilterBarProps {
  filters: BinFilterState;
  onFilterChange: (filters: Partial<BinFilterState>) => void;
  onClearFilters: () => void;
  isFetching?: boolean;
}

export const BinFilterBar: React.FC<BinFilterBarProps> = ({
  filters,
  onFilterChange,
  onClearFilters,
  isFetching = false,
}) => {
  const [search, setSearch] = useState(filters.search ?? '');

  useEffect(() => {
    setSearch(filters.search ?? '');
  }, [filters.search]);

  useEffect(() => {
    const timer = setTimeout(() => {
      if (search !== (filters.search ?? '')) onFilterChange({ search });
    }, 350);
    return () => clearTimeout(timer);
  }, [filters.search, onFilterChange, search]);

  const isFiltered = Boolean(filters.search || filters.status || filters.wasteType || filters.condition) ||
    filters.minFillLevel !== '' && filters.minFillLevel !== undefined;

  return (
    <Card className="p-4 sm:p-5 shadow-2xs border-slate-200/80">
      <div className="flex flex-col gap-3.5">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
          <label className="block text-xs font-semibold text-slate-700">
            Search bins
            <input
              aria-label="Search bins"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Bin code or address..."
              className="mt-1 w-full px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white"
            />
          </label>
          <SelectFilter label="Administrative status" value={filters.status ?? ''} onChange={(status) => onFilterChange({ status: status as BinAdministrativeStatus | '' })}>
            <option value="">All statuses</option>
            {statuses.map((status) => <option key={status} value={status}>{BIN_STATUS_LABELS[status]}</option>)}
          </SelectFilter>
          <SelectFilter label="Accepted waste type" value={filters.wasteType ?? ''} onChange={(wasteType) => onFilterChange({ wasteType: wasteType as WasteType | '' })}>
            <option value="">All waste types</option>
            {wasteTypes.map((wasteType) => <option key={wasteType} value={wasteType}>{wasteType}</option>)}
          </SelectFilter>
          <SelectFilter label="Latest condition" value={filters.condition ?? ''} onChange={(condition) => onFilterChange({ condition: condition as BinCondition | '' })}>
            <option value="">All conditions</option>
            {conditions.map((condition) => <option key={condition} value={condition}>{BIN_CONDITION_LABELS[condition]}</option>)}
          </SelectFilter>
          <SelectFilter label="Minimum fill level" value={filters.minFillLevel ?? ''} onChange={(value) => onFilterChange({ minFillLevel: value === '' ? '' : Number(value) as 0 | 25 | 50 | 75 | 100 })}>
            <option value="">Any latest fill level</option>
            {fillLevels.map((level) => <option key={level} value={level}>{level}% or higher</option>)}
          </SelectFilter>
        </div>
        <div className="flex items-center justify-between pt-2 border-t border-slate-100 text-xs">
          {isFetching ? <span className="inline-flex items-center gap-1.5 text-slate-400 font-medium"><span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />Updating list...</span> : <span />}
          {isFiltered && <Button variant="ghost" size="sm" onClick={onClearFilters} className="text-xs text-emerald-700 hover:text-emerald-800 hover:bg-emerald-50 font-semibold">Clear filters</Button>}
        </div>
      </div>
    </Card>
  );
};

const SelectFilter: React.FC<{ label: string; value: string | number; onChange: (value: string) => void; children: React.ReactNode }> = ({ label, value, onChange, children }) => (
  <label className="block text-xs font-semibold text-slate-700">
    {label}
    <select value={value} onChange={(event) => onChange(event.target.value)} className="mt-1 w-full px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white">
      {children}
    </select>
  </label>
);

