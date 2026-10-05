import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '../../../store/authStore';
import { dashboardApi } from '../api/dashboardApi';
import { OverviewCard } from '../components/OverviewCard';
import { OfficerNeedsAttentionQueue } from '../components/OfficerNeedsAttentionQueue';
import { QuickActions } from '../components/QuickActions';
import {
  ReportsIcon,
  BinsIcon,
  SchedulesIcon,
  TasksIcon,
  ComplaintsIcon,
  LeafIcon,
} from '../../../components/ui/Icons';

export const OfficerDashboardPage: React.FC = () => {
  const { user } = useAuthStore();
  const overviewQuery = useQuery({
    queryKey: ['waste-officer-dashboard', 'overview'],
    queryFn: dashboardApi.getOverview,
    staleTime: 30_000,
    refetchOnMount: 'always',
  });
  const overview = overviewQuery.isError ? undefined : overviewQuery.data;
  const needsAttentionQuery = useQuery({
    queryKey: ['waste-officer-dashboard', 'needs-attention'],
    queryFn: dashboardApi.getNeedsAttention,
    staleTime: 30_000,
    refetchOnMount: 'always',
  });

  const currentFormattedDate = new Intl.DateTimeFormat('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date());

  return (
    <div className="space-y-6">
      {/* Dashboard Top Heading / Breadcrumb */}
      <div className="border-b border-slate-200/80 pb-5">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <div className="flex items-center gap-1.5 text-xs text-slate-400 font-medium mb-1">
              <span>Operations</span>
              <span>/</span>
              <span className="text-slate-600 font-semibold">Waste Officer Dashboard</span>
            </div>
            <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 flex items-center gap-2">
              <span>Waste Officer Dashboard</span>
              <LeafIcon className="w-6 h-6 text-emerald-600 shrink-0 inline-block" />
            </h1>
            <p className="mt-1 text-xs sm:text-sm text-slate-500 leading-relaxed">
              Welcome back,{' '}
              <span className="font-semibold text-slate-800">
                {user?.fullName || 'Waste Officer'}
              </span>
              . Here&apos;s an overview of municipal waste operations requiring your
              attention.
            </p>
          </div>

          {/* Present Date & Environmental Tagline */}
          <div className="text-left sm:text-right shrink-0">
            <p className="text-xs sm:text-sm font-bold text-slate-800 tracking-tight">
              {currentFormattedDate}
            </p>
            <p className="text-[11px] sm:text-xs text-slate-500 font-medium flex items-center sm:justify-end gap-1 mt-0.5">
              <span>Together for a cleaner, greener city</span>
              <span role="img" aria-label="leaf">🍃</span>
            </p>
          </div>
        </div>
      </div>

      {/* Operational Overview KPI Cards */}
      <div>
        <div className="mb-4">
          <h2 className="text-xs font-bold text-slate-900 tracking-wider uppercase">
            Operational Overview
          </h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Real-time status indicators across municipal collection domains
          </p>
        </div>

        {overviewQuery.isError && (
          <div role="alert" className="mb-4 flex items-center gap-3 text-sm text-rose-700">
            <span>Unable to load operational overview.</span>
            <button type="button" className="font-semibold underline" onClick={() => void overviewQuery.refetch()}>
              Retry
            </button>
          </div>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-3.5">
          <OverviewCard
            label="Reports Awaiting Review"
            value={overview?.reportsAwaitingReview ?? '—'}
            description="Pending field verification"
            icon={<ReportsIcon />}
            variant="mint"
            to="/officer/waste-reports"
          />
          <OverviewCard
            label="Active Bins"
            value={overview?.activeBins ?? '—'}
            description="Monitored collection points"
            icon={<BinsIcon />}
            variant="emerald"
          />
          <OverviewCard
            label="Scheduled Collections"
            value={overview?.scheduledCollections ?? '—'}
            description="Active route schedules"
            icon={<SchedulesIcon />}
            variant="blue"
          />
          <OverviewCard
            label="Open Collection Tasks"
            value={overview?.openCollectionTasks ?? '—'}
            description="Assigned & in-progress"
            icon={<TasksIcon />}
            variant="mint"
          />
          <OverviewCard
            label="Open Complaints"
            value={overview?.openComplaints ?? '—'}
            description="Citizen service inquiries"
            icon={<ComplaintsIcon />}
            variant="rose"
          />
        </div>
      </div>

      {/* Quick Actions Bar */}
      <QuickActions />

      {/* Needs Attention Section */}
      <OfficerNeedsAttentionQueue
        items={needsAttentionQuery.data}
        isLoading={needsAttentionQuery.isLoading}
        isError={needsAttentionQuery.isError}
        onRetry={() => void needsAttentionQuery.refetch()}
      />
    </div>
  );
};

export default OfficerDashboardPage;
