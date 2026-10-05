import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import { useAuthStore } from '../store/authStore';

describe('ProtectedRoute', () => {
  beforeEach(() => {
    useAuthStore.getState().logout();
  });

  it('redirects unauthenticated user to /login', () => {
    useAuthStore.setState({ isAuthenticated: false, isLoading: false });

    render(
      <MemoryRouter initialEntries={['/secret']}>
        <Routes>
          <Route path="/login" element={<div>Login Page Mock</div>} />
          <Route element={<ProtectedRoute />}>
            <Route path="/secret" element={<div>Secret Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText('Login Page Mock')).toBeInTheDocument();
    expect(screen.queryByText('Secret Content')).not.toBeInTheDocument();
  });

  it('allows access to authenticated user', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'valid-token',
      user: {
        id: '123',
        fullName: 'Test User',
        email: 'test@example.com',
        role: 'Citizen',
      },
    });

    render(
      <MemoryRouter initialEntries={['/secret']}>
        <Routes>
          <Route path="/login" element={<div>Login Page Mock</div>} />
          <Route element={<ProtectedRoute />}>
            <Route path="/secret" element={<div>Secret Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText('Secret Content')).toBeInTheDocument();
    expect(screen.queryByText('Login Page Mock')).not.toBeInTheDocument();
  });
});
