/**
 * Returns the default home/dashboard route for an authenticated user based on their role.
 *
 * - WasteOfficer -> '/officer/dashboard'
 * - Other roles without a dedicated React dashboard yet (e.g. Citizen, Driver) -> '/' (HomePage fallback)
 *
 * Extensible for future roles (e.g. MunicipalManager -> '/manager/dashboard').
 */
export const getDefaultRouteForRole = (role?: string | null): string => {
  switch (role) {
    case 'WasteOfficer':
      return '/officer/dashboard';
    case 'MunicipalManager':
      return '/manager/dashboard';
    default:
      return '/';
  }
};
