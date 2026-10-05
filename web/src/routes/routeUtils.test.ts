import { describe, it, expect } from 'vitest';
import { getDefaultRouteForRole } from './routeUtils';

describe('getDefaultRouteForRole', () => {
  it('returns /officer/dashboard for WasteOfficer', () => {
    expect(getDefaultRouteForRole('WasteOfficer')).toBe('/officer/dashboard');
  });

  it('returns /manager/dashboard for MunicipalManager', () => {
    expect(getDefaultRouteForRole('MunicipalManager')).toBe('/manager/dashboard');
  });

  it('returns / for Citizen', () => {
    expect(getDefaultRouteForRole('Citizen')).toBe('/');
  });

  it('returns / for Driver', () => {
    expect(getDefaultRouteForRole('Driver')).toBe('/');
  });

  it('returns / for null or undefined or unknown role', () => {
    expect(getDefaultRouteForRole(null)).toBe('/');
    expect(getDefaultRouteForRole(undefined)).toBe('/');
    expect(getDefaultRouteForRole('')).toBe('/');
    expect(getDefaultRouteForRole('UnknownRole')).toBe('/');
  });
});
