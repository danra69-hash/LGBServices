import { useCallback, useEffect, useState } from 'react';
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
import { AdminWorkflowConfig } from './components/AdminWorkflowConfig';
import { BillingPartiesAdmin } from './components/BillingPartiesAdmin';
import { AdminFormTemplates } from './components/AdminFormTemplates';
import { AdminDashboard } from './components/AdminDashboard';
import { ClientPortal } from './components/ClientPortal';
import { ClientPackages } from './components/ClientPackages';
import { CreateUserModal } from './components/CreateUserModal';
import { FormsManagement } from './components/FormsManagement';
import { CreateFormModal } from './components/CreateFormModal';
import { MOIFormModal } from './components/MOIFormModal';
import { MOAFormModal } from './components/MOAFormModal';
import { Login } from './components/Login';
import { ChangePasswordModal } from './components/ChangePasswordModal';
import { PackageTracking } from './components/PackageTracking';
import { PackageWorkboard } from './components/PackageWorkboard';
import { MyWorkTracker } from './components/MyWorkTracker';
import { buildCustomerAddOnLines } from './lib/packagePricing';
import { canOpenMoaForJob, canSignatoryStartMoi } from './lib/packageItemStatus';
import {
  ApiError,
  assignJobRequest,
  clearAuth,
  createCustomer,
  deleteCustomer,
  adminOverrideMoiForm,
  advanceJobHandoff,
  clientApproveMoaForm,
  clientApproveMoiForm,
  clientRejectMoaForm,
  clientRejectMoiForm,
  createMOAForm,
  createMOIForm,
  getMOAForm,
  getMOIForm,
  startMoaWorkflow,
  submitMoiForApproval,
  updateMOAForm,
  updateMoaPack,
  recommendMoiForm,
  createProduct,
  createUser,
  getCustomers,
  getJobRequest,
  getMOIForms,
  getMyCompany,
  getProducts,
  getAuthUser,
  getUsers,
  isAdmin,
  canManageUsers,
  isClientAdmin,
  isClientSignatory,
  isExternalUser,
  isInternalStaff,
  roleLabel,
  setAuthExpiredHandler,
  updateCustomer,
  updateJobRequest,
  updateMOIForm,
  updateProduct,
  type CustomerPackageDto,
  type CustomerResponse,
  type JobRequestResponse,
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
  const [apiUsers, setApiUsers] = useState<{ id: number; name: string }[]>([]);
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
  const [isCreateFormModalOpen, setIsCreateFormModalOpen] = useState(false);
  const [isMOIFormOpen, setIsMOIFormOpen] = useState(false);
  const [isMOAFormOpen, setIsMOAFormOpen] = useState(false);
  const [isJobDetailsModalOpen, setIsJobDetailsModalOpen] = useState(false);
  const [selectedJobRequest, setSelectedJobRequest] = useState<JobRequestResponse | null>(null);
  const [moiDataForMOA, setMOIDataForMOA] = useState<Record<string, unknown> | null>(null);
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

  const loadCustomers = useCallback(async () => {
    try {
      const data = await getCustomers();
      setCustomers(data);
    } catch {
      setCustomers([]);
    }
  }, []);

  const loadProducts = useCallback(async () => {
    try {
      const data = await getProducts();
      setProducts([...data].sort((a, b) => (a.packagePrice ?? 0) - (b.packagePrice ?? 0)));
    } catch {
      setProducts([]);
    }
  }, []);

  const loadMyCompany = useCallback(async () => {
    try {
      const data = await getMyCompany();
      setMyCompany(data);
    } catch {
      setMyCompany(null);
    }
  }, []);

  const loadUsers = useCallback(async () => {
    try {
      const data = await getUsers();
      setApiUsers(data.map((u) => ({ id: u.userId, name: u.name })));
    } catch {
      setApiUsers([]);
    }
  }, []);

  const loadMOIForms = useCallback(async () => {
    try {
      const forms = await getMOIForms();
      setSubmittedMOIForms(forms.map((f) => ({
        ...f.data,
        id: f.id,
        jobId: f.jobId,
        workflowState: f.workflowState,
        clientApprovals: f.clientApprovals,
        requiredApprovers: f.requiredApprovers,
        pendingApprovers: f.pendingApprovers,
      })));
    } catch {
      setSubmittedMOIForms([]);
    }
  }, []);

  const userIsAdmin = isAdmin(currentUser);
  const userIsExternal = isExternalUser(currentUser);
  const userIsClientAdmin = isClientAdmin(currentUser);
  const userIsSignatory = isClientSignatory(currentUser);
  const userIsInternal = isInternalStaff(currentUser);
  const userCanManageTeam = canManageUsers(currentUser);

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
    }
    if (userIsAdmin && activeTab === 'dashboard') {
      loadMOIForms();
    }
    if (userIsExternal && activeTab === 'dashboard') {
      void loadMyCompany();
      loadMOIForms();
      loadProducts();
    }
  }, [currentUser, userIsAdmin, userIsClientAdmin, userIsInternal, userIsExternal, activeTab, refreshKey, loadCustomers, loadProducts, loadUsers, loadMOIForms, loadMyCompany]);

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

  const appTabs = userIsAdmin
    ? [
        { id: 'dashboard' as const, label: 'Operations', icon: LayoutDashboard },
        { id: 'customers' as const, label: 'Customers', icon: Users },
        { id: 'products' as const, label: 'Products', icon: Package },
        { id: 'tracking' as const, label: 'Tracking', icon: CalendarDays },
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

  const handleMOISubmit = async (data: Record<string, unknown>) => {
    try {
      const payload = {
        jobId: selectedJobRequest?.id ?? (data.jobId as number | undefined),
        company: String(data.company ?? ''),
        formTemplateCode: data.formTemplateCode as string | undefined,
        financeRelated: Boolean(data.financeRelated),
        bankSignatoryMatter: Boolean(data.bankSignatoryMatter),
        data,
      };
      let formId = selectedMOIForm?.id as number | undefined;
      if (formId) {
        await updateMOIForm(formId, payload);
      } else {
        const created = await createMOIForm(payload);
        formId = created.id;
      }
      const saved = formId ? await getMOIForm(formId) : null;
      await loadMOIForms();
      bumpRefresh();
      if (saved && selectedJobRequest) {
        setSelectedMOIForm({
          ...saved.data,
          id: saved.id,
          jobId: saved.jobId ?? selectedJobRequest.id,
          workflowState: saved.workflowState,
          clientApprovals: saved.clientApprovals,
          requiredApprovers: saved.requiredApprovers,
          pendingApprovers: saved.pendingApprovers,
          status: selectedJobRequest.status,
          service: selectedJobRequest.service,
          typeOfDocument: selectedJobRequest.service,
          taskType: selectedJobRequest.taskType,
        });
      }
      showToast('MOI form saved.');
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to save MOI form.');
    }
  };

  const openMoaFormForJob = async (job: JobRequestResponse) => {
    setSelectedJobRequest(job);
    try {
      if (job.linkedFormId) {
        const form = await getMOAForm(job.linkedFormId);
        setMOIDataForMOA({
          ...form.data,
          id: form.id,
          jobId: form.jobId ?? job.id,
          moiFormId: form.moiFormId,
          company: form.company,
          financeRelated: form.financeRelated,
          bankSignatoryMatter: form.bankSignatoryMatter,
          shareMovement: form.shareMovement,
          packChecklist: form.packChecklist,
          packValidationErrors: form.packValidationErrors,
          workflow: form.workflow,
          clientApprovals: form.clientApprovals,
          rejections: form.rejections,
          requiredApprovers: form.requiredApprovers,
          pendingApprovers: form.pendingApprovers,
          sharonApprovedAt: form.sharonApprovedAt,
        });
        setIsMOAFormOpen(true);
        return;
      }
    } catch {
      // fall through to empty shell
    }
    setMOIDataForMOA({
      id: job.linkedFormId,
      company: job.customer,
      signerName: job.accountHolder,
      signerEmail: job.accountHolderEmail,
      signerPhone: job.accountHolderPhone,
    });
    setIsMOAFormOpen(true);
  };

  const openMoiFormForJob = async (job: JobRequestResponse, viewMode = true) => {
    setSelectedJobRequest(job);
    let moiForm = submittedMOIForms.find((f) => f.id === job.linkedFormId || f.jobId === job.id);
    if (!moiForm) {
      try {
        const f = job.linkedFormId
          ? await getMOIForm(job.linkedFormId)
          : (await getMOIForms(job.id, job.activeUnitNumber))[0];
        if (f) {
          moiForm = {
            ...f.data,
            id: f.id,
            jobId: f.jobId ?? job.id,
            workflowState: f.workflowState,
            clientApprovals: f.clientApprovals,
            requiredApprovers: f.requiredApprovers,
            pendingApprovers: f.pendingApprovers,
          };
        }
      } catch {
        // fall through to empty shell
      }
    }
    if (moiForm) {
      setSelectedMOIForm({
        ...moiForm,
        status: job.status,
        service: job.service,
        typeOfDocument: job.service,
        taskType: job.taskType,
      });
      const clientCanEdit = moiForm.workflowState === 'Draft'
        && job.taskType !== 'MOI Approval'
        && (userIsClientAdmin || (userIsSignatory && canSignatoryStartMoi(job, currentUser!)));
      setIsMOIViewMode(!clientCanEdit);
    } else {
      setSelectedMOIForm({
        id: job.linkedFormId,
        company: job.customer,
        signerName: job.accountHolder,
        signerEmail: job.accountHolderEmail,
        signerPhone: job.accountHolderPhone,
        taskType: job.taskType,
        service: job.service,
        typeOfDocument: job.service,
        jobId: job.id,
      });
      const clientCanEdit = job.taskType !== 'MOI Approval'
        && (userIsClientAdmin || (userIsSignatory && canSignatoryStartMoi(job, currentUser!)));
      setIsMOIViewMode(Boolean(job.linkedFormId) && viewMode && !clientCanEdit);
    }
    setIsMOIFormOpen(true);
  };

  const handleOpenClientForm = (job: JobRequestResponse) => {
    if (canOpenMoaForJob(job)) {
      void openMoaFormForJob(job);
      return;
    }
    void openMoiFormForJob(job, true);
  };

  const handleOpenFormTask = (job: JobRequestResponse) => {
    if (canOpenMoaForJob(job)) {
      void openMoaFormForJob(job);
      return;
    }
    void openMoiFormForJob(job, true);
  };

  const handleConvertToMOA = (moiData: Record<string, unknown>) => {
    setMOIDataForMOA(moiData);
    setIsMOIFormOpen(false);
    setIsMOIViewMode(false);
    setSelectedMOIForm(null);
    setSelectedJobRequest(null);
    setIsMOAFormOpen(true);
  };

  const handleMOASubmit = async (data: Record<string, unknown>) => {
    try {
      const existingId = (data.id as number | undefined) ?? (moiDataForMOA?.id as number | undefined);
      const company = String(data.company ?? moiDataForMOA?.company ?? '');
      const payload = {
        jobId: (data.jobId as number | undefined) ?? selectedJobRequest?.id,
        moiFormId: (data.moiFormId as number | undefined) ?? (moiDataForMOA?.moiFormId as number | undefined),
        company,
        formTemplateCode: data.formTemplateCode as string | undefined,
        financeRelated: Boolean(data.financeRelated ?? moiDataForMOA?.financeRelated),
        bankSignatoryMatter: Boolean(data.bankSignatoryMatter ?? moiDataForMOA?.bankSignatoryMatter),
        shareMovement: Boolean(data.shareMovement),
        data,
      };

      let formId = existingId;
      if (formId) {
        await updateMOAForm(formId, payload);
      } else {
        const saved = await createMOAForm(payload);
        formId = saved.id;
      }

      const pack = data.packChecklist as Record<string, unknown> | undefined;
      let saved = formId
        ? await updateMoaPack(formId, {
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
          })
        : null;

      if (!saved && formId) saved = await getMOAForm(formId);
      setMOIDataForMOA({
        ...data,
        id: formId,
        workflow: saved?.workflow,
        packChecklist: saved?.packChecklist,
        packValidationErrors: saved?.packValidationErrors,
        company,
      });
      showToast('MOA form saved.');
      bumpRefresh();
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to save MOA form.');
    }
  };

  const handleStartMoaWorkflow = async (moaFormId: number) => {
    try {
      const saved = await startMoaWorkflow(moaFormId);
      setMOIDataForMOA((prev) => ({
        ...(prev ?? {}),
        id: moaFormId,
        workflow: saved.workflow,
        packValidationErrors: saved.packValidationErrors,
      }));
      showToast('MOA circulation started.');
      bumpRefresh();
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Cannot start MOA workflow — complete the pack checklist.');
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

  const handleSubmitMoiForApproval = async (formId: number) => {
    try {
      await submitMoiForApproval(formId);
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
      await clientApproveMoaForm(formId, payload);
      bumpRefresh();
      showToast('MOA approved — returned to LGB for execution.');
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
      showToast('MOI rejected — returned to draft for revision.');
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
      await advanceJobHandoff(jobId, 'sharon-approve-moa');
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
      await advanceJobHandoff(jobId, 'reject-moa', reason);
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
      await advanceJobHandoff(jobId, 'approve-for-moa');
      bumpRefresh();
      showToast('MOA sent to client for approval.');
      setIsMOAFormOpen(false);
      setMOIDataForMOA(null);
      setSelectedJobRequest(null);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to send MOA to client.');
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

  const handleOpenTrackerTask = async (jobId: number) => {
    try {
      const job = await getJobRequest(jobId);
      handleOpenFormTask(job);
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to open task.');
    }
  };

  const toFormModalCustomer = (c: CustomerResponse) => ({
    id: c.id,
    company: c.company,
    package: c.packages?.find((p) => p.status === 'Active')?.packageName ?? c.package,
    packageNames: c.packages?.filter((p) => p.status === 'Active').map((p) => p.packageName)
      ?? (c.package ? [c.package] : []),
    accountHolders: c.accountHolders.map((h) => ({
      id: h.id,
      name: h.name,
      moi: (c.moi || []).includes(h.name),
      moiApproval: (c.moiApproval || []).includes(h.name),
      moa: (c.moa || []).includes(h.name),
    })),
  });

  const customerOptions = customers.map(toFormModalCustomer);

  const formModalCustomers = (() => {
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
          ? myCompany.accountHolders.map((h) => ({
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
  })();

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
      <CreateFormModal
        isOpen={isCreateFormModalOpen}
        onClose={() => setIsCreateFormModalOpen(false)}
        onSubmit={() => showToast('Form template saved locally.')}
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
        onClose={() => {
          setIsMOIFormOpen(false);
          setIsMOIViewMode(false);
          setSelectedMOIForm(null);
          setSelectedJobRequest(null);
        }}
        onSubmit={handleMOISubmit}
        onConvertToMOA={handleConvertToMOA}
        onAccept={userIsAdmin ? handleAcceptJob : undefined}
        onRecommend={handleRecommendMoi}
        onSubmitForApproval={handleSubmitMoiForApproval}
        onClientApprove={handleClientApproveMoi}
        onClientReject={userIsClientAdmin || userIsSignatory ? handleClientRejectMoi : undefined}
        onAdminOverride={userIsAdmin ? handleAdminOverrideMoi : undefined}
        isClientUser={userIsClientAdmin || userIsSignatory}
        isMoiApprovalTask={selectedJobRequest?.taskType === 'MOI Approval'}
        currentUserName={currentUser?.name}
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
        }}
        onSubmit={handleMOASubmit}
        onStartWorkflow={userIsInternal ? handleStartMoaWorkflow : undefined}
        onClientApprove={userIsClientAdmin || userIsSignatory ? handleClientApproveMoa : undefined}
        onClientReject={userIsClientAdmin || userIsSignatory ? handleClientRejectMoa : undefined}
        onSharonApprove={userIsAdmin || currentUser?.canApproveMoa ? handleSharonApproveMoa : undefined}
        onSharonReject={userIsAdmin || currentUser?.canApproveMoa ? handleSharonRejectMoa : undefined}
        onSendToClient={userIsAdmin || currentUser?.canApproveMoa ? handleSendMoaToClient : undefined}
        jobHandoffStatus={selectedJobRequest?.internalHandoffStatus ?? ''}
        moiData={moiDataForMOA}
        initialData={moiDataForMOA}
        viewMode={
          (Boolean(moiDataForMOA?.workflow) && !userIsInternal)
          || userIsClientAdmin
          || userIsSignatory
          || (userIsInternal && Boolean(moiDataForMOA?.sharonApprovedAt))
        }
        users={apiUsers}
        customers={formModalCustomers}
        userIsAdmin={userIsAdmin}
        canApproveMoa={Boolean(currentUser?.canApproveMoa)}
        isClientUser={userIsClientAdmin || userIsSignatory}
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
                onOpenForm={handleOpenClientForm}
                mode={userIsSignatory ? 'signatory' : 'admin'}
              />
            ) : userIsAdmin ? (
              selectedPackageWork ? (
                <PackageWorkboard
                  customer={selectedPackageWork.customer}
                  package={selectedPackageWork.package}
                  users={apiUsers}
                  userIsAdmin={userIsAdmin}
                  canApproveIntake={Boolean(currentUser?.canApproveMoiIntake)}
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
                  onManagePackage={(customer, pkg) => setSelectedPackageWork({ customer, package: pkg })}
                  onOpenTask={(jobId) => void handleOpenTrackerTask(jobId)}
                  onViewHistory={() => setIsHistoryModalOpen(true)}
                  onError={showToast}
                  onSuccess={bumpRefresh}
                />
              )
            ) : (
              <>
                <MyWorkTracker
                  refreshKey={refreshKey}
                  onOpenTask={(jobId) => void handleOpenTrackerTask(jobId)}
                  onError={showToast}
                  onSuccess={bumpRefresh}
                />
                <JobRequestsTable
                  refreshKey={refreshKey}
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
            <AdminWorkflowConfig refreshKey={refreshKey} />
            <AdminFormTemplates refreshKey={refreshKey} />
            <FormsManagement onViewMOI={openMOICreate} onViewMOA={openMOACreate} />
          </div>
        )}
      </main>
    </div>
  );
}
