import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthLayout } from '../layouts/AuthLayout';
import { AppLayout } from '../layouts/AppLayout';
import { LoginPage } from '../features/auth/pages/LoginPage';
import { HomePage } from '../pages/HomePage';
import { UnauthorizedPage } from '../pages/UnauthorizedPage';
import { NotFoundPage } from '../pages/NotFoundPage';
import { ProtectedRoute } from './ProtectedRoute';
import { RoleRoute } from './RoleRoute';
import { useAuthStore } from '../store/authStore';
import { getDefaultRouteForRole } from './routeUtils';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';

import { DashboardLayout } from '../layouts/DashboardLayout';
import { OfficerDashboardPage } from '../features/officer/pages/OfficerDashboardPage';
import { ManagerDashboardPage } from '../features/manager/pages/ManagerDashboardPage';
import { UserManagementPage } from '../features/manager/pages/UserManagementPage';
import { ChangePasswordPage } from '../features/auth/pages/ChangePasswordPage';
import { managerNavItems } from '../components/layout/navConfig';
import { WasteReportsPage } from '../features/reporting/pages/WasteReportsPage';
import { WasteReportDetailPage } from '../features/reporting/pages/WasteReportDetailPage';
import { BinsPage } from '../features/collection/pages/BinsPage';
import { BinDetailPage } from '../features/collection/pages/BinDetailPage';
import { EditBinPage, RegisterBinPage } from '../features/collection/pages/BinFormPages';
import { RecordBinObservationPage } from '../features/collection/pages/RecordBinObservationPage';
import { CollectionNeedsPage } from '../features/collection/pages/CollectionNeedsPage';
import { CollectionTaskEnhancedDetailPage, CollectionTasksPage } from '../features/collection/pages/CollectionTasksPages';
import { ManagerFleetRoutesPage } from '../features/fleet/pages/ManagerFleetRoutesPage';
import { DispatchRoutesPage } from '../features/fleet/pages/DispatchRoutesPage';
import { AiApprovalsPage } from '../features/ai-approvals/pages/AiApprovalsPage';
import { AiWorkflowDetailPage } from '../features/ai-approvals/pages/AiWorkflowDetailPage';
import { ComplaintsPage } from '../features/complaints/pages/ComplaintsPage';
import { ComplaintDetailPage } from '../features/complaints/pages/ComplaintDetailPage';
import { OperationalIssuesPage } from '../features/operations/pages/OperationalIssuesPage';
import { OperationalIssueDetailPage } from '../features/operations/pages/OperationalIssueDetailPage';

/**
 * Renders auth pages or redirects already-authenticated users to their role default dashboard.
 * Holds session restoration state to prevent intermediate redirect flashes.
 */
const PublicAuthRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { isAuthenticated, isLoading, user } = useAuthStore();

  if (isLoading && isAuthenticated) {
    return (
      <div
        className="py-12 flex flex-col items-center justify-center text-slate-500 gap-3"
        role="status"
        aria-label="Verifying session"
      >
        <LoadingSpinner size="md" label="Verifying session" />
        <p className="text-sm text-slate-500">Verifying session...</p>
      </div>
    );
  }

  if (isAuthenticated) {
    const destination = user?.mustChangePassword
      ? '/account/change-password'
      : getDefaultRouteForRole(user?.role);
    return <Navigate to={destination} replace />;
  }

  return <>{children}</>;
};

/**
 * Root route handler (/).
 * If the authenticated role has a dedicated dashboard (e.g. WasteOfficer),
 * redirect directly to that dashboard. Otherwise, render HomePage fallback.
 */
const RootRoute: React.FC = () => {
  const { user } = useAuthStore();
  if (user?.mustChangePassword) {
    return <Navigate to="/account/change-password" replace />;
  }
  const defaultRoute = getDefaultRouteForRole(user?.role);

  if (defaultRoute !== '/') {
    return <Navigate to={defaultRoute} replace />;
  }

  return <HomePage />;
};

const defaultQueryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
});

export const AppRoutes: React.FC = () => {
  return (
    <QueryClientProvider client={defaultQueryClient}>
      <Routes>
      {/* Public / Auth routes */}
      <Route element={<AuthLayout />}>
        <Route
          path="/login"
          element={
            <PublicAuthRoute>
              <LoginPage />
            </PublicAuthRoute>
          }
        />
        <Route path="/register" element={<Navigate to="/login" replace />} />
      </Route>

      {/* WasteOfficer protected routes wrapped in DashboardLayout */}
      <Route element={<ProtectedRoute />}>
        <Route element={<RoleRoute allowedRoles={['WasteOfficer']} />}>
          <Route element={<DashboardLayout />}>
            <Route path="/officer/dashboard" element={<OfficerDashboardPage />} />
            <Route path="/officer/waste-reports" element={<WasteReportsPage />} />
            <Route path="/officer/waste-reports/:id" element={<WasteReportDetailPage />} />
            <Route path="/officer/reports" element={<Navigate to="/officer/waste-reports" replace />} />
            <Route path="/officer/reports/:id" element={<WasteReportDetailPage />} />
            <Route path="/officer/bins" element={<BinsPage />} />
            <Route path="/officer/bins/register" element={<RegisterBinPage />} />
            <Route path="/officer/bins/:id/observations/new" element={<RecordBinObservationPage />} />
            <Route path="/officer/bins/:id" element={<BinDetailPage />} />
            <Route path="/officer/bins/:id/edit" element={<EditBinPage />} />
            <Route path="/officer/schedules" element={<CollectionNeedsPage />} />
            <Route path="/officer/tasks" element={<CollectionTasksPage />} />
            <Route path="/officer/tasks/:id" element={<CollectionTaskEnhancedDetailPage />} />
            <Route path="/officer/ai-approvals" element={<AiApprovalsPage />} />
            <Route path="/officer/ai-approvals/:workflowId" element={<AiWorkflowDetailPage />} />
            <Route path="/officer/dispatch" element={<DispatchRoutesPage />} />
            <Route path="/officer/complaints" element={<ComplaintsPage />} />
            <Route path="/officer/complaints/:id" element={<ComplaintDetailPage />} />
            <Route path="/officer/operations" element={<OperationalIssuesPage />} />
            <Route path="/officer/operations/:id" element={<OperationalIssueDetailPage />} />
          </Route>
        </Route>
      </Route>

      {/* MunicipalManager protected routes wrapped in DashboardLayout */}
      <Route element={<ProtectedRoute />}>
        <Route element={<RoleRoute allowedRoles={['MunicipalManager']} />}>
          <Route element={<DashboardLayout navItems={managerNavItems} title="Municipal Manager Dashboard" />}>
            <Route path="/manager/dashboard" element={<ManagerDashboardPage />} />
            <Route path="/manager/reports" element={<WasteReportsPage />} />
            <Route path="/manager/reports/:id" element={<WasteReportDetailPage />} />
            <Route path="/manager/waste-reports" element={<Navigate to="/manager/reports" replace />} />
            <Route path="/manager/waste-reports/:id" element={<WasteReportDetailPage />} />
            <Route path="/manager/complaints" element={<ComplaintsPage />} />
            <Route path="/manager/complaints/:id" element={<ComplaintDetailPage />} />
            <Route path="/manager/operations" element={<OperationalIssuesPage />} />
            <Route path="/manager/operations/:id" element={<OperationalIssueDetailPage />} />
            <Route path="/manager/users" element={<UserManagementPage />} />
            <Route path="/manager/tasks" element={<CollectionTasksPage />} />
            <Route path="/manager/tasks/:id" element={<CollectionTaskEnhancedDetailPage />} />
            <Route path="/manager/ai-approvals" element={<AiApprovalsPage />} />
            <Route path="/manager/ai-approvals/:workflowId" element={<AiWorkflowDetailPage />} />
            <Route
              path="/manager/fleet"
              element={<ManagerFleetRoutesPage />}
            />
          </Route>
        </Route>
      </Route>

      {/* Shared Change Password route */}
      <Route element={<ProtectedRoute />}>
        <Route element={<DashboardLayout title="Change Password" />}>
          <Route path="/account/change-password" element={<ChangePasswordPage />} />
        </Route>
      </Route>

      {/* Protected routes wrapped in AppLayout */}
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route path="/" element={<RootRoute />} />

          {/* Verification role routes */}
          <Route
            element={
              <RoleRoute allowedRoles={['MunicipalManager']} />
            }
          >
            <Route
              path="/manager-test"
              element={
                <div className="p-6 bg-white rounded-xl border border-slate-200 shadow-sm text-slate-800 font-medium">
                  Municipal Manager Exclusive Area
                </div>
              }
            />
          </Route>
        </Route>
      </Route>

      {/* Error / info routes */}
      <Route path="/unauthorized" element={<UnauthorizedPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
    </QueryClientProvider>
  );
};
