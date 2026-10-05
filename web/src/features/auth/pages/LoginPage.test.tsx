import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import { authApi } from '../../../api/authApi';
import { useAuthStore } from '../../../store/authStore';

// Mock authApi
vi.mock('../../../api/authApi', () => ({
  authApi: {
    login: vi.fn(),
  },
}));

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().logout();
  });

  it('validates that email and password are required', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    const submitBtn = screen.getByRole('button', { name: /sign in/i });
    await user.click(submitBtn);

    expect(await screen.findByText(/email is required/i)).toBeInTheDocument();
    expect(await screen.findByText(/password is required/i)).toBeInTheDocument();
    expect(authApi.login).not.toHaveBeenCalled();
  });

  it('rejects invalid email formats', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    const emailInput = screen.getByLabelText(/email address/i);
    const passwordInput = screen.getByLabelText(/password/i);
    const submitBtn = screen.getByRole('button', { name: /sign in/i });

    await user.type(emailInput, 'invalid-email-format');
    await user.type(passwordInput, 'Password123!');
    await user.click(submitBtn);

    expect(await screen.findByText(/valid email address/i)).toBeInTheDocument();
    expect(authApi.login).not.toHaveBeenCalled();
  });

  it('calls authApi.login and updates store on successful submit', async () => {
    const user = userEvent.setup();
    const mockAuthResponse = {
      accessToken: 'fake.jwt.token',
      expiresAt: '2026-09-07T16:00:00Z',
      user: {
        id: '11111111-1111-1111-1111-111111111111',
        fullName: 'Kamal Silva',
        email: 'kamal@example.com',
        role: 'WasteOfficer',
        mustChangePassword: false,
      },
    };

    vi.mocked(authApi.login).mockResolvedValueOnce(mockAuthResponse);

    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    const emailInput = screen.getByLabelText(/email address/i);
    const passwordInput = screen.getByLabelText(/password/i);
    const submitBtn = screen.getByRole('button', { name: /sign in/i });

    await user.type(emailInput, 'kamal@example.com');
    await user.type(passwordInput, 'Password123!');
    await user.click(submitBtn);

    await waitFor(() => {
      expect(authApi.login).toHaveBeenCalledWith({
        email: 'kamal@example.com',
        password: 'Password123!',
        clientType: 'web',
      });
    });

    const storeState = useAuthStore.getState();
    expect(storeState.accessToken).toBe('fake.jwt.token');
    expect(storeState.user?.fullName).toBe('Kamal Silva');
    expect(storeState.isAuthenticated).toBe(true);
  });

  it('rejects Citizen login on web with friendly mobile guidance and does not persist session', async () => {
    const user = userEvent.setup();
    const mockCitizenResponse = {
      accessToken: 'fake.jwt.token',
      expiresAt: '2026-09-07T16:00:00Z',
      user: {
        id: 'citizen-123',
        fullName: 'Citizen User',
        email: 'citizen@example.com',
        role: 'Citizen',
        mustChangePassword: false,
      },
    };

    vi.mocked(authApi.login).mockResolvedValueOnce(mockCitizenResponse);

    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/email address/i), 'citizen@example.com');
    await user.type(screen.getByLabelText(/password/i), 'Citizen123!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText(/this account is for the smartwaste mobile application/i)).toBeInTheDocument();

    const storeState = useAuthStore.getState();
    expect(storeState.isAuthenticated).toBe(false);
    expect(storeState.accessToken).toBeNull();
  });

  it('allows MunicipalManager login and updates store', async () => {
    const user = userEvent.setup();
    const mockManagerResponse = {
      accessToken: 'fake.manager.jwt.token',
      expiresAt: '2026-09-07T16:00:00Z',
      user: {
        id: 'manager-123',
        fullName: 'Anura Manager',
        email: 'anura@smartwaste.lk',
        role: 'MunicipalManager',
        mustChangePassword: false,
      },
    };

    vi.mocked(authApi.login).mockResolvedValueOnce(mockManagerResponse);

    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/email address/i), 'anura@smartwaste.lk');
    await user.type(screen.getByLabelText(/password/i), 'Manager123!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(authApi.login).toHaveBeenCalledWith({
        email: 'anura@smartwaste.lk',
        password: 'Manager123!',
        clientType: 'web',
      });
    });

    const storeState = useAuthStore.getState();
    expect(storeState.accessToken).toBe('fake.manager.jwt.token');
    expect(storeState.user?.fullName).toBe('Anura Manager');
    expect(storeState.isAuthenticated).toBe(true);
  });

  it('verifies that "Register as Citizen" is not displayed on login page', () => {
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    expect(screen.queryByText(/register as citizen/i)).not.toBeInTheDocument();
  });

  it('opens access-information modal when "Don\'t have an account?" is clicked and can be closed', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    // Initial state: modal not visible
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    // Click "Don't have an account?"
    const helpButton = screen.getByRole('button', { name: /don't have an account\?/i });
    await user.click(helpButton);

    // Modal opens
    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
    expect(screen.getByText('Need access to SmartWaste?')).toBeInTheDocument();

    // Message includes Citizen/Driver mobile guidance
    expect(
      screen.getByText(/smartwaste mobile services are available through the mobile application/i)
    ).toBeInTheDocument();
    expect(
      screen.getByText(/citizens can create an account from the mobile app/i)
    ).toBeInTheDocument();
    expect(
      screen.getByText(/driver accounts are created by the municipal administration/i)
    ).toBeInTheDocument();

    // Message includes Officer/Manager contact-admin guidance
    expect(
      screen.getByText(/staff accounts are created internally/i)
    ).toBeInTheDocument();
    expect(
      screen.getByText(/please contact your municipal manager or authorized municipal administrator/i)
    ).toBeInTheDocument();

    // Close button dismisses modal
    const closeBtn = screen.getByRole('button', { name: 'Close' });
    await user.click(closeBtn);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    // Re-open and verify "Close dialog" icon button also closes modal
    await user.click(helpButton);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    const closeIconBtn = screen.getByRole('button', { name: 'Close dialog' });
    await user.click(closeIconBtn);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('toggles password visibility when the view password button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    const passwordInput = screen.getByLabelText(/^password$/i);
    expect(passwordInput).toHaveAttribute('type', 'password');

    const toggleBtn = screen.getByRole('button', { name: 'Show' });
    await user.click(toggleBtn);

    expect(passwordInput).toHaveAttribute('type', 'text');
    expect(screen.getByRole('button', { name: 'Hide' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Hide' }));
    expect(passwordInput).toHaveAttribute('type', 'password');
    expect(screen.getByRole('button', { name: 'Show' })).toBeInTheDocument();
  });
});

