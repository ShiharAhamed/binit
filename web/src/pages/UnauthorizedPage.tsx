import React from 'react';
import { Link } from 'react-router-dom';

export const UnauthorizedPage: React.FC = () => {
  return (
    <div className="min-h-[60vh] flex items-center justify-center p-6 text-center">
      <div className="max-w-md w-full bg-white rounded-xl border border-slate-200 shadow-sm p-8">
        <h1 className="text-6xl font-extrabold text-red-500 tracking-tight">403</h1>
        <h2 className="text-xl font-bold text-slate-800 mt-2 mb-3">Access Denied</h2>
        <p className="text-sm text-slate-500 leading-relaxed mb-6">
          You do not have the required permissions or role to access this resource.
        </p>
        <Link
          to="/"
          className="inline-flex items-center justify-center font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 bg-emerald-600 text-white hover:bg-emerald-700 active:bg-emerald-800 shadow-sm focus:ring-emerald-500 px-4 py-2 text-sm rounded-lg"
        >
          Return to Home
        </Link>
      </div>
    </div>
  );
};
