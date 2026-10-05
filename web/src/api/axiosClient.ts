import axios, { type InternalAxiosRequestConfig } from 'axios';
import { tokenStorage } from '../utils/tokenStorage';

const baseURL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5276/api/v1';

export const axiosClient = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor: Attach JWT Bearer token if present
axiosClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = tokenStorage.getToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor: Standardize error extraction
axiosClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // If token is invalid/expired (401), callers can react accordingly
    return Promise.reject(error);
  }
);
