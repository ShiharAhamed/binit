import React from 'react';
import type { CollectionAssignmentStatus } from '../types/assignments';

const statusClasses: Record<CollectionAssignmentStatus, string> = {
  Assigned: 'bg-violet-50 text-violet-800 border-violet-200/80',
  InProgress: 'bg-blue-50 text-blue-800 border-blue-200/80',
  Completed: 'bg-emerald-50 text-emerald-800 border-emerald-200/80',
  PartiallyCompleted: 'bg-amber-50 text-amber-800 border-amber-200/80',
  Failed: 'bg-rose-50 text-rose-800 border-rose-200/80',
  Cancelled: 'bg-slate-100 text-slate-700 border-slate-200',
};

const statusLabels: Record<CollectionAssignmentStatus, string> = {
  Assigned: 'Assigned',
  InProgress: 'In progress',
  Completed: 'Completed',
  PartiallyCompleted: 'Partially completed',
  Failed: 'Failed',
  Cancelled: 'Cancelled',
};

export const AssignmentStatusBadge: React.FC<{ status: CollectionAssignmentStatus }> = ({ status }) => (
  <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-[11px] font-semibold ${statusClasses[status]}`}>
    {statusLabels[status]}
  </span>
);
