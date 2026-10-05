import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';

interface ProtectedRouteProps {
  children?: React.ReactNode;
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children }) => {
  const { isAuthenticated, isLoading, user } = useAuthStore();
  const location = useLocation();

  if (isLoading) {
    return (
      <div
        className="min-h-screen flex flex-col items-center justify-center bg-slate-50 text-slate-600"
        role="status"
        aria-label="Loading authentication"
      >
        <LoadingSpinner size="lg" label="Verifying session" />
        <p className="mt-4 text-sm text-slate-500">Verifying session...</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (user?.mustChangePassword && location.pathname !== '/account/change-password') {
    return <Navigate to="/account/change-password" replace />;
  }

  return children ? <>{children}</> : <Outlet />;
};
