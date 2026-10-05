import React from 'react';
import { Link } from 'react-router-dom';
import type { WasteReportSummaryDto } from '../types/reporting';
import { WASTE_TYPE_LABELS } from '../types/reporting';
import { WasteReportStatusBadge } from './WasteReportStatusBadge';
import { WasteReportPriorityBadge } from './WasteReportPriorityBadge';
import { Button } from '../../../components/ui/Button';
import { useAuthStore } from '../../../store/authStore';
import { reportLabel } from '../../../utils/displayReferences';

export interface WasteReportsTableProps {
  reports: WasteReportSummaryDto[];
  isLoading: boolean;
  isFiltered: boolean;
  onClearFilters: () => void;
  detailBasePath?: string;
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

export const WasteReportsTable: React.FC<WasteReportsTableProps> = ({
  reports,
  isLoading,
  isFiltered,
  onClearFilters,
  detailBasePath,
}) => {
  const { user } = useAuthStore();
  const basePath =
    detailBasePath ||
    (user?.role === 'MunicipalManager' ? '/manager/reports' : '/officer/waste-reports');

  // 1. Loading Skeleton State
  if (isLoading) {
    return (
      <div className="overflow-x-auto" data-testid="reports-table-loading">
        <table className="w-full text-left text-xs border-collapse">
          <thead>
            <tr className="bg-slate-50/80 border-b border-slate-200/80 text-slate-500 uppercase tracking-wider font-semibold">
              <th className="py-3.5 px-4">Report</th>
              <th className="py-3.5 px-4">Waste Type</th>
              <th className="py-3.5 px-4">Status</th>
              <th className="py-3.5 px-4">Priority</th>
              <th className="py-3.5 px-4">Location</th>
              <th className="py-3.5 px-4">Citizen</th>
              <th className="py-3.5 px-4">Submitted</th>
              <th className="py-3.5 px-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {Array.from({ length: 6 }).map((_, idx) => (
              <tr key={idx} className="animate-pulse">
                <td className="py-3.5 px-4">
                  <div className="h-3.5 w-16 bg-slate-200 rounded mb-1.5" />
                  <div className="h-3 w-36 bg-slate-100 rounded" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-4 w-16 bg-slate-200 rounded-full" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-4 w-20 bg-slate-200 rounded-full" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-3 w-12 bg-slate-100 rounded" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-3 w-28 bg-slate-200 rounded" />
                </td>
                <td className="py-3.5 px-4">
                  <div className="h-3 w-20 bg-slate-100 rounded" />
                </td>
                <td className="py-3.5 px-4 text-right">
                  <div className="h-3 w-20 bg-slate-100 rounded ml-auto" />
                </td>
                <td className="py-3.5 px-4 text-right">
                  <div className="h-6 w-20 bg-slate-200 rounded ml-auto" />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  // 2. Filtered Empty State
  if (reports.length === 0 && isFiltered) {
    return (
      <div className="py-16 px-4 text-center" data-testid="reports-table-filtered-empty">
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
          No reports match the current filters.
        </p>
        <p className="text-xs text-slate-500 max-w-sm mx-auto mt-1 mb-4 leading-relaxed">
          Try adjusting search terms or resetting status/waste type filters to view operational reports.
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
  if (reports.length === 0) {
    return (
      <div className="py-16 px-4 text-center" data-testid="reports-table-empty">
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
        <p className="text-base font-bold text-slate-800">No waste reports found.</p>
        <p className="text-xs text-slate-500 max-w-md mx-auto mt-1 leading-relaxed">
          Citizen waste incident reports submitted from the mobile application will appear here for verification and dispatch.
        </p>
      </div>
    );
  }

  // 4. Operational Table with Horizontal Scroll Overflow Container
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-xs border-collapse min-w-[820px]">
        <thead>
          <tr className="bg-slate-50/80 border-b border-slate-200/80 text-slate-500 uppercase tracking-wider font-semibold select-none">
            <th className="py-3.5 px-4 w-[24%]">Report</th>
            <th className="py-3.5 px-4">Waste Type</th>
            <th className="py-3.5 px-4">Status</th>
            <th className="py-3.5 px-4">Priority</th>
            <th className="py-3.5 px-4 w-[20%]">Location</th>
            <th className="py-3.5 px-4">Citizen</th>
            <th className="py-3.5 px-4">Submitted</th>
            <th className="py-3.5 px-4 text-right">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {reports.map((report) => {
            const locationText =
              report.addressText && report.addressText.trim()
                ? report.addressText
                : `${report.latitude.toFixed(5)}, ${report.longitude.toFixed(5)}`;

            const shortRef = reportLabel(report.id, report.reportReference);

            return (
              <tr
                key={report.id}
                className="hover:bg-slate-50/70 transition-colors group"
                data-testid={`report-row-${report.id}`}
              >
                {/* Report Identifier & Description snippet */}
                <td className="py-3.5 px-4">
                  <div className="flex items-center gap-2">
                    <Link
                      to={`${basePath}/${report.id}`}
                      className="font-mono text-[11px] font-bold text-emerald-800 bg-emerald-50 hover:bg-emerald-100 px-1.5 py-0.5 rounded border border-emerald-200/60 transition-colors"
                      title={`Open ${shortRef}`}
                    >
                      {shortRef}
                    </Link>
                  </div>
                  <p
                    className="mt-1 text-slate-700 text-xs line-clamp-1 leading-relaxed"
                    title={report.description}
                  >
                    {report.description}
                  </p>
                </td>

                {/* Waste Type */}
                <td className="py-3.5 px-4">
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-700 border border-slate-200/60">
                    {WASTE_TYPE_LABELS[report.wasteType] || report.wasteType}
                  </span>
                </td>

                {/* Status Badge */}
                <td className="py-3.5 px-4">
                  <WasteReportStatusBadge status={report.status} />
                </td>

                {/* Priority Indicator */}
                <td className="py-3.5 px-4">
                  <WasteReportPriorityBadge priority={report.priority} />
                </td>

                {/* Location Text or Fallback Coordinates */}
                <td className="py-3.5 px-4">
                  <div className="flex items-start gap-1.5 text-slate-600">
                    <svg
                      className="w-3.5 h-3.5 text-slate-400 shrink-0 mt-0.5"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                      aria-hidden="true"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"
                      />
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"
                      />
                    </svg>
                    <span className="line-clamp-2 leading-relaxed" title={locationText}>
                      {locationText}
                    </span>
                  </div>
                </td>

                {/* Citizen Name */}
                <td className="py-3.5 px-4">
                  <span className="text-slate-700 font-medium">
                    {report.citizenName || 'Citizen'}
                  </span>
                </td>

                {/* Submitted Date */}
                <td className="py-3.5 px-4 text-slate-500 whitespace-nowrap">
                  {formatDate(report.createdAt)}
                </td>

                {/* Actions Column */}
                <td className="py-3.5 px-4 text-right whitespace-nowrap">
                  <Link
                    to={`${basePath}/${report.id}`}
                    className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold text-emerald-700 hover:text-emerald-800 bg-emerald-50 hover:bg-emerald-100/80 rounded-md border border-emerald-200/80 transition-colors"
                    aria-label={`View details for report ${shortRef}`}
                    data-testid={`view-details-${report.id}`}
                  >
                    <span>View Details</span>
                    <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                    </svg>
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
