import { useEffect } from 'react';
import { authApi } from '../api/authApi';
import { useAuthStore } from '../store/authStore';
import { tokenStorage } from '../utils/tokenStorage';

/**
 * Hook to restore authentication state on application startup.
 * Checks for stored token, validates it against GET /auth/me, and restores user profile.
 */
export const useAuthInit = () => {
  const { setUser, logout, setLoading } = useAuthStore();

  useEffect(() => {
    let isMounted = true;

    const restoreSession = async () => {
      const token = tokenStorage.getToken();

      if (!token) {
        if (isMounted) setLoading(false);
        return;
      }

      try {
        const userProfile = await authApi.getMe();
        if (isMounted) {
          setUser({
            id: userProfile.id,
            fullName: userProfile.fullName,
            email: userProfile.email,
            phoneNumber: userProfile.phoneNumber,
            role: userProfile.role,
            isActive: userProfile.isActive,
            createdAt: userProfile.createdAt,
          });
        }
      } catch (error) {
        console.warn('Session expired or invalid, clearing stored credentials.', error);
        if (isMounted) {
          logout();
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    };

    restoreSession();

    return () => {
      isMounted = false;
    };
  }, [setUser, logout, setLoading]);
};
