import { apiClient } from './client'

export type UserRole = 'Admin' | 'Dispatcher' | 'Driver' | 'Operations'

export interface AuthUser {
  id: string
  email: string
  fullName: string
  role: UserRole
}

export interface LoginResponse {
  accessToken: string
  accessTokenExpiresAt: string
  user: AuthUser
}

export interface RefreshResponse {
  accessToken: string
  accessTokenExpiresAt: string
}

export function login(email: string, password: string): Promise<LoginResponse> {
  return apiClient.post<LoginResponse>('/api/auth/login', { email, password })
}

export function refresh(): Promise<RefreshResponse> {
  return apiClient.post<RefreshResponse>('/api/auth/refresh')
}

export function logout(): Promise<void> {
  return apiClient.post<void>('/api/auth/logout')
}

export function getMe(): Promise<AuthUser> {
  return apiClient.get<AuthUser>('/api/auth/me')
}
