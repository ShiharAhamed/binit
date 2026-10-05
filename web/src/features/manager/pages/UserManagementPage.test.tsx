import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { UserManagementPage } from './UserManagementPage';
import { usersApi } from '../../../api/authApi';
import { useAuthStore } from '../../../store/authStore';

vi.mock('../../../api/authApi', () => ({
  usersApi: {
    getUsers: vi.fn(),
    createUser: vi.fn(),
    updateUserStatus: vi.fn(),
  },
}));

const mockUsersData = {
  items: [
    {
      id: 'manager-1',
      fullName: 'Anura Bandara',
      email: 'anura@smartwaste.lk',
      username: 'anura_mgr',
      role: 'MunicipalManager',
      isActive: true,
      mustChangePassword: false,
      createdAt: '2026-09-01T10:00:00Z',
    },
    {
      id: 'driver-2',
      fullName: 'Saman Driver',
      email: 'saman@smartwaste.lk',
      username: 'saman_driver',
      role: 'Driver',
      isActive: true,
      mustChangePassword: true,
      createdAt: '2026-09-02T10:00:00Z',
    },
    {
      id: 'officer-3',
      fullName: 'Nimal Officer',
      email: 'nimal@smartwaste.lk',
      username: 'nimal_officer',
      role: 'WasteOfficer',
      isActive: false,
      mustChangePassword: false,
      createdAt: '2026-09-03T10:00:00Z',
    },
  ],
  totalCount: 3,
  totalPages: 1,
  page: 1,
  pageSize: 15,
};

describe('UserManagementPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      accessToken: 'manager-token',
      isAuthenticated: true,
      isLoading: false,
      user: {
        id: 'manager-1',
        fullName: 'Anura Bandara',
        email: 'anura@smartwaste.lk',
        role: 'MunicipalManager',
        mustChangePassword: false,
      },
    });

    vi.mocked(usersApi.getUsers).mockResolvedValue(mockUsersData);
  });

  it('renders staff list with roles and security status', async () => {
    render(
      <MemoryRouter>
        <UserManagementPage />
      </MemoryRouter>
    );

    expect(await screen.findByText('Anura Bandara')).toBeInTheDocument();
    expect(screen.getByText('Saman Driver')).toBeInTheDocument();
    expect(screen.getByText('Nimal Officer')).toBeInTheDocument();

    // Security setup badges
    expect(screen.getByText('Temp Password')).toBeInTheDocument();
    expect(screen.getAllByText('Normal').length).toBeGreaterThanOrEqual(1);

    // Self badge on logged in manager
    expect(screen.getByText('You')).toBeInTheDocument();
  });

  it('prevents manager from deactivating their own account', async () => {
    render(
      <MemoryRouter>
        <UserManagementPage />
      </MemoryRouter>
    );

    await screen.findByText('Anura Bandara');

    const deactButtons = screen.getAllByRole('button', { name: /deactivate/i });
    // First deactivate button is for manager-1 (self)
    expect(deactButtons[0]).toBeDisabled();
    expect(deactButtons[0]).toHaveAttribute('title', 'You cannot deactivate your own account');
  });

  it('toggles another staff member active/inactive status', async () => {
    const user = userEvent.setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    vi.mocked(usersApi.updateUserStatus).mockResolvedValueOnce({
      id: 'driver-2',
      fullName: 'Saman Driver',
      email: 'saman@smartwaste.lk',
      username: 'saman_driver',
      role: 'Driver',
      isActive: false,
      mustChangePassword: true,
      createdAt: '2026-09-02T10:00:00Z',
    });

    render(
      <MemoryRouter>
        <UserManagementPage />
      </MemoryRouter>
    );

    await screen.findByText('Saman Driver');

    const deactButtons = screen.getAllByRole('button', { name: /deactivate/i });
    // Second deactivate button is for driver-2
    await user.click(deactButtons[1]);

    await waitFor(() => {
      expect(usersApi.updateUserStatus).toHaveBeenCalledWith('driver-2', false);
    });
  });

  it('opens create modal, verifies Citizen is NOT an option, and creates user with temp password display', async () => {
    const user = userEvent.setup();
    vi.mocked(usersApi.createUser).mockResolvedValueOnce({
      user: {
        id: 'new-officer-99',
        fullName: 'Kasun Fernando',
        email: 'kasun@smartwaste.lk',
        username: 'kasun_officer',
        role: 'WasteOfficer',
        isActive: true,
        mustChangePassword: true,
        createdAt: '2026-09-10T12:00:00Z',
      },
      temporaryPassword: 'TempPassword!99',
    });

    render(
      <MemoryRouter>
        <UserManagementPage />
      </MemoryRouter>
    );

    await screen.findByText('Anura Bandara');

    const openCreateBtn = screen.getByRole('button', { name: /^create user$/i });
    await user.click(openCreateBtn);

    // Modal opens
    expect(screen.getByRole('heading', { name: 'Create Staff User' })).toBeInTheDocument();

    // Verify Citizen role is NOT available in select dropdown
    const roleSelect = screen.getByLabelText(/assigned staff role/i) as HTMLSelectElement;
    const options = Array.from(roleSelect.options).map((o) => o.value);
    expect(options).toContain('Driver');
    expect(options).toContain('WasteOfficer');
    expect(options).toContain('MunicipalManager');
    expect(options).not.toContain('Citizen');

    // Fill form
    await user.type(screen.getByLabelText(/full name/i), 'Kasun Fernando');
    await user.type(screen.getByLabelText(/email address/i), 'kasun@smartwaste.lk');
    await user.type(screen.getByLabelText(/username/i), 'kasun_officer');
    await user.selectOptions(roleSelect, 'WasteOfficer');

    const submitBtn = screen.getByRole('button', { name: /create account/i });
    await user.click(submitBtn);

    await waitFor(() => {
      expect(usersApi.createUser).toHaveBeenCalledWith({
        fullName: 'Kasun Fernando',
        email: 'kasun@smartwaste.lk',
        username: 'kasun_officer',
        role: 'WasteOfficer',
      });
    });

    // Temporary password result modal should be visible
    expect(await screen.findByText('Account Created Successfully')).toBeInTheDocument();
    expect(screen.getByText('TempPassword!99')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^copy$/i })).toBeInTheDocument();
  });
});
