import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '../../../store/authStore';
import { managerDashboardApi } from '../api/dashboardApi';
import { OverviewCard } from '../../officer/components/OverviewCard';
import { QuickActions, type ActionItem } from '../../officer/components/QuickActions';
import { DashboardNeedsAttentionQueue } from '../../officer/components/OfficerNeedsAttentionQueue';
import {
  AiWorkflowIcon,
  FleetIcon,
  OperationsIcon,
  TasksIcon,
  ComplaintsIcon,
  LeafIcon,
} from '../../../components/ui/Icons';

const managerActions: ActionItem[] = [
  {
    label: 'Review AI Approvals',
    to: '/manager/ai-approvals',
    icon: <AiWorkflowIcon className="w-4 h-4" />,
    variant: 'mint',
  },
  {
    label: 'View Fleet & Routes',
    to: '/manager/fleet',
    icon: <FleetIcon className="w-4 h-4" />,
    variant: 'emerald',
  },
  {
    label: 'Monitor Operations',
    to: '/manager/operations',
    icon: <OperationsIcon className="w-4 h-4" />,
    variant: 'blue',
  },
];

export const ManagerDashboardPage: React.FC = () => {
  const { user } = useAuthStore();
  const overviewQuery = useQuery({
    queryKey: ['municipal-manager-dashboard', 'overview'],
    queryFn: managerDashboardApi.getOverview,
    staleTime: 30_000,
    refetchOnMount: 'always',
  });
  const overview = overviewQuery.isError ? undefined : overviewQuery.data;
  const needsAttentionQuery = useQuery({
    queryKey: ['municipal-manager-dashboard', 'needs-attention'],
    queryFn: managerDashboardApi.getNeedsAttention,
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
              <span className="text-slate-600 font-semibold">Municipal Manager Dashboard</span>
            </div>
            <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 flex items-center gap-2">
              <span>Municipal Manager Dashboard</span>
              <LeafIcon className="w-6 h-6 text-emerald-600 shrink-0 inline-block" />
            </h1>
            <p className="mt-1 text-xs sm:text-sm text-slate-500 leading-relaxed">
              Welcome back,{' '}
              <span className="font-semibold text-slate-800">
                {user?.fullName || 'Municipal Manager'}
              </span>
              . Review operational performance, pending decisions and AI-assisted recommendations.
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

      {/* Management Overview KPI Cards */}
      <div>
        <div className="mb-4">
          <h2 className="text-xs font-bold text-slate-900 tracking-wider uppercase">
            Management Overview
          </h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Real-time status indicators across municipal collection domains
          </p>
        </div>

        {overviewQuery.isError && (
          <div role="alert" className="mb-4 flex items-center gap-3 text-sm text-rose-700">
            <span>Unable to load management overview.</span>
            <button type="button" className="font-semibold underline" onClick={() => void overviewQuery.refetch()}>
              Retry
            </button>
          </div>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-3.5">
          <OverviewCard
            label="AI Workflows Awaiting Approval"
            value={overview?.aiWorkflowsAwaitingApproval ?? '—'}
            description="Pending executive review"
            icon={<AiWorkflowIcon />}
            variant="mint"
          />
          <OverviewCard
            label="Active Collection Assignments"
            value={overview?.activeCollectionAssignments ?? '—'}
            description="Assigned & in-progress routes"
            icon={<TasksIcon />}
            variant="emerald"
          />
          <OverviewCard
            label="Available Vehicles"
            value={overview?.availableVehicles ?? '—'}
            description="Ready for assignment"
            icon={<FleetIcon />}
            variant="blue"
          />
          <OverviewCard
            label="Open Operational Incidents"
            value={overview?.openOperationalIncidents ?? '—'}
            description="Field exceptions reported"
            icon={<OperationsIcon />}
            variant="mint"
            to="/manager/reports"
          />
          <OverviewCard
            label="Unresolved Complaints"
            value={overview?.unresolvedComplaints ?? '—'}
            description="Citizen escalations"
            icon={<ComplaintsIcon />}
            variant="rose"
          />
        </div>
      </div>

      {/* Quick Actions Bar */}
      <QuickActions
        title="Quick Actions"
        description="Fast-track executive oversight, approvals, and decision workflows"
        actions={managerActions}
      />

      {/* Initial review queue */}
      <DashboardNeedsAttentionQueue
        role="manager"
        items={needsAttentionQuery.data}
        isLoading={needsAttentionQuery.isLoading}
        isError={needsAttentionQuery.isError}
        onRetry={() => void needsAttentionQuery.refetch()}
      />
    </div>
  );
};

export default ManagerDashboardPage;
