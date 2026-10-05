import React from 'react';
import type { WasteReportPriority } from '../types/reporting';

export interface WasteReportPriorityBadgeProps {
  priority: WasteReportPriority | null | undefined;
  className?: string;
}

const priorityStyles: Record<WasteReportPriority, string> = {
  Low: 'text-slate-600 bg-slate-50 border-slate-200',
  Medium: 'text-amber-700 bg-amber-50 border-amber-200',
  High: 'text-orange-700 bg-orange-50 border-orange-200',
  Urgent: 'text-rose-700 bg-rose-50 border-rose-200 font-bold',
};

export const WasteReportPriorityBadge: React.FC<WasteReportPriorityBadgeProps> = ({
  priority,
  className = '',
}) => {
  if (!priority) {
    return (
      <span
        className={`text-slate-400 text-xs font-normal italic ${className}`}
        title="Priority not yet assigned"
      >
        Not assigned
      </span>
    );
  }

  const badgeStyle = priorityStyles[priority] || 'text-slate-600 bg-slate-50 border-slate-200';

  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded text-[10px] font-medium border ${badgeStyle} ${className}`}
    >
      {priority}
    </span>
  );
};
