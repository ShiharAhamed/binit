import { create } from 'zustand';
import type { User } from '../features/auth/types';
import { tokenStorage } from '../utils/tokenStorage';

interface AuthState {
  accessToken: string | null;
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;

  setAuth: (token: string, user: User) => void;
  setUser: (user: User) => void;
  logout: () => void;
  setLoading: (loading: boolean) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: tokenStorage.getToken(),
  user: null,
  isAuthenticated: Boolean(tokenStorage.getToken()),
  isLoading: true, // starts true until initial auth verification completes

  setAuth: (token: string, user: User) => {
    tokenStorage.setToken(token);
    set({
      accessToken: token,
      user,
      isAuthenticated: true,
      isLoading: false,
    });
  },

  setUser: (user: User) => {
    set({
      user,
      isAuthenticated: true,
      isLoading: false,
    });
  },

  logout: () => {
    tokenStorage.removeToken();
    set({
      accessToken: null,
      user: null,
      isAuthenticated: false,
      isLoading: false,
    });
  },

  setLoading: (loading: boolean) => {
    set({ isLoading: loading });
  },
}));
