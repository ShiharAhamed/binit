import React, { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../store/authStore';
import { MenuIcon, BellIcon, ChevronDownIcon, LogoutIcon, SearchIcon } from '../ui/Icons';

interface DashboardHeaderProps {
  onOpenMobile?: () => void;
  title?: string;
}

export const DashboardHeader: React.FC<DashboardHeaderProps> = ({
  onOpenMobile,
}) => {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  const formatRole = (role?: string) => {
    if (role === 'MunicipalManager') return 'Municipal Manager';
    if (role === 'WasteOfficer') return 'Waste Officer';
    return role || 'Waste Officer';
  };

  const defaultDisplayName = formatRole(user?.role);

  const getInitials = (name?: string) => {
    if (!name) {
      return user?.role === 'MunicipalManager' ? 'MM' : 'WO';
    }
    return name
      .split(' ')
      .map((n) => n[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  };

  useEffect(() => {
    if (!isOpen) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  const handleLogout = () => {
    setIsOpen(false);
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <header className="h-16 bg-white border-b border-slate-200/80 px-4 sm:px-6 lg:px-8 flex items-center justify-between sticky top-0 z-20 shadow-2xs">
      <div className="flex items-center gap-3">
        {onOpenMobile && (
          <button
            type="button"
            onClick={onOpenMobile}
            className="md:hidden text-slate-500 hover:text-slate-700 p-2 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500"
            aria-label="Open navigation menu"
          >
            <MenuIcon className="w-5 h-5" />
          </button>
        )}
        {/* Search Bar UI */}
        <div className="relative w-64 sm:w-80 md:w-96 lg:w-[420px]">
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
            <SearchIcon className="w-4 h-4" />
          </div>
          <input
            type="text"
            placeholder="Search reports, bins, schedules, tasks, or complaints..."
            readOnly
            className="w-full pl-9 pr-4 py-2 text-xs sm:text-sm bg-white border border-slate-200/90 rounded-lg text-slate-800 placeholder-slate-400 shadow-2xs focus:outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 transition-colors"
          />
        </div>
      </div>

      <div className="flex items-center gap-3 sm:gap-4">
        <div
          className="w-8 h-8 rounded-lg border border-slate-200/80 text-slate-400 flex items-center justify-center transition-colors"
          aria-hidden="true"
        >
          <BellIcon className="w-4 h-4" />
        </div>

        {/* Account Menu Dropdown */}
        <div className="relative" ref={menuRef}>
          <button
            type="button"
            id="user-account-menu-button"
            aria-haspopup="menu"
            aria-expanded={isOpen}
            aria-label="User account menu"
            onClick={() => setIsOpen((prev) => !prev)}
            className="flex items-center gap-2.5 pl-3 border-l border-slate-200/80 rounded-lg py-1 px-1.5 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-colors cursor-pointer"
          >
            <div
              className="w-8 h-8 rounded-full bg-emerald-800 text-white font-bold text-xs flex items-center justify-center shrink-0 shadow-2xs"
              aria-hidden="true"
            >
              {getInitials(user?.fullName)}
            </div>
            <div className="hidden sm:block text-left">
              <p className="text-xs font-bold text-slate-900 leading-tight">
                {user?.fullName || defaultDisplayName}
              </p>
              <p className="text-[11px] text-slate-400 font-medium">
                {formatRole(user?.role)}
              </p>
            </div>
            <ChevronDownIcon
              className={`w-3.5 h-3.5 text-slate-400 transition-transform duration-150 ${
                isOpen ? 'rotate-180 text-slate-600' : ''
              }`}
              aria-hidden="true"
            />
          </button>

          {isOpen && (
            <div
              role="menu"
              aria-orientation="vertical"
              aria-labelledby="user-account-menu-button"
              className="absolute right-0 mt-2 w-60 origin-top-right rounded-xl bg-white shadow-lg ring-1 ring-slate-900/10 border border-slate-100 py-1.5 focus:outline-none z-50 animate-in fade-in slide-in-from-top-1"
            >
              <div className="px-4 py-2.5 border-b border-slate-100">
                <p className="text-xs font-bold text-slate-900 truncate">
                  {user?.fullName || defaultDisplayName}
                </p>
                {user?.email && (
                  <p className="text-[11px] text-slate-500 truncate mt-0.5">
                    {user.email}
                  </p>
                )}
                <span className="inline-block mt-1.5 px-2 py-0.5 text-[10px] font-semibold rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200/60">
                  {formatRole(user?.role)}
                </span>
              </div>

              <div className="p-1 space-y-0.5">
                <button
                  type="button"
                  role="menuitem"
                  onClick={() => {
                    setIsOpen(false);
                    navigate('/account/change-password');
                  }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 rounded-lg transition-colors focus:outline-none focus:bg-slate-50"
                >
                  <svg
                    className="w-4 h-4 text-slate-500 shrink-0"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"
                    />
                  </svg>
                  <span>Change Password</span>
                </button>
                <button
                  type="button"
                  role="menuitem"
                  onClick={handleLogout}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold text-rose-700 hover:bg-rose-50 rounded-lg transition-colors focus:outline-none focus:bg-rose-50"
                >
                  <LogoutIcon className="w-4 h-4 text-rose-600 shrink-0" />
                  <span>Logout</span>
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </header>
  );
};
