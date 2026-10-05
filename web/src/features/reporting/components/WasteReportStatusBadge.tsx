import React from 'react';
import { STATUS_LABELS, type WasteReportStatus } from '../types/reporting';

export interface WasteReportStatusBadgeProps {
  status: WasteReportStatus;
  className?: string;
}

const statusStyles: Record<WasteReportStatus, string> = {
  Submitted: 'bg-amber-50 text-amber-800 border-amber-200/80',
  UnderReview: 'bg-amber-50 text-amber-800 border-amber-200/80',
  Verified: 'bg-emerald-50 text-emerald-800 border-emerald-200/80',
  Resolved: 'bg-emerald-50 text-emerald-800 border-emerald-200/80',
  Rejected: 'bg-rose-50 text-rose-800 border-rose-200/80',
  Cancelled: 'bg-slate-100 text-slate-700 border-slate-200',
  Scheduled: 'bg-sky-50 text-sky-800 border-sky-200/80',
  InProgress: 'bg-blue-50 text-blue-800 border-blue-200/80',
};

export const WasteReportStatusBadge: React.FC<WasteReportStatusBadgeProps> = ({
  status,
  className = '',
}) => {
  const badgeStyle = statusStyles[status] || 'bg-slate-100 text-slate-700 border-slate-200';
  const label = STATUS_LABELS[status] || status;

  return (
    <span
      className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[11px] font-semibold border ${badgeStyle} ${className}`}
      data-status={status}
    >
      {label}
    </span>
  );
};
