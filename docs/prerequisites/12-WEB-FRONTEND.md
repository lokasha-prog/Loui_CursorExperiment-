# 12 — Web Frontend

> React 18 + Vite + TypeScript + shadcn/ui + Tailwind. Architecture, routing, state, i18n RTL, and component patterns.

---

## 1. Frontend Stack

| Layer | Technology | Why |
|---|---|---|
| Framework | React 18.3 | Industry standard, AI-friendly, ecosystem |
| Build tool | Vite 5.4 | Fast dev server, modern build |
| Language | TypeScript 5.5 | Type safety, refactor confidence |
| UI primitives | shadcn/ui + Radix UI | Copy-paste components, accessible, customizable |
| Styling | Tailwind CSS 3.4 | Utility-first, consistent, RTL-ready via logical properties |
| State (server) | TanStack Query v5 | Server state, caching, mutations |
| State (client) | Zustand | Lightweight, ergonomic, no boilerplate |
| Forms | React Hook Form + Zod | Performant, type-safe validation |
| Routing | React Router v6.4+ | Mature, type-safe with data loaders |
| i18n | react-i18next + i18next | Mature, RTL-ready, lazy loading |
| Charts | Recharts + Visx | Recharts for standard, Visx for custom |
| PDF (client) | @react-pdf/renderer | Parent-facing PDFs (daily report, invoice) |
| Icons | Lucide React | Clean, consistent, tree-shakeable |
| Dates | date-fns + date-fns-tz | Tree-shakeable, immutable, timezone-aware |
| API client | Axios + openapi-typescript-codegen | Generated from OpenAPI spec |
| Real-time | @microsoft/signalr | Native SignalR client |
| Auth | keycloak-js | Keycloak JS adapter |
| Testing | Vitest + React Testing Library + Playwright | Unit + component + E2E |
| Linting | ESLint + Prettier | Consistency |
| Bundle analysis | vite-bundle-visualizer | Size monitoring |

---

## 2. Project Structure

```
web/
├── public/
│   ├── locales/
│   │   ├── en/translation.json
│   │   └── ar/translation.json
│   └── favicon.ico
├── src/
│   ├── main.tsx                  # App entry, providers
│   ├── App.tsx                   # Root component, router
│   ├── router/
│   │   ├── index.tsx             # Router setup
│   │   ├── routes.tsx            # Route definitions
│   │   ├── AuthGuard.tsx         # Protected route guard
│   │   └── RoleGuard.tsx         # Role-based guard
│   ├── api/                      # API client (generated from OpenAPI)
│   │   ├── client.ts             # Axios instance
│   │   ├── hooks/                # TanStack Query hooks per endpoint
│   │   │   ├── useStudents.ts
│   │   │   ├── useAttendance.ts
│   │   │   └── ...
│   │   └── types/                # Generated TS types
│   ├── modules/                  # Feature folders (one per business module)
│   │   ├── dashboard/
│   │   │   ├── components/
│   │   │   ├── hooks/
│   │   │   ├── pages/
│   │   │   └── index.ts
│   │   ├── centre-management/
│   │   ├── attendance/
│   │   ├── parent-communication/
│   │   ├── messaging/
│   │   ├── billing/
│   │   ├── enquiry/
│   │   ├── tours/
│   │   ├── calendar/
│   │   ├── forms/
│   │   ├── tools/
│   │   ├── reports/
│   │   ├── settings/
│   │   ├── learning/             # Phase 2
│   │   └── payments/             # Phase 3
│   ├── components/               # Shared components
│   │   ├── ui/                   # shadcn/ui primitives (Button, Input, etc.)
│   │   ├── layout/
│   │   │   ├── AppShell.tsx      # Main app layout (sidebar + header + content)
│   │   │   ├── Sidebar.tsx
│   │   │   ├── Header.tsx
│   │   │   └── Breadcrumbs.tsx
│   │   ├── shared/
│   │   │   ├── EntityTable.tsx   # Generic data table with pagination
│   │   │   ├── FormDialog.tsx    # Modal with form
│   │   │   ├── ConfirmDialog.tsx
│   │   │   ├── EmptyState.tsx
│   │   │   ├── ErrorState.tsx
│   │   │   ├── LoadingState.tsx
│   │   │   ├── LanguageSwitcher.tsx
│   │   │   ├── NotificationBell.tsx
│   │   │   └── ...
│   │   └── charts/
│   ├── hooks/
│   │   ├── useAuth.ts
│   │   ├── useTenant.ts
│   │   ├── useSignalR.ts
│   │   ├── useDebounce.ts
│   │   └── ...
│   ├── stores/
│   │   ├── authStore.ts          # Zustand: current user, tokens
│   │   ├── uiStore.ts            # Zustand: sidebar, theme, locale
│   │   └── notificationStore.ts  # Zustand: unread count, banner
│   ├── lib/
│   │   ├── i18n.ts               # i18next setup
│   │   ├── api-client.ts         # Axios setup with interceptors
│   │   ├── auth.ts               # Keycloak JS adapter wrapper
│   │   ├── signalr.ts            # SignalR connection manager
│   │   ├── query-client.ts       # TanStack Query client
│   │   ├── utils.ts              # cn(), formatters, etc.
│   │   └── constants.ts
│   ├── types/                    # Shared TS types
│   └── styles/
│       └── globals.css           # Tailwind directives + theme variables
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.ts
├── postcss.config.js
├── .eslintrc.cjs
├── .prettierrc
└── index.html
```

---

## 3. Routing & Layout

### 3.1 Route Structure

```
/                           → redirect based on role
/login                      → public
/forgot-password            → public
/reset-password             → public
/accept-invitation          → public

/dashboard                  → Centre Admin, Teacher, Receptionist (auth required)
/students                   → list (staff)
/students/:id               → detail (staff)
/students/new               → create (Centre Admin)
/students/:id/edit          → edit (Centre Admin)

/attendance                 → daily attendance (staff)
/attendance/monthly         → monthly report (Centre Admin)
/attendance/forecast        → forecast (staff)
/attendance/leaves          → leave requests (staff)

/activities                 → activity feed (staff)
/activities/new             → create post (staff)
/activities/pending         → pending approval (Centre Admin)
/activities/daily-reports   → daily report list (staff)

/messaging                  → inbox (staff + parent)
/messaging/:threadId        → thread view

/billing                    → billing dashboard (Centre Admin, Accounts)
/billing/invoices           → invoice list
/billing/invoices/:id       → invoice detail
/billing/fee-plans          → fee plan management
/billing/expenses           → expenses

/enquiries                  → enquiry pipeline (staff)
/enquiries/:id              → enquiry detail
/tours                      → tour calendar (staff)

/calendar                   → school calendar (staff)
/calendar/admin             → admin calendar
/calendar/staff/:staffId    → staff calendar

/forms                      → form builder (Centre Admin)
/forms/:id/submissions      → form submissions
/admissions                 → admissions processes

/tools/id-cards             → ID card generator
/tools/certificates         → certificate generator

/reports                    → reports list

/settings                   → settings (Super Admin, Centre Admin)
/settings/audit-log         → audit log viewer (Super Admin)
/settings/users             → user management
/settings/roles             → role management

/parent/feed                → parent activity feed (parent role)
/parent/messaging           → parent messaging
/parent/daily-report/:date  → parent daily report
/parent/invoices            → parent invoices (Phase 3 payment)
/parent/profile             → parent profile + settings

/profile                    → own profile (any role)
```

### 3.2 Route Guards

```tsx
// router/AuthGuard.tsx
export function AuthGuard({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();
  
  if (isLoading) return <FullScreenLoader />;
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

// router/RoleGuard.tsx
export function RoleGuard({ 
  children, 
  allowedRoles 
}: { 
  children: React.ReactNode;
  allowedRoles: string[];
}) {
  const { user } = useAuth();
  
  if (!user || !allowedRoles.some(role => user.roles.includes(role))) {
    return <Navigate to="/forbidden" replace />;
  }
  return <>{children}</>;
}

// Usage in routes.tsx
<Route
  path="/billing"
  element={
    <AuthGuard>
      <RoleGuard allowedRoles={['super_admin', 'centre_admin', 'accounts']}>
        <BillingPage />
      </RoleGuard>
    </AuthGuard>
  }
/>
```

### 3.3 App Shell

```tsx
// components/layout/AppShell.tsx
export function AppShell({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const { sidebarCollapsed } = useUiStore();
  const { dir } = useTranslation();
  
  return (
    <div className="flex h-screen overflow-hidden bg-background" dir={dir}>
      <Sidebar collapsed={sidebarCollapsed} />
      <div className="flex flex-1 flex-col overflow-hidden">
        <Header />
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
    </div>
  );
}
```

### 3.4 Sidebar

Role-aware navigation. Different roles see different menu items:

```tsx
// components/layout/Sidebar.tsx
const navItems = [
  { label: 'nav.dashboard', icon: LayoutDashboard, path: '/dashboard', roles: ['super_admin', 'centre_admin', 'teacher', 'receptionist', 'accounts', 'it', 'marketing'] },
  { label: 'nav.students', icon: Users, path: '/students', roles: ['super_admin', 'centre_admin', 'teacher', 'receptionist', 'it'] },
  { label: 'nav.attendance', icon: ClipboardCheck, path: '/attendance', roles: ['super_admin', 'centre_admin', 'teacher', 'receptionist', 'it'] },
  { label: 'nav.activities', icon: Image, path: '/activities', roles: ['super_admin', 'centre_admin', 'teacher', 'receptionist', 'it'] },
  { label: 'nav.messaging', icon: MessageSquare, path: '/messaging', roles: ['super_admin', 'centre_admin', 'teacher', 'receptionist', 'it', 'marketing'] },
  { label: 'nav.billing', icon: DollarSign, path: '/billing', roles: ['super_admin', 'centre_admin', 'accounts', 'it'] },
  { label: 'nav.enquiries', icon: UserPlus, path: '/enquiries', roles: ['super_admin', 'centre_admin', 'receptionist', 'it', 'marketing'] },
  { label: 'nav.tours', icon: Calendar, path: '/tours', roles: ['super_admin', 'centre_admin', 'receptionist', 'it'] },
  { label: 'nav.calendar', icon: Calendar, path: '/calendar', roles: ['super_admin', 'centre_admin', 'teacher', 'receptionist', 'it', 'marketing'] },
  { label: 'nav.forms', icon: FileText, path: '/forms', roles: ['super_admin', 'centre_admin', 'receptionist', 'it'] },
  { label: 'nav.tools', icon: Wrench, path: '/tools', roles: ['super_admin', 'centre_admin', 'it'] },
  { label: 'nav.reports', icon: BarChart3, path: '/reports', roles: ['super_admin', 'centre_admin', 'receptionist', 'accounts', 'it', 'marketing'] },
  { label: 'nav.settings', icon: Settings, path: '/settings', roles: ['super_admin', 'centre_admin', 'it'] },
];
```

---

## 4. State Management

### 4.1 Server State — TanStack Query

All server data flows through TanStack Query. No direct fetch in components.

```tsx
// api/hooks/useStudents.ts
export function useStudents(params?: StudentListParams) {
  return useQuery({
    queryKey: ['students', params],
    queryFn: () => studentsApi.list(params),
    staleTime: 5 * 60 * 1000,  // 5 min
  });
}

export function useStudent(id: string) {
  return useQuery({
    queryKey: ['student', id],
    queryFn: () => studentsApi.get(id),
    enabled: !!id,
  });
}

export function useCreateStudent() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: studentsApi.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['students'] });
      toast.success(t('students.createSuccess'));
    },
    onError: (error) => {
      toast.error(formatApiError(error));
    },
  });
}
```

### 4.2 Client State — Zustand

Only truly global UI state goes in Zustand. Per-page state stays in TanStack Query cache.

```tsx
// stores/authStore.ts
interface AuthState {
  user: User | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  setUser: (user: User | null) => void;
  setAccessToken: (token: string | null) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  accessToken: null,
  isAuthenticated: false,
  setUser: (user) => set({ user, isAuthenticated: !!user }),
  setAccessToken: (token) => set({ accessToken: token }),
  logout: () => set({ user: null, accessToken: null, isAuthenticated: false }),
}));

// stores/uiStore.ts
interface UiState {
  sidebarCollapsed: boolean;
  theme: 'light' | 'dark';
  locale: 'en' | 'ar';
  toggleSidebar: () => void;
  setTheme: (theme: 'light' | 'dark') => void;
  setLocale: (locale: 'en' | 'ar') => void;
}

export const useUiStore = create<UiState>((set) => ({
  sidebarCollapsed: false,
  theme: 'light',
  locale: 'en',
  toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
  setTheme: (theme) => set({ theme }),
  setLocale: (locale) => set({ locale }),
}));
```

### 4.3 URL State

For filters + pagination, use URL search params (synced with React Router):

```tsx
// Use case: student list page
function StudentsListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = searchParams.get('cursor') || undefined;
  const status = searchParams.get('status') || 'active';
  const roomId = searchParams.get('roomId') || undefined;
  
  const { data, isLoading } = useStudents({ cursor: page, status, roomId });
  
  const handleFilterChange = (filters: StudentFilters) => {
    setSearchParams(filters);
  };
  
  // ...
}
```

---

## 5. Authentication Flow

### 5.1 Keycloak JS Adapter

```tsx
// lib/auth.ts
import Keycloak from 'keycloak-js';

const keycloak = new Keycloak({
  url: import.meta.env.VITE_KEYCLOAK_URL,
  realm: import.meta.env.VITE_KEYCLOAK_REALM,
  clientId: 'nms-web',
});

export async function initAuth(): Promise<boolean> {
  const authenticated = await keycloak.init({
    onLoad: 'check-sso',
    silentCheckSsoRedirectUri: window.location.origin + '/silent-check-sso.html',
    pkceMethod: 'S256',
    checkLoginIframe: false,
  });
  
  if (authenticated) {
    // Setup token refresh
    keycloak.onTokenExpired = () => keycloak.updateToken(30);
    
    // Schedule proactive refresh (every 5 min, token expires in 15 min)
    setInterval(() => {
      keycloak.updateToken(60).catch(() => keycloak.login());
    }, 5 * 60 * 1000);
  }
  
  return authenticated;
}

export function login() {
  return keycloak.login();
}

export function logout() {
  return keycloak.logout({ redirectUri: window.location.origin + '/login' });
}

export function getToken() {
  return keycloak.token;
}

export function getUserInfo() {
  return keycloak.loadUserInfo();
}
```

### 5.2 Auth Hook

```tsx
// hooks/useAuth.ts
export function useAuth() {
  const { user, isAuthenticated, setUser } = useAuthStore();
  const [isLoading, setIsLoading] = useState(true);
  
  useEffect(() => {
    initAuth()
      .then(async (authenticated) => {
        if (authenticated) {
          const userInfo = await getUserInfo();
          const me = await apiClient.get<AccountDto>('/auth/me');
          setUser(me.data);
        }
        setIsLoading(false);
      })
      .catch(() => setIsLoading(false));
  }, []);
  
  return { user, isAuthenticated, isLoading, login, logout };
}
```

### 5.3 API Client with Auth Interceptor

```tsx
// lib/api-client.ts
import axios from 'axios';
import { getToken, refreshAndGetToken } from './auth';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
  timeout: 30000,
  withCredentials: true,  // For refresh token cookie
});

// Request interceptor: add Authorization header
apiClient.interceptors.request.use((config) => {
  const token = getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Response interceptor: handle 401, refresh token
let isRefreshing = false;
let failedQueue: Array<(token: string | null) => void> = [];

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;
    
    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push((token) => {
            if (token) {
              originalRequest.headers.Authorization = `Bearer ${token}`;
              resolve(apiClient(originalRequest));
            } else {
              reject(error);
            }
          });
        });
      }
      
      originalRequest._retry = true;
      isRefreshing = true;
      
      try {
        const newToken = await refreshAndGetToken();
        failedQueue.forEach((cb) => cb(newToken));
        failedQueue = [];
        originalRequest.headers.Authorization = `Bearer ${newToken}`;
        return apiClient(originalRequest);
      } catch (refreshError) {
        failedQueue.forEach((cb) => cb(null));
        failedQueue = [];
        // Redirect to login
        window.location.href = '/login';
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }
    
    return Promise.reject(error);
  }
);
```

---

## 6. Internationalization (i18n)

### 6.1 i18next Setup

```tsx
// lib/i18n.ts
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import HttpBackend from 'i18next-http-backend';

i18n
  .use(HttpBackend)
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: 'en',
    supportedLngs: ['en', 'ar'],
    backend: {
      loadPath: '/locales/{{lng}}/translation.json',
    },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'nms-language',
      caches: ['localStorage'],
    },
    interpolation: {
      escapeValue: false,  // React already escapes
    },
    react: {
      useSuspense: true,
    },
  });

export default i18n;

// Set RTL direction on language change
i18n.on('languageChanged', (lng) => {
  document.documentElement.dir = lng === 'ar' ? 'rtl' : 'ltr';
  document.documentElement.lang = lng;
});
```

### 6.2 Translation File Structure

```json
// public/locales/en/translation.json
{
  "common": {
    "save": "Save",
    "cancel": "Cancel",
    "delete": "Delete",
    "edit": "Edit",
    "search": "Search",
    "loading": "Loading...",
    "noResults": "No results found",
    "actions": "Actions",
    "confirm": "Confirm",
    "yes": "Yes",
    "no": "No"
  },
  "nav": {
    "dashboard": "Dashboard",
    "students": "Students",
    "attendance": "Attendance",
    "activities": "Activities",
    "messaging": "Messaging",
    "billing": "Billing",
    "enquiries": "Enquiries",
    "tours": "Tours",
    "calendar": "Calendar",
    "forms": "Forms",
    "tools": "Tools",
    "reports": "Reports",
    "settings": "Settings"
  },
  "students": {
    "title": "Students",
    "createSuccess": "Student created successfully",
    "createError": "Failed to create student",
    "firstName": "First Name",
    "lastName": "Last Name",
    "dateOfBirth": "Date of Birth",
    "room": "Room",
    "program": "Program",
    "status": "Status",
    "guardians": "Guardians",
    "addGuardian": "Add Guardian"
  },
  "attendance": {
    "checkIn": "Check In",
    "checkOut": "Check Out",
    "markAbsent": "Mark Absent",
    "present": "Present",
    "absent": "Absent",
    "pending": "Pending",
    "ratioBreach": "Ratio breach in {{room}}"
  }
}
```

### 6.3 RTL Support

**Critical rules** (per SRS Section 5: non-RTL libraries are rejected):

1. **CSS logical properties only**:
   ```css
   /* ❌ NEVER */
   margin-left: 16px;
   padding-right: 8px;
   text-align: left;
   
   /* ✅ ALWAYS */
   margin-inline-start: 16px;
   padding-inline-end: 8px;
   text-align: start;
   ```

2. **Tailwind logical properties** (Tailwind 3.3+ supports these out of the box):
   ```html
   <!-- ❌ NEVER -->
   <div class="ml-4 pr-2 text-left">
   
   <!-- ✅ ALWAYS -->
   <div class="ms-4 pe-2 text-start">
   ```

3. **Flexbox direction**: use `flex-row` (auto-flips in RTL via Tailwind).
4. **Icons**: directional icons must be flipped in RTL. Use `rtl:rotate-180` Tailwind utility:
   ```html
   <ChevronRight className="rtl:rotate-180" />
   ```
5. **Numbers + dates**: use `Intl.NumberFormat` and `Intl.DateTimeFormat` with locale param.

### 6.4 i18n Hook

```tsx
import { useTranslation } from 'react-i18next';

function StudentsListPage() {
  const { t, i18n } = useTranslation();
  
  return (
    <div>
      <h1>{t('students.title')}</h1>
      <Button>{t('common.save')}</Button>
    </div>
  );
}
```

### 6.5 RTL QA Gate

Every PR must pass RTL QA:
- Visual regression test (Playwright) in both LTR and RTL.
- ESLint rule: forbid physical properties (`margin-left`, `padding-right`, `text-align: left/right`).
- Storybook stories render in both LTR + RTL.

```js
// .eslintrc.cjs
module.exports = {
  rules: {
    'no-restricted-syntax': [
      'error',
      {
        selector: "Literal[value=/margin-left|margin-right|padding-left|padding-right|text-align:\\s*(left|right)/]",
        message: 'Use CSS logical properties (margin-inline-start, padding-inline-end, text-align: start) instead of physical properties for RTL support.'
      }
    ]
  }
};
```

---

## 7. Component Patterns

### 7.1 shadcn/ui Setup

```bash
# Initialize shadcn/ui
npx shadcn@latest init

# Add components as needed
npx shadcn@latest add button input label select dialog dropdown-menu
npx shadcn@latest add table toast badge avatar card tabs
npx shadcn@latest add form checkbox radio-group switch
npx shadcn@latest add command popover calendar  # For date pickers
npx shadcn@latest add alert-tooltip separator
```

### 7.2 EntityTable (Generic)

```tsx
// components/shared/EntityTable.tsx
interface EntityTableProps<T> {
  data: T[];
  columns: ColumnDef<T>[];
  isLoading?: boolean;
  onRowClick?: (row: T) => void;
  emptyState?: React.ReactNode;
  pagination?: {
    cursor: string | null;
    hasNext: boolean;
    onLoadMore: () => void;
  };
}

export function EntityTable<T>({ data, columns, isLoading, ... }: EntityTableProps<T>) {
  // Uses TanStack Table for sorting, filtering
  // Cursor-based pagination (not offset)
  // RTL-aware (table direction follows document dir)
  // Loading skeletons
  // Empty state
}
```

### 7.3 FormDialog

```tsx
// components/shared/FormDialog.tsx
interface FormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  size?: 'sm' | 'md' | 'lg' | 'xl';
}

export function FormDialog({ open, onOpenChange, title, children, size = 'md' }: FormDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={cn('max-w-[90vw]', sizeClasses[size])}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        {children}
      </DialogContent>
    </Dialog>
  );
}

// Usage with form
function CreateStudentDialog({ open, onOpenChange }) {
  const form = useForm<CreateStudentDto>({
    resolver: zodResolver(createStudentSchema),
  });
  const createMutation = useCreateStudent();
  
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} title={t('students.create')}>
      <Form {...form}>
        <form onSubmit={form.handleSubmit((data) => createMutation.mutate(data))}>
          <FormField name="firstName" render={({ field }) => (
            <FormItem>
              <FormLabel>{t('students.firstName')}</FormLabel>
              <FormControl>
                <Input {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )} />
          {/* More fields */}
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={createMutation.isPending}>
              {t('common.save')}
            </Button>
          </DialogFooter>
        </form>
      </Form>
    </FormDialog>
  );
}
```

### 7.4 Loading / Error / Empty States

```tsx
// components/shared/LoadingState.tsx
export function LoadingState({ message }: { message?: string }) {
  return (
    <div className="flex h-64 items-center justify-center">
      <div className="flex flex-col items-center gap-3">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        <p className="text-sm text-muted-foreground">{message || t('common.loading')}</p>
      </div>
    </div>
  );
}

// components/shared/ErrorState.tsx
export function ErrorState({ 
  message, 
  onRetry 
}: { 
  message?: string; 
  onRetry?: () => void;
}) {
  return (
    <div className="flex h-64 items-center justify-center">
      <div className="flex flex-col items-center gap-3">
        <AlertCircle className="h-8 w-8 text-destructive" />
        <p className="text-sm text-muted-foreground">
          {message || t('common.errorOccurred')}
        </p>
        {onRetry && (
          <Button variant="outline" size="sm" onClick={onRetry}>
            {t('common.retry')}
          </Button>
        )}
      </div>
    </div>
  );
}

// components/shared/EmptyState.tsx
export function EmptyState({ 
  icon: Icon, 
  title, 
  description, 
  action 
}: { 
  icon: LucideIcon;
  title: string;
  description?: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex h-64 items-center justify-center">
      <div className="flex flex-col items-center gap-3 text-center">
        <Icon className="h-12 w-12 text-muted-foreground/50" />
        <div>
          <p className="font-medium">{title}</p>
          {description && <p className="text-sm text-muted-foreground">{description}</p>}
        </div>
        {action}
      </div>
    </div>
  );
}
```

---

## 8. Real-Time with SignalR

### 8.1 Connection Manager

```tsx
// lib/signalr.ts
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { getToken } from './auth';

let connection: HubConnection | null = null;

export async function getConnection(): Promise<HubConnection> {
  if (connection) return connection;
  
  connection = new HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_API_URL}/hubs/tenant`, {
      accessTokenFactory: () => getToken() || '',
    })
    .withAutomaticReconnect([0, 2000, 10000, 30000])
    .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
    .build();
  
  await connection.start();
  return connection;
}

export function onConnectionStateChanged(callback: (state: string) => void) {
  if (!connection) return;
  connection.onreconnecting(() => callback('reconnecting'));
  connection.onreconnected(() => callback('connected'));
  connection.onclose(() => callback('disconnected'));
}
```

### 8.2 Real-Time Hook

```tsx
// hooks/useSignalR.ts
export function useSignalR() {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<'connecting' | 'connected' | 'reconnecting' | 'disconnected'>('connecting');
  
  useEffect(() => {
    let connection: HubConnection | null = null;
    
    async function setup() {
      connection = await getConnection();
      
      // Listen for events
      connection.on('AttendanceUpdated', (data) => {
        queryClient.invalidateQueries({ queryKey: ['attendance'] });
        queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      });
      
      connection.on('RatioUpdated', (data) => {
        queryClient.setQueryData(['liveRatio', data.roomId], data);
      });
      
      connection.on('NotificationReceived', (notification) => {
        useNotificationStore.getState().addNotification(notification);
        queryClient.invalidateQueries({ queryKey: ['notifications'] });
      });
      
      connection.on('MessageReceived', ({ threadId, message }) => {
        queryClient.invalidateQueries({ queryKey: ['messages', threadId] });
        queryClient.invalidateQueries({ queryKey: ['threads'] });
      });
      
      connection.on('ActivityPosted', (activity) => {
        queryClient.invalidateQueries({ queryKey: ['activities'] });
      });
      
      connection.on('DashboardRefreshed', ({ widget, data }) => {
        queryClient.setQueryData(['dashboard', widget], data);
      });
      
      connection.on('BillingUpdated', ({ invoiceId, status }) => {
        queryClient.invalidateQueries({ queryKey: ['invoices'] });
        queryClient.invalidateQueries({ queryKey: ['billingDashboard'] });
      });
      
      onConnectionStateChanged(setConnectionState);
    }
    
    setup().catch(console.error);
    
    return () => {
      // Don't disconnect — connection is shared
    };
  }, [queryClient]);
  
  return { connectionState };
}
```

---

## 9. Performance Optimization

### 9.1 Code Splitting

Route-level code splitting via React.lazy:

```tsx
// router/routes.tsx
const DashboardPage = lazy(() => import('@/modules/dashboard/pages/DashboardPage'));
const StudentsListPage = lazy(() => import('@/modules/centre-management/pages/StudentsListPage'));
const AttendancePage = lazy(() => import('@/modules/attendance/pages/AttendancePage'));
// ... etc

export const routes = (
  <Routes>
    <Route path="/dashboard" element={<Suspense fallback={<FullScreenLoader />}><DashboardPage /></Suspense>} />
    <Route path="/students" element={<Suspense fallback={<FullScreenLoader />}><StudentsListPage /></Suspense>} />
    {/* ... */}
  </Routes>
);
```

### 9.2 Bundle Size Budget

```js
// vite.config.ts
build: {
  rollupOptions: {
    output: {
      manualChunks: {
        'react-vendor': ['react', 'react-dom', 'react-router-dom'],
        'query-vendor': ['@tanstack/react-query'],
        'ui-vendor': ['@radix-ui/react-dialog', '@radix-ui/react-dropdown-menu', /* ... */],
        'form-vendor': ['react-hook-form', 'zod', '@hookform/resolvers'],
        'i18n-vendor': ['i18next', 'react-i18next', 'i18next-browser-languagedetector'],
      },
    },
  },
  chunkSizeWarningLimit: 500,  // 500 KB warning
}
```

- Initial bundle: < 300 KB (gzip).
- Per-route chunk: < 200 KB (gzip).
- Total app: < 2 MB (gzip).

### 9.3 List Virtualization

For long lists (e.g., audit log with 10,000+ entries), use `@tanstack/react-virtual`:

```tsx
import { useVirtualizer } from '@tanstack/react-virtual';

function AuditLogList({ entries }: { entries: AuditLogEntry[] }) {
  const parentRef = useRef(null);
  
  const virtualizer = useVirtualizer({
    count: entries.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 80,
    overscan: 10,
  });
  
  return (
    <div ref={parentRef} className="h-[600px] overflow-auto">
      <div style={{ height: virtualizer.getTotalSize() }}>
        {virtualizer.getVirtualItems().map((item) => (
          <div key={item.key} style={{ position: 'absolute', top: item.start, height: item.size }}>
            <AuditLogEntry entry={entries[item.index]} />
          </div>
        ))}
      </div>
    </div>
  );
}
```

### 9.4 Image Optimization

- Use `next/image` equivalent via `@unpic/react` for responsive images with WebP.
- All user-uploaded images served via CloudFront with on-the-fly resize (via Lambda@Edge or imgix-style service).
- Lazy load images with `loading="lazy"` and proper `width`/`height` to prevent layout shift.

### 9.5 Performance Budget

- Dashboard p95 load: < 2s (SRS mandate).
- API p99 latency: < 500ms.
- TTI (Time to Interactive): < 3s on standard broadband.
- Lighthouse score: > 90 (Performance, Accessibility, Best Practices, SEO).

---

## 10. Testing

### 10.1 Test Pyramid

| Type | Tool | Coverage | Speed |
|---|---|---|---|
| Unit | Vitest + React Testing Library | 80%+ | Fast (<10s) |
| Component | Vitest + RTL + MSW | Critical paths | Fast |
| Integration | Vitest + Testcontainers (Postgres, Redis) | API contract | Medium |
| E2E | Playwright | Critical user journeys | Slow (minutes) |
| Visual regression | Playwright + Percy / Chromatic | All pages in LTR + RTL | Slow |

### 10.2 Unit Test Example

```tsx
// modules/centre-management/components/StudentForm.test.tsx
describe('StudentForm', () => {
  it('should validate required fields', async () => {
    const { result } = renderHook(() => useForm({
      resolver: zodResolver(createStudentSchema),
    }));
    
    await act(async () => {
      await result.current.handleSubmit(onSubmit)({ firstName: '' });
    });
    
    expect(result.current.formState.errors.firstName).toBeDefined();
  });
  
  it('should call onSubmit with valid data', async () => {
    const onSubmit = vi.fn();
    render(<StudentForm onSubmit={onSubmit} />);
    
    await user.type(screen.getByLabelText(/first name/i), 'Ahmed');
    await user.type(screen.getByLabelText(/last name/i), 'Ali');
    await user.click(screen.getByRole('button', { name: /save/i }));
    
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
        firstName: 'Ahmed',
        lastName: 'Ali',
      }));
    });
  });
});
```

### 10.3 E2E Test Example

```tsx
// e2e/attendance.spec.ts
test('staff can check in a student via barcode', async ({ page }) => {
  await loginAsStaff(page, 'teacher@brightfutures.ae');
  
  await page.goto('/attendance');
  await page.click('[data-testid="barcode-input"]');
  await page.keyboard.type('STU-12345\n');
  
  await expect(page.locator('[data-testid="check-in-success"]')).toBeVisible();
  await expect(page.locator('text=Ahmed Ali')).toContainText('Checked In');
});
```

---

## 11. Accessibility

Per SRS: WCAG 2.1 AA for parent portal (we apply to all surfaces).

### 11.1 Patterns

- Semantic HTML (`nav`, `main`, `section`, `article`, `header`, `footer`).
- ARIA labels on icon-only buttons.
- Focus management (FocusTrap for modals, focus return on close).
- Keyboard navigation: all interactive elements reachable via Tab.
- Skip-to-content link at top of every page.
- Color contrast: minimum 4.5:1 for normal text, 3:1 for large text.
- Form errors announced via `aria-live`.
- Loading states announced via `aria-busy`.

### 11.2 Testing

- `axe-core` in unit tests: every component test runs axe accessibility checks.
- `@axe-core/playwright` in E2E tests: every page checked.
- Lighthouse Accessibility audit in CI (target > 90).
- Manual screen reader testing (NVDA on Windows, VoiceOver on macOS) before each phase go-live.

```tsx
// Example axe test
import { axe } from 'vitest-axe';

describe('Dashboard accessibility', () => {
  it('should have no accessibility violations', async () => {
    const { container } = render(<DashboardPage />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

---

## 12. Build & Deploy

### 12.1 Build Process

```bash
# Development
pnpm dev            # Vite dev server with HMR

# Production build
pnpm build          # Type-check + Vite build → dist/

# Preview production build
pnpm preview        # Serve dist/ locally for verification
```

### 12.2 Dockerfile

```dockerfile
# infra/docker/web.Dockerfile
FROM node:20-alpine AS builder
WORKDIR /app
RUN npm install -g pnpm
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
RUN pnpm build

FROM nginx:alpine
COPY --from=builder /app/dist /usr/share/nginx/html
COPY infra/docker/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

```nginx
# infra/docker/nginx.conf
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;
    
    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }
    
    # Cache static assets (hashed filenames = immutable)
    location /assets/ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
    
    # Gzip
    gzip on;
    gzip_types text/plain text/css application/javascript application/json image/svg+xml;
    gzip_min_length 1000;
    
    # Security headers
    add_header X-Frame-Options "DENY" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;
    add_header Permissions-Policy "geolocation=(), microphone=(), camera=()" always;
}
```

### 12.3 Environment Variables

```bash
# .env.example
VITE_API_URL=https://api.nms.ae
VITE_KEYCLOAK_URL=https://keycloak.nms.ae
VITE_KEYCLOAK_REALM=bright-futures-nursery
VITE_SENTRY_DSN=https://...@sentry.io/...
VITE_GA_MEASUREMENT_ID=G-XXXXXXXX
```

Environment-specific values injected at build time or runtime (via runtime config file for SPA).

---

**Next file to read**: `13-MOBILE-APP.md` for React Native + Expo architecture and offline capabilities.
