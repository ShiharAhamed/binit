import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, Link } from 'react-router-dom';
import axios from 'axios';
import { authApi } from '../../../api/authApi';
import { useAuthStore } from '../../../store/authStore';
import { changePasswordSchema, type ChangePasswordFormData } from '../schemas/authSchemas';
import { Input } from '../../../components/ui/Input';
import { Button } from '../../../components/ui/Button';
import { Alert } from '../../../components/ui/Alert';
import { Card } from '../../../components/ui/Card';
import { getDefaultRouteForRole } from '../../../routes/routeUtils';

export const ChangePasswordPage: React.FC = () => {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();
  const [serverError, setServerError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const isForced = Boolean(user?.mustChangePassword);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ChangePasswordFormData>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: {
      currentPassword: '',
      newPassword: '',
      confirmNewPassword: '',
    },
  });

  const onSubmit = async (data: ChangePasswordFormData) => {
    setServerError(null);
    setIsLoading(true);

    try {
      await authApi.changePassword({
        currentPassword: data.currentPassword,
        newPassword: data.newPassword,
      });

      // Clear current JWT/session and redirect to Login with friendly message
      logout();
      navigate('/login', {
        replace: true,
        state: {
          successMessage: 'Password changed successfully. Sign in with your new password.',
        },
      });
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const detail =
          err.response?.data?.detail ||
          err.response?.data?.title ||
          'Failed to change password. Please check your credentials.';
        setServerError(detail);
      } else {
        setServerError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  const cancelDestination = getDefaultRouteForRole(user?.role);

  return (
    <div className="py-8 max-w-xl mx-auto px-4">
      <Card className="p-6 sm:p-8">
        <div className="mb-6">
          <div className="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-emerald-50 text-emerald-700 border border-emerald-200 mb-3">
            <svg
              className="w-6 h-6"
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
          </div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            {isForced ? 'Change Temporary Password' : 'Change Password'}
          </h1>
          <p className="text-sm text-slate-500 mt-1">
            {isForced
              ? 'You must change your temporary password before continuing.'
              : 'Update your account password below.'}
          </p>
        </div>

        {isForced && (
          <Alert variant="info" className="mb-5">
            You must change your temporary password before continuing.
          </Alert>
        )}

        {serverError && (
          <Alert variant="error" className="mb-5">
            {serverError}
          </Alert>
        )}

        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
          <Input
            id="currentPassword"
            label={isForced ? 'Current Temporary Password' : 'Current Password'}
            type="password"
            autoComplete="current-password"
            placeholder="••••••••"
            error={errors.currentPassword?.message}
            {...register('currentPassword')}
          />

          <Input
            id="newPassword"
            label="New Password"
            type="password"
            autoComplete="new-password"
            placeholder="••••••••"
            error={errors.newPassword?.message}
            {...register('newPassword')}
          />

          <Input
            id="confirmNewPassword"
            label="Confirm New Password"
            type="password"
            autoComplete="new-password"
            placeholder="••••••••"
            error={errors.confirmNewPassword?.message}
            {...register('confirmNewPassword')}
          />

          <div className="pt-2 flex items-center justify-between gap-3">
            {!isForced ? (
              <Link
                to={cancelDestination}
                className="px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-900 rounded-lg hover:bg-slate-100 transition-colors"
              >
                Cancel
              </Link>
            ) : (
              <button
                type="button"
                onClick={() => {
                  logout();
                  navigate('/login', { replace: true });
                }}
                className="px-4 py-2 text-sm font-medium text-slate-500 hover:text-slate-800 rounded-lg hover:bg-slate-100 transition-colors"
              >
                Sign out
              </button>
            )}

            <Button
              type="submit"
              variant="primary"
              size="md"
              isLoading={isLoading}
              className="ml-auto"
            >
              {isLoading ? 'Updating Password...' : 'Save New Password'}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  );
};

export default ChangePasswordPage;
