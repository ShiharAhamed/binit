import React from 'react';
import {
  DashboardIcon,
  ReportsIcon,
  BinsIcon,
  SchedulesIcon,
  TasksIcon,
  ComplaintsIcon,
  AiWorkflowIcon,
  FleetIcon,
  OperationsIcon,
  UsersIcon,
} from '../ui/Icons';

export interface NavItem {
  label: string;
  to: string;
  icon: React.ReactNode;
}

export const defaultOfficerNavItems: NavItem[] = [
  { label: 'Dashboard', to: '/officer/dashboard', icon: <DashboardIcon /> },
  { label: 'Waste Reports', to: '/officer/waste-reports', icon: <ReportsIcon /> },
  { label: 'Bin Management', to: '/officer/bins', icon: <BinsIcon /> },
  { label: 'Collection Needs', to: '/officer/schedules', icon: <SchedulesIcon /> },
  { label: 'Collection Tasks', to: '/officer/tasks', icon: <TasksIcon /> },
  { label: 'AI Approvals', to: '/officer/ai-approvals', icon: <AiWorkflowIcon /> },
  { label: 'Dispatch & Routes', to: '/officer/dispatch', icon: <FleetIcon /> },
  { label: 'Complaints', to: '/officer/complaints', icon: <ComplaintsIcon /> },
  { label: 'Operations', to: '/officer/operations', icon: <OperationsIcon /> },
];

export const managerNavItems: NavItem[] = [
  { label: 'Dashboard', to: '/manager/dashboard', icon: <DashboardIcon /> },
  { label: 'User Management', to: '/manager/users', icon: <UsersIcon /> },
  { label: 'AI Approvals', to: '/manager/ai-approvals', icon: <AiWorkflowIcon /> },
  { label: 'Fleet & Routes', to: '/manager/fleet', icon: <FleetIcon /> },
  { label: 'Waste Reports', to: '/manager/reports', icon: <ReportsIcon /> },
  { label: 'Complaints', to: '/manager/complaints', icon: <ComplaintsIcon /> },
  { label: 'Operations', to: '/manager/operations', icon: <OperationsIcon /> },
];
