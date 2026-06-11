import type { UserResponse } from '@/lib/api';

export const ROLES = {
  Admin: 'Admin',
  User: 'User',
  ClientAdmin: 'ClientAdmin',
  ClientSignatory: 'ClientSignatory',
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

export function isClientSignatory(user: UserResponse | null): boolean {
  return user?.role === ROLES.ClientSignatory;
}

/** @deprecated Client staff role removed — use isClientAdmin */
export function isClientStaff(_user: UserResponse | null): boolean {
  return false;
}

export function isExternalUser(user: UserResponse | null): boolean {
  return isClientAdmin(user) || isClientSignatory(user);
}

export function canManageUsers(user: UserResponse | null): boolean {
  return isAdmin(user) || isClientAdmin(user);
}

/** Internal Admins + resolution secretaries (no client link, no approval roles). */
export function isAssignableInternalStaff(user: UserResponse): boolean {
  if (user.customerId != null) return false;
  if (isAdmin(user)) return true;
  return user.role === ROLES.User
    && !user.canApproveMoiIntake
    && !user.canApproveMoi
    && !user.canApproveMoa;
}

export function canAssignJobStaff(user: UserResponse | null): boolean {
  return isAdmin(user) || Boolean(user?.canApproveMoi);
}

export function roleLabel(role: string): string {
  switch (role) {
    case ROLES.Admin: return 'Admin (internal)';
    case ROLES.User: return 'User (internal secretary)';
    case ROLES.ClientAdmin: return 'Client Admin (external)';
    case ROLES.ClientSignatory: return 'Client Signatory (external)';
    default: return role;
  }
}

export const JOB_CATEGORIES = ['MOI', 'MOI Approval', 'MOA', 'Services'] as const;

export function jobCategory(taskType: string): (typeof JOB_CATEGORIES)[number] {
  if (taskType === 'MOI' || taskType === 'MOI Approval' || taskType === 'MOA') return taskType;
  return 'Services';
}
