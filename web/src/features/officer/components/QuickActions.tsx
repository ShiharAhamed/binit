import React from 'react';
import { Link } from 'react-router-dom';
import {
  ReportsIcon,
  BinsIcon,
  SchedulesIcon,
  TasksIcon,
  ComplaintsIcon,
} from '../../../components/ui/Icons';

export type QuickActionVariant = 'amber' | 'emerald' | 'blue' | 'mint' | 'rose';

export interface ActionItem {
  label: string;
  to: string;
  icon: React.ReactNode;
  variant?: QuickActionVariant;
}

export interface QuickActionsProps {
  title?: string;
  description?: string;
  actions?: ActionItem[];
}

const quickActionVariants: Record<
  QuickActionVariant,
  { card: string; iconContainer: string; arrow: string }
> = {
  amber: {
    card: 'bg-amber-100/30 border-amber-300 hover:border-amber-400 hover:bg-amber-200/80',
    iconContainer: 'bg-[#FEF3C7] border-amber-200/80 text-[#D97706]',
    arrow: 'text-[#D97706] group-hover:text-amber-700',
  },
  emerald: {
    card: 'bg-emerald-100/30 border-emerald-300 hover:border-emerald-400 hover:bg-emerald-200/80',
    iconContainer: 'bg-[#D1FAE5] border-emerald-200/80 text-[#059669]',
    arrow: 'text-[#059669] group-hover:text-emerald-700',
  },
  blue: {
    card: 'bg-sky-100/30 border-sky-300 hover:border-sky-400 hover:bg-sky-200/80',
    iconContainer: 'bg-[#DBEAFE] border-blue-200/80 text-[#2563EB]',
    arrow: 'text-[#2563EB] group-hover:text-blue-700',
  },
  mint: {
    card: 'bg-teal-100/30 border-teal-300 hover:border-teal-400 hover:bg-teal-200/80',
    iconContainer: 'bg-[#D1FAE5] border-emerald-200/80 text-[#10B981]',
    arrow: 'text-[#10B981] group-hover:text-emerald-700',
  },
  rose: {
    card: 'bg-rose-100/30 border-rose-300 hover:border-rose-400 hover:bg-rose-200/80',
    iconContainer: 'bg-[#FFE4E6] border-rose-200/80 text-[#E11D48]',
    arrow: 'text-[#E11D48] group-hover:text-rose-700',
  },
};

const defaultVariantOrder: QuickActionVariant[] = ['amber', 'emerald', 'blue', 'mint', 'rose'];

const defaultActions: ActionItem[] = [
  {
    label: 'Review Waste Reports',
    to: '/officer/waste-reports',
    icon: <ReportsIcon className="w-4 h-4" />,
    variant: 'mint',
  },
  {
    label: 'Manage Bins',
    to: '/officer/bins',
    icon: <BinsIcon className="w-4 h-4" />,
    variant: 'emerald',
  },
  {
    label: 'View Collection Schedules',
    to: '/officer/schedules',
    icon: <SchedulesIcon className="w-4 h-4" />,
    variant: 'blue',
  },
  {
    label: 'View Collection Tasks',
    to: '/officer/tasks',
    icon: <TasksIcon className="w-4 h-4" />,
    variant: 'mint',
  },
  {
    label: 'Review Complaints',
    to: '/officer/complaints',
    icon: <ComplaintsIcon className="w-4 h-4" />,
    variant: 'rose',
  },
];

export const QuickActions: React.FC<QuickActionsProps> = ({
  title = 'Quick Actions',
  description = 'Fast-track operational dispatch and review workflows',
  actions = defaultActions,
}) => {
  return (
    <div>
      <div className="mb-4">
        <h2 className="text-xs font-bold text-slate-900 tracking-wider uppercase">
          {title}
        </h2>
        {description && (
          <p className="text-xs text-slate-500 mt-0.5">
            {description}
          </p>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3.5">
        {actions.map((act, idx) => {
          const v = act.variant || defaultVariantOrder[idx % defaultVariantOrder.length];
          const styles = quickActionVariants[v];

          return (
            <Link
              key={act.to}
              to={act.to}
              className={`group flex items-center justify-between p-3 rounded-xl border transition-all focus:outline-none focus:ring-2 focus:ring-emerald-500 shadow-sm hover:shadow-md ${styles.card}`}
            >
              <div className="flex items-center gap-2.5 min-w-0">
                <div
                  className={`w-9 h-9 rounded-full border flex items-center justify-center shrink-0 transition-colors shadow-2xs ${styles.iconContainer}`}
                  aria-hidden="true"
                >
                  {act.icon}
                </div>
                <span className="text-xs font-semibold text-slate-800 group-hover:text-slate-950 transition-colors leading-snug truncate">
                  {act.label}
                </span>
              </div>
              <span
                className={`group-hover:translate-x-0.5 transition-transform text-xs font-bold shrink-0 ml-2 ${styles.arrow}`}
                aria-hidden="true"
              >
                &rarr;
              </span>
            </Link>
          );
        })}
      </div>
    </div>
  );
};
