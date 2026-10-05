import React, { useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { DashboardSidebar, type NavItem } from '../components/layout/DashboardSidebar';
import { DashboardHeader } from '../components/layout/DashboardHeader';

interface DashboardLayoutProps {
  navItems?: NavItem[];
  title?: string;
}

export const DashboardLayout: React.FC<DashboardLayoutProps> = ({
  navItems,
  title,
}) => {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const { pathname } = useLocation();
  const isDashboardHome = pathname === '/officer/dashboard' || pathname === '/manager/dashboard';

  return (
    <div className={`min-h-screen flex text-slate-900 ${isDashboardHome ? 'bg-[#F8FBFC]' : 'bg-[#F0F6F9]'}`}>
      {/* Desktop Fixed Sidebar */}
      <div className="hidden md:flex md:shrink-0 sticky top-0 h-screen">
        <DashboardSidebar items={navItems} />
      </div>

      {/* Mobile Drawer Backdrop & Drawer */}
      {isMobileMenuOpen && (
        <div
          className="fixed inset-0 z-40 md:hidden flex"
          role="dialog"
          aria-modal="true"
        >
          {/* Backdrop */}
          <div
            className="fixed inset-0 bg-slate-900/40 backdrop-blur-xs transition-opacity"
            onClick={() => setIsMobileMenuOpen(false)}
            aria-hidden="true"
          />

          {/* Drawer Sidebar */}
          <div className="relative z-50 w-64 max-w-xs flex flex-col bg-[#064e3b] shadow-xl">
            <DashboardSidebar
              items={navItems}
              onCloseMobile={() => setIsMobileMenuOpen(false)}
            />
          </div>
        </div>
      )}

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        <DashboardHeader
          title={title}
          onOpenMobile={() => setIsMobileMenuOpen(true)}
        />
        <main className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8 max-w-7xl w-full mx-auto">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default DashboardLayout;
