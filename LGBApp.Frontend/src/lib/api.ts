const API_BASE = import.meta.env.VITE_API_BASE ?? '';

export interface UserResponse {
  userId: number;
  email: string;
  name: string;
  mobile: string;
  role: string;
  jobTitle: string;
  canRecommendMoi: boolean;
  canApproveMoiIntake: boolean;
  canApproveMoi: boolean;
  canApproveMoa: boolean;
  isInternalSignatory?: boolean;
  customerId?: number;
  customerName?: string;
  needsMoi?: boolean;
  needsMoiApproval?: boolean;
  needsMoa?: boolean;
  isVerified: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  accessibleCompanies?: SignatoryCompanyAccessDto[];
  /** Account-holder names linked to this signatory across companies. */
  signatoryHolderNames?: string[];
}

export interface SignatoryCompanyAccessDto {
  customerId: number;
  company: string;
}

export interface SignatoryOverlapDto {
  email: string;
  primaryName: string;
  companyCount: number;
  isLinked: boolean;
  linkedUserId?: number;
  companies: SignatoryOverlapCompanyDto[];
}

export interface SignatoryOverlapCompanyDto {
  customerId: number;
  company: string;
  accountHolderId: number;
  holderName: string;
  needsMoi: boolean;
  needsMoiApproval: boolean;
  needsMoa: boolean;
  userId?: number;
}

export interface SignatoryLinkResultDto {
  email: string;
  userId: number;
  holdersLinked: number;
  customerIds: number[];
  message: string;
}

export interface AuthResponse {
  token: string;
  user: UserResponse;
}

export interface AccountHolderDto {
  id: number;
  name: string;
  email: string;
  phone: string;
  moi?: boolean;
  moiApproval?: boolean;
  moa?: boolean;
  userId?: number;
  clientAdded?: boolean;
  addedByUserId?: number;
}

export interface AddOnLineDto {
  name: string;
  qty: number;
  unitPrice: number;
}

export interface PackagePricingDto {
  validity: string;
  basePackagePrice: number;
  addOnLines: AddOnLineDto[];
}

export interface CustomerPackageDto {
  id: number;
  packageName: string;
  packageValue: number;
  activeValue: number;
  packageDetail?: string;
  purchasedDate: string;
  expiryDate: string;
  validity: string;
  pricing?: PackagePricingDto;
  status: string;
}

export interface PackageScheduleItemDto {
  id: number;
  customerId: number;
  customerPackageId: number;
  packageName: string;
  customerName: string;
  itemType: string;
  title: string;
  scheduledAt: string;
  durationMinutes?: number;
  status: string;
  notes?: string;
  bookingUrl?: string;
  sequenceNumber?: number;
}

export interface CustomerResponse {
  id: number;
  name: string;
  email: string;
  phone: string;
  company: string;
  status: 'Active' | 'Non-Active';
  value: number;
  lastContact: string;
  invoiceBy: string;
  chargeTo: string;
  invoiceByPartyIds?: number[];
  chargeToPartyIds?: number[];
  package: string;
  packageValue: number;
  cosec: boolean;
  divisionGroupCode: string;
  hasLoa: boolean;
  loaHolders: string[];
  moiFormTemplateCode?: string;
  moaFormTemplateCode?: string;
  moaWorkflowTemplateCode?: string;
  moi: string[];
  moiApproval: string[];
  moiApprovalMode?: 'AllRequired' | 'AnyOne';
  moa: string[];
  purchasedDate: string;
  expiryDate: string;
  packages: CustomerPackageDto[];
  accountHolders: AccountHolderDto[];
}

export interface ProductResponse {
  id: number;
  packageName: string;
  services: string[];
  serviceQuantities: Record<string, number>;
  unit: string;
  qtyPerYear: number;
  packagePrice: number;
  addOns: string[];
  addOnQuantities: Record<string, number>;
  addOnsQty: number;
  addOnPrice: number;
}

export interface UnitAssigneeDto {
  userId: number;
  userName: string;
}

export interface JobRequestUnitDto {
  id: number;
  unitNumber: number;
  assignedUserId?: number;
  assignedUserName: string;
  assignees?: UnitAssigneeDto[];
  scheduledDate?: string;
  status: 'Pending' | 'In Progress' | 'Completed';
  internalHandoffStatus?: string;
  linkedFormKind?: string;
  linkedFormId?: number;
  hasMoiForm?: boolean;
  hasMoaForm?: boolean;
  moiFormId?: number;
  moiWorkflowState?: string;
  requiredExecutionDate?: string;
  displayStatus?: string;
  displayStatusKey?: string;
  awaitingIntakeApproval?: boolean;
  documentTitle?: string;
  workflowMode?: string;
  adminBypassNote?: string;
  adminBypassAt?: string;
  /** Multi-qty: ISO timestamp when client claimed this session; absent = dormant. */
  clientActivatedAt?: string;
}

export interface WorkTrackerItemDto {
  unitId: number;
  jobId: number;
  unitNumber: number;
  customerPackageId?: number;
  customer: string;
  taskType: string;
  service: string;
  accountHolder: string;
  scheduledDate?: string;
  dateRequested?: string;
  status: string;
  assignedUserId?: number;
  assignedUserName: string;
  assignees?: UnitAssigneeDto[];
  internalHandoffStatus?: string;
  displayStatus?: string;
  displayStatusKey?: string;
  linkedFormKind?: string;
  linkedFormId?: number;
  hasMoiForm?: boolean;
  hasMoaForm?: boolean;
  moiFormId?: number;
  moiWorkflowState?: string;
  requiredExecutionDate?: string;
  documentTitle?: string;
}

export interface JobRequestResponse {
  id: number;
  customerId?: number;
  customer: string;
  taskType: string;
  service: string;
  usedQty: number;
  totalQty: number;
  dateRequested: string;
  dateCompleted?: string;
  accountHolder: string;
  accountHolderEmail?: string;
  accountHolderPhone?: string;
  customerPackageId?: number;
  scheduledDate?: string;
  assignedUserId?: number;
  jobAssignedTo: string;
  status: 'Pending' | 'In Progress' | 'Completed' | 'Canceled';
  assignmentComments?: string;
  units?: JobRequestUnitDto[];
  linkedFormKind?: string;
  linkedFormId?: number;
  hasMoiForm?: boolean;
  hasMoaForm?: boolean;
  moiWorkflowState?: string;
  internalHandoffStatus?: string;
  awaitingIntakeApproval?: boolean;
  taskPhase?: string;
  displayStatus?: string;
  displayStatusKey?: string;
  /** Active session when opening a per-unit MOI from the portal. */
  activeUnitNumber?: number;
  /** Curated MOI document title when set — preferred list/workboard label. */
  documentTitle?: string;
  /** D1: "" | MoiMoa | AdminBypass */
  workflowMode?: string;
  adminBypassNote?: string;
  adminBypassAt?: string;
}

export interface BillingPartyDto {
  id: number;
  name: string;
  category: string;
  isActive: boolean;
  sortOrder: number;
}

export interface MoaPackChecklistDto {
  internalChecklistA: boolean;
  internalChecklistB: boolean;
  cleanAgreementAttached: boolean;
  shareholdingTableAttached: boolean;
  ssmRegistrationNo: string;
  ssmNewRegistrationNo: string;
  ssmEntityType: string;
  ssmStatus: string;
  ssmAsAtDate: string;
}

export interface CompletedServiceResponse {
  id: number;
  customer: string;
  service: string;
  usedQty: number;
  totalQty: number;
  dateRequested: string;
  dateCompleted: string;
  accountHolder: string;
  jobAssignedTo: string;
  status: 'Completed' | 'Canceled';
}

export interface WorkflowStepInstanceDto {
  id: number;
  stepOrder: number;
  stepKey: string;
  displayName: string;
  assigneeName: string;
  assigneeUserId?: number;
  status: string;
  approvedAt?: string;
  comments: string;
  adminOverridden: boolean;
  isCurrent: boolean;
}

export interface WorkflowInstanceDto {
  id: number;
  templateCode: string;
  formType: string;
  status: string;
  currentStepOrder: number;
  conditions: { financeRelated: boolean; bankSignatory: boolean; shareMovement: boolean };
  steps: WorkflowStepInstanceDto[];
}

export interface FormTemplateDto {
  id: number;
  formType: string;
  code: string;
  name: string;
  description: string;
  addressedTo: string;
  divisionLabel: string;
  issuerEntity: string;
  packageServiceName?: string;
  fields: { key: string; label: string; type: string; required: boolean; section: string }[];
  isDefault: boolean;
  isActive: boolean;
}

export interface WorkflowTemplateDto {
  id: number;
  code: string;
  name: string;
  workflowType: string;
  description: string;
  isActive: boolean;
  steps: {
    id: number;
    stepOrder: number;
    stepKey: string;
    displayName: string;
    conditionType: string;
    assigneeType: string;
    assigneeRole?: string;
    assigneeUserId?: number;
    assigneeDisplayName?: string;
    allowAdminOverride: boolean;
  }[];
}

export interface DivisionGroupDto {
  id: number;
  code: string;
  name: string;
  moaWorkflowTemplateCode: string;
  defaultMoiFormTemplateCode?: string;
  defaultMoaFormTemplateCode?: string;
  isActive: boolean;
  recommenders: { id: number; userId?: number; displayName: string }[];
}

export interface ClientApprovalDto {
  userId: number;
  accountHolderName: string;
  comments: string;
  signedAt: string;
  signatureFileName?: string;
  signatureDataUrl?: string;
}

export interface FormRejectionDto {
  stage: string;
  userId: number;
  userName: string;
  reason: string;
  rejectedAt: string;
}

export interface FormResponse {
  id: number;
  jobId?: number;
  moiFormId?: number;
  company: string;
  data: Record<string, unknown>;
  formTemplateCode?: string;
  workflowState?: string;
  financeRelated?: boolean;
  bankSignatoryMatter?: boolean;
  shareMovement?: boolean;
  packChecklist?: MoaPackChecklistDto;
  packValidationErrors?: string[];
  workflow?: WorkflowInstanceDto;
  clientApprovals?: ClientApprovalDto[];
  rejections?: FormRejectionDto[];
  requiredApprovers?: string[];
  pendingApprovers?: string[];
  sharonApprovedAt?: string;
  submittedForAdminReviewAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface DashboardStatsResponse {
  activeCustomers: number;
  activeCustomersChange: string;
  totalRevenue: number;
  totalRevenueChange: string;
  outstandingServices: number;
  outstandingServicesChange: string;
  totalServicesCompleted: number;
  totalServicesCompletedChange: string;
  adHocServicesCount: number;
  adHocRevenue: number;
  adHocRevenueChange: string;
}

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

type AuthExpiredHandler = () => void;
let onAuthExpired: AuthExpiredHandler | null = null;

export function setAuthExpiredHandler(handler: AuthExpiredHandler | null): void {
  onAuthExpired = handler;
}

function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1])) as { exp?: number };
    if (!payload.exp) return false;
    return payload.exp * 1000 < Date.now();
  } catch {
    return true;
  }
}

function getToken(): string | null {
  const token = localStorage.getItem('lgb_token');
  if (!token) return null;
  if (isTokenExpired(token)) {
    clearAuth();
    onAuthExpired?.();
    return null;
  }
  return token;
}

export function setAuth(token: string, user: UserResponse): void {
  localStorage.setItem('lgb_token', token);
  localStorage.setItem('lgb_user', JSON.stringify(user));
}

export function clearAuth(): void {
  localStorage.removeItem('lgb_token');
  localStorage.removeItem('lgb_user');
}

export function getStoredUser(): UserResponse | null {
  const raw = localStorage.getItem('lgb_user');
  if (!raw) return null;
  try {
    return JSON.parse(raw) as UserResponse;
  } catch {
    return null;
  }
}

/** Returns the current user only when a valid, non-expired token is present. */
export function getAuthUser(): UserResponse | null {
  const token = localStorage.getItem('lgb_token');
  if (!token) {
    if (localStorage.getItem('lgb_user')) {
      clearAuth();
    }
    return null;
  }
  if (isTokenExpired(token)) {
    clearAuth();
    onAuthExpired?.();
    return null;
  }
  return getStoredUser();
}

export {
  isAdmin,
  isInternalStaff,
  isClientAdmin,
  isClientSignatory,
  isClientStaff,
  isExternalUser,
  canManageUsers,
  canAssignJobStaff,
  isAssignableInternalStaff,
  isInternalSecretaryOnly,
  roleLabel,
} from '@/lib/roles';

export async function addClientSignatory(data: {
  name: string;
  email: string;
  phone?: string;
  moi?: boolean;
  moiApproval?: boolean;
  moa?: boolean;
}): Promise<AccountHolderDto> {
  return request<AccountHolderDto>('/api/clientsignatories', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function getClientSignatories(): Promise<AccountHolderDto[]> {
  return request<AccountHolderDto[]>('/api/clientsignatories');
}

export interface ClientPortalSummaryDto {
  companyName: string;
  activePackages: number;
  activePackageValue: number;
  openJobs: number;
  completedJobs: number;
  teamMembers: number;
  categoryProgress: TaskCategoryProgressDto[];
}

export interface TaskCategoryProgressDto {
  category: string;
  pending: number;
  inProgress: number;
  completed: number;
  total: number;
}

function formatApiError(text: string, status: number): string {
  const trimmed = text.trim();
  if (!trimmed) {
    if (status === 403) return 'You do not have permission to perform this action.';
    if (status >= 500) {
      return 'Cannot reach the API. Start the backend on port 5003 (dotnet run in LGBApp.Backend).';
    }
    return `Request failed (${status})`;
  }
  if (trimmed.includes('System.') && trimmed.includes(' at ')) {
    return `Server error (${status}). Make sure the backend is running.`;
  }
  try {
    const json = JSON.parse(trimmed) as {
      title?: string;
      detail?: string;
      message?: string;
      errors?: Record<string, string[]>;
    };
    if (json.errors) {
      const first = Object.values(json.errors).flat().find(Boolean);
      if (first) return first;
    }
    return json.detail || json.message || json.title || trimmed;
  } catch {
    return trimmed.length > 160 ? `${trimmed.slice(0, 160)}…` : trimmed;
  }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  if (!headers.has('Content-Type') && options.body) {
    headers.set('Content-Type', 'application/json');
  }

  const token = getToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  });

  if (response.status === 401) {
    const text = await response.text();
    const isLoginAttempt = path === '/api/auth/login' || path === '/api/auth/register';
    if (isLoginAttempt) {
      throw new ApiError(formatApiError(text, 401), 401);
    }
    clearAuth();
    onAuthExpired?.();
    throw new ApiError('Session expired. Please sign in again.', 401);
  }

  if (!response.ok) {
    const text = await response.text();
    throw new ApiError(formatApiError(text, response.status), response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

// Auth
export async function login(email: string, password: string): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  });
}

export async function register(
  email: string,
  password: string,
  name: string,
  mobile = '',
): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password, name, mobile }),
  });
}

export async function changePassword(data: {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/change-password', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function forgotPassword(email: string): Promise<{ message: string }> {
  return request<{ message: string }>('/api/auth/forgot-password', {
    method: 'POST',
    body: JSON.stringify({ email }),
  });
}

export async function resetPasswordWithOtp(data: {
  email: string;
  code: string;
  newPassword: string;
  confirmPassword: string;
}): Promise<{ message: string }> {
  return request<{ message: string }>('/api/auth/reset-password', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

// Users
export async function getUsers(): Promise<UserResponse[]> {
  return request<UserResponse[]>('/api/users');
}

export interface AssignableUserDto {
  userId: number;
  name: string;
}

export async function getInternalDirectoryUsers(): Promise<AssignableUserDto[]> {
  return request<AssignableUserDto[]>('/api/users/internal-directory');
}

export async function createUser(data: {
  email: string;
  password: string;
  name: string;
  mobile: string;
  role: string;
  jobTitle?: string;
  canRecommendMoi?: boolean;
  canApproveMoiIntake?: boolean;
  canApproveMoi?: boolean;
  canApproveMoa?: boolean;
  isInternalSignatory?: boolean;
  customerId?: number;
}): Promise<UserResponse> {
  return request<UserResponse>('/api/users', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updateUser(
  id: number,
  data: {
    email: string;
    name: string;
    mobile: string;
    role: string;
    jobTitle?: string;
    canRecommendMoi?: boolean;
    canApproveMoiIntake?: boolean;
    canApproveMoi?: boolean;
    canApproveMoa?: boolean;
    isInternalSignatory?: boolean;
    customerId?: number;
  },
): Promise<void> {
  return request<void>(`/api/users/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function deleteUser(id: number): Promise<void> {
  return request<void>(`/api/users/${id}`, { method: 'DELETE' });
}

// Customers
export async function getCustomers(search?: string): Promise<CustomerResponse[]> {
  const params = search ? `?search=${encodeURIComponent(search)}` : '';
  return request<CustomerResponse[]>(`/api/customers${params}`);
}

export async function createCustomer(data: Record<string, unknown>): Promise<CustomerResponse> {
  return request<CustomerResponse>('/api/customers', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updateCustomer(id: number, data: CustomerResponse): Promise<CustomerResponse> {
  return request<CustomerResponse>(`/api/customers/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function deleteCustomer(id: number): Promise<void> {
  return request<void>(`/api/customers/${id}`, { method: 'DELETE' });
}

// Products
function toNumber(value: unknown, fallback = 0): number {
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : fallback;
}

export function buildProductPayload(data: Record<string, unknown>): Omit<ProductResponse, 'id'> {
  const addOnQuantities = (data.addOnQuantities as Record<string, number> | undefined) ?? {};
  const serviceQuantities = (data.serviceQuantities as Record<string, number> | undefined) ?? {};
  const isAdHoc = data.productType === 'Ad-hoc';
  const addOnsQty =
    toNumber(data.addOnsQty) ||
    Object.values(addOnQuantities).reduce((sum, qty) => sum + (toNumber(qty) > 0 ? toNumber(qty) : 0), 0);

  return {
    packageName: String(data.packageName ?? '').trim(),
    services: Array.isArray(data.services) ? data.services.map(String) : [],
    serviceQuantities,
    unit: String(data.unit ?? 'EACH'),
    qtyPerYear: isAdHoc ? 0 : toNumber(data.qtyPerYear),
    packagePrice: isAdHoc ? toNumber(data.unitPrice) : toNumber(data.packagePrice),
    addOns: Array.isArray(data.addOns) ? data.addOns.map(String) : [],
    addOnQuantities,
    addOnsQty: isAdHoc ? 0 : addOnsQty,
    addOnPrice: isAdHoc ? 0 : toNumber(data.addOnPrice),
  };
}

export async function getProducts(): Promise<ProductResponse[]> {
  return request<ProductResponse[]>('/api/products');
}

export async function createProduct(data: Omit<ProductResponse, 'id'>): Promise<ProductResponse> {
  return request<ProductResponse>('/api/products', {
    method: 'POST',
    body: JSON.stringify(buildProductPayload(data as Record<string, unknown>)),
  });
}

export async function updateProduct(id: number, data: Omit<ProductResponse, 'id'>): Promise<void> {
  return request<void>(`/api/products/${id}`, {
    method: 'PUT',
    body: JSON.stringify(buildProductPayload(data as Record<string, unknown>)),
  });
}

export async function deleteProduct(id: number): Promise<void> {
  return request<void>(`/api/products/${id}`, { method: 'DELETE' });
}

// Job requests
export async function getJobRequest(id: number): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/jobrequests/${id}`);
}

export async function getJobRequests(
  customerPackageId?: number,
  includeCompleted = false,
): Promise<JobRequestResponse[]> {
  const params = new URLSearchParams();
  if (customerPackageId != null) params.set('customerPackageId', String(customerPackageId));
  if (includeCompleted) params.set('includeCompleted', 'true');
  const qs = params.toString();
  return request<JobRequestResponse[]>(`/api/jobrequests${qs ? `?${qs}` : ''}`);
}

export async function recordJobProgress(
  id: number,
  data: {
    unitNumber?: number;
    userId?: number;
    scheduledDate?: string;
    markUnitComplete?: boolean;
    markUnitIncomplete?: boolean;
  },
): Promise<JobRequestResponse> {
  const body: Record<string, unknown> = {
    markUnitComplete: data.markUnitComplete ?? false,
    markUnitIncomplete: data.markUnitIncomplete ?? false,
  };
  if (data.unitNumber !== undefined) body.unitNumber = data.unitNumber;
  if (data.userId !== undefined) body.userId = data.userId;
  if (data.scheduledDate !== undefined) body.scheduledDate = data.scheduledDate;
  return request<JobRequestResponse>(`/api/jobrequests/${id}/progress`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export async function assignSecretarialTeam(jobId: number): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/jobrequests/${jobId}/assign-secretarial-team`, {
    method: 'POST',
  });
}

export async function assignJobRequest(
  id: number,
  data: {
    userId: number;
    unitNumber?: number;
    acceptedDate?: string;
    comments?: string;
    remove?: boolean;
  },
): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/jobrequests/${id}/assign`, {
    method: 'POST',
    body: JSON.stringify({
      userId: data.userId,
      unitNumber: data.unitNumber,
      acceptedDate: data.acceptedDate,
      comments: data.comments,
      remove: data.remove ?? false,
    }),
  });
}

export async function getMyWorkTracker(): Promise<WorkTrackerItemDto[]> {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), 20_000);
  try {
    return await request<WorkTrackerItemDto[]>('/api/jobrequests/my-tracker', {
      signal: controller.signal,
    });
  } finally {
    window.clearTimeout(timeout);
  }
}

export async function getClientJobs(includeCompleted = false): Promise<JobRequestResponse[]> {
  const qs = includeCompleted ? '?includeCompleted=true' : '';
  return request<JobRequestResponse[]>(`/api/clientjobs/my-jobs${qs}`);
}

/** Claim the next dormant multi-qty session (client portal "Add"). */
export async function activateClientSession(jobId: number): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/clientjobs/${jobId}/activate-session`, {
    method: 'POST',
  });
}

export async function getMyCompany(): Promise<CustomerResponse> {
  return request<CustomerResponse>('/api/clientportal/my-company');
}

export async function getClientPortalSummary(): Promise<ClientPortalSummaryDto> {
  return request<ClientPortalSummaryDto>('/api/clientportal/summary');
}

export async function updateMoiApprovalMode(
  mode: 'AllRequired' | 'AnyOne',
): Promise<CustomerResponse> {
  return request<CustomerResponse>('/api/clientportal/moi-approval-mode', {
    method: 'PATCH',
    body: JSON.stringify({ moiApprovalMode: mode }),
  });
}

export interface JobItemDocumentDto {
  id: number;
  jobId: number;
  folder: string;
  fileName: string;
  contentType: string;
  uploadedByName: string;
  uploadedAt: string;
  visibleToInternal: boolean;
}

export interface JobItemFolderDto {
  folder: string;
  documents: JobItemDocumentDto[];
}

export interface JobItemFoldersResponse {
  jobId: number;
  service: string;
  moiFormId?: number;
  moaFormId?: number;
  moiWorkflowState?: string;
  folders: JobItemFolderDto[];
}

export async function getJobItemFolders(
  jobId: number,
  unitNumber?: number,
): Promise<JobItemFoldersResponse> {
  const params = unitNumber ? `?unitNumber=${unitNumber}` : '';
  return request<JobItemFoldersResponse>(`/api/jobs/${jobId}/documents/folders${params}`);
}

export async function uploadJobItemDocument(
  jobId: number,
  folder: 'moi' | 'moa' | 'supporting',
  file: File,
  unitNumber?: number,
): Promise<JobItemDocumentDto> {
  const formData = new FormData();
  formData.append('file', file);
  const token = getToken();
  const headers = new Headers();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  const qs = new URLSearchParams({ folder });
  if (unitNumber) qs.set('unitNumber', String(unitNumber));
  const response = await fetch(
    `${API_BASE}/api/jobs/${jobId}/documents?${qs.toString()}`,
    { method: 'POST', headers, body: formData },
  );
  if (!response.ok) {
    const text = await response.text();
    throw new ApiError(formatApiError(text, response.status), response.status);
  }
  return response.json() as Promise<JobItemDocumentDto>;
}

export async function deleteJobItemDocument(jobId: number, documentId: number): Promise<void> {
  return request<void>(`/api/jobs/${jobId}/documents/${documentId}`, { method: 'DELETE' });
}

export async function downloadJobItemDocument(jobId: number, documentId: number): Promise<Blob> {
  const token = getToken();
  const headers = new Headers();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  const response = await fetch(
    `${API_BASE}/api/jobs/${jobId}/documents/${documentId}/download`,
    { headers },
  );
  if (!response.ok) {
    const text = await response.text();
    throw new ApiError(formatApiError(text, response.status), response.status);
  }
  return response.blob();
}

export async function assignClientJob(
  jobId: number,
  data: { userId: number; unitNumber?: number; comments?: string; remove?: boolean },
): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/clientjobs/${jobId}/assign`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function recordClientJobProgress(
  jobId: number,
  data: {
    unitNumber?: number;
    markUnitComplete?: boolean;
    markUnitIncomplete?: boolean;
    scheduledDate?: string;
  },
): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/clientjobs/${jobId}/progress`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function issueMoiJob(data: {
  customerId?: number;
  customerPackageId?: number;
  service: string;
  typeOfDocument?: string;
  documentTitle?: string;
  initiationDate?: string;
  requestedBy?: string;
  adHoc?: boolean;
}): Promise<JobRequestResponse> {
  return request<JobRequestResponse>('/api/clientjobs/issue-moi', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function issueMoiForJob(
  jobId: number,
  data?: {
    service?: string;
    typeOfDocument?: string;
    documentTitle?: string;
    requestedBy?: string;
    unitNumber?: number;
  },
): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/clientjobs/${jobId}/issue-moi`, {
    method: 'POST',
    body: JSON.stringify(data ?? {}),
  });
}

/** D1: MoiMoa → continue workflow; AdminBypass → note for Sharon (no MOI/MOA). */
export async function chooseJobWorkflow(
  jobId: number,
  data: { mode: 'MoiMoa' | 'AdminBypass'; unitNumber?: number; note?: string },
): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/clientjobs/${jobId}/workflow-choice`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updateJobRequest(id: number, data: Partial<JobRequestResponse>): Promise<void> {
  return request<void>(`/api/jobrequests/${id}`, {
    method: 'PUT',
    body: JSON.stringify({
      customerId: data.customerId,
      customer: data.customer,
      taskType: data.taskType,
      service: data.service,
      usedQty: data.usedQty,
      totalQty: data.totalQty,
      dateRequested: data.dateRequested,
      scheduledDate: data.scheduledDate,
      customerPackageId: data.customerPackageId,
      accountHolder: data.accountHolder,
      assignedUserId: data.assignedUserId,
      jobAssignedTo: data.jobAssignedTo,
      status: data.status,
      assignmentComments: data.assignmentComments,
    }),
  });
}

// Completed services
export async function getCompletedServices(search?: string, year?: number): Promise<CompletedServiceResponse[]> {
  const params = new URLSearchParams();
  if (search) params.set('search', search);
  if (year) params.set('year', String(year));
  const qs = params.toString();
  return request<CompletedServiceResponse[]>(`/api/completedservices${qs ? `?${qs}` : ''}`);
}

// Forms
export async function getMOIForms(jobId?: number, unitNumber?: number): Promise<FormResponse[]> {
  const params = new URLSearchParams();
  if (jobId) params.set('jobId', String(jobId));
  if (unitNumber) params.set('unitNumber', String(unitNumber));
  const qs = params.toString();
  return request<FormResponse[]>(`/api/moiforms${qs ? `?${qs}` : ''}`);
}

export async function getMOIForm(id: number): Promise<FormResponse> {
  return request<FormResponse>(`/api/moiforms/${id}`);
}

export async function createMOIForm(data: {
  jobId?: number;
  unitNumber?: number;
  company: string;
  formTemplateCode?: string;
  financeRelated?: boolean;
  bankSignatoryMatter?: boolean;
  data: Record<string, unknown>;
}): Promise<FormResponse> {
  return request<FormResponse>('/api/moiforms', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updateMOIForm(id: number, data: {
  jobId?: number;
  unitNumber?: number;
  company: string;
  formTemplateCode?: string;
  financeRelated?: boolean;
  bankSignatoryMatter?: boolean;
  expectedUpdatedAt?: string;
  data: Record<string, unknown>;
}): Promise<void> {
  return request<void>(`/api/moiforms/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function submitMoiForApproval(id: number): Promise<FormResponse> {
  return request<FormResponse>(`/api/moiforms/${id}/submit-for-approval`, { method: 'POST' });
}

export interface ClientApprovePayload {
  comments?: string;
  signatureFileName?: string;
  signatureDataUrl?: string;
}

export async function clientApproveMoiForm(id: number, payload: ClientApprovePayload): Promise<FormResponse> {
  return request<FormResponse>(`/api/moiforms/${id}/client-approve`, {
    method: 'POST',
    body: JSON.stringify({
      comments: payload.comments ?? '',
      signatureFileName: payload.signatureFileName,
      signatureDataUrl: payload.signatureDataUrl,
    }),
  });
}

export async function clientApproveMoaForm(id: number, payload: ClientApprovePayload): Promise<FormResponse> {
  return request<FormResponse>(`/api/moaforms/${id}/client-approve`, {
    method: 'POST',
    body: JSON.stringify({
      comments: payload.comments ?? '',
      signatureFileName: payload.signatureFileName,
      signatureDataUrl: payload.signatureDataUrl,
    }),
  });
}

export async function clientRejectMoiForm(id: number, reason: string): Promise<FormResponse> {
  return request<FormResponse>(`/api/moiforms/${id}/client-reject`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

export async function clientRejectMoaForm(id: number, reason: string): Promise<FormResponse> {
  return request<FormResponse>(`/api/moaforms/${id}/client-reject`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

export async function rejectMoiIntake(jobId: number, reason: string, unitNumber?: number): Promise<JobRequestResponse> {
  const qs = unitNumber != null ? `?unitNumber=${unitNumber}` : '';
  return request<JobRequestResponse>(`/api/jobrequests/${jobId}/reject-intake${qs}`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

export async function recommendMoiForm(id: number, comments: string): Promise<FormResponse> {
  return request<FormResponse>(`/api/moiforms/${id}/recommend`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

export async function approveMoiForm(id: number, comments: string): Promise<FormResponse> {
  return request<FormResponse>(`/api/moiforms/${id}/approve`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

export async function adminOverrideMoiForm(id: number, comments: string): Promise<FormResponse> {
  return request<FormResponse>(`/api/moiforms/${id}/admin-override`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

export async function getMOAForms(moiFormId?: number): Promise<FormResponse[]> {
  const q = moiFormId != null ? `?moiFormId=${moiFormId}` : '';
  return request<FormResponse[]>(`/api/moaforms${q}`);
}

export async function getMOAForm(id: number): Promise<FormResponse> {
  return request<FormResponse>(`/api/moaforms/${id}`);
}

export async function getMOAFormForJob(jobId: number, unitNumber?: number): Promise<FormResponse> {
  const qs = unitNumber != null ? `?unitNumber=${unitNumber}` : '';
  return request<FormResponse>(`/api/moaforms/for-job/${jobId}${qs}`);
}

export async function updateMOAForm(id: number, data: {
  jobId?: number;
  moiFormId?: number;
  company: string;
  formTemplateCode?: string;
  financeRelated?: boolean;
  bankSignatoryMatter?: boolean;
  shareMovement?: boolean;
  expectedUpdatedAt?: string;
  data: Record<string, unknown>;
}): Promise<void> {
  return request<void>(`/api/moaforms/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function updateMoaPack(id: number, data: {
  checklist: MoaPackChecklistDto;
  financeRelated: boolean;
  bankSignatoryMatter: boolean;
  shareMovement: boolean;
  expectedUpdatedAt?: string;
}): Promise<FormResponse> {
  return request<FormResponse>(`/api/moaforms/${id}/pack`, {
    method: 'PUT',
    body: JSON.stringify({
      checklist: data.checklist,
      financeRelated: data.financeRelated,
      bankSignatoryMatter: data.bankSignatoryMatter,
      shareMovement: data.shareMovement,
      expectedUpdatedAt: data.expectedUpdatedAt,
    }),
  });
}

export async function startMoaWorkflow(id: number): Promise<FormResponse> {
  return request<FormResponse>(`/api/moaforms/${id}/start-workflow`, { method: 'POST' });
}

export async function getBillingParties(activeOnly = true, category?: string): Promise<BillingPartyDto[]> {
  const params = new URLSearchParams();
  if (!activeOnly) params.set('activeOnly', 'false');
  if (category) params.set('category', category);
  const qs = params.toString();
  return request<BillingPartyDto[]>(`/api/billingparties${qs ? `?${qs}` : ''}`);
}

export async function createBillingParty(data: {
  name: string;
  category?: string;
  isActive?: boolean;
  sortOrder?: number;
}): Promise<BillingPartyDto> {
  return request<BillingPartyDto>('/api/billingparties', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function deleteBillingParty(id: number): Promise<void> {
  return request<void>(`/api/billingparties/${id}`, { method: 'DELETE' });
}

export async function approveMoiIntake(jobId: number, unitNumber?: number): Promise<JobRequestResponse> {
  const qs = unitNumber != null ? `?unitNumber=${unitNumber}` : '';
  return request<JobRequestResponse>(`/api/jobrequests/${jobId}/approve-intake${qs}`, {
    method: 'POST',
    body: JSON.stringify({}),
  });
}

export async function advanceJobHandoff(
  jobId: number,
  action: 'start-prep' | 'start-reso' | 'submit-admin-review' | 'sharon-approve-moa' | 'approve-for-moa' | 'reject-moa',
  comments?: string,
  unitNumber?: number,
): Promise<JobRequestResponse> {
  return request<JobRequestResponse>(`/api/jobrequests/${jobId}/handoff`, {
    method: 'POST',
    body: JSON.stringify({ action, comments, unitNumber }),
  });
}

export async function createMOAForm(data: {
  jobId?: number;
  moiFormId?: number;
  company: string;
  formTemplateCode?: string;
  financeRelated?: boolean;
  bankSignatoryMatter?: boolean;
  shareMovement?: boolean;
  data: Record<string, unknown>;
}): Promise<FormResponse> {
  return request<FormResponse>('/api/moaforms', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function getMoaWorkflow(moaFormId: number): Promise<WorkflowInstanceDto> {
  return request<WorkflowInstanceDto>(`/api/workflowinstances/moa/${moaFormId}`);
}

export async function approveMoaWorkflowStep(moaFormId: number, comments: string): Promise<WorkflowInstanceDto> {
  return request<WorkflowInstanceDto>(`/api/workflowinstances/moa/${moaFormId}/approve-step`, {
    method: 'POST',
    body: JSON.stringify({ comments }),
  });
}

export async function adminOverrideMoaStep(
  moaFormId: number,
  stepInstanceId: number,
  comments: string,
): Promise<WorkflowInstanceDto> {
  return request<WorkflowInstanceDto>(`/api/workflowinstances/moa/${moaFormId}/admin-override`, {
    method: 'POST',
    body: JSON.stringify({ stepInstanceId, comments }),
  });
}

export async function getDivisionGroups(): Promise<DivisionGroupDto[]> {
  return request<DivisionGroupDto[]>('/api/divisiongroups');
}

export async function updateDivisionGroup(id: number, data: DivisionGroupDto): Promise<void> {
  return request<void>(`/api/divisiongroups/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function importDivisionGroups(rows: { company: string; divisionGroup: string; hasLoa: boolean }[]): Promise<{ updated: number; unmatched: string[] }> {
  return request('/api/divisiongroups/import', {
    method: 'POST',
    body: JSON.stringify(rows),
  });
}

export async function getWorkflowTemplates(workflowType?: string): Promise<WorkflowTemplateDto[]> {
  const qs = workflowType ? `?workflowType=${encodeURIComponent(workflowType)}` : '';
  return request<WorkflowTemplateDto[]>(`/api/workflowtemplates${qs}`);
}

export async function updateWorkflowTemplate(id: number, data: WorkflowTemplateDto): Promise<void> {
  return request<void>(`/api/workflowtemplates/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function getFormTemplates(formType?: string): Promise<FormTemplateDto[]> {
  const qs = formType ? `?formType=${encodeURIComponent(formType)}` : '';
  return request<FormTemplateDto[]>(`/api/formtemplates${qs}`);
}

export async function resolveFormTemplate(
  formType: string,
  company?: string,
  templateCode?: string,
  service?: string,
): Promise<FormTemplateDto> {
  const params = new URLSearchParams({ formType });
  if (company) params.set('company', company);
  if (templateCode) params.set('templateCode', templateCode);
  if (service) params.set('service', service);
  return request<FormTemplateDto>(`/api/formtemplates/resolve?${params}`);
}

export async function updateFormTemplate(id: number, data: FormTemplateDto): Promise<void> {
  return request<void>(`/api/formtemplates/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function createFormTemplate(data: FormTemplateDto): Promise<FormTemplateDto> {
  return request<FormTemplateDto>('/api/formtemplates', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

// Dashboard
export async function getDashboardStats(): Promise<DashboardStatsResponse> {
  return request<DashboardStatsResponse>('/api/dashboard/stats');
}

// Package schedule / tracking
export async function getPackageSchedules(params?: {
  customerId?: number;
  customerPackageId?: number;
  from?: string;
  to?: string;
}): Promise<PackageScheduleItemDto[]> {
  const qs = new URLSearchParams();
  if (params?.customerId) qs.set('customerId', String(params.customerId));
  if (params?.customerPackageId) qs.set('customerPackageId', String(params.customerPackageId));
  if (params?.from) qs.set('from', params.from);
  if (params?.to) qs.set('to', params.to);
  const query = qs.toString();
  return request<PackageScheduleItemDto[]>(`/api/packageschedules${query ? `?${query}` : ''}`);
}

export async function createPackageSchedule(data: {
  customerId: number;
  customerPackageId: number;
  itemType: string;
  title: string;
  scheduledAt: string;
  durationMinutes?: number;
  status?: string;
  notes?: string;
  bookingUrl?: string;
  sequenceNumber?: number;
}): Promise<PackageScheduleItemDto> {
  return request<PackageScheduleItemDto>('/api/packageschedules', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updatePackageSchedule(
  id: number,
  data: {
    customerId: number;
    customerPackageId: number;
    itemType: string;
    title: string;
    scheduledAt: string;
    durationMinutes?: number;
    status?: string;
    notes?: string;
    bookingUrl?: string;
    sequenceNumber?: number;
  },
): Promise<void> {
  return request<void>(`/api/packageschedules/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function deletePackageSchedule(id: number): Promise<void> {
  return request<void>(`/api/packageschedules/${id}`, { method: 'DELETE' });
}

export async function getSignatoryOverlaps(): Promise<SignatoryOverlapDto[]> {
  return request<SignatoryOverlapDto[]>('/api/signatory-dedup/overlaps');
}

export async function linkSignatoryByEmail(email: string): Promise<SignatoryLinkResultDto> {
  return request<SignatoryLinkResultDto>('/api/signatory-dedup/link', {
    method: 'POST',
    body: JSON.stringify({ email }),
  });
}

export interface NotificationDto {
  id: number;
  eventType: string;
  title: string;
  message: string;
  jobRequestId?: number;
  customerId?: number;
  isRead: boolean;
  createdAt: string;
}

export async function getNotifications(unreadOnly = false): Promise<NotificationDto[]> {
  const query = unreadOnly ? '?unreadOnly=true' : '';
  return request<NotificationDto[]>(`/api/notifications${query}`);
}

export async function markNotificationRead(id: number): Promise<void> {
  return request<void>(`/api/notifications/${id}/read`, { method: 'POST' });
}

async function fetchAuthenticatedBlob(path: string): Promise<Blob> {
  const token = getToken();
  const headers = new Headers();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  const response = await fetch(`${API_BASE}${path}`, { headers });
  if (!response.ok) {
    const text = await response.text();
    throw new ApiError(formatApiError(text, response.status), response.status);
  }
  return response.blob();
}

export async function downloadMoiExportPack(formId: number): Promise<Blob> {
  return fetchAuthenticatedBlob(`/api/moiforms/${formId}/export-pack`);
}

export async function downloadMoaExportPack(formId: number): Promise<Blob> {
  return fetchAuthenticatedBlob(`/api/moaforms/${formId}/export-pack`);
}

export function saveBlobAsFile(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

/** Fetch the whole printable pack for one task (optionally one session) as an HTML blob. */
export async function downloadTaskPack(jobId: number, unitNumber?: number): Promise<Blob> {
  const q = unitNumber ? `?unitNumber=${unitNumber}` : '';
  return fetchAuthenticatedBlob(`/api/jobs/${jobId}/pack${q}`);
}

/** Open the task pack in a new tab, ready to print / save as PDF. */
export async function openTaskPack(jobId: number, unitNumber?: number): Promise<void> {
  const blob = await downloadTaskPack(jobId, unitNumber);
  const url = URL.createObjectURL(blob);
  const win = window.open(url, '_blank');
  if (!win) {
    // Popup blocked — fall back to a direct download so the action never silently fails.
    const suffix = unitNumber ? `-s${unitNumber}` : '';
    saveBlobAsFile(blob, `task-${jobId}${suffix}-pack.html`);
  }
  // Revoke after the new tab has had time to load the document.
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
