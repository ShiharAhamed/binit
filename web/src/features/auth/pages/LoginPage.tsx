import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useLocation } from 'react-router-dom';
import axios from 'axios';
import { authApi } from '../../../api/authApi';
import { useAuthStore } from '../../../store/authStore';
import { loginSchema, type LoginFormData } from '../schemas/authSchemas';
import { Input } from '../../../components/ui/Input';
import { Button } from '../../../components/ui/Button';
import { Alert } from '../../../components/ui/Alert';
import { getDefaultRouteForRole } from '../../../routes/routeUtils';
import { EyeIcon, EyeOffIcon } from '../../../components/ui/Icons';

export const LoginPage: React.FC = () => {
  const [serverError, setServerError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [isAccessModalOpen, setIsAccessModalOpen] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { setAuth } = useAuthStore();

  const successMessage = (location.state as { successMessage?: string })?.successMessage;

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const onSubmit = async (data: LoginFormData) => {
    setServerError(null);
    setIsLoading(true);

    try {
      const response = await authApi.login({ ...data, clientType: 'web' });

      // Client-side defense-in-depth: React only persists WasteOfficer and MunicipalManager
      if (response.user?.role !== 'WasteOfficer' && response.user?.role !== 'MunicipalManager') {
        useAuthStore.getState().logout();
        setServerError('This account is for the SmartWaste mobile application.');
        return;
      }

      setAuth(response.accessToken, response.user);

      // Check if temporary password forced change is required
      if (response.mustChangePassword || response.user?.mustChangePassword) {
        navigate('/account/change-password', { replace: true });
        return;
      }

      const destination = getDefaultRouteForRole(response.user?.role);
      navigate(destination, { replace: true });
    } catch (err: unknown) {
      useAuthStore.getState().logout();
      if (axios.isAxiosError(err)) {
        const detail =
          err.response?.data?.detail ||
          err.response?.data?.title ||
          'Failed to sign in. Please check your credentials.';
        setServerError(detail);
      } else {
        setServerError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <h2 className="text-xl font-semibold text-slate-900 text-center mb-5">
        Sign in to your account
      </h2>

      {successMessage && (
        <Alert variant="success" className="mb-4">
          {successMessage}
        </Alert>
      )}

      {serverError && (
        <Alert variant="error" className="mb-4">
          {serverError}
        </Alert>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
        <Input
          id="email"
          label="Email address"
          type="email"
          autoComplete="email"
          placeholder="you@example.com"
          error={errors.email?.message}
          {...register('email')}
        />

        <Input
          id="password"
          label="Password"
          type={showPassword ? 'text' : 'password'}
          autoComplete="current-password"
          placeholder="••••••••"
          error={errors.password?.message}
          {...register('password')}
          rightElement={
            <button
              type="button"
              onClick={() => setShowPassword((prev) => !prev)}
              className="text-slate-400 hover:text-slate-600 focus:outline-none focus:text-slate-600 p-1 transition-colors cursor-pointer rounded"
              aria-label={showPassword ? 'Hide' : 'Show'}
              title={showPassword ? 'Hide' : 'Show'}
            >
              {showPassword ? (
                <EyeOffIcon className="w-4.5 h-4.5 text-slate-500" />
              ) : (
                <EyeIcon className="w-4.5 h-4.5 text-slate-400" />
              )}
            </button>
          }
        />

        <Button
          type="submit"
          variant="primary"
          size="md"
          isLoading={isLoading}
          className="w-full mt-2 font-semibold shadow-xs"
        >
          {isLoading ? 'Signing in...' : 'Sign in'}
        </Button>
      </form>

      <div className="mt-6 text-center text-sm">
        <button
          type="button"
          onClick={() => setIsAccessModalOpen(true)}
          className="text-slate-500 hover:text-emerald-700 underline underline-offset-2 transition-colors cursor-pointer text-sm font-medium"
        >
          Don't have an account?
        </button>
      </div>

      {/* Access Information Modal */}
      {isAccessModalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-2xs animate-in fade-in"
          role="dialog"
          aria-modal="true"
          aria-labelledby="access-modal-title"
          onClick={(e) => {
            if (e.target === e.currentTarget) setIsAccessModalOpen(false);
          }}
        >
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-xl border border-slate-100 relative">
            <div className="flex items-center justify-between mb-4 pb-3 border-b border-slate-100">
              <h3 id="access-modal-title" className="text-lg font-bold text-slate-900">
                Need access to SmartWaste?
              </h3>
              <button
                type="button"
                onClick={() => setIsAccessModalOpen(false)}
                className="text-slate-400 hover:text-slate-600 p-1 rounded-lg transition-colors cursor-pointer"
                aria-label="Close dialog"
              >
                ✕
              </button>
            </div>

            <div className="space-y-4 text-sm text-slate-600">
              <div>
                <h4 className="font-semibold text-slate-800 mb-1">
                  For Citizens and Drivers:
                </h4>
                <p className="leading-relaxed">
                  SmartWaste mobile services are available through the mobile application.
                  Citizens can create an account from the mobile app.
                  Driver accounts are created by the municipal administration.
                </p>
              </div>

              <div>
                <h4 className="font-semibold text-slate-800 mb-1">
                  For Waste Officers and Municipal Managers:
                </h4>
                <p className="leading-relaxed">
                  Staff accounts are created internally.
                  Please contact your Municipal Manager or authorized municipal administrator if you need an account.
                </p>
              </div>
            </div>

            <div className="mt-6 pt-4 border-t border-slate-100 flex justify-end">
              <Button
                type="button"
                variant="primary"
                size="sm"
                onClick={() => setIsAccessModalOpen(false)}
              >
                Close
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
