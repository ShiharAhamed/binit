import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { ChangePasswordPage } from './ChangePasswordPage';
import { authApi } from '../../../api/authApi';
import { useAuthStore } from '../../../store/authStore';

vi.mock('../../../api/authApi', () => ({
  authApi: {
    changePassword: vi.fn(),
  },
}));

describe('ChangePasswordPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      accessToken: 'test-token',
      isAuthenticated: true,
      isLoading: false,
      user: {
        id: 'user-1',
        fullName: 'Officer Kamal',
        email: 'kamal@smartwaste.lk',
        role: 'WasteOfficer',
        mustChangePassword: false,
      },
    });
  });

  it('renders normal mode when mustChangePassword is false', () => {
    render(
      <MemoryRouter>
        <ChangePasswordPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /change password/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/^current password$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^new password$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^confirm new password$/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /cancel/i })).toBeInTheDocument();
  });

  it('renders forced mode when mustChangePassword is true', () => {
    useAuthStore.setState({
      user: {
        id: 'user-1',
        fullName: 'Officer Kamal',
        email: 'kamal@smartwaste.lk',
        role: 'WasteOfficer',
        mustChangePassword: true,
      },
    });

    render(
      <MemoryRouter>
        <ChangePasswordPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /change temporary password/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/current temporary password/i)).toBeInTheDocument();
    expect(screen.getAllByText(/you must change your temporary password before continuing/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByRole('button', { name: /sign out/i })).toBeInTheDocument();
  });

  it('validates required fields and password mismatch', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <ChangePasswordPage />
      </MemoryRouter>
    );

    const submitBtn = screen.getByRole('button', { name: /save new password/i });
    await user.click(submitBtn);

    expect(await screen.findByText(/current password is required/i)).toBeInTheDocument();
    expect(await screen.findByText(/new password must be at least 8 characters/i)).toBeInTheDocument();

    await user.type(screen.getByLabelText(/^current password$/i), 'OldPass123!');
    await user.type(screen.getByLabelText(/^new password$/i), 'NewPass123!');
    await user.type(screen.getByLabelText(/^confirm new password$/i), 'MismatchPass!');
    await user.click(submitBtn);

    expect(await screen.findByText(/passwords do not match/i)).toBeInTheDocument();
    expect(authApi.changePassword).not.toHaveBeenCalled();
  });

  it('submits valid passwords, calls authApi.changePassword, and clears session', async () => {
    const user = userEvent.setup();
    vi.mocked(authApi.changePassword).mockResolvedValueOnce(undefined);

    render(
      <MemoryRouter initialEntries={['/account/change-password']}>
        <Routes>
          <Route path="/account/change-password" element={<ChangePasswordPage />} />
          <Route path="/login" element={<div>Login Page Target</div>} />
        </Routes>
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/^current password$/i), 'OldPass123!');
    await user.type(screen.getByLabelText(/^new password$/i), 'NewPass123!');
    await user.type(screen.getByLabelText(/^confirm new password$/i), 'NewPass123!');

    const submitBtn = screen.getByRole('button', { name: /save new password/i });
    await user.click(submitBtn);

    await waitFor(() => {
      expect(authApi.changePassword).toHaveBeenCalledWith({
        currentPassword: 'OldPass123!',
        newPassword: 'NewPass123!',
      });
    });

    // Session cleared and redirected to /login
    await waitFor(() => {
      expect(screen.getByText('Login Page Target')).toBeInTheDocument();
    });

    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(false);
    expect(state.accessToken).toBeNull();
  });
});
