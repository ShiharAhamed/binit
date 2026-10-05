import { axiosClient } from './axiosClient';
import type {
  AuthResponse,
  ChangePasswordRequest,
  CurrentUserResponse,
  LoginRequest,
  RegisterRequest,
  UserManagementDto,
  CreateUserRequest,
  CreateUserResponse,
  UserListQuery,
  PagedResult,
} from '../features/auth/types';

export const authApi = {
  /**
   * Public registration endpoint. Always creates a Citizen role.
   */
  register: async (data: RegisterRequest): Promise<AuthResponse> => {
    const response = await axiosClient.post<AuthResponse>('/auth/register', data);
    return response.data;
  },

  /**
   * User login endpoint with email/username, password, and clientType.
   */
  login: async (data: LoginRequest): Promise<AuthResponse> => {
    const payload = {
      ...data,
      clientType: data.clientType || 'web',
    };
    const response = await axiosClient.post<AuthResponse>('/auth/login', payload);
    return response.data;
  },

  /**
   * Retrieves profile of currently authenticated user using JWT token.
   */
  getMe: async (): Promise<CurrentUserResponse> => {
    const response = await axiosClient.get<CurrentUserResponse>('/auth/me');
    return response.data;
  },

  /**
   * Changes password for currently authenticated user.
   */
  changePassword: async (data: ChangePasswordRequest): Promise<void> => {
    await axiosClient.post('/auth/change-password', data);
  },
};

export const usersApi = {
  /**
   * Retrieves paginated list of internal staff accounts.
   */
  getUsers: async (query?: UserListQuery): Promise<PagedResult<UserManagementDto>> => {
    const response = await axiosClient.get<PagedResult<UserManagementDto>>('/users', {
      params: query,
    });
    return response.data;
  },

  /**
   * Creates a new internal staff user with temporary password.
   */
  createUser: async (data: CreateUserRequest): Promise<CreateUserResponse> => {
    const response = await axiosClient.post<CreateUserResponse>('/users', data);
    return response.data;
  },

  /**
   * Activates or deactivates an internal staff user.
   */
  updateUserStatus: async (id: string, isActive: boolean): Promise<UserManagementDto> => {
    const response = await axiosClient.patch<UserManagementDto>(`/users/${id}/status`, {
      isActive,
    });
    return response.data;
  },
};

