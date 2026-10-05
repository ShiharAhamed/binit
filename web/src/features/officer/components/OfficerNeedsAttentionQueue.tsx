import React from 'react';
import { Link } from 'react-router-dom';
import type { WasteOfficerNeedsAttentionItem } from '../types/dashboard';
import { NeedsAttentionSection } from './NeedsAttentionSection';

interface DashboardNeedsAttentionQueueProps {
  items?: WasteOfficerNeedsAttentionItem[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  role?: 'officer' | 'manager';
}

export const DashboardNeedsAttentionQueue: React.FC<DashboardNeedsAttentionQueueProps> = ({
  items, isLoading, isError, onRetry, role = 'officer',
}) => {
  let content: React.ReactNode;

  if (isLoading) {
    content = <div role="status" className="py-8 text-center text-sm text-slate-500">Loading items needing review…</div>;
  } else if (isError) {
    content = <div role="alert" className="py-8 text-center text-sm text-rose-700">
      Unable to load items needing attention.{' '}
      <button type="button" className="font-semibold underline" onClick={onRetry}>Retry</button>
    </div>;
  } else if (items?.length) {
    content = <div className="overflow-x-auto">
      <table className="w-full min-w-[900px] table-fixed text-left text-sm border-collapse" aria-label="Items needing initial review">
        <thead>
          <tr className="bg-slate-50/80 border-b border-slate-200/80 text-xs font-semibold uppercase tracking-wider text-slate-500">
            <th scope="col" className="w-[12%] px-3 py-3">Type</th>
            <th scope="col" className="w-[18%] px-3 py-3">Reference</th>
            <th scope="col" className="w-[14%] px-3 py-3">Category</th>
            <th scope="col" className="w-[19%] px-3 py-3">Submitted by</th>
            <th scope="col" className="w-[23%] px-3 py-3">Location</th>
            <th scope="col" className="w-[14%] px-3 py-3 whitespace-nowrap">Submitted</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {items.map((item) => {
            const isReport = item.itemType === 'WasteReport';
            const path = isReport ? `/${role}/waste-reports/${item.id}` : `/${role}/complaints/${item.id}`;
            const address = item.addressText?.trim();
            return <tr key={`${item.itemType}-${item.id}`} className="hover:bg-slate-50/70 transition-colors">
              <td className="px-3 py-3">
                <span className={`inline-flex rounded-md px-2 py-1 text-[11px] font-semibold ${isReport ? 'bg-emerald-50 text-emerald-700' : 'bg-blue-50 text-blue-700'}`}>
                  {isReport ? 'Report' : 'Complaint'}
                </span>
              </td>
              <td className="px-3 py-3">
                <Link to={path} className="font-semibold text-slate-900 hover:text-emerald-700 hover:underline focus-visible:rounded focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-600">
                  {item.reference}
                </Link>
              </td>
              <td className="px-3 py-3 text-slate-700">{item.secondaryLabel}</td>
              <td className="px-3 py-3 text-slate-700"><span className="block truncate" title={item.submittedByName}>{item.submittedByName}</span></td>
              <td className="px-3 py-3 text-slate-600">
                {address ? <span className="block truncate" title={address}>{address}</span> : <span className="text-slate-400">—</span>}
              </td>
              <td className="px-3 py-3 text-xs text-slate-500 whitespace-nowrap">
                <time dateTime={item.createdAt}>
                  {new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(item.createdAt))}
                </time>
              </td>
            </tr>;
          })}
        </tbody>
      </table>
    </div>;
  }

  return <NeedsAttentionSection
    subtitle="Newest waste reports and complaints awaiting initial review"
    emptyTitle={role === 'manager' ? 'All Review Queues Clear' : 'All Operational Queues Clear'}
    emptyDescription={role === 'manager'
      ? 'No waste reports or complaints are currently awaiting initial review.'
      : 'No waste reports or complaints are currently awaiting review.'}
  >{content}</NeedsAttentionSection>;
};

export const OfficerNeedsAttentionQueue = DashboardNeedsAttentionQueue;
