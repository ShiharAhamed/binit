import React, { useState, useEffect, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import axios from 'axios';
import { usersApi } from '../../../api/authApi';
import { useAuthStore } from '../../../store/authStore';
import type { UserManagementDto, CreateUserResponse } from '../../auth/types';
import { createUserSchema, type CreateUserFormData } from '../../auth/schemas/authSchemas';
import { Card } from '../../../components/ui/Card';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { Alert } from '../../../components/ui/Alert';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';

export const UserManagementPage: React.FC = () => {
  const { user: currentUser } = useAuthStore();

  const [users, setUsers] = useState<UserManagementDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filter state
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 15;

  // Create User Modal state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);

  // One-time temporary password result modal state
  const [createdResult, setCreatedResult] = useState<CreateUserResponse | null>(null);
  const [copied, setCopied] = useState(false);

  // Status updating state
  const [updatingId, setUpdatingId] = useState<string | null>(null);
  const [refreshIndex, setRefreshIndex] = useState(0);
  const refreshUsers = useCallback(() => setRefreshIndex((prev) => prev + 1), []);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setIsLoading(true);
      setError(null);
      try {
        const query: { page: number; pageSize: number; search?: string; role?: string; isActive?: boolean } = {
          page,
          pageSize,
        };
        if (search.trim()) query.search = search.trim();
        if (roleFilter) query.role = roleFilter;
        if (statusFilter !== '') query.isActive = statusFilter === 'true';

        const result = await usersApi.getUsers(query);
        if (!ignore) {
          setUsers(result.items);
          setTotalCount(result.totalCount);
        }
      } catch (err: unknown) {
        if (!ignore) {
          if (axios.isAxiosError(err)) {
            setError(err.response?.data?.detail || 'Failed to load user accounts.');
          } else {
            setError('An unexpected error occurred while loading users.');
          }
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    load();
    return () => {
      ignore = true;
    };
  }, [page, search, roleFilter, statusFilter, refreshIndex]);

  const {
    register,
    handleSubmit,
    reset: resetCreateForm,
    formState: { errors: createErrors },
  } = useForm<CreateUserFormData>({
    resolver: zodResolver(createUserSchema),
    defaultValues: {
      fullName: '',
      email: '',
      username: '',
      role: 'Driver',
    },
  });

  const handleOpenCreateModal = () => {
    resetCreateForm();
    setCreateError(null);
    setIsCreateModalOpen(true);
  };

  const handleCloseCreateModal = () => {
    setIsCreateModalOpen(false);
    resetCreateForm();
    setCreateError(null);
  };

  const handleCreateSubmit = async (data: CreateUserFormData) => {
    setCreateError(null);
    setIsCreating(true);
    try {
      const response = await usersApi.createUser(data);
      handleCloseCreateModal();
      // Show one-time temporary password modal
      setCreatedResult(response);
      setCopied(false);
      refreshUsers();
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const detail =
          err.response?.data?.detail ||
          err.response?.data?.title ||
          'Failed to create user account. Please check your inputs.';
        setCreateError(detail);
      } else {
        setCreateError('An unexpected error occurred while creating user.');
      }
    } finally {
      setIsCreating(false);
    }
  };

  const handleCloseTempPasswordModal = () => {
    // Explicitly wipe the one-time temporary password from memory
    setCreatedResult(null);
    setCopied(false);
  };

  const handleCopyPassword = () => {
    if (createdResult?.temporaryPassword) {
      navigator.clipboard.writeText(createdResult.temporaryPassword);
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    }
  };

  const handleToggleStatus = async (user: UserManagementDto) => {
    if (user.id === currentUser?.id) {
      return;
    }
    const newStatus = !user.isActive;
    const confirmMessage = newStatus
      ? `Reactivate account for ${user.fullName}?`
      : `Deactivate account for ${user.fullName}? They will not be able to sign in.`;

    if (!window.confirm(confirmMessage)) {
      return;
    }

    setUpdatingId(user.id);
    try {
      await usersApi.updateUserStatus(user.id, newStatus);
      refreshUsers();
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        alert(err.response?.data?.detail || 'Failed to update user status.');
      } else {
        alert('An unexpected error occurred.');
      }
    } finally {
      setUpdatingId(null);
    }
  };

  const formatRoleBadge = (role: string) => {
    switch (role) {
      case 'MunicipalManager':
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-purple-50 text-purple-700 border border-purple-200">
            Manager
          </span>
        );
      case 'WasteOfficer':
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
            Waste Officer
          </span>
        );
      case 'Driver':
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-50 text-amber-700 border border-amber-200">
            Driver
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-700">
            {role}
          </span>
        );
    }
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 text-xs font-medium text-slate-400 mb-1">
            <span>Operations</span>
            <span>/</span>
            <span className="text-emerald-700 font-semibold">User Management</span>
          </div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">User Management</h1>
          <p className="text-sm text-slate-500 mt-0.5">
            Create and manage municipal officers, managers, and collection drivers.
          </p>
        </div>

        <Button
          type="button"
          variant="primary"
          size="md"
          onClick={handleOpenCreateModal}
          className="flex items-center gap-2 self-start sm:self-auto"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
          </svg>
          <span>Create User</span>
        </Button>
      </div>

      {/* Filter Bar */}
      <Card className="p-4">
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <div>
            <label htmlFor="user-search" className="block text-xs font-medium text-slate-600 mb-1">
              Search Staff
            </label>
            <input
              id="user-search"
              type="text"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              placeholder="Search by name, username, email..."
              className="w-full px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            />
          </div>

          <div>
            <label htmlFor="role-filter" className="block text-xs font-medium text-slate-600 mb-1">
              Role Filter
            </label>
            <select
              id="role-filter"
              value={roleFilter}
              onChange={(e) => {
                setRoleFilter(e.target.value);
                setPage(1);
              }}
              className="w-full px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option value="">All Staff Roles</option>
              <option value="Driver">Driver</option>
              <option value="WasteOfficer">Waste Officer</option>
              <option value="MunicipalManager">Municipal Manager</option>
            </select>
          </div>

          <div>
            <label htmlFor="status-filter" className="block text-xs font-medium text-slate-600 mb-1">
              Account Status
            </label>
            <select
              id="status-filter"
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setPage(1);
              }}
              className="w-full px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option value="">All Statuses</option>
              <option value="true">Active Only</option>
              <option value="false">Inactive Only</option>
            </select>
          </div>
        </div>
      </Card>

      {/* Error Message */}
      {error && (
        <Alert variant="error">
          {error}
        </Alert>
      )}

      {/* Users Table */}
      <Card className="overflow-hidden">
        {isLoading ? (
          <div className="py-16 flex flex-col items-center justify-center gap-3 text-slate-500">
            <LoadingSpinner size="lg" />
            <p className="text-sm font-medium">Loading user accounts...</p>
          </div>
        ) : users.length === 0 ? (
          <div className="py-16 text-center text-slate-500">
            <p className="text-base font-semibold text-slate-800">No staff users found</p>
            <p className="text-xs text-slate-400 mt-1">Try adjusting your search criteria or create a new user.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs border-collapse">
              <thead>
                <tr className="bg-slate-50/80 border-b border-slate-200/80 text-slate-500 uppercase tracking-wider font-semibold">
                  <th className="py-3.5 px-4">Full Name</th>
                  <th className="py-3.5 px-4">Username</th>
                  <th className="py-3.5 px-4">Email</th>
                  <th className="py-3.5 px-4">Role</th>
                  <th className="py-3.5 px-4">Status</th>
                  <th className="py-3.5 px-4">Security Setup</th>
                  <th className="py-3.5 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {users.map((u) => {
                  const isSelf = u.id === currentUser?.id;
                  return (
                    <tr key={u.id} className="hover:bg-slate-50/60 transition-colors">
                      <td className="py-3 px-4 font-semibold text-slate-900">
                        {u.fullName}
                        {isSelf && (
                          <span className="ml-2 text-[10px] bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded font-medium">
                            You
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-4 text-slate-600 font-mono text-[11px]">{u.username}</td>
                      <td className="py-3 px-4 text-slate-600">{u.email}</td>
                      <td className="py-3 px-4">{formatRoleBadge(u.role)}</td>
                      <td className="py-3 px-4">
                        {u.isActive ? (
                          <span className="inline-flex items-center gap-1.5 text-emerald-700 font-medium text-xs">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                            Active
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1.5 text-rose-700 font-medium text-xs">
                            <span className="w-1.5 h-1.5 rounded-full bg-rose-500" />
                            Inactive
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-4">
                        {u.mustChangePassword ? (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-amber-50 text-amber-800 border border-amber-200">
                            Temp Password
                          </span>
                        ) : (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-slate-50 text-slate-600 border border-slate-200">
                            Normal
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-4 text-right">
                        <button
                          type="button"
                          disabled={isSelf || updatingId === u.id}
                          onClick={() => handleToggleStatus(u)}
                          title={isSelf ? 'You cannot deactivate your own account' : undefined}
                          className={`px-3 py-1 text-xs font-semibold rounded-lg transition-colors ${
                            isSelf
                              ? 'opacity-40 cursor-not-allowed bg-slate-100 text-slate-400'
                              : u.isActive
                              ? 'text-rose-700 bg-rose-50 hover:bg-rose-100'
                              : 'text-emerald-700 bg-emerald-50 hover:bg-emerald-100'
                          }`}
                        >
                          {updatingId === u.id ? 'Updating...' : u.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalCount > pageSize && (
          <div className="p-4 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
            <span>
              Showing {Math.min((page - 1) * pageSize + 1, totalCount)} to{' '}
              {Math.min(page * pageSize, totalCount)} of {totalCount} users
            </span>
            <div className="flex items-center gap-2">
              <Button
                variant="secondary"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Previous
              </Button>
              <span className="font-semibold text-slate-700">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="secondary"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              >
                Next
              </Button>
            </div>
          </div>
        )}
      </Card>

      {/* Create User Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-2xs animate-in fade-in">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-xl border border-slate-100">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-bold text-slate-900">Create Staff User</h3>
              <button
                type="button"
                onClick={handleCloseCreateModal}
                className="text-slate-400 hover:text-slate-600 p-1 rounded-lg"
              >
                ✕
              </button>
            </div>

            {createError && (
              <Alert variant="error" className="mb-4">
                {createError}
              </Alert>
            )}

            <form onSubmit={handleSubmit(handleCreateSubmit)} noValidate className="space-y-4">
              <Input
                id="fullName"
                label="Full Name"
                type="text"
                placeholder="e.g. Kasun Fernando"
                error={createErrors.fullName?.message}
                {...register('fullName')}
              />

              <Input
                id="email"
                label="Email Address"
                type="email"
                placeholder="staff@smartwaste.local"
                error={createErrors.email?.message}
                {...register('email')}
              />

              <Input
                id="username"
                label="Username"
                type="text"
                placeholder="kasun_driver"
                error={createErrors.username?.message}
                {...register('username')}
              />

              <div>
                <label htmlFor="role" className="block text-xs font-medium text-slate-700 mb-1">
                  Assigned Staff Role
                </label>
                <select
                  id="role"
                  className="w-full px-3.5 py-2.5 text-sm bg-white border border-slate-300 rounded-lg text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                  {...register('role')}
                >
                  <option value="Driver">Driver</option>
                  <option value="WasteOfficer">Waste Officer</option>
                  <option value="MunicipalManager">Municipal Manager</option>
                </select>
                {createErrors.role?.message && (
                  <p className="mt-1 text-xs text-rose-600 font-medium">
                    {createErrors.role.message}
                  </p>
                )}
                <p className="text-[11px] text-slate-400 mt-1">
                  Note: Citizen accounts cannot be created through internal user management.
                </p>
              </div>

              <div className="pt-3 flex items-center justify-end gap-3">
                <Button
                  type="button"
                  variant="secondary"
                  size="md"
                  onClick={handleCloseCreateModal}
                >
                  Cancel
                </Button>
                <Button
                  type="submit"
                  variant="primary"
                  size="md"
                  isLoading={isCreating}
                >
                  {isCreating ? 'Creating User...' : 'Create Account'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* One-Time Temporary Password Display Modal */}
      {createdResult && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-2xs animate-in fade-in">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200">
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold">
                ✓
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">Account Created Successfully</h3>
                <p className="text-xs text-slate-500">{createdResult.user.fullName} ({createdResult.user.role})</p>
              </div>
            </div>

            <div className="bg-amber-50 border border-amber-200 rounded-xl p-3.5 text-xs text-amber-900 mb-4 leading-relaxed">
              <span className="font-bold block mb-1">Important: One-Time Display</span>
              This temporary password is shown only once. Give it securely to the user. They will be required to create a new password at first login.
            </div>

            <div className="space-y-1.5 mb-6">
              <label className="block text-xs font-semibold text-slate-700">
                Temporary Password:
              </label>
              <div className="flex items-center gap-2">
                <div className="flex-1 bg-slate-100 border border-slate-300 rounded-lg px-3 py-2 font-mono text-sm font-bold text-slate-900 select-all tracking-wider">
                  {createdResult.temporaryPassword}
                </div>
                <Button
                  type="button"
                  variant={copied ? 'primary' : 'secondary'}
                  size="md"
                  onClick={handleCopyPassword}
                  className="shrink-0"
                >
                  {copied ? 'Copied!' : 'Copy'}
                </Button>
              </div>
            </div>

            <Button
              type="button"
              variant="primary"
              size="md"
              onClick={handleCloseTempPasswordModal}
              className="w-full"
            >
              Done / Close
            </Button>
          </div>
        </div>
      )}
    </div>
  );
};

export default UserManagementPage;
