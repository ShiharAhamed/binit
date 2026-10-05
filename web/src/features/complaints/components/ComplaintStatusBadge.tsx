import React from 'react';
import { COMPLAINT_STATUS_LABELS, type ComplaintStatus } from '../types/complaints';

export interface ComplaintStatusBadgeProps {
  status: ComplaintStatus;
  className?: string;
}

const statusStyles: Record<ComplaintStatus, string> = {
  Submitted: 'bg-amber-50 text-amber-800 border-amber-200/80',
  InReview: 'bg-blue-50 text-blue-800 border-blue-200/80',
  Resolved: 'bg-emerald-50 text-emerald-800 border-emerald-200/80',
};

export const ComplaintStatusBadge: React.FC<ComplaintStatusBadgeProps> = ({
  status,
  className = '',
}) => {
  const badgeStyle = statusStyles[status] || 'bg-slate-100 text-slate-700 border-slate-200';
  const label = COMPLAINT_STATUS_LABELS[status] || status;

  return (
    <span
      className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[11px] font-semibold border ${badgeStyle} ${className}`}
      data-status={status}
      data-testid="complaint-status-badge"
    >
      {label}
    </span>
  );
};
