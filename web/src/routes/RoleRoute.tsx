import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';

interface RoleRouteProps {
  allowedRoles: string[];
  children?: React.ReactNode;
}

export const RoleRoute: React.FC<RoleRouteProps> = ({ allowedRoles, children }) => {
  const { user, isAuthenticated, isLoading } = useAuthStore();

  if (isLoading) {
    return (
      <div
        className="py-12 flex flex-col items-center justify-center text-slate-500 gap-3"
        role="status"
        aria-label="Checking permissions"
      >
        <LoadingSpinner size="md" label="Checking permissions" />
        <p className="text-sm text-slate-500">Checking permissions...</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  const hasRole = user?.role && allowedRoles.includes(user.role);

  if (!hasRole) {
    return <Navigate to="/unauthorized" replace />;
  }

  return children ? <>{children}</> : <Outlet />;
};
