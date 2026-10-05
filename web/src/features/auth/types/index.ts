export interface User {
  id: string;
  fullName: string;
  email: string;
  username?: string;
  role: string;
  phoneNumber?: string;
  isActive?: boolean;
  mustChangePassword?: boolean;
  createdAt?: string;
}

export interface LoginRequest {
  email?: string;
  username?: string;
  password: string;
  clientType?: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  phoneNumber: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  mustChangePassword?: boolean;
  user: User;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface CurrentUserResponse {
  id: string;
  fullName: string;
  email: string;
  phoneNumber?: string;
  role: string;
  isActive: boolean;
  mustChangePassword?: boolean;
  createdAt: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

export interface UserManagementDto {
  id: string;
  fullName: string;
  email: string;
  username: string;
  role: string;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
}

export interface CreateUserRequest {
  fullName: string;
  email: string;
  username: string;
  role: string;
}

export interface CreateUserResponse {
  user: UserManagementDto;
  temporaryPassword: string;
}

export interface UpdateUserStatusRequest {
  isActive: boolean;
}

export interface UserListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  role?: string;
  isActive?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

