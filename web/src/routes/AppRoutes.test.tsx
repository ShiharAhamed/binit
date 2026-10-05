import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppRoutes } from './AppRoutes';
import { useAuthStore } from '../store/authStore';

describe('AppRoutes - Registration Route Handling', () => {
  beforeEach(() => {
    useAuthStore.getState().logout();
    useAuthStore.setState({ isAuthenticated: false, isLoading: false, user: null, accessToken: null });
  });

  it('redirects /register to /login and does not render any registration form', () => {
    render(
      <MemoryRouter initialEntries={['/register']}>
        <AppRoutes />
      </MemoryRouter>
    );

    // Should be redirected to /login
    expect(screen.getByRole('heading', { name: /sign in to your account/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();

    // Confirm no registration form fields exist
    expect(screen.queryByRole('heading', { name: /create citizen account/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /create citizen account/i })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/confirm password/i)).not.toBeInTheDocument();
  });
});

describe('AppRoutes - Complaints Routes Access', () => {
  beforeEach(() => {
    useAuthStore.getState().logout();
  });

  it('allows WasteOfficer to access /officer/complaints', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      user: {
        id: 'officer-1',
        fullName: 'Officer Perera',
        email: 'officer@smartwaste.local',
        role: 'WasteOfficer',
        mustChangePassword: false,
      },
      accessToken: 'officer-token',
    });

    render(
      <MemoryRouter initialEntries={['/officer/complaints']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByTestId('complaints-page')).toBeInTheDocument();
  });

  it('allows MunicipalManager to access /manager/complaints', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      user: {
        id: 'mgr-1',
        fullName: 'Manager Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
        mustChangePassword: false,
      },
      accessToken: 'manager-token',
    });

    render(
      <MemoryRouter initialEntries={['/manager/complaints']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByTestId('complaints-page')).toBeInTheDocument();
  });

  it('redirects unauthenticated user accessing /officer/complaints to /login', () => {
    useAuthStore.setState({
      isAuthenticated: false,
      isLoading: false,
      user: null,
      accessToken: null,
    });

    render(
      <MemoryRouter initialEntries={['/officer/complaints']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /sign in to your account/i })).toBeInTheDocument();
  });
});

describe('AppRoutes - Operations Routes Access', () => {
  beforeEach(() => {
    useAuthStore.getState().logout();
  });

  it('allows WasteOfficer to access /officer/operations', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      user: {
        id: 'officer-1',
        fullName: 'Officer Perera',
        email: 'officer@smartwaste.local',
        role: 'WasteOfficer',
        mustChangePassword: false,
      },
      accessToken: 'officer-token',
    });

    render(
      <MemoryRouter initialEntries={['/officer/operations']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByTestId('operational-issues-page')).toBeInTheDocument();
  });

  it('allows MunicipalManager to access /manager/operations', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      user: {
        id: 'mgr-1',
        fullName: 'Manager Silva',
        email: 'manager@smartwaste.local',
        role: 'MunicipalManager',
        mustChangePassword: false,
      },
      accessToken: 'manager-token',
    });

    render(
      <MemoryRouter initialEntries={['/manager/operations']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByTestId('operational-issues-page')).toBeInTheDocument();
  });

  it('redirects unauthenticated user accessing /officer/operations to /login', () => {
    useAuthStore.setState({
      isAuthenticated: false,
      isLoading: false,
      user: null,
      accessToken: null,
    });

    render(
      <MemoryRouter initialEntries={['/officer/operations']}>
        <AppRoutes />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /sign in to your account/i })).toBeInTheDocument();
  });
});


