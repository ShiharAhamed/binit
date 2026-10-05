import React from 'react';
import { CheckCircleIcon } from '../../../components/ui/Icons';

export interface NeedsAttentionSectionProps {
  title?: string;
  badgeText?: string;
  subtitle?: string;
  emptyTitle?: string;
  emptyDescription?: string;
  children?: React.ReactNode;
}

export const NeedsAttentionSection: React.FC<NeedsAttentionSectionProps> = ({
  title = 'Needs Attention',
  badgeText = 'Active Queue',
  subtitle = 'Items requiring immediate officer review or operational intervention',
  emptyTitle = 'All Operational Queues Clear',
  emptyDescription = 'No live operational data is available yet. Waste reports and other operational items will appear here once the corresponding modules are connected.',
  children,
}) => {
  return (
    <div className="bg-white rounded-xl border border-slate-200/80 shadow-2xs p-5 sm:p-6">
      <div className="flex items-center justify-between gap-4 mb-4 pb-3 border-b border-slate-100">
        <div>
          <div className="flex items-center gap-2.5">
            <h2 className="text-base font-bold text-slate-900 tracking-tight">
              {title}
            </h2>
            <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200/80">
              {badgeText}
            </span>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            {subtitle}
          </p>
        </div>

        <span className="text-xs font-semibold text-emerald-700 hover:text-emerald-800 hidden sm:inline-flex items-center gap-1 cursor-default select-none">
          <span>Active Operations</span>
        </span>
      </div>

      {children ?? <div className="py-10 px-4 text-center rounded-xl bg-slate-50/60 border border-dashed border-slate-200">
        <div
          className="w-11 h-11 rounded-full bg-emerald-50 border border-emerald-100 text-emerald-600 mx-auto flex items-center justify-center mb-3 shadow-2xs"
          aria-hidden="true"
        >
          <CheckCircleIcon className="w-5 h-5 text-emerald-600" />
        </div>
        <p className="text-sm font-bold text-slate-800">
          {emptyTitle}
        </p>
        <p className="text-xs text-slate-500 max-w-md mx-auto mt-1.5 leading-relaxed">
          {emptyDescription}
        </p>
      </div>}
    </div>
  );
};
