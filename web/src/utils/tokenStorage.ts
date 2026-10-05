/**
 * Centralized token storage helper.
 *
 * Current implementation: Browser localStorage.
 * Centralizing this access ensures that if storage mechanism changes
 * (e.g. secure cookies, session storage), only this single file needs to be updated.
 */
const TOKEN_KEY = 'smartwaste_access_token';

export const tokenStorage = {
  getToken: (): string | null => {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  },

  setToken: (token: string): void => {
    try {
      localStorage.setItem(TOKEN_KEY, token);
    } catch (e) {
      console.error('Failed to save token to localStorage', e);
    }
  },

  removeToken: (): void => {
    try {
      localStorage.removeItem(TOKEN_KEY);
    } catch (e) {
      console.error('Failed to remove token from localStorage', e);
    }
  },

  hasToken: (): boolean => {
    return Boolean(tokenStorage.getToken());
  },
};
