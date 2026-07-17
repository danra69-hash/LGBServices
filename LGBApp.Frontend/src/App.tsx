import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { LayoutDashboard, Users, Package, Shield, CalendarDays, UserPlus } from 'lucide-react';
import { StatsCards } from './components/StatsCards';
import { CustomerTable } from './components/CustomerTable';
import { CustomerDetails } from './components/CustomerDetails';
import { ProductsTable } from './components/ProductsTable';
import { JobRequestsTable } from './components/JobRequestsTable';
import { JobRequestDetailsModal } from './components/JobRequestDetailsModal';
import { CompletedServicesTable } from './components/CompletedServicesTable';
import { ServiceHistoryModal } from './components/ServiceHistoryModal';
import { CreateCustomerModal } from './components/CreateCustomerModal';
import { CreateProductModal } from './components/CreateProductModal';
import { UserManagement } from './components/UserManagement';
import { AdminSignatoryDedup } from './components/AdminSignatoryDedup';
import { AdminWorkflowConfig } from './components/AdminWorkflowConfig';
import { BillingPartiesAdmin } from './components/BillingPartiesAdmin';
import { AdminFormTemplates } from './components/AdminFormTemplates';
import { AdminDashboard } from './components/AdminDashboard';
import { ClientPortal } from './components/ClientPortal';
import { ClientPackages } from './components/ClientPackages';
import { CreateUserModal } from './components/CreateUserModal';
import { MOIFormModal } from './components/MOIFormModal';
import { MOAFormModal } from './components/MOAFormModal';
import { Login } from './components/Login';
import { ChangePasswordModal } from './components/ChangePasswordModal';
import { PackageTracking } from './components/PackageTracking';
import { PackageWorkboard } from './components/PackageWorkboard';
import { MyWorkTracker } from './components/MyWorkTracker';
import { NotificationBell } from './components/NotificationBell';
import { buildCustomerAddOnLines } from './lib/packagePricing';
import { mapMoaFormResponseToModalState, resolveMoaUnitNumber, SHOW_MOA_SEQUENTIAL_WORKFLOW } from './lib/moaFormState';
import { mapMoiFormResponseToModalState, resolveLinkedMoiFormId } from './lib/moiFormState';
import {
  canClientEditMoiDraft,
  canInternalEditMoi,
  canOpenMoaForJob,
  canInternalEditMoa,
  canClientViewMoa,
  resolveActiveHandoff,
  shouldOpenMoaForm,
  scopeJobForUnit,
  isExecutingHandoff,
  executingUnitsForJob,
} from './lib/packageItemStatus';
import {
  ApiError,
  assignJobRequest,
  clearAuth,
  createCustomer,
  deleteCustomer,
  adminOverrideMoiForm,
  approveMoiIntake,
  advanceJobHandoff,
  clientApproveMoaForm,
  uploadJobItemDocument,
  clientApproveMoiForm,
  clientRejectMoaForm,
  clientRejectMoiForm,
  createMOAForm,
  createMOIForm,
  getMOAForm,
  getMOAFormForJob,
  getMOIForm,
  startMoaWorkflow,
  submitMoiForApproval,
  updateMOAForm,
  updateMoaPack,
  recommendMoiForm,
  recordJobProgress,
  rejectMoiIntake,
  createProduct,
  createUser,
  getCustomers,
  getJobRequest,
  getMOIForms,
  getMyCompany,
  getProducts,
  getAuthUser,
  getUsers,
  getInternalDirectoryUsers,
  isAdmin,
  canManageUsers,
  isClientAdmin,
  isClientSignatory,
  isExternalUser,
  isInternalStaff,
  roleLabel,
  isAssignableInternalStaff,
  isInternalSecretaryOnly,
  setAuthExpiredHandler,
  updateCustomer,
  updateJobRequest,
  updateMOIForm,
  updateProduct,
  type CustomerPackageDto,
  type CustomerResponse,
  type JobRequestResponse,
  type JobRequestUnitDto,
  type ProductResponse,
  type UserResponse,
} from './lib/api';

type Customer = CustomerResponse;
type Tab = 'dashboard' | 'packages' | 'team' | 'customers' | 'products' | 'tracking' | 'admin';

export default function App() {
  const [currentUser, setCurrentUser] = useState<UserResponse | null>(() => getAuthUser());
  const [activeTab, setActiveTab] = useState<Tab>('dashboard');
  const [toast, setToast] = useState('');
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [myCompany, setMyCompany] = useState<CustomerResponse | null>(null);
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [directoryUsers, setDirectoryUsers] = useState<UserResponse[]>([]);
  const [apiUsers, setApiUsers] = useState<{ id: number; name: string }[]>([]);
  const [internalDirectoryUsers, setInternalDirectoryUsers] = useState<{ id: number; name: string }[]>([]);
  const assignableUsers = useMemo(
    () => directoryUsers
      .filter(isAssignableInternalStaff)
      .map((u) => ({ id: u.userId, name: u.name })),
    [directoryUsers],
  );
  const secTeamUsers = useMemo(
    () => directoryUsers
      .filter(isInternalSecretaryOnly)
      .map((u) => ({ id: u.userId, name: u.name })),
    [directoryUsers],
  );
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [selectedPackageWork, setSelectedPackageWork] = useState<{
    customer: Customer;
    package: CustomerPackageDto;
  } | null>(null);
  const [isCreateCustomerModalOpen, setIsCreateCustomerModalOpen] = useState(false);
  const [isEditCustomerMode, setIsEditCustomerMode] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState<Customer | null>(null);
  const [isCreateProductModalOpen, setIsCreateProductModalOpen] = useState(false);
  const [isEditProductMode, setIsEditProductMode] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<ProductResponse | null>(null);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState(false);
  const [isCreateUserModalOpen, setIsCreateUserModalOpen] = useState(false);
  const [isMOIFormOpen, setIsMOIFormOpen] = useState(false);
  const [isMOAFormOpen, setIsMOAFormOpen] = useState(false);
  const [isJobDetailsModalOpen, setIsJobDetailsModalOpen] = useState(false);
  const [selectedJobRequest, setSelectedJobRequest] = useState<JobRequestResponse | null>(null);
  const [moiDataForMOA, setMOIDataForMOA] = useState<Record<string, unknown> | null>(null);
  const [linkedMoiForMoa, setLinkedMoiForMoa] = useState<Record<string, unknown> | null>(null);
  const [moiStackedOnMoa, setMoiStackedOnMoa] = useState(false);
  const [submittedMOIForms, setSubmittedMOIForms] = useState<Record<string, unknown>[]>([]);
  const [selectedMOIForm, setSelectedMOIForm] = useState<Record<string, unknown> | null>(null);
  const [isMOIViewMode, setIsMOIViewMode] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [scheduleRefreshKey, setScheduleRefreshKey] = useState(0);
  const [userRefreshKey, setUserRefreshKey] = useState(0);

  const showToast = useCallback((message: string) => {
    setToast(message);
    setTimeout(() => setToast(''), 4000);
  }, []);

  const handleAuthExpired = useCallback(() => {
    setCurrentUser(null);
    setActiveTab('dashboard');
    showToast('Session expired. Please sign in again.');
  }, [showToast]);

  const handleSignOut = useCallback(() => {
    clearAuth();
    setCurrentUser(null);
    setActiveTab('dashboard');
  }, []);

  useEffect(() => {
    setAuthExpiredHandler(handleAuthExpired);
  }, [handleAuthExpired]);

  const bumpRefresh = () => setRefreshKey((k) => k + 1);
  const handleActionSuccess = useCallback((message?: string) => {
    if (message) showToast(message);
    bumpRefresh();
  }, [showToast]);

  const loadCustomers = useCallback(async () => {
    try {
      const data = await getCustomers();
      setCustomers(data);
    } catch (err) {
      setCustomers([]);
      showToast(err instanceof ApiError ? err.message : 'Failed to load customers.');
    }
  }, [showToast]);

  const loadProducts = useCallback(async () => {
    try {
      const data = await getProducts();
      setProducts([...data].sort((a, b) => (a.packagePrice ?? 0) - (b.packagePrice ?? 0)));
    } catch (err) {
      setProducts([]);
      showToast(err instanceof ApiError ? err.message : 'Failed to load products.');
    }
  }, [showToast]);

  const loadMyCompany = useCallback(async () => {
    try {
      const data = await getMyCompany();
      setMyCompany(data);
    } catch (err) {
      setMyCompany(null);
      showToast(err instanceof ApiError ? err.message : 'Failed to load company.');
    }
  }, [showToast]);

  const loadUsers = useCallback(async () => {
    try {
      const data = await getUsers();
      setDirectoryUsers(data);
      setApiUsers(data.map((u) => ({ id: u.userId, name: u.name })));
    } catch (err) {
      setDirectoryUsers([]);
      setApiUsers([]);
      showToast(err instanceof ApiError ? err.message : 'Failed to load users.');
    }
  }, [showToast]);

  const loadInternalDirectory = useCallback(async () => {
    try {
      const data = await getInternalDirectoryUsers();
      setInternalDirectoryUsers(data.map((u) => ({ id: u.userId, name: u.name })));
    } catch (err) {
      setInternalDirectoryUsers([]);
      showToast(err instanceof ApiError ? err.message : 'Failed to load staff directory.');
    }
  }, [showToast]);

  const loadMOIForms = useCallback(async () => {
    try {
      const forms = await getMOIForms();
      setSubmittedMOIForms(forms.map((f) => mapMoiFormResponseToModalState(f)));
    } catch (err) {
      setSubmittedMOIForms([]);
      showToast(err instanceof ApiError ? err.message : 'Failed to load MOI forms.');
    }
  }, [showToast]);

  const userIsAdmin = isAdmin(currentUser);
  const userIsExternal = isExternalUser(currentUser);
  const userIsClientAdmin = isClientAdmin(currentUser);
  const userIsSignatory = isClientSignatory(currentUser);
  const userIsInternal = isInternalStaff(currentUser);
  const userCanManageTeam = canManageUsers(currentUser);
  const moaHandoffUnitNumber = resolveMoaUnitNumber(
    selectedJobRequest ?? undefined,
    moiDataForMOA ?? undefined,
    moiDataForMOA?.unitNumber as number | undefined,
  );
  const selectedMoaHandoff = selectedJobRequest
    ? resolveActiveHandoff(selectedJobRequest, moaHandoffUnitNumber)
    : '';
  const moaAwaitingAdminReview = selectedMoaHandoff === 'AdminReview';
  const moaInternallyEditable = userIsInternal
    && !userIsClientAdmin
    && !userIsSignatory
    && canInternalEditMoa(selectedMoaHandoff, {
      hasWorkflow: SHOW_MOA_SEQUENTIAL_WORKFLOW && Boolean(moiDataForMOA?.workflow),
      sharonApproved: Boolean(moiDataForMOA?.sharonApprovedAt),
      awaitingAdminReview: moaAwaitingAdminReview,
      canApproveMoa: Boolean(currentUser?.canApproveMoa),
      isAdmin: userIsAdmin,
      moiWorkflowState: (moiDataForMOA?.moiWorkflowState as string | undefined)
        ?? selectedJobRequest?.moiWorkflowState,
    });
  const canSubmitMoaForAdmin = moaInternallyEditable
    && !moaAwaitingAdminReview
    && !Boolean(moiDataForMOA?.submittedForAdminReviewAt);
  const moaExecutingPhase = isExecutingHandoff(selectedMoaHandoff);
  const canMarkMoaExecutionComplete = moaExecutingPhase
    && (userIsAdmin || Boolean(currentUser?.canApproveMoa));
  const moaFormDirtyRef = useRef(false);
  const handleMoaDirtyChange = useCallback((dirty: boolean) => {
    moaFormDirtyRef.current = dirty;
  }, []);

  useEffect(() => {
    if (!userIsAdmin || !isCreateCustomerModalOpen) return;
    loadProducts();
  }, [userIsAdmin, isCreateCustomerModalOpen, loadProducts]);

  useEffect(() => {
    if (!userIsAdmin || (!isCreateUserModalOpen && activeTab !== 'admin')) return;
    void loadCustomers();
  }, [userIsAdmin, isCreateUserModalOpen, activeTab, loadCustomers]);

  useEffect(() => {
    if (!currentUser) return;
    if (userIsAdmin && (activeTab === 'customers' || activeTab === 'tracking' || activeTab === 'admin' || activeTab === 'dashboard')) {
      loadCustomers();
      loadProducts();
    }
    if (userIsAdmin && (activeTab === 'products' || activeTab === 'admin')) {
      loadProducts();
    }
    if ((userIsAdmin || userIsClientAdmin) && (activeTab === 'admin' || activeTab === 'team' || activeTab === 'dashboard' || activeTab === 'customers')) {
      loadUsers();
    }
    if (userIsInternal && activeTab === 'dashboard') {
      loadMOIForms();
      void loadInternalDirectory();
    }
    if (userIsAdmin && activeTab === 'dashboard') {
      loadMOIForms();
    }
    if (userIsExternal && activeTab === 'dashboard') {
      void loadMyCompany();
      loadMOIForms();
      loadProducts();
    }
  }, [currentUser, userIsAdmin, userIsClientAdmin, userIsInternal, userIsExternal, activeTab, refreshKey, loadCustomers, loadProducts, loadUsers, loadMOIForms, loadMyCompany, loadInternalDirectory]);

  useEffect(() => {
    if (!currentUser || !userIsInternal || !isMOAFormOpen) return;
    void loadInternalDirectory();
  }, [currentUser, userIsInternal, isMOAFormOpen, loadInternalDirectory]);

  useEffect(() => {
    if (!currentUser) return;
    const shouldPoll = isMOAFormOpen || isMOIFormOpen
      || (userIsInternal && activeTab === 'dashboard')
      || (userIsExternal && activeTab === 'dashboard');
    if (!shouldPoll) return;

    const timer = window.setInterval(() => {
      if (!(isMOAFormOpen && moaInternallyEditable)) {
        bumpRefresh();
      }
      if (isMOIFormOpen && selectedMOIForm?.id && isMOIViewMode) {
        void getMOIForm(selectedMOIForm.id as number)
          .then((form) => {
            setSelectedMOIForm((prev) => {
              if (!prev) return mapMoiFormResponseToModalState(form, selectedJobRequest ?? undefined);
              return {
                ...prev,
                workflowState: form.workflowState,
                updatedAt: form.updatedAt,
                clientApprovals: form.clientApprovals,
                pendingApprovers: form.pendingApprovers,
                requiredApprovers: form.requiredApprovers,
              };
            });
          })
          .catch(() => undefined);
      }
      if (isMOAFormOpen && moiDataForMOA?.id && selectedJobRequest && !moaFormDirtyRef.current && !isMOIFormOpen && !moaInternallyEditable) {
        const unitNumber = moaHandoffUnitNumber;
        const localUpdatedAt = moiDataForMOA?.updatedAt as string | undefined;
        void getMOAFormForJob(selectedJobRequest.id, unitNumber)
          .then((form) => {
            if (moaFormDirtyRef.current) return;
            if (localUpdatedAt && form.updatedAt && form.updatedAt <= localUpdatedAt) return;
            setMOIDataForMOA(mapMoaFormResponseToModalState(form, selectedJobRequest));
          })
          .catch(() => undefined);
      }
    }, 20000);

    return () => window.clearInterval(timer);
  }, [
    currentUser,
    userIsInternal,
    userIsExternal,
    activeTab,
    isMOAFormOpen,
    isMOIFormOpen,
    isMOIViewMode,
    selectedMOIForm?.id,
    moiDataForMOA?.id,
    selectedJobRequest,
    moaHandoffUnitNumber,
    moaInternallyEditable,
    bumpRefresh,
  ]);

  useEffect(() => {
    if (!currentUser) return;
    if (userIsClientAdmin && !['dashboard', 'packages', 'team'].includes(activeTab)) {
      setActiveTab('dashboard');
    } else if (userIsSignatory && activeTab !== 'dashboard') {
      setActiveTab('dashboard');
    } else if (!userIsAdmin && !userIsExternal && activeTab !== 'dashboard') {
      setActiveTab('dashboard');
    }
  }, [currentUser, userIsAdmin, userIsClientAdmin, userIsExternal, activeTab]);

  const clientAdminTabs: { id: Tab; label: string; icon: typeof LayoutDashboard }[] = [
    { id: 'dashboard', label: 'Portal', icon: LayoutDashboard },
    { id: 'packages', label: 'My Packages', icon: Package },
    { id: 'team', label: 'Team', icon: UserPlus },
  ];

  const navigateToTab = (tab: Tab) => {
    if (userIsClientAdmin && !clientAdminTabs.some((t) => t.id === tab)) {
      setActiveTab('dashboard');
      return;
    }
    if (userIsSignatory && tab !== 'dashboard') {
      setActiveTab('dashboard');
      return;
    }
    if (!userIsAdmin && !userIsClientAdmin && !userIsSignatory && tab !== 'dashboard') {
      setActiveTab('dashboard');
      return;
    }
    if (tab === 'admin' && !userIsAdmin) {
      setActiveTab('dashboard');
      return;
    }
    setActiveTab(tab);
  };

  /** Set true to show the Tracking tab in Sharon's nav again. */
  const SHOW_TRACKING_TAB = false;

  const appTabs = userIsAdmin
    ? [
        { id: 'dashboard' as const, label: 'Operations', icon: LayoutDashboard },
        { id: 'customers' as const, label: 'Customers', icon: Users },
        { id: 'products' as const, label: 'Products', icon: Package },
        ...(SHOW_TRACKING_TAB
          ? [{ id: 'tracking' as const, label: 'Tracking', icon: CalendarDays }]
          : []),
        { id: 'admin' as const, label: 'Settings', icon: Shield },
      ]
    : userIsClientAdmin
      ? clientAdminTabs
      : userIsSignatory
        ? [{ id: 'dashboard' as const, label: 'My documents', icon: LayoutDashboard }]
        : [{ id: 'dashboard' as const, label: 'Client Portal', icon: LayoutDashboard }];

  const computeExpiryDate = (purchased: string, validity: string) => {
    const start = new Date(purchased);
    const match = validity.match(/(\d+)/);
    const amount = match ? Math.min(parseInt(match[1], 10), 10) : 1;
    const end = new Date(start);
    if (validity.toLowerCase().includes('month')) {
      end.setMonth(end.getMonth() + amount);
    } else {
      end.setFullYear(end.getFullYear() + amount);
    }
    return end.toISOString().split('T')[0];
  };

  const handleCustomerSubmit = async (data: Record<string, unknown>) => {
    const holders = (data.accountHolders as Array<Record<string, unknown>> | undefined) ?? [];
    const packageRows = (data.packages as Array<Record<string, unknown>> | undefined) ?? [];
    const dateCreated = String(data.dateCreated ?? new Date().toISOString().split('T')[0]);
    const moi = holders.filter((h) => h.moi).map((h) => String(h.name ?? '')).filter(Boolean);
    const moiApproval = holders.filter((h) => h.moiApproval).map((h) => String(h.name ?? '')).filter(Boolean);
    const moa = holders.filter((h) => h.moa).map((h) => String(h.name ?? '')).filter(Boolean);

    const customerId = data.customerId as number | undefined;
    if (customerId) {
      const existing = editingCustomer ?? customers.find((c) => c.id === customerId);
      const packages = packageRows.map((p, index) => {
        const existingPkg = existing?.packages?.[index];
        const validity = String(p.validity ?? '1 Year');
        const purchased = String(p.purchasedDate ?? existingPkg?.purchasedDate ?? dateCreated);
        const addOnLines = buildCustomerAddOnLines(
          p.addOnLines as Array<{ name: string; qty: number; unitPrice: number }> | undefined,
        );
        return {
          id: typeof p.key === 'number' && (p.key as number) < 1_000_000 ? (p.key as number) : existingPkg?.id ?? 0,
          packageName: String(p.packageName ?? ''),
          packageValue: Number(p.packageValue ?? 0),
          packageDetail: String(p.packageDetail ?? '') || undefined,
          purchasedDate: purchased,
          expiryDate: computeExpiryDate(purchased, validity),
          validity,
          pricing: {
            validity,
            basePackagePrice: Number(p.basePackagePrice ?? 0),
            addOnLines,
          },
          status: String(p.status ?? 'Active'),
          activeValue: 0,
        };
      });
      const primary = packages.find((p) => p.status === 'Active') ?? packages[0];

      const updated = await updateCustomer(customerId, {
        id: customerId,
        name: String(data.contactName ?? ''),
        email: String(data.email ?? ''),
        phone: String(data.mobile ?? ''),
        company: String(data.companyName ?? ''),
        status: (data.status as Customer['status']) ?? 'Active',
        value: 0,
        lastContact: existing?.lastContact ?? dateCreated,
        invoiceBy: String(data.invoiceBy ?? ''),
        chargeTo: String(data.chargeTo ?? ''),
        invoiceByPartyIds: (data.invoiceByPartyIds as number[] | undefined) ?? [],
        chargeToPartyIds: (data.chargeToPartyIds as number[] | undefined) ?? [],
        package: primary?.packageName ?? '',
        packageValue: primary?.packageValue ?? 0,
        cosec: Boolean(data.cosec),
        divisionGroupCode: String(data.divisionGroupCode ?? existing?.divisionGroupCode ?? ''),
        hasLoa: Boolean(data.hasLoa ?? existing?.hasLoa),
        loaHolders: Array.isArray(data.loaHolders)
          ? data.loaHolders as string[]
          : String(data.loaHolders ?? '').split(',').map((s) => s.trim()).filter(Boolean).length > 0
            ? String(data.loaHolders ?? '').split(',').map((s) => s.trim()).filter(Boolean)
            : existing?.loaHolders ?? [],
        moiFormTemplateCode: data.moiFormTemplateCode as string | undefined ?? existing?.moiFormTemplateCode,
        moaFormTemplateCode: data.moaFormTemplateCode as string | undefined ?? existing?.moaFormTemplateCode,
        moaWorkflowTemplateCode: data.moaWorkflowTemplateCode as string | undefined ?? existing?.moaWorkflowTemplateCode,
        moi,
        moiApproval: moiApproval.length > 0 ? moiApproval : existing?.moiApproval ?? [],
        moa,
        purchasedDate: primary?.purchasedDate ?? dateCreated,
        expiryDate: primary?.expiryDate ?? dateCreated,
        packages,
        accountHolders: holders.map((h) => ({
          id: Number(h.id ?? 0),
          name: String(h.name ?? ''),
          email: String(h.email ?? ''),
          phone: String(h.phone ?? ''),
          moi: Boolean(h.moi),
          moiApproval: Boolean(h.moiApproval),
          moa: Boolean(h.moa),
        })),
      });
      setSelectedCustomer(updated);
      showToast('Customer updated successfully.');
    } else {
      await createCustomer({
        companyName: data.companyName,
        contactName: data.contactName,
        email: data.email,
        mobile: data.mobile,
        invoiceBy: data.invoiceBy,
        chargeTo: data.chargeTo,
        invoiceByPartyIds: data.invoiceByPartyIds as number[] | undefined,
        chargeToPartyIds: data.chargeToPartyIds as number[] | undefined,
        cosec: data.cosec ?? false,
        divisionGroupCode: data.divisionGroupCode,
        hasLoa: data.hasLoa ?? false,
        loaHolders: String(data.loaHolders ?? '').split(',').map((s) => s.trim()).filter(Boolean),
        moiFormTemplateCode: data.moiFormTemplateCode,
        moaFormTemplateCode: data.moaFormTemplateCode,
        moaWorkflowTemplateCode: data.moaWorkflowTemplateCode,
        dateCreated,
        packages: packageRows.map((p) => {
          const validity = String(p.validity ?? '1 Year');
          const purchased = String(p.purchasedDate ?? dateCreated);
          const addOnLines = buildCustomerAddOnLines(
            p.addOnLines as Array<{ name: string; qty: number; unitPrice: number }> | undefined,
          );
          return {
            packageName: p.packageName,
            packageValue: p.packageValue,
            packageDetail: p.packageDetail,
            validity,
            purchasedDate: purchased,
            pricingJson: JSON.stringify({
              validity,
              basePackagePrice: Number(p.basePackagePrice ?? 0),
              addOnLines,
            }),
            status: 'Active',
          };
        }),
        accountHolders: holders.map((h) => ({
          id: h.id,
          name: h.name,
          email: h.email,
          phone: h.phone,
          moi: h.moi ?? false,
          moiApproval: h.moiApproval ?? false,
          moa: h.moa ?? false,
        })),
      });
      showToast('Customer created successfully.');
    }
    await loadCustomers();
    bumpRefresh();
  };

  const handleEditCustomer = (customer: Customer) => {
    setEditingCustomer(customer);
    setIsEditCustomerMode(true);
    setIsCreateCustomerModalOpen(true);
  };

  const handleDeleteCustomer = async (customer: Customer) => {
    if (!window.confirm(`Delete customer "${customer.company}"? This cannot be undone.`)) return;
    try {
      await deleteCustomer(customer.id);
      if (selectedCustomer?.id === customer.id) setSelectedCustomer(null);
      await loadCustomers();
      bumpRefresh();
      showToast('Customer deleted.');
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to delete customer.');
    }
  };

  const handleProductSubmit = async (data: ProductResponse & { id?: number; productType?: string; unitPrice?: number }) => {
    try {
      if (data.id) {
        await updateProduct(data.id, data);
        showToast('Product updated successfully.');
      } else {
        await createProduct(data);
        showToast('Product created successfully.');
      }
      await loadProducts();
      bumpRefresh();
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to save product.');
      throw err;
    }
  };

  const handleUserSubmit = async (data: {
    name: string;
    email: string;
    mobile: string;
    password: string;
    role: string;
    jobTitle?: string;
    canRecommendMoi?: boolean;
    canApproveMoiIntake?: boolean;
    canApproveMoi?: boolean;
    canApproveMoa?: boolean;
    customerId?: number;
  }) => {
    try {
      await createUser({
        email: data.email,
        password: data.password,
        name: data.name,
        mobile: data.mobile,
        role: data.role,
        jobTitle: data.jobTitle,
        canRecommendMoi: data.canRecommendMoi,
        canApproveMoiIntake: data.canApproveMoiIntake,
        canApproveMoi: data.canApproveMoi,
        canApproveMoa: data.canApproveMoa,
        customerId: data.customerId,
      });
      setUserRefreshKey((k) => k + 1);
      showToast('User created successfully.');
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to create user.');
    }
  };

  const saveMoiDraft = async (
    data: Record<string, unknown>,
    options?: { silent?: boolean },
  ): Promise<{ id: number; jobId: number; unitNumber?: number }> => {
    const jobId = selectedJobRequest?.id ?? (data.jobId as number | undefined);
    if (!jobId) throw new ApiError('MOI must be linked to a package item before saving.', 400);

    const unitNumber = selectedJobRequest?.activeUnitNumber
      ?? (data.unitNumber as number | undefined)
      ?? (data.activeUnitNumber as number | undefined)
      ?? ((selectedJobRequest?.totalQty ?? 1) <= 1 ? 1 : undefined);
    const pendingFiles = Array.isArray(data.attachedFiles)
      ? data.attachedFiles.filter((f): f is File => f instanceof File)
      : [];
    const { attachedFiles: _omit, ...formFields } = data;
    if (unitNumber != null) {
      formFields.unitNumber = unitNumber;
      formFields.activeUnitNumber = unitNumber;
    }
    const payload = {
      jobId,
      unitNumber,
      company: String(
        data.company
        ?? selectedJobRequest?.customer
        ?? selectedMOIForm?.company
        ?? '',
      ),
      formTemplateCode: data.formTemplateCode as string | undefined,
      financeRelated: Boolean(data.financeRelated),
      bankSignatoryMatter: Boolean(data.bankSignatoryMatter),
      expectedUpdatedAt: (data.updatedAt as string | undefined)
        ?? (selectedMOIForm?.updatedAt as string | undefined),
      data: formFields,
    };
    let formId = (data.id as number | undefined) ?? (selectedMOIForm?.id as number | undefined);
    if (formId) {
      await updateMOIForm(formId, payload);
    } else {
      const created = await createMOIForm(payload);
      formId = created.id;
    }
    if (pendingFiles.length > 0) {
      const folder = formFields.supportingDocument ? 'supporting' : 'moi';
      for (const file of pendingFiles) {
        await uploadJobItemDocument(jobId, folder, file, unitNumber);
      }
    }
    const saved = await getMOIForm(formId);
    // Always refresh jobs so curated MOI documentTitle replaces generic names as soon as saved
    bumpRefresh();
    if (!options?.silent) {
      await loadMOIForms();
    }
    const nextForm = mapMoiFormResponseToModalState(saved, {
      id: jobId,
      service: selectedJobRequest?.service,
      taskType: selectedJobRequest?.taskType,
      status: selectedJobRequest?.status,
      totalQty: selectedJobRequest?.totalQty,
      activeUnitNumber: unitNumber,
    });
    if (options?.silent) {
      setSelectedMOIForm((prev) => {
        if (!prev) return nextForm;
        return {
          ...prev,
          id: saved.id,
          updatedAt: saved.updatedAt,
          workflowState: saved.workflowState ?? prev.workflowState,
          clientApprovals: saved.clientApprovals ?? prev.clientApprovals,
          pendingApprovers: saved.pendingApprovers ?? prev.pendingApprovers,
          requiredApprovers: saved.requiredApprovers ?? prev.requiredApprovers,
        };
      });
    } else {
      setSelectedMOIForm(nextForm);
    }
    if (!options?.silent) showToast('MOI form saved.');
    return { id: saved.id, jobId, unitNumber };
  };

  const handleMOISubmit = async (data: Record<string, unknown>) => {
    try {
      await saveMoiDraft(data);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to save MOI form.');
    }
  };

  const openMoaFormForJob = async (job: JobRequestResponse, unitNumber?: number) => {
    const activeUnit = unitNumber ?? job.activeUnitNumber;
    const scopedJob = { ...job, activeUnitNumber: activeUnit };
    setSelectedJobRequest(scopedJob);
    try {
      const moaFormId = job.linkedFormKind === 'MOA' ? job.linkedFormId : undefined;
      const form = moaFormId
        ? await getMOAForm(moaFormId)
        : await getMOAFormForJob(job.id, activeUnit);
      setMOIDataForMOA(mapMoaFormResponseToModalState(form, scopedJob));

      const unit = job.units?.find((u) => u.unitNumber === activeUnit);
      const moiFormId = form.moiFormId
        ?? unit?.moiFormId
        ?? resolveLinkedMoiFormId(scopedJob);
      if (moiFormId) {
        try {
          const moi = await getMOIForm(moiFormId);
          setLinkedMoiForMoa(mapMoiFormResponseToModalState(moi, scopedJob));
        } catch {
          setLinkedMoiForMoa(null);
        }
      } else {
        setLinkedMoiForMoa(null);
      }

      setIsMOAFormOpen(true);
      return;
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to open MOA form.');
    }
  };

  const openMoiFormForJob = async (job: JobRequestResponse, viewMode = true) => {
    setSelectedJobRequest(job);
    const unitNumber = job.activeUnitNumber ?? ((job.totalQty ?? 1) > 1 ? undefined : 1);
    const unit = unitNumber != null ? job.units?.find((u) => u.unitNumber === unitNumber) : undefined;
    const formId = resolveLinkedMoiFormId(job) ?? unit?.moiFormId ?? (job.linkedFormKind === 'MOI' ? job.linkedFormId : undefined);

    const resolveMoiEditable = (workflowState?: string) => {
      const state = workflowState ?? job.moiWorkflowState ?? 'Draft';
      const clientCanEdit = canClientEditMoiDraft(job, {
        workflowState: state,
        isClientAdmin: userIsClientAdmin,
        isSignatory: userIsSignatory,
        userName: currentUser?.name,
      });
      const internalCanEdit = userIsInternal && canInternalEditMoi(state);
      return clientCanEdit || internalCanEdit;
    };

    let moiForm: Record<string, unknown> | undefined;
    if (!userIsExternal) {
      moiForm = submittedMOIForms.find((f) =>
        f.id === formId
        || (f.jobId === job.id && (unitNumber == null || f.unitNumber === unitNumber || f.activeUnitNumber === unitNumber)));
    }

    if (!moiForm) {
      try {
        const f = formId
          ? await getMOIForm(formId)
          : unitNumber != null
            ? (await getMOIForms(job.id, unitNumber))[0]
            : undefined;
        if (f) {
          moiForm = mapMoiFormResponseToModalState(f, job);
        }
      } catch (err) {
        showToast(err instanceof ApiError ? err.message : 'Failed to load MOI form.');
        return;
      }
    }

    if (moiForm) {
      const workflowState = String(moiForm.workflowState ?? job.moiWorkflowState ?? 'Draft');
      setSelectedMOIForm({
        ...moiForm,
        workflowState,
        status: job.status,
        service: job.service,
        taskType: job.taskType,
        unitNumber: job.activeUnitNumber ?? ((job.totalQty ?? 1) <= 1 ? 1 : undefined),
        activeUnitNumber: job.activeUnitNumber ?? ((job.totalQty ?? 1) <= 1 ? 1 : undefined),
        totalQty: job.totalQty,
      });
      setIsMOIViewMode(!resolveMoiEditable(workflowState));
    } else {
      setSelectedMOIForm({
        id: formId ?? job.linkedFormId,
        company: job.customer,
        signerName: job.accountHolder,
        signerEmail: job.accountHolderEmail,
        signerPhone: job.accountHolderPhone,
        taskType: job.taskType,
        service: job.service,
        typeOfDocument: job.service,
        requestedBy: job.accountHolder ?? '',
        jobId: job.id,
        unitNumber: job.activeUnitNumber ?? ((job.totalQty ?? 1) <= 1 ? 1 : undefined),
        activeUnitNumber: job.activeUnitNumber ?? ((job.totalQty ?? 1) <= 1 ? 1 : undefined),
        totalQty: job.totalQty,
        workflowState: job.moiWorkflowState ?? 'Draft',
      });
      const editable = resolveMoiEditable(job.moiWorkflowState ?? 'Draft');
      setIsMOIViewMode(!editable);
    }
    setIsMOIFormOpen(true);
  };

  const handleOpenClientMoiForm = (job: JobRequestResponse) => {
    void openMoiFormForJob(job, true);
  };

  const handleOpenClientMoaForm = (job: JobRequestResponse) => {
    void openMoaFormForJob(job, job.activeUnitNumber);
  };

  const jobForUnit = (job: JobRequestResponse, unit?: JobRequestUnitDto): JobRequestResponse =>
    scopeJobForUnit(job, unit);

  const handleViewLinkedMoi = (job?: JobRequestResponse, moiFormId?: number, unitNumber?: number) => {
    const baseJob = job ?? selectedJobRequest;
    if (!baseJob) return;
    const formId = moiFormId
      ?? (linkedMoiForMoa?.id as number | undefined)
      ?? resolveLinkedMoiFormId(baseJob);
    if (!formId) return;
    if (isMOAFormOpen) setMoiStackedOnMoa(true);
    void openMoiFormForJob({
      ...baseJob,
      activeUnitNumber: unitNumber ?? baseJob.activeUnitNumber ?? moaHandoffUnitNumber,
      linkedFormId: formId,
      linkedFormKind: 'MOI',
      hasMoiForm: true,
    }, true);
  };

  const handleCloseMoiForm = () => {
    setIsMOIFormOpen(false);
    setIsMOIViewMode(false);
    setSelectedMOIForm(null);
    if (moiStackedOnMoa) {
      setMoiStackedOnMoa(false);
      return;
    }
    setSelectedJobRequest(null);
  };

  const handleViewLinkedMoiFromTracker = async (jobId: number, unitNumber: number, moiFormId: number) => {
    try {
      const job = await getJobRequest(jobId);
      const unit = job.units?.find((u) => u.unitNumber === unitNumber);
      const scoped = unit ? jobForUnit(job, unit) : { ...job, activeUnitNumber: unitNumber };
      handleViewLinkedMoi(scoped, moiFormId, unitNumber);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to open MOI.');
    }
  };

  const handleOpenFormTask = (job: JobRequestResponse, unit?: JobRequestUnitDto) => {
    const scoped = jobForUnit(job, unit);
    if (shouldOpenMoaForm(job, unit)) {
      void openMoaFormForJob(scoped, unit?.unitNumber);
      return;
    }
    void openMoiFormForJob(scoped, true);
  };

  const saveMoaDraft = async (
    data: Record<string, unknown>,
    options?: { silent?: boolean },
  ): Promise<{ id: number; jobId: number; unitNumber?: number; updatedAt?: string }> => {
    const existingId = (data.id as number | undefined) ?? (moiDataForMOA?.id as number | undefined);
    const jobId = (data.jobId as number | undefined) ?? selectedJobRequest?.id;
    if (!jobId) throw new ApiError('MOA must be linked to a package item before saving.', 400);

    const unitNumber = resolveMoaUnitNumber(
      selectedJobRequest ?? undefined,
      data,
      (data.unitNumber as number | undefined) ?? (moiDataForMOA?.unitNumber as number | undefined),
    );
    if ((selectedJobRequest?.totalQty ?? 1) > 1 && unitNumber == null) {
      throw new ApiError('Select a session before saving this MOA.', 400);
    }

    const company = String(data.company ?? moiDataForMOA?.company ?? '');
    const {
      packChecklist: packFromData,
      id: _id,
      jobId: _jobId,
      unitNumber: _unitNumber,
      activeUnitNumber: _activeUnitNumber,
      updatedAt: _updatedAt,
      workflow: _workflow,
      packValidationErrors: _packErrors,
      submittedForAdminReviewAt: _submitted,
      sharonApprovedAt: _sharon,
      clientApprovals: _clientApprovals,
      rejections: _rejections,
      requiredApprovers: _requiredApprovers,
      pendingApprovers: _pendingApprovers,
      ...formFields
    } = data;

    if (unitNumber != null) {
      formFields.unitNumber = unitNumber;
      formFields.activeUnitNumber = unitNumber;
    }

    let expectedUpdatedAt = (data.updatedAt as string | undefined)
      ?? (moiDataForMOA?.updatedAt as string | undefined);

    const payload = {
      jobId,
      moiFormId: (data.moiFormId as number | undefined) ?? (moiDataForMOA?.moiFormId as number | undefined),
      company,
      formTemplateCode: data.formTemplateCode as string | undefined,
      financeRelated: Boolean(data.financeRelated ?? moiDataForMOA?.financeRelated),
      bankSignatoryMatter: Boolean(data.bankSignatoryMatter ?? moiDataForMOA?.bankSignatoryMatter),
      shareMovement: Boolean(data.shareMovement),
      expectedUpdatedAt,
      data: formFields,
    };

    let formId = existingId;
    if (formId) {
      await updateMOAForm(formId, payload);
      const latestAfterForm = await getMOAForm(formId);
      expectedUpdatedAt = latestAfterForm.updatedAt;
    } else {
      const created = await createMOAForm(payload);
      formId = created.id;
      expectedUpdatedAt = created.updatedAt;
    }

    const pack = (packFromData ?? data.packChecklist) as Record<string, unknown> | undefined;
    const saved = await updateMoaPack(formId, {
      checklist: {
        internalChecklistA: Boolean(pack?.internalChecklistA),
        internalChecklistB: Boolean(pack?.internalChecklistB),
        cleanAgreementAttached: Boolean(pack?.cleanAgreementAttached),
        shareholdingTableAttached: Boolean(pack?.shareholdingTableAttached),
        ssmRegistrationNo: String(pack?.ssmRegistrationNo ?? ''),
        ssmNewRegistrationNo: String(pack?.ssmNewRegistrationNo ?? ''),
        ssmEntityType: String(pack?.ssmEntityType ?? ''),
        ssmStatus: String(pack?.ssmStatus ?? ''),
        ssmAsAtDate: String(pack?.ssmAsAtDate ?? ''),
      },
      financeRelated: payload.financeRelated,
      bankSignatoryMatter: payload.bankSignatoryMatter,
      shareMovement: payload.shareMovement,
      expectedUpdatedAt,
    });

    moaFormDirtyRef.current = false;
    if (unitNumber != null && selectedJobRequest && selectedJobRequest.activeUnitNumber !== unitNumber) {
      setSelectedJobRequest((prev) => prev ? { ...prev, activeUnitNumber: unitNumber } : prev);
    }
    if (options?.silent) {
      if (!existingId && formId) {
        setMOIDataForMOA((prev) => {
          if (!prev) {
            return {
              ...data,
              id: formId,
              jobId,
              unitNumber,
              activeUnitNumber: unitNumber,
              updatedAt: saved.updatedAt,
            };
          }
          return { ...prev, id: formId, updatedAt: saved.updatedAt };
        });
      }
    } else {
      setMOIDataForMOA({
        ...data,
        ...formFields,
        id: formId,
        jobId,
        unitNumber,
        activeUnitNumber: unitNumber,
        workflow: saved.workflow,
        packChecklist: saved.packChecklist,
        packValidationErrors: saved.packValidationErrors,
        submittedForAdminReviewAt: saved.submittedForAdminReviewAt,
        updatedAt: saved.updatedAt,
        company,
      });
      bumpRefresh();
    }
    if (!options?.silent) showToast('MOA form saved.');
    return { id: formId!, jobId, unitNumber, updatedAt: saved.updatedAt };
  };

  const handleMOASubmit = async (data: Record<string, unknown>) => {
    try {
      return await saveMoaDraft(data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        const existingId = (data.id as number | undefined) ?? (moiDataForMOA?.id as number | undefined);
        showToast(`${err.message} Reloading latest draft…`);
        if (existingId) {
          const latest = await getMOAForm(existingId).catch(() => null);
          if (latest && selectedJobRequest) {
            setMOIDataForMOA(mapMoaFormResponseToModalState(latest, selectedJobRequest));
          }
        }
      } else {
        showToast(err instanceof ApiError ? err.message : 'Failed to save MOA form.');
      }
      throw err;
    }
  };

  const handleStartMoaWorkflow = async (
    draft: Record<string, unknown>,
    options?: { moaApprovers?: string[] },
  ) => {
    try {
      const { id } = await saveMoaDraft(draft, { silent: true });
      const saved = await startMoaWorkflow(id, options);
      setMOIDataForMOA((prev) => ({
        ...(prev ?? {}),
        ...draft,
        id,
        workflow: saved.workflow,
        packChecklist: saved.packChecklist,
        packValidationErrors: saved.packValidationErrors,
        updatedAt: saved.updatedAt,
      }));
      showToast('MOA internal routing started.');
      bumpRefresh();
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Cannot start MOA internal routing — complete the pack checklist.');
    }
  };

  const handleRecommendMoi = async (formId: number, comments: string) => {
    try {
      await recommendMoiForm(formId, comments);
      await loadMOIForms();
      showToast('MOI recommended.');
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to recommend MOI.');
    }
  };

  const handleSubmitMoiForApproval = async (formId: number, formData?: Record<string, unknown>) => {
    try {
      let id = formId;
      if (formData) {
        const saved = await saveMoiDraft(formData, { silent: true });
        id = saved.id;
      }
      if (!id) {
        showToast('Save the MOI draft before submitting for approval.');
        return;
      }
      await submitMoiForApproval(id);
      await loadMOIForms();
      bumpRefresh();
      showToast('MOI submitted for approval.');
      setIsMOIFormOpen(false);
      setSelectedMOIForm(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to submit MOI for approval.');
    }
  };

  const handleClientApproveMoi = async (
    formId: number,
    payload: { comments: string; signatureFileName?: string; signatureDataUrl?: string },
  ) => {
    try {
      await clientApproveMoiForm(formId, payload);
      await loadMOIForms();
      bumpRefresh();
      showToast('MOI signed.');
      setIsMOIFormOpen(false);
      setSelectedMOIForm(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to approve MOI.');
    }
  };

  const handleClientApproveMoa = async (
    formId: number,
    payload: { comments: string; signatureFileName?: string; signatureDataUrl?: string },
  ) => {
    try {
      const result = await clientApproveMoaForm(formId, payload);
      bumpRefresh();
      const pendingCount = result.pendingApprovers?.length ?? 0;
      if (pendingCount > 0) {
        showToast(`MOA signed — ${pendingCount} more client signator${pendingCount === 1 ? 'y' : 'ies'} required.`);
      } else {
        showToast('MOA fully signed — package line completed.');
      }
      setIsMOAFormOpen(false);
      setMOIDataForMOA(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to sign off MOA.');
    }
  };

  const handleClientRejectMoi = async (formId: number, reason: string) => {
    try {
      await clientRejectMoiForm(formId, reason);
      await loadMOIForms();
      bumpRefresh();
      showToast('MOI rejected — please revise and resubmit.');
      setIsMOIFormOpen(false);
      setSelectedMOIForm(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to reject MOI.');
    }
  };

  const handleClientRejectMoa = async (formId: number, reason: string) => {
    try {
      await clientRejectMoaForm(formId, reason);
      bumpRefresh();
      showToast('MOA rejected — returned to internal team.');
      setIsMOAFormOpen(false);
      setMOIDataForMOA(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to reject MOA.');
    }
  };

  const handleSharonApproveMoa = async (jobId: number) => {
    try {
      await advanceJobHandoff(jobId, 'sharon-approve-moa', undefined, moaHandoffUnitNumber);
      bumpRefresh();
      showToast('MOA approved — ready to send to client.');
      if (selectedJobRequest?.id === jobId) {
        const updated = await getJobRequest(jobId);
        setSelectedJobRequest(updated);
      }
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'MOA approval failed.');
    }
  };

  const handleSharonRejectMoa = async (jobId: number, reason: string) => {
    try {
      await advanceJobHandoff(jobId, 'reject-moa', reason, moaHandoffUnitNumber);
      bumpRefresh();
      showToast('MOA rejected — returned to secretary.');
      setIsMOAFormOpen(false);
      setMOIDataForMOA(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'MOA rejection failed.');
    }
  };

  const handleSendMoaToClient = async (jobId: number) => {
    try {
      await advanceJobHandoff(jobId, 'approve-for-moa', undefined, moaHandoffUnitNumber);
      bumpRefresh();
      showToast('MOA sent to client for approval.');
      setIsMOAFormOpen(false);
      setMOIDataForMOA(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to send MOA to client.');
    }
  };

  const handleSubmitMoaForAdminReview = async (jobId: number) => {
    try {
      const unitNumber = moaHandoffUnitNumber
        ?? resolveMoaUnitNumber(selectedJobRequest ?? undefined, moiDataForMOA ?? undefined);
      if ((selectedJobRequest?.totalQty ?? 1) > 1 && unitNumber == null) {
        showToast('Select a session before submitting this MOA.');
        return;
      }
      const updated = await advanceJobHandoff(jobId, 'submit-admin-review', undefined, unitNumber);
      setSelectedJobRequest(updated);
      if (moiDataForMOA?.id) {
        const form = await getMOAForm(moiDataForMOA.id as number);
        setMOIDataForMOA((prev) => ({
          ...(prev ?? {}),
          submittedForAdminReviewAt: form.submittedForAdminReviewAt,
        }));
      }
      bumpRefresh();
      showToast('MOA draft submitted for admin approval.');
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to submit MOA for approval.');
    }
  };

  const handleMarkExecutionComplete = async (jobId: number, unitNumber?: number) => {
    try {
      const resolvedUnit = unitNumber
        ?? moaHandoffUnitNumber
        ?? resolveMoaUnitNumber(selectedJobRequest ?? undefined, moiDataForMOA ?? undefined)
        ?? ((selectedJobRequest?.totalQty ?? 1) <= 1 ? 1 : undefined);
      if ((selectedJobRequest?.totalQty ?? 1) > 1 && resolvedUnit == null) {
        showToast('Select a session before marking this line completed.');
        return;
      }
      const updated = await recordJobProgress(jobId, {
        unitNumber: resolvedUnit,
        markUnitComplete: true,
      });
      if (selectedJobRequest?.id === jobId) {
        setSelectedJobRequest(updated);
      }
      bumpRefresh();
      showToast('Package line marked completed.');
      setIsMOAFormOpen(false);
      setMOIDataForMOA(null);
      setLinkedMoiForMoa(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to mark completed.');
    }
  };

  const handleAdminOverrideMoi = async (formId: number, comments: string) => {
    try {
      await adminOverrideMoiForm(formId, comments);
      await loadMOIForms();
      showToast('MOI admin override applied.');
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to override MOI.');
    }
  };

  const resolveIntakeUnitNumber = (
    job: JobRequestResponse | null,
    moiForm: Record<string, unknown> | null | undefined,
  ): number | undefined => {
    if (!job || (job.totalQty ?? 1) <= 1) return undefined;
    const n = job.activeUnitNumber
      ?? (moiForm?.unitNumber as number | undefined)
      ?? (moiForm?.activeUnitNumber as number | undefined);
    return n;
  };

  const isAwaitingMoiIntake = (
    job: JobRequestResponse | null,
    moiForm: Record<string, unknown> | null | undefined,
  ): boolean => {
    if (!job) return false;
    const unitNumber = resolveIntakeUnitNumber(job, moiForm);
    if (unitNumber != null) {
      const unit = job.units?.find((u) => u.unitNumber === unitNumber);
      if (unit?.awaitingIntakeApproval) return true;
    }
    if (job.awaitingIntakeApproval) return true;
    if (job.units?.some((u) => u.awaitingIntakeApproval)) return true;
    const workflowState = String(moiForm?.workflowState ?? job.moiWorkflowState ?? '');
    return workflowState === 'PendingAdminIntake';
  };

  const handleApproveMoiIntake = async () => {
    if (!selectedJobRequest) return;
    try {
      const unitNumber = resolveIntakeUnitNumber(selectedJobRequest, selectedMOIForm);
      await approveMoiIntake(selectedJobRequest.id, unitNumber);
      bumpRefresh();
      await loadMOIForms();
      showToast('MOI signed off — assign secretarial team to start resolution.');
      setIsMOIFormOpen(false);
      setIsMOIViewMode(false);
      setSelectedMOIForm(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to accept MOI.');
      throw err;
    }
  };

  const handleRejectMoiIntake = async (reason: string) => {
    if (!selectedJobRequest) return;
    try {
      const unitNumber = resolveIntakeUnitNumber(selectedJobRequest, selectedMOIForm);
      await rejectMoiIntake(selectedJobRequest.id, reason, unitNumber);
      bumpRefresh();
      await loadMOIForms();
      showToast('MOI rejected — client can revise and resubmit.');
      setIsMOIFormOpen(false);
      setIsMOIViewMode(false);
      setSelectedMOIForm(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to reject MOI.');
      throw err;
    }
  };

  const handleAcceptJob = async (jobId: number, assignedTo: string, comments: string) => {
    try {
      if (selectedJobRequest) {
        await updateJobRequest(jobId, {
          ...selectedJobRequest,
          jobAssignedTo: assignedTo,
          status: 'In Progress',
          assignmentComments: comments,
        });
        bumpRefresh();
        showToast('Job accepted and assigned.');
      }
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to accept job.');
    }
    setIsMOIFormOpen(false);
    setIsMOIViewMode(false);
    setSelectedMOIForm(null);
    setSelectedJobRequest(null);
  };

  const openMOICreate = () => {
    setIsMOIViewMode(false);
    setSelectedMOIForm(null);
    setSelectedJobRequest(null);
    setIsMOIFormOpen(true);
  };

  const openMOACreate = () => {
    setMOIDataForMOA(null);
    setIsMOAFormOpen(true);
  };

  const handleViewJob = (job: JobRequestResponse) => {
    setSelectedJobRequest(job);
    setIsJobDetailsModalOpen(true);
  };

  const handleJobAssignment = async (
    jobId: number,
    userId: number,
    acceptedDate: string,
    comments: string,
    unitNumber?: number,
    remove?: boolean,
  ) => {
    try {
      const updated = await assignJobRequest(jobId, {
        userId,
        unitNumber,
        acceptedDate: acceptedDate || undefined,
        comments: comments || undefined,
        remove,
      });
      if (selectedJobRequest?.id === jobId) {
        setSelectedJobRequest(updated);
      }
      bumpRefresh();
      showToast(remove ? 'User removed from task.' : 'Assignment updated.');
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to update assignment.');
    }
  };

  const handleOpenTrackerTask = async (jobId: number, unitNumber?: number) => {
    try {
      const job = await getJobRequest(jobId);
      const executingUnits = executingUnitsForJob(job);
      const unit = unitNumber != null
        ? job.units?.find((u) => u.unitNumber === unitNumber)
        : executingUnits.length === 1
          ? executingUnits[0]
          : job.units?.find((u) => u.awaitingIntakeApproval)
            ?? (job.units?.length === 1 ? job.units[0] : undefined);
      handleOpenFormTask(job, unit);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to open task.');
    }
  };

  const toFormModalCustomer = (c: CustomerResponse) => ({
    id: c.id,
    company: c.company,
    package: c.packages?.find((p) => p.status === 'Active')?.packageName ?? c.package,
    hasLoa: c.hasLoa,
    moaWorkflowTemplateCode: c.moaWorkflowTemplateCode,
    packageNames: c.packages?.filter((p) => p.status === 'Active').map((p) => p.packageName)
      ?? (c.package ? [c.package] : []),
    accountHolders: (c.accountHolders ?? []).map((h) => ({
      id: h.id,
      name: h.name,
      moi: (c.moi || []).includes(h.name),
      moiApproval: (c.moiApproval || []).includes(h.name),
      moa: (c.moa || []).includes(h.name),
    })),
  });

  const customerOptions = customers.map(toFormModalCustomer);

  const formModalCustomers = useMemo(() => {
    if (selectedJobRequest) {
      const full = customerOptions.find(
        (c) => c.id === selectedJobRequest.customerId || c.company === selectedJobRequest.customer,
      );
      if (full) return [full];
      if (myCompany && (myCompany.id === selectedJobRequest.customerId || myCompany.company === selectedJobRequest.customer)) {
        return [toFormModalCustomer(myCompany)];
      }
      return [{
        id: selectedJobRequest.customerId ?? 0,
        company: selectedJobRequest.customer,
        package: myCompany?.packages?.find((p) => p.status === 'Active')?.packageName ?? myCompany?.package ?? '',
        packageNames: myCompany?.packages?.filter((p) => p.status === 'Active').map((p) => p.packageName)
          ?? (myCompany?.package ? [myCompany.package] : []),
        accountHolders: myCompany
          ? (myCompany.accountHolders ?? []).map((h) => ({
              id: h.id,
              name: h.name,
              moi: (myCompany.moi || []).includes(h.name),
              moiApproval: (myCompany.moiApproval || []).includes(h.name),
              moa: (myCompany.moa || []).includes(h.name),
            }))
          : [{
              id: 0,
              name: selectedJobRequest.accountHolder,
              moi: selectedJobRequest.taskType === 'MOI',
              moiApproval: selectedJobRequest.taskType === 'MOI Approval',
              moa: selectedJobRequest.taskType === 'MOA',
            }],
      }];
    }
    if (userIsExternal && myCompany) return [toFormModalCustomer(myCompany)];
    return customerOptions;
  }, [selectedJobRequest, customerOptions, myCompany, userIsExternal]);

  const productOptions = products.map((p) => ({
    id: p.id,
    packageName: p.packageName,
    services: p.services,
    serviceQuantities: p.serviceQuantities,
  }));

  const toastBanner = toast ? (
    <div className="fixed top-4 right-4 z-[100] px-4 py-3 rounded-lg bg-card border border-border shadow-lg text-sm max-w-sm">
      {toast}
    </div>
  ) : null;

  const modals = (
    <>
      {userIsAdmin && (
        <>
      <CreateCustomerModal
        isOpen={isCreateCustomerModalOpen}
        editMode={isEditCustomerMode}
        initialData={editingCustomer}
        products={products}
        onClose={() => {
          setIsCreateCustomerModalOpen(false);
          setIsEditCustomerMode(false);
          setEditingCustomer(null);
        }}
        onSubmit={handleCustomerSubmit}
      />
      <CreateProductModal
        isOpen={isCreateProductModalOpen}
        onClose={() => {
          setIsCreateProductModalOpen(false);
          setIsEditProductMode(false);
          setSelectedProduct(null);
        }}
        onSubmit={handleProductSubmit}
        editMode={isEditProductMode}
        initialData={selectedProduct}
      />
      <ServiceHistoryModal
        isOpen={isHistoryModalOpen}
        onClose={() => setIsHistoryModalOpen(false)}
      />
        </>
      )}
      {userCanManageTeam && (
        <CreateUserModal
          isOpen={isCreateUserModalOpen}
          customers={customers}
          mode={userIsClientAdmin ? 'clientTeam' : 'internal'}
          fixedCustomerId={currentUser?.customerId}
          onClose={() => setIsCreateUserModalOpen(false)}
          onSubmit={handleUserSubmit}
        />
      )}
      <MOIFormModal
        isOpen={isMOIFormOpen}
        onClose={handleCloseMoiForm}
        elevated={moiStackedOnMoa}
        closeLabel={moiStackedOnMoa ? 'Back to MOA' : undefined}
        onSubmit={handleMOISubmit}
        onSaveDraft={(data) => saveMoiDraft(data, { silent: true })}
        onAccept={userIsAdmin ? handleAcceptJob : undefined}
        onRecommend={handleRecommendMoi}
        onSubmitForApproval={handleSubmitMoiForApproval}
        onClientApprove={handleClientApproveMoi}
        onClientReject={userIsClientAdmin || userIsSignatory ? handleClientRejectMoi : undefined}
        onApproveIntake={(currentUser?.canApproveMoiIntake || currentUser?.canApproveMoi) ? handleApproveMoiIntake : undefined}
        onRejectIntake={currentUser?.canApproveMoiIntake ? handleRejectMoiIntake : undefined}
        canApproveIntake={Boolean(currentUser?.canApproveMoiIntake || currentUser?.canApproveMoi)}
        awaitingIntakeApproval={isAwaitingMoiIntake(selectedJobRequest, selectedMOIForm)}
        onAdminOverride={userIsAdmin ? handleAdminOverrideMoi : undefined}
        isClientUser={userIsClientAdmin || userIsSignatory}
        isMoiApprovalTask={selectedJobRequest?.taskType === 'MOI Approval'}
        currentUserName={currentUser?.name}
        signatoryHolderNames={currentUser?.signatoryHolderNames}
        needsMoiApproval={Boolean(currentUser?.needsMoiApproval)}
        userIsAdmin={userIsAdmin}
        viewMode={isMOIViewMode}
        initialData={selectedMOIForm}
        jobId={selectedJobRequest?.id}
        jobStatus={selectedJobRequest?.status}
        users={apiUsers}
        customers={formModalCustomers}
        products={productOptions}
        serviceUsage={[]}
      />
      <MOAFormModal
        isOpen={isMOAFormOpen}
        onClose={() => {
          setIsMOAFormOpen(false);
          setMOIDataForMOA(null);
          setLinkedMoiForMoa(null);
          setMoiStackedOnMoa(false);
          if (isMOIFormOpen) {
            setIsMOIFormOpen(false);
            setIsMOIViewMode(false);
            setSelectedMOIForm(null);
          }
          setSelectedJobRequest(null);
        }}
        onSubmit={handleMOASubmit}
        onSaveDraft={(data) => saveMoaDraft(data, { silent: true })}
        onDirtyChange={handleMoaDirtyChange}
        onStartWorkflow={SHOW_MOA_SEQUENTIAL_WORKFLOW && moaInternallyEditable ? handleStartMoaWorkflow : undefined}
        onClientApprove={
          (currentUser?.needsMoa || currentUser?.isInternalSignatory || currentUser?.canApproveMoa)
            ? handleClientApproveMoa
            : undefined
        }
        onClientReject={currentUser?.needsMoa ? handleClientRejectMoa : undefined}
        onSharonApprove={userIsAdmin || currentUser?.canApproveMoa ? handleSharonApproveMoa : undefined}
        onSharonReject={userIsAdmin || currentUser?.canApproveMoa ? handleSharonRejectMoa : undefined}
        onSendToClient={userIsAdmin || currentUser?.canApproveMoa ? handleSendMoaToClient : undefined}
        onSubmitForAdminReview={canSubmitMoaForAdmin ? handleSubmitMoaForAdminReview : undefined}
        canSubmitForAdminReview={canSubmitMoaForAdmin}
        canMarkExecutionComplete={canMarkMoaExecutionComplete}
        onMarkExecutionComplete={canMarkMoaExecutionComplete ? handleMarkExecutionComplete : undefined}
        jobHandoffStatus={selectedMoaHandoff}
        moiData={moiDataForMOA}
        initialData={moiDataForMOA}
        viewMode={
          userIsClientAdmin
          || userIsSignatory
          || (SHOW_MOA_SEQUENTIAL_WORKFLOW && Boolean(moiDataForMOA?.workflow) && !userIsInternal)
          || (userIsInternal && !moaInternallyEditable && !currentUser?.isInternalSignatory && !currentUser?.canApproveMoa)
        }
        users={internalDirectoryUsers}
        customers={formModalCustomers}
        userIsAdmin={userIsAdmin}
        canApproveMoa={Boolean(currentUser?.canApproveMoa)}
        isClientUser={userIsClientAdmin || userIsSignatory}
        needsMoa={Boolean(currentUser?.needsMoa)}
        isInternalSignatory={Boolean(currentUser?.isInternalSignatory)}
        currentUserName={currentUser?.name}
        signatoryHolderNames={currentUser?.signatoryHolderNames}
        linkedMoiData={linkedMoiForMoa}
        jobId={selectedJobRequest?.id}
        unitNumber={moaHandoffUnitNumber}
        allowMoaAttachments={moaInternallyEditable || moaExecutingPhase || userIsAdmin || Boolean(currentUser?.canApproveMoa)}
        onViewLinkedMoi={() => handleViewLinkedMoi()}
      />
      <JobRequestDetailsModal
        isOpen={isJobDetailsModalOpen}
        onClose={() => setIsJobDetailsModalOpen(false)}
        jobRequest={selectedJobRequest}
        onAssign={handleJobAssignment}
        users={apiUsers}
        canAssign={userIsAdmin}
      />
    </>
  );

  if (!currentUser) {
    return (
      <div className="size-full bg-background">
        {toastBanner}
        <Login
          onSuccess={() => {
            setCurrentUser(getAuthUser());
            setActiveTab('dashboard');
          }}
        />
      </div>
    );
  }

  if (currentUser.mustChangePassword) {
    return (
      <div className="size-full bg-background">
        <ChangePasswordModal
          userName={currentUser.name}
          onSuccess={() => setCurrentUser(getAuthUser())}
        />
      </div>
    );
  }

  return (
    <div className="size-full bg-background">
      {toastBanner}
      {modals}

      <header className="border-b border-border bg-card">
        <div className="flex items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-primary flex items-center justify-center">
              <span className="text-primary-foreground font-bold">L</span>
            </div>
            <h1>LGB Services</h1>
          </div>
          <div className="flex items-center gap-3">
            <NotificationBell
              refreshKey={refreshKey}
              onOpenJob={(jobId) => void handleOpenTrackerTask(jobId)}
            />
            <span className="text-sm text-muted-foreground">
              {currentUser.name} ({roleLabel(currentUser.role)})
            </span>
            <button
              type="button"
              onClick={handleSignOut}
              className="px-3 py-1.5 text-sm border border-border rounded-lg hover:bg-muted transition-colors"
            >
              Sign out
            </button>
          </div>
        </div>

        <div className="flex gap-1 px-6">
          {appTabs.map((tab) => {
            const Icon = tab.icon;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => navigateToTab(tab.id)}
                className={`flex items-center gap-2 px-4 py-3 border-b-2 transition-colors ${
                  activeTab === tab.id
                    ? 'border-primary text-primary'
                    : 'border-transparent text-muted-foreground hover:text-foreground'
                }`}
              >
                <Icon className="w-4 h-4" />
                {tab.label}
              </button>
            );
          })}
        </div>
      </header>

      <main className="p-6 overflow-auto" style={{ height: 'calc(100% - 129px)' }}>
        {activeTab === 'dashboard' && (
          <div className="space-y-6">
            {(userIsClientAdmin || userIsSignatory) && currentUser ? (
              <ClientPortal
                currentUser={currentUser}
                refreshKey={refreshKey}
                onOpenMoiForm={handleOpenClientMoiForm}
                onOpenMoaForm={handleOpenClientMoaForm}
                mode={userIsSignatory ? 'signatory' : 'admin'}
              />
            ) : userIsAdmin ? (
              selectedPackageWork ? (
                <PackageWorkboard
                  customer={selectedPackageWork.customer}
                  package={selectedPackageWork.package}
                  users={assignableUsers}
                  userIsAdmin={userIsAdmin}
                  canApproveIntake={Boolean(currentUser?.canApproveMoiIntake)}
                  canApproveMoa={Boolean(currentUser?.canApproveMoa)}
                  refreshKey={refreshKey}
                  onBack={() => setSelectedPackageWork(null)}
                  onOpenTask={handleOpenFormTask}
                  onError={showToast}
                  onSuccess={bumpRefresh}
                  onScheduleSaved={() => setScheduleRefreshKey((k) => k + 1)}
                />
              ) : (
                <AdminDashboard
                  refreshKey={refreshKey}
                  currentUser={currentUser}
                  assignableUsers={assignableUsers}
                  secTeamUsers={secTeamUsers}
                  onOpenTask={(jobId) => void handleOpenTrackerTask(jobId)}
                  onViewMoi={(jobId, unitNumber, moiFormId) => void handleViewLinkedMoiFromTracker(jobId, unitNumber, moiFormId)}
                  onViewHistory={() => setIsHistoryModalOpen(true)}
                  onError={showToast}
                  onSuccess={handleActionSuccess}
                />
              )
            ) : (
              <>
                <MyWorkTracker
                  refreshKey={refreshKey}
                  userId={currentUser?.userId}
                  canApproveMoa={Boolean(currentUser?.canApproveMoa)}
                  isAdmin={currentUser?.role === 'Admin'}
                  onMarkExecutionComplete={
                    (currentUser?.canApproveMoa || currentUser?.role === 'Admin')
                      ? handleMarkExecutionComplete
                      : undefined
                  }
                  onOpenTask={(jobId, _taskType, unitNumber) => void handleOpenTrackerTask(jobId, unitNumber)}
                  onViewMoi={(jobId, unitNumber, moiFormId) => void handleViewLinkedMoiFromTracker(jobId, unitNumber, moiFormId)}
                  onError={showToast}
                  onSuccess={handleActionSuccess}
                />
                <JobRequestsTable
                  refreshKey={refreshKey}
                  userId={currentUser?.userId}
                  onViewJob={handleViewJob}
                  onOpenTask={handleOpenFormTask}
                  onActionError={showToast}
                  onActionSuccess={bumpRefresh}
                  isAdmin={false}
                />
                <CompletedServicesTable refreshKey={refreshKey} />
              </>
            )}
          </div>
        )}

        {activeTab === 'packages' && userIsClientAdmin && (
          <ClientPackages refreshKey={refreshKey} />
        )}

        {activeTab === 'team' && userIsClientAdmin && (
          <UserManagement
            mode="clientTeam"
            title="Team & signatories"
            description="Invite client admins or add signatories who can complete MOI/MOA forms assigned to them. Signatories cannot manage the portal."
            refreshKey={userRefreshKey}
            onCreateUser={() => setIsCreateUserModalOpen(true)}
          />
        )}

        {activeTab === 'customers' && userIsAdmin && (
          selectedPackageWork ? (
            <PackageWorkboard
              customer={selectedPackageWork.customer}
              package={selectedPackageWork.package}
              users={apiUsers}
              userIsAdmin={userIsAdmin}
              canApproveIntake={Boolean(currentUser?.canApproveMoiIntake)}
              canApproveMoa={Boolean(currentUser?.canApproveMoa)}
              refreshKey={refreshKey}
              onBack={() => setSelectedPackageWork(null)}
              onOpenTask={handleOpenFormTask}
              onError={showToast}
              onSuccess={bumpRefresh}
              onScheduleSaved={() => setScheduleRefreshKey((k) => k + 1)}
            />
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <div className="lg:col-span-2">
                <CustomerTable
                  customers={customers}
                  onSelectCustomer={setSelectedCustomer}
                  selectedCustomer={selectedCustomer}
                  onCreateNew={() => {
                    setIsEditCustomerMode(false);
                    setEditingCustomer(null);
                    setIsCreateCustomerModalOpen(true);
                  }}
                />
              </div>
              <div>
                {selectedCustomer ? (
                  <CustomerDetails
                    customer={selectedCustomer}
                    products={products}
                    onClose={() => setSelectedCustomer(null)}
                    onEdit={handleEditCustomer}
                    onDelete={handleDeleteCustomer}
                    onOpenTracking={(c) => {
                      setSelectedCustomer(c);
                      setActiveTab('tracking');
                    }}
                    onOpenPackageWork={(customer, pkg) => {
                      setSelectedPackageWork({ customer, package: pkg });
                    }}
                  />
                ) : (
                  <div className="bg-card rounded-lg border border-border h-full flex items-center justify-center text-muted-foreground">
                    Select a customer to view details
                  </div>
                )}
              </div>
            </div>
          )
        )}

        {activeTab === 'tracking' && userIsAdmin && (
          <PackageTracking
            customers={customers}
            refreshKey={refreshKey + scheduleRefreshKey}
            onError={showToast}
            initialCustomerId={selectedCustomer?.id}
          />
        )}

        {activeTab === 'products' && userIsAdmin && (
          <ProductsTable
            refreshKey={refreshKey}
            onCreateNew={() => {
              setIsEditProductMode(false);
              setSelectedProduct(null);
              setIsCreateProductModalOpen(true);
            }}
            onEdit={(product) => {
              setIsEditProductMode(true);
              setSelectedProduct(product);
              setIsCreateProductModalOpen(true);
            }}
            onDelete={() => {
              bumpRefresh();
              showToast('Product deleted.');
            }}
          />
        )}

        {activeTab === 'admin' && userIsAdmin && (
          <div className="space-y-6">
            <UserManagement
              refreshKey={userRefreshKey}
              onCreateUser={() => setIsCreateUserModalOpen(true)}
            />
            <BillingPartiesAdmin />
            <AdminSignatoryDedup refreshKey={refreshKey} />
            <AdminWorkflowConfig refreshKey={refreshKey} />
            <AdminFormTemplates refreshKey={refreshKey} />
          </div>
        )}
      </main>
    </div>
  );
}
