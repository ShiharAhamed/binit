import React from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { Card } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Alert } from '../components/ui/Alert';

export const HomePage: React.FC = () => {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="py-4">
      <Card className="max-w-2xl mx-auto">
        <h2 className="text-2xl font-bold text-emerald-700 mb-6">
          Smart Waste Management System
        </h2>

        <div className="bg-slate-50 rounded-lg border border-slate-200 p-5 mb-6 space-y-2">
          <p className="text-base text-slate-800">
            Welcome, <strong className="font-semibold text-slate-950">{user?.fullName || 'User'}</strong>
          </p>
          <p className="text-sm text-slate-600 flex items-center gap-2">
            Role:{' '}
            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              {user?.role}
            </span>
          </p>
          <p className="text-xs text-slate-500">Email: {user?.email}</p>
        </div>

        {user?.role === 'WasteOfficer' && (
          <div className="mb-6 p-4 rounded-xl bg-emerald-50 border border-emerald-200 flex items-center justify-between gap-4">
            <div>
              <p className="text-sm font-bold text-emerald-900">
                Waste Officer Console
              </p>
              <p className="text-xs text-emerald-700 mt-0.5">
                Access daily waste reports, bin monitoring, and collection tasks.
              </p>
            </div>
            <Link
              to="/officer/dashboard"
              className="shrink-0 inline-flex items-center gap-1 px-3.5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold rounded-lg shadow-2xs transition-colors"
            >
              Open Dashboard &rarr;
            </Link>
          </div>
        )}

        <Alert variant="info" className="mb-6">
          <strong className="font-semibold">Authentication Foundation Ready:</strong> You
          are securely logged in. Business modules (Waste Reporting, Bin Monitoring,
          Collection Fleet, Complaints, AI Services) will be mounted in subsequent steps.
        </Alert>

        <div className="flex gap-3">
          <Button variant="danger" size="md" onClick={handleLogout}>
            Logout
          </Button>
        </div>
      </Card>
    </div>
  );
};
