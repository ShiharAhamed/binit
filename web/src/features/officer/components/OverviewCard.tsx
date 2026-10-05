import React from 'react';
import { Link } from 'react-router-dom';

export type OverviewVariant = 'amber' | 'emerald' | 'blue' | 'mint' | 'rose' | 'slate';

export interface OverviewCardProps {
  label: string;
  value?: string | number;
  description?: string;
  icon: React.ReactNode;
  iconColorStyle?: string;
  iconBgColor?: string;
  variant?: OverviewVariant;
  to?: string;
}

const variantStyles: Record<
  OverviewVariant,
  { card: string; iconContainer: string; iconText: string }
> = {
  amber: {
    card: 'bg-amber-100/30 border-amber-300 hover:border-amber-400 hover:bg-amber-200/80',
    iconContainer: 'bg-[#FEF3C7] border-amber-200/80',
    iconText: 'text-[#D97706]',
  },
  emerald: {
    card: 'bg-emerald-100/30 border-emerald-300 hover:border-emerald-400 hover:bg-emerald-200/80',
    iconContainer: 'bg-[#D1FAE5] border-emerald-200/80',
    iconText: 'text-[#059669]',
  },
  blue: {
    card: 'bg-sky-100/30 border-sky-300 hover:border-sky-400 hover:bg-sky-200/80',
    iconContainer: 'bg-[#DBEAFE] border-blue-200/80',
    iconText: 'text-[#2563EB]',
  },
  mint: {
    card: 'bg-teal-100/30 border-teal-300 hover:border-teal-400 hover:bg-teal-200/80',
    iconContainer: 'bg-[#D1FAE5] border-emerald-200/80',
    iconText: 'text-[#10B981]',
  },
  rose: {
    card: 'bg-rose-100/30 border-rose-300 hover:border-rose-400 hover:bg-rose-200/80',
    iconContainer: 'bg-[#FFE4E6] border-rose-200/80',
    iconText: 'text-[#E11D48]',
  },
  slate: {
    card: 'bg-slate-100/30 border-slate-300 hover:border-slate-400 hover:bg-slate-200/80',
    iconContainer: 'bg-slate-100 border-slate-200',
    iconText: 'text-slate-700',
  },
};

export const OverviewCard: React.FC<OverviewCardProps> = ({
  label,
  value = '—',
  description,
  icon,
  iconColorStyle,
  iconBgColor,
  variant = 'emerald',
  to,
}) => {
  const currentVariant = variantStyles[variant] || variantStyles.emerald;
  const resolvedCardStyle = currentVariant.card;
  const resolvedIconContainerStyle =
    iconColorStyle ||
    iconBgColor ||
    `${currentVariant.iconContainer} ${currentVariant.iconText}`;

  const content = (
    <>
      <div
        className={`w-11 h-11 rounded-full border flex items-center justify-center shrink-0 mt-0.5 shadow-2xs ${resolvedIconContainerStyle}`}
        aria-hidden="true"
      >
        {icon}
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-2xl font-bold text-slate-900 leading-tight tracking-tight">
          {value}
        </p>
        <p className="text-xs font-bold text-slate-800 leading-snug mt-0.5">
          {label}
        </p>
        {description && (
          <p className="text-[11px] text-slate-500 mt-1 leading-normal">
            {description}
          </p>
        )}
      </div>
    </>
  );

  if (to) {
    return (
      <Link
        to={to}
        className={`rounded-xl border shadow-sm hover:shadow-md p-4 flex items-start gap-3.5 transition-all block text-left ${resolvedCardStyle}`}
      >
        {content}
      </Link>
    );
  }

  return (
    <div
      className={`rounded-xl border shadow-sm p-4 flex items-start gap-3.5 transition-all ${resolvedCardStyle}`}
    >
      {content}
    </div>
  );
};
