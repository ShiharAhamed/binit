import React from 'react';
import { Button } from '../../../components/ui/Button';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { BIN_CONDITION_LABELS, type BinCondition, type BinObservationDto } from '../types/bins';

const conditionClasses: Record<BinCondition, string> = { Good: 'bg-emerald-50 text-emerald-800 border-emerald-200', Damaged: 'bg-amber-50 text-amber-800 border-amber-200', Blocked: 'bg-red-50 text-red-800 border-red-200', Missing: 'bg-red-50 text-red-800 border-red-200' };
const formatDate = (value: string) => { const date = new Date(value); return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(date); };

interface ObservationHistoryProps {
  observations: BinObservationDto[];
  isLoading: boolean;
  isError: boolean;
  errorMessage: string | null;
  totalCount: number;
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onRetry: () => void;
}

export const ObservationHistory: React.FC<ObservationHistoryProps> = ({ observations, isLoading, isError, errorMessage, totalCount, currentPage, totalPages, onPageChange, onRetry }) => {
  if (isLoading) return <div className="py-10 flex justify-center" data-testid="observation-history-loading"><LoadingSpinner label="Loading observation history" /></div>;
  if (isError) return <div className="rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-800" data-testid="observation-history-error"><p className="font-semibold">Unable to load observation history</p><p className="mt-1 text-xs">{errorMessage}</p><Button variant="secondary" size="sm" className="mt-3 border-red-200 text-red-800" onClick={onRetry}>Retry</Button></div>;
  if (!observations.length) return <div className="rounded-lg bg-slate-50 border border-slate-200 p-5 text-center" data-testid="observation-history-empty"><p className="font-semibold text-slate-800">No observations recorded</p><p className="mt-1 text-xs text-slate-500">Historical human-recorded fill levels and conditions will appear here.</p></div>;
  const newestFirst = [...observations].sort((a, b) => b.recordedAt.localeCompare(a.recordedAt) || b.id.localeCompare(a.id));
  return <div className="space-y-3" data-testid="observation-history-list"><div className="space-y-2.5">{newestFirst.map((observation, index) => <article key={observation.id} className={`rounded-lg border p-3.5 ${index === 0 ? 'border-emerald-200 bg-emerald-50/40' : 'border-slate-200 bg-white'}`}><div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2"><div><div className="flex items-center gap-2"><span className="text-sm font-bold text-slate-900">Observed {observation.fillLevelPercent}%</span><span className={`inline-flex px-2 py-0.5 rounded border text-[11px] font-semibold ${conditionClasses[observation.condition]}`}>{BIN_CONDITION_LABELS[observation.condition]}</span>{index === 0 && <span className="text-[10px] font-bold uppercase tracking-wide text-emerald-700">Most recent</span>}</div><p className="mt-1 text-xs text-slate-500">Recorded by {observation.recordedByUserName || 'Officer name unavailable'} · {formatDate(observation.recordedAt)}</p></div></div>{observation.notes && <p className="mt-3 text-xs text-slate-700 whitespace-pre-wrap border-t border-slate-100 pt-2.5">{observation.notes}</p>}</article>)}</div>{totalCount > 0 && <div className="pt-3 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 text-xs text-slate-500"><span>Page {currentPage} of {Math.max(1, totalPages)} · {totalCount} {totalCount === 1 ? 'observation' : 'observations'}</span><div className="flex gap-2"><Button variant="secondary" size="sm" disabled={currentPage <= 1} onClick={() => onPageChange(currentPage - 1)}>Previous</Button><Button variant="secondary" size="sm" disabled={currentPage >= totalPages} onClick={() => onPageChange(currentPage + 1)}>Next</Button></div></div>}</div>;
};
