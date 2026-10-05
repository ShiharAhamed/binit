import React from 'react';
import type { WasteReportStatusHistoryDto } from '../types/reporting';
import { STATUS_LABELS } from '../types/reporting';
import { WasteReportStatusBadge } from './WasteReportStatusBadge';
import { Button } from '../../../components/ui/Button';

export interface ReportStatusHistoryTimelineProps {
  history: WasteReportStatusHistoryDto[] | undefined;
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
  onRetry: () => void;
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
      second: '2-digit',
      hour12: false,
    }).format(d);
  } catch {
    return isoString;
  }
}

export const ReportStatusHistoryTimeline: React.FC<ReportStatusHistoryTimelineProps> = ({
  history,
  isLoading,
  isError,
  error,
  onRetry,
}) => {
  if (isLoading) {
    return (
      <div className="py-6 space-y-4" data-testid="status-history-loading">
        {Array.from({ length: 3 }).map((_, idx) => (
          <div key={idx} className="flex items-start gap-4 animate-pulse">
            <div className="w-3 h-3 mt-1.5 rounded-full bg-slate-300 shrink-0" />
            <div className="flex-1 space-y-2">
              <div className="h-4 bg-slate-200 rounded w-1/3" />
              <div className="h-3 bg-slate-100 rounded w-1/2" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div
        className="p-4 rounded-lg bg-red-50/70 border border-red-200 text-sm text-red-700"
        data-testid="status-history-error"
        role="alert"
      >
        <p className="font-semibold text-red-800">Failed to load status history</p>
        <p className="text-xs text-red-600 mt-1">
          {error?.message || 'An unexpected error occurred while fetching history audit records.'}
        </p>
        <div className="mt-3">
          <Button variant="secondary" size="sm" onClick={onRetry}>
            Retry History
          </Button>
        </div>
      </div>
    );
  }

  if (!history || history.length === 0) {
    return (
      <div
        className="py-6 px-4 text-center rounded-lg border border-dashed border-slate-200 bg-slate-50/50"
        data-testid="status-history-empty"
      >
        <p className="text-sm font-medium text-slate-600">No status transition history available.</p>
        <p className="text-xs text-slate-400 mt-0.5">
          Transition audit records will appear here as the report progresses through lifecycle stages.
        </p>
      </div>
    );
  }

  // Sort chronological order (oldest to newest)
  const sortedHistory = [...history].sort(
    (a, b) => new Date(a.changedAt).getTime() - new Date(b.changedAt).getTime()
  );

  return (
    <div className="relative pl-6 space-y-6" data-testid="status-history-timeline">
      {/* Continuous vertical timeline connecting line */}
      <div
        className="absolute left-2 top-2 bottom-2 w-0.5 bg-slate-200"
        aria-hidden="true"
      />

      {sortedHistory.map((entry, index) => {
        const isInitial = entry.fromStatus === null;
        const fromLabel = entry.fromStatus ? STATUS_LABELS[entry.fromStatus] || entry.fromStatus : null;

        return (
          <div
            key={entry.id || index}
            className="relative flex items-start gap-3 group"
            data-testid={`history-step-${index}`}
          >
            {/* Timeline node dot */}
            <div
              className={`absolute -left-[1.85rem] mt-1.5 w-3 h-3 rounded-full border-2 border-white shadow-xs ${
                index === sortedHistory.length - 1
                  ? 'bg-emerald-600 ring-4 ring-emerald-100'
                  : 'bg-slate-400'
              }`}
              aria-hidden="true"
            />

            <div className="flex-1 bg-white p-3.5 rounded-lg border border-slate-200/80 shadow-2xs">
              {/* Header: Transition & Time */}
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-800">
                  {isInitial ? (
                    <span className="flex items-center gap-1.5">
                      <span>Report Submitted</span>
                      <WasteReportStatusBadge status={entry.toStatus} />
                    </span>
                  ) : (
                    <span className="flex items-center gap-1.5">
                      <span className="text-slate-600 font-medium">{fromLabel}</span>
                      <svg
                        className="w-3.5 h-3.5 text-slate-400 shrink-0"
                        fill="none"
                        stroke="currentColor"
                        viewBox="0 0 24 24"
                        aria-hidden="true"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          strokeWidth={2}
                          d="M14 5l7 7m0 0l-7 7m7-7H3"
                        />
                      </svg>
                      <WasteReportStatusBadge status={entry.toStatus} />
                    </span>
                  )}
                </div>

                <time
                  dateTime={entry.changedAt}
                  className="text-[11px] font-mono text-slate-400 whitespace-nowrap"
                >
                  {formatDate(entry.changedAt)}
                </time>
              </div>

              {/* Transition Actor metadata */}
              <div className="mt-1.5 text-xs text-slate-500 flex items-center gap-1.5">
                <svg
                  className="w-3.5 h-3.5 text-slate-400 shrink-0"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
                  />
                </svg>
                <span>
                  Updated by:{' '}
                  <strong className="text-slate-700 font-medium">
                    {entry.changedByUserName || (isInitial ? 'Citizen' : 'System')}
                  </strong>
                </span>
              </div>

              {/* Notes callout if present */}
              {entry.notes && entry.notes.trim() && (
                <div className="mt-2.5 p-2.5 rounded bg-slate-50 border-l-2 border-slate-300 text-xs text-slate-700">
                  <p className="font-medium text-slate-500 text-[10px] uppercase tracking-wider mb-0.5">
                    Notes
                  </p>
                  <p className="italic">{entry.notes}</p>
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
};
