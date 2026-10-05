import React from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { Button } from '../components/ui/Button';

export const AppLayout: React.FC = () => {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="min-h-screen flex flex-col bg-slate-50 text-slate-900">
      <header className="bg-white border-b border-slate-200 shadow-sm px-4 sm:px-6 lg:px-8 py-3.5 flex justify-between items-center sticky top-0 z-10">
        <div className="flex items-center gap-2.5">
          <div
            className="w-8 h-8 rounded-lg bg-emerald-600 text-white flex items-center justify-center font-bold text-sm shadow-xs"
            aria-hidden="true"
          >
            SW
          </div>
          <h1 className="text-lg font-bold text-slate-900 tracking-tight">
            Smart Waste Management System
          </h1>
        </div>

        <div className="flex items-center gap-3 sm:gap-4">
          {user && (
            <div className="flex items-center gap-2">
              <span className="text-sm font-semibold text-slate-800 hidden sm:inline">
                {user.fullName}
              </span>
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-sky-50 text-sky-700 border border-sky-200 uppercase tracking-wide">
                {user.role}
              </span>
            </div>
          )}
          <Button variant="danger" size="sm" onClick={handleLogout}>
            Logout
          </Button>
        </div>
      </header>

      <main className="flex-1 w-full max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <Outlet />
      </main>
    </div>
  );
};
