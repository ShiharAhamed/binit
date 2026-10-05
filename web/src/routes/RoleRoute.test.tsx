import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { RoleRoute } from './RoleRoute';
import { useAuthStore } from '../store/authStore';

describe('RoleRoute', () => {
  beforeEach(() => {
    useAuthStore.getState().logout();
  });

  it('allows user with matching role to access route', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token',
      user: {
        id: '1',
        fullName: 'Manager User',
        email: 'manager@example.com',
        role: 'MunicipalManager',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager-area']}>
        <Routes>
          <Route path="/unauthorized" element={<div>Unauthorized Page Mock</div>} />
          <Route element={<RoleRoute allowedRoles={['MunicipalManager']} />}>
            <Route path="/manager-area" element={<div>Manager Dashboard</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText('Manager Dashboard')).toBeInTheDocument();
    expect(screen.queryByText('Unauthorized Page Mock')).not.toBeInTheDocument();
  });

  it('redirects user with unpermitted role to /unauthorized', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'token',
      user: {
        id: '2',
        fullName: 'Citizen User',
        email: 'citizen@example.com',
        role: 'Citizen',
      },
    });

    render(
      <MemoryRouter initialEntries={['/manager-area']}>
        <Routes>
          <Route path="/unauthorized" element={<div>Unauthorized Page Mock</div>} />
          <Route element={<RoleRoute allowedRoles={['MunicipalManager']} />}>
            <Route path="/manager-area" element={<div>Manager Dashboard</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText('Unauthorized Page Mock')).toBeInTheDocument();
    expect(screen.queryByText('Manager Dashboard')).not.toBeInTheDocument();
  });

  it('redirects unauthenticated user to /login', () => {
    useAuthStore.setState({
      isAuthenticated: false,
      isLoading: false,
      accessToken: null,
      user: null,
    });

    render(
      <MemoryRouter initialEntries={['/manager-area']}>
        <Routes>
          <Route path="/login" element={<div>Login Page Mock</div>} />
          <Route path="/unauthorized" element={<div>Unauthorized Page Mock</div>} />
          <Route element={<RoleRoute allowedRoles={['MunicipalManager']} />}>
            <Route path="/manager-area" element={<div>Manager Dashboard</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText('Login Page Mock')).toBeInTheDocument();
    expect(screen.queryByText('Manager Dashboard')).not.toBeInTheDocument();
  });
});
