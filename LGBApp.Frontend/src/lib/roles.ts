import type { UserResponse } from '@/lib/api';

export const ROLES = {
  Admin: 'Admin',
  User: 'User',
  ClientAdmin: 'ClientAdmin',
  Client: 'Client',
} as const;

export type AppRole = (typeof ROLES)[keyof typeof ROLES];

export function isAdmin(user: UserResponse | null): boolean {
  return user?.role?.toLowerCase() === 'admin';
}

export function isInternalUser(user: UserResponse | null): boolean {
  return user?.role === ROLES.User;
}

export function isInternalStaff(user: UserResponse | null): boolean {
  return isAdmin(user) || isInternalUser(user);
}

export function isClientAdmin(user: UserResponse | null): boolean {
  return user?.role === ROLES.ClientAdmin;
}

export function isClientStaff(user: UserResponse | null): boolean {
  return user?.role === ROLES.Client;
}

export function isExternalUser(user: UserResponse | null): boolean {
  return isClientAdmin(user) || isClientStaff(user);
}

export function canManageUsers(user: UserResponse | null): boolean {
  return isAdmin(user) || isClientAdmin(user);
}

export function roleLabel(role: string): string {
  switch (role) {
    case ROLES.Admin: return 'Admin (internal)';
    case ROLES.User: return 'User (internal secretary)';
    case ROLES.ClientAdmin: return 'Client Admin (external)';
    case ROLES.Client: return 'Client (external staff)';
    default: return role;
  }
}
