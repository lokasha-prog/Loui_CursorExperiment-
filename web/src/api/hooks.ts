import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, authApi, type Page } from './client'
import type {
  GroupCentre,
  GroupOverview,
  GroupReportTable,
  MyNewsletter,
  NewsletterDetail,
  NewsletterItem,
  NewsletterStats,
  ReadNewsletter,
  ResourceDocument,
  ResourceDownloadItem,
  ResourceFolder,
  ResourceVersionItem,
  Analytics,
  ReportDefinition,
  ReportRun,
  AdmissionStatus,
  AdmissionStepItem,
  CertificateTemplateItem,
  FillableForm,
  FormAnalytics,
  FormDetail,
  FormFieldDef,
  FormItem,
  FormSubmissionItem,
  IssuedCertificate,
  StudentAdmission,
  AgendaItem,
  CalendarEventDetail,
  CalendarOccurrence,
  IdCardTemplate,
  ScheduleData,
  CentreSettings,
  RoleItem,
  BlockOff,
  Enquiry,
  EnquiryDetail,
  EnquirySummary,
  FollowUpTask,
  Tour,
  BillingSettings,
  Expense,
  ExpenseReport,
  OverdueReport,
  ReminderLogItem,
  RevenueReport,
  BillingSnapshot,
  ComponentInput,
  FeePlan,
  FeePlanDetail,
  InvoiceDetail,
  InvoiceSummary,
  FeedbackItem,
  RetentionPolicy,
  AppNotification,
  AuditEntry,
  NotificationPreference,
  ActivityPost,
  DailyReport,
  MediaItem,
  MessageThread,
  ThreadDetail,
  ThreadMessage,
  Forecast,
  MonthlyReport,
  RatioBreach,
  AttendanceInsights,
  FrameworkDetail,
  FrameworkSummary,
  Lesson,
  LessonInput,
  LessonDraft,
  MilestoneSuggestion,
  Observation,
  ObservationInput,
  AssessmentTemplate,
  AssessmentRun,
  ParentReport,
  Journey,
  SystemOverview,
  Briefing,
  BriefingArea,
  AttendanceRow,
  CheckOutRequest,
  CreateLeaveRequest,
  CreateLeaveTypeRequest,
  DashboardSummary,
  LeaveRequest,
  LeaveType,
  RoomRatio,
  ScanRequest,
  CreateGroupRequest,
  CreateLabelRequest,
  CreateProgramRequest,
  CreateRoomRequest,
  CreateStaffRequest,
  CreateStudentRequest,
  CreateUserRequest,
  Group,
  Guardian,
  GuardianRequest,
  Label,
  Program,
  Room,
  StaffSummary,
  StudentDetail,
  StudentSummary,
  UserAccount,
} from './types'

const PAGE_SIZE = 25

function useInfiniteList<T>(key: unknown[], path: string, query: Record<string, string | undefined>, enabled = true) {
  return useInfiniteQuery({
    queryKey: [...key, query],
    enabled,
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }): Promise<Page<T>> => api.page<T>(path, { ...query, cursor: pageParam, limit: PAGE_SIZE }),
    getNextPageParam: (last) => (last.pagination.has_more ? (last.pagination.next_cursor ?? undefined) : undefined),
  })
}

export function flatten<T>(data: { pages: Page<T>[] } | undefined): T[] {
  return data?.pages.flatMap((page) => page.items) ?? []
}

// ── Students ────────────────────────────────────────────────────────────────

export const useStudents = (filters: { search?: string; status?: string; roomId?: string }) =>
  useInfiniteList<StudentSummary>(['students'], '/students', {
    search: filters.search || undefined,
    status: filters.status || undefined,
    roomId: filters.roomId || undefined,
  })

export const useStudent = (id: string) =>
  useQuery({ queryKey: ['student', id], queryFn: () => api.get<StudentDetail>(`/students/${id}`) })

export const useGuardians = (studentId: string) =>
  useQuery({ queryKey: ['guardians', studentId], queryFn: () => api.get<Guardian[]>(`/students/${studentId}/guardians`) })

export function useUpdateStudent(id: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { student: CreateStudentRequest['student'] }) => api.patch<StudentDetail>(`/students/${id}`, body),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: ['students'] }), client.invalidateQueries({ queryKey: ['student', id] })]),
  })
}

export function useCreateStudent() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateStudentRequest) => api.post<StudentDetail>('/students', body),
    onSuccess: () => client.invalidateQueries({ queryKey: ['students'] }),
  })
}

export function useTransitionStudent(id: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { status: string; reason?: string }) =>
      api.post<StudentDetail>(`/students/${id}/actions/transition-status`, body),
    onSuccess: (student) => {
      client.setQueryData(['student', id], student)
      return client.invalidateQueries({ queryKey: ['students'] })
    },
  })
}

export function useAddGuardian(studentId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: GuardianRequest) => api.post<Guardian>(`/students/${studentId}/guardians`, body),
    onSuccess: () => client.invalidateQueries({ queryKey: ['guardians', studentId] }),
  })
}

export function useRemoveGuardian(studentId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (guardianId: string) => api.delete(`/students/${studentId}/guardians/${guardianId}`),
    onSuccess: () => client.invalidateQueries({ queryKey: ['guardians', studentId] }),
  })
}

// ── Staff ───────────────────────────────────────────────────────────────────

export const useStaff = (filters: { search?: string; status?: string }) =>
  useInfiniteList<StaffSummary>(['staff'], '/staff', { search: filters.search || undefined, status: filters.status || undefined })

export function useCreateStaff() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateStaffRequest) => api.post<StaffSummary>('/staff', body),
    onSuccess: () => client.invalidateQueries({ queryKey: ['staff'] }),
  })
}

// ── Rooms, programs, groups, labels ─────────────────────────────────────────

export const useRooms = (enabled = true) => useInfiniteList<Room>(['rooms'], '/rooms', {}, enabled)
export const usePrograms = (enabled = true) => useInfiniteList<Program>(['programs'], '/programs', {}, enabled)
export const useGroups = (enabled = true) => useInfiniteList<Group>(['groups'], '/groups', {}, enabled)
export const useLabels = (enabled = true) => useInfiniteList<Label>(['labels'], '/labels', {}, enabled)

/** Everything a dropdown needs (the API caps a page at 100). */
export const useRoomOptions = (enabled = true) =>
  useQuery({ queryKey: ['room-options'], enabled, queryFn: async () => (await api.page<Room>('/rooms', { limit: 100 })).items })

export const useProgramOptions = (enabled = true) =>
  useQuery({
    queryKey: ['program-options'],
    enabled,
    queryFn: async () => (await api.page<Program>('/programs', { limit: 100 })).items,
  })

function useCreate<TBody, TResult>(path: string, invalidate: string[]) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: TBody) => api.post<TResult>(path, body),
    onSuccess: () => Promise.all(invalidate.map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useCreateRoom = () => useCreate<CreateRoomRequest, Room>('/rooms', ['rooms', 'room-options'])
export const useCreateProgram = () => useCreate<CreateProgramRequest, Program>('/programs', ['programs', 'program-options'])
export const useCreateGroup = () => useCreate<CreateGroupRequest, Group>('/groups', ['groups'])
export const useCreateLabel = () => useCreate<CreateLabelRequest, Label>('/labels', ['labels'])

// ── Users ───────────────────────────────────────────────────────────────────

export const useUsers = (enabled = true) => useInfiniteList<UserAccount>(['users'], '/users', {}, enabled)

export const useCreateUser = () => useCreate<CreateUserRequest, UserAccount>('/users', ['users'])

export function useSetUserStatus() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: 'active' | 'suspended' }) =>
      api.post<UserAccount>(`/users/${id}/actions/set-status`, { status }),
    onSuccess: () => client.invalidateQueries({ queryKey: ['users'] }),
  })
}

// ── Public policy ───────────────────────────────────────────────────────────

/** First-run state and the password rule, both read from the server configuration (never hard-coded in the web app). */
export const useSetupStatus = () =>
  useQuery({ queryKey: ['setup-status'], queryFn: () => authApi.setupStatus(), staleTime: 60_000 })

// ── Attendance ──────────────────────────────────────────────────────────────

/** How often live screens ask again until push updates (SignalR) are connected. */
export const LIVE_REFRESH_MS = 15_000

export const useAttendance = (filters: { date?: string; room_id?: string; person_type?: string }, enabled = true) =>
  useInfiniteList<AttendanceRow>(['attendance'], '/attendance', { ...filters }, enabled)

export const useLiveRatios = (enabled = true) =>
  useQuery({
    queryKey: ['live-ratios'],
    enabled,
    refetchInterval: LIVE_REFRESH_MS,
    queryFn: () => api.get<RoomRatio[]>('/attendance/ratio/live'),
  })

export const useDashboardSummary = (enabled = true) =>
  useQuery({
    queryKey: ['dashboard-summary'],
    enabled,
    refetchInterval: LIVE_REFRESH_MS,
    queryFn: () => api.get<DashboardSummary>('/dashboard/attendance-summary'),
  })

function useAttendanceAction<TBody>(action: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: TBody) => api.post<AttendanceRow>(`/attendance/actions/${action}`, body),
    onSuccess: () =>
      Promise.all(['attendance', 'live-ratios', 'dashboard-summary'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useCheckIn = () => useAttendanceAction<ScanRequest>('check-in')
export const useCheckOut = () => useAttendanceAction<CheckOutRequest>('check-out')
export const useMarkAbsent = () => useAttendanceAction<{ student_id: string; notes?: string }>('mark-absent')

export const useStudentOptions = (enabled = true, status = 'active') =>
  useQuery({
    queryKey: ['student-options', status],
    enabled,
    queryFn: async () => (await api.page<StudentSummary>('/students', { status, limit: 100 })).items,
  })

export const useStaffOptions = (enabled = true) =>
  useQuery({
    queryKey: ['staff-options'],
    enabled,
    queryFn: async () => (await api.page<StaffSummary>('/staff', { status: 'active', limit: 100 })).items,
  })

// ── Leave ───────────────────────────────────────────────────────────────────

export const useLeaveTypes = (enabled = true) =>
  useQuery({
    queryKey: ['leave-types'],
    enabled,
    queryFn: async () => (await api.page<LeaveType>('/leave-types', { limit: 100 })).items,
  })

export const useCreateLeaveType = () => useCreate<CreateLeaveTypeRequest, LeaveType>('/leave-types', ['leave-types'])

export const useLeaves = (filters: { status?: string }) => useInfiniteList<LeaveRequest>(['leaves'], '/leaves', { ...filters })

export const useCreateLeave = () => useCreate<CreateLeaveRequest, LeaveRequest>('/leaves', ['leaves', 'dashboard-summary'])

export function useLeaveAction() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, action, notes }: { id: string; action: 'approve' | 'reject' | 'cancel'; notes?: string }) =>
      api.post<LeaveRequest>(`/leaves/${id}/actions/${action}`, action === 'cancel' ? undefined : { notes }),
    onSuccess: () => Promise.all(['leaves', 'dashboard-summary'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

// ── AI Brain ────────────────────────────────────────────────────────────────

/** Asks the AI Brain for a plain-language summary of recent attendance. Only totals are ever sent to the provider. */
export const useAttendanceInsights = () =>
  useMutation({
    mutationFn: ({ days, language }: { days: number; language: string }) =>
      api.get<AttendanceInsights>('/ai/attendance-summary', { days, language }),
  })

/** Asks the AI Brain for a management briefing on one area (overview, finance, admissions, people). Only totals are sent to the provider. */
export const useBriefing = (area: BriefingArea) =>
  useMutation({ mutationFn: (language: string) => api.get<Briefing>(`/ai/briefing/${area}`, { language }) })

// ── Reports ─────────────────────────────────────────────────────────────────

export const useMonthlyReport = (month: string, personType: 'student' | 'staff') =>
  useQuery({
    queryKey: ['monthly-report', month, personType],
    queryFn: () => api.get<MonthlyReport>('/attendance/report/monthly', { month, person_type: personType }),
  })

export const useForecast = (date?: string) =>
  useQuery({ queryKey: ['forecast', date], queryFn: () => api.get<Forecast>('/attendance/forecast', { date }) })

export const useRatioHistory = () => useInfiniteList<RatioBreach>(['ratio-history'], '/attendance/ratio/history', {})

// ── Activity feed ───────────────────────────────────────────────────────────

export const useActivities = (filters: { status?: string }) =>
  useInfiniteList<ActivityPost>(['activities'], '/activities', { ...filters })

export const useUploadMedia = () => useMutation({ mutationFn: (file: File) => api.upload<MediaItem>('/media', file) })

export const useCreateActivity = () =>
  useCreate<
    { type: string; caption: Record<string, string> | null; student_ids: string[]; room_id?: string | null; media_ids: string[] },
    ActivityPost
  >('/activities', ['activities'])

export function useDecideActivity() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, action, reason }: { id: string; action: 'approve' | 'reject'; reason?: string }) =>
      api.post<ActivityPost>(`/activities/${id}/actions/${action}`, action === 'reject' ? { reason } : undefined),
    onSuccess: () => client.invalidateQueries({ queryKey: ['activities'] }),
  })
}

export const useDailyReports = (enabled = true) => useInfiniteList<DailyReport>(['daily-reports'], '/daily-reports', {}, enabled)

// ── Messages ────────────────────────────────────────────────────────────────

export const useThreads = () => useInfiniteList<MessageThread>(['threads'], '/messages/threads', {})

export const useThread = (id: string | null) =>
  useQuery({ queryKey: ['thread', id], enabled: !!id, queryFn: () => api.get<ThreadDetail>(`/messages/threads/${id}`) })

export const useCreateThread = () =>
  useCreate<{ subject: string; body: string; student_id?: string | null; broadcast?: boolean; room_id?: string | null }, ThreadDetail>(
    '/messages/threads',
    ['threads'],
  )

export function useReply(threadId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: string) => api.post<ThreadMessage>(`/messages/threads/${threadId}/messages`, { body }),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: ['thread', threadId] }), client.invalidateQueries({ queryKey: ['threads'] })]),
  })
}

// ── Notifications ───────────────────────────────────────────────────────────

export const useNotifications = () => useInfiniteList<AppNotification>(['notifications'], '/notifications', {})

export const useUnreadCount = () =>
  useQuery({
    queryKey: ['unread-count'],
    refetchInterval: 60_000,
    queryFn: async () => (await api.get<{ count: number }>('/notifications/unread-count')).count,
  })

export function useMarkRead() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string | null) => api.post<{ marked: number }>(id ? `/notifications/${id}/actions/read` : '/notifications/actions/read-all'),
    onSuccess: () => Promise.all(['notifications', 'unread-count'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const usePreferences = () =>
  useQuery({ queryKey: ['notification-preferences'], queryFn: () => api.get<NotificationPreference[]>('/notifications/preferences') })

export function useSavePreferences() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (preferences: NotificationPreference[]) => api.put<NotificationPreference[]>('/notifications/preferences', { preferences }),
    onSuccess: (data) => client.setQueryData(['notification-preferences'], data),
  })
}

// ── Audit log ───────────────────────────────────────────────────────────────

export const useAuditLogs = (filters: { action?: string; entity_type?: string; from?: string; to?: string }) =>
  useInfiniteList<AuditEntry>(['audit'], '/audit-logs', { ...filters })

export const useRetention = () => useQuery({ queryKey: ['audit-retention'], queryFn: () => api.get<RetentionPolicy>('/audit-logs/retention') })

export function useSaveRetention() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { retention_days: number; auto_purge_enabled: boolean }) => api.put<RetentionPolicy>('/audit-logs/retention', body),
    onSuccess: (data) => client.setQueryData(['audit-retention'], data),
  })
}

export function usePurgeAudit() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<{ run_at: string; mode: string; cutoff: string; rows_deleted: number }>('/audit-logs/retention/actions/purge', { confirm: true }),
    onSuccess: () => Promise.all(['audit-retention', 'audit'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

// ── Feedback ────────────────────────────────────────────────────────────────

export const useSendFeedback = () =>
  useMutation({
    mutationFn: (body: { kind: string; message: string; page: string; rating: number | null }) => api.post<FeedbackItem>('/feedback', body),
  })

export const useFeedbackList = (status: string) => useInfiniteList<FeedbackItem>(['feedback'], '/feedback', { status: status || undefined })

export function useSetFeedbackStatus() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) => api.patch<FeedbackItem>(`/feedback/${id}`, { status }),
    onSuccess: () => client.invalidateQueries({ queryKey: ['feedback'] }),
  })
}

// ── Billing ─────────────────────────────────────────────────────────────────

export const useBillingSnapshot = (enabled = true) =>
  useQuery({ queryKey: ['billing-snapshot'], enabled, queryFn: () => api.get<BillingSnapshot>('/dashboard/billing-snapshot') })

export const useFeePlans = (enabled = true) => useInfiniteList<FeePlan>(['fee-plans'], '/fee-plans', {}, enabled)

export const useFeePlan = (id: string | null) =>
  useQuery({ queryKey: ['fee-plan', id], enabled: !!id, queryFn: () => api.get<FeePlanDetail>(`/fee-plans/${id}`) })

export interface FeePlanInput {
  name: Record<string, string>
  description?: Record<string, string> | null
  period: string
  program_id: string | null
  status: string
  components: ComponentInput[]
}

export function useSaveFeePlan(id?: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: FeePlanInput) => (id ? api.patch<FeePlanDetail>(`/fee-plans/${id}`, body) : api.post<FeePlanDetail>('/fee-plans', body)),
    onSuccess: () => Promise.all(['fee-plans', 'fee-plan'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export function useAssignToPlan(planId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { student_ids: string[]; effective_from: string }) => api.post<FeePlanDetail>(`/fee-plans/${planId}/students`, body),
    onSuccess: () => Promise.all(['fee-plans', 'fee-plan'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useInvoices = (filters: { status?: string }) => useInfiniteList<InvoiceSummary>(['invoices'], '/invoices', { ...filters })

export const useInvoice = (id: string | null) =>
  useQuery({ queryKey: ['invoice', id], enabled: !!id, queryFn: () => api.get<InvoiceDetail>(`/invoices/${id}`) })

function useBillingMutation<TBody, TResult>(run: (body: TBody) => Promise<TResult>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: () => Promise.all(['invoices', 'invoice', 'billing-snapshot'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useCreateInvoice = () =>
  useBillingMutation((body: {
    student_id: string
    period_start: string
    period_end: string
    issue: boolean
    lines: { description: Record<string, string>; quantity: number; unit_price_fils: number; tax_rate_percent: number | null }[]
  }) => api.post<InvoiceDetail>('/invoices', body))

export const useInvoiceAction = () =>
  useBillingMutation(({ id, action }: { id: string; action: 'issue' | 'cancel' | 'send' }) => api.post<InvoiceDetail>(`/invoices/${id}/actions/${action}`))

export const useRecordPayment = () =>
  useBillingMutation(({ id, ...body }: { id: string; amount_fils: number; payment_date: string; method: string; reference?: string | null }) =>
    api.post<InvoiceDetail>(`/invoices/${id}/payments`, body))

export const useGenerateInvoices = () => useBillingMutation(() => api.post<{ created: number }>('/invoices/actions/generate', {}))

// ── Expenses, reports and billing settings ──────────────────────────────────

export const useExpenses = (month: string) => useInfiniteList<Expense>(['expenses'], '/expenses', { month: month || undefined })

export const useExpenseReport = (month: string) =>
  useQuery({ queryKey: ['expense-report', month], queryFn: () => api.get<ExpenseReport>(`/expenses/report${month ? `?month=${month}` : ''}`) })

export interface ExpenseInput {
  expense_date: string
  amount_fils: number
  category: string
  description: string
  receipt_media_id: string | null
}

export function useSaveExpense(id?: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: ExpenseInput) => (id ? api.patch<Expense>(`/expenses/${id}`, body) : api.post<Expense>('/expenses', body)),
    onSuccess: () => Promise.all(['expenses', 'expense-report', 'billing-snapshot', 'revenue-report'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export function useDeleteExpense() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/expenses/${id}`),
    onSuccess: () => Promise.all(['expenses', 'expense-report', 'billing-snapshot', 'revenue-report'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useRevenueReport = (month: string) =>
  useQuery({ queryKey: ['revenue-report', month], queryFn: () => api.get<RevenueReport>(`/reports/billing/revenue${month ? `?month=${month}` : ''}`) })

export const useOverdueReport = () => useQuery({ queryKey: ['overdue-report'], queryFn: () => api.get<OverdueReport>('/reports/billing/overdue') })

export const useBillingSettings = () => useQuery({ queryKey: ['billing-settings'], queryFn: () => api.get<BillingSettings>('/billing/settings') })

export function useSaveBillingSettings() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: BillingSettings) => api.put<BillingSettings>('/billing/settings', body),
    onSuccess: () => client.invalidateQueries({ queryKey: ['billing-settings'] }),
  })
}

export const useReminderLog = () => useInfiniteList<ReminderLogItem>(['reminders'], '/billing/reminders', {})

export function useRunReminders() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<{ sent: number }>('/billing/reminders/actions/run', {}),
    onSuccess: () => client.invalidateQueries({ queryKey: ['reminders'] }),
  })
}

// ── Enquiries, tours and tasks ──────────────────────────────────────────────

export interface EnquiryFilters {
  stage?: string
  source?: string
  staff_id?: string
  from?: string
  to?: string
  archived?: boolean
  search?: string
}

export const useEnquiries = (filters: EnquiryFilters) =>
  useInfiniteList<Enquiry>(['enquiries'], '/enquiries', { ...filters, archived: filters.archived ? 'true' : undefined })

export const useEnquirySummary = () => useQuery({ queryKey: ['enquiry-summary'], queryFn: () => api.get<EnquirySummary>('/enquiries/summary') })

export const useEnquiry = (id: string | null) =>
  useQuery({ queryKey: ['enquiry', id], enabled: !!id, queryFn: () => api.get<EnquiryDetail>(`/enquiries/${id}`) })

export interface EnquiryInput {
  family_name: string
  child_name: string
  phone: string | null
  email: string | null
  preferred_contact: string
  child_date_of_birth: string | null
  desired_start_date: string | null
  program_id: string | null
  source: string
  assigned_staff_id: string | null
  notes: string | null
}

function useEnquiryMutation<TBody>(run: (body: TBody) => Promise<unknown>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: () => Promise.all(['enquiries', 'enquiry', 'enquiry-summary', 'tours', 'tasks', 'students'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useSaveEnquiry = (id?: string) =>
  useEnquiryMutation((body: EnquiryInput) => (id ? api.patch<EnquiryDetail>(`/enquiries/${id}`, body) : api.post<EnquiryDetail>('/enquiries', body)))

export const useChangeStage = (id: string) =>
  useEnquiryMutation((body: { stage: string; reason?: string | null }) => api.post<EnquiryDetail>(`/enquiries/${id}/actions/change-stage`, body))

export const useAddActivity = (id: string) =>
  useEnquiryMutation((body: { kind: string; summary: string }) => api.post<EnquiryDetail>(`/enquiries/${id}/activities`, body))

export const useArchiveEnquiry = (id: string) =>
  useEnquiryMutation((archive: boolean) => api.post<EnquiryDetail>(`/enquiries/${id}/actions/${archive ? 'archive' : 'unarchive'}`))

export const useConvertEnquiry = (id: string) =>
  useEnquiryMutation((body: { room_id: string | null; start_date: string | null }) => api.post<EnquiryDetail>(`/enquiries/${id}/actions/convert`, body))

export const useTours = (from: string, to: string, staffId?: string) =>
  useQuery({ queryKey: ['tours', from, to, staffId ?? ''], queryFn: () => api.get<Tour[]>('/tours', { from, to, staff_id: staffId }) })

export interface TourInput {
  enquiry_id: string
  starts_at: string
  duration_minutes: number
  staff_id: string | null
  tour_type: string
  adults: number
  children: number
  notes: string | null
}

export const useSaveTour = (id?: string) =>
  useEnquiryMutation((body: TourInput) => (id ? api.patch<Tour>(`/tours/${id}`, body) : api.post<Tour>('/tours', body)))

export const useSetTourStatus = () =>
  useEnquiryMutation(({ id, status }: { id: string; status: string }) => api.post<Tour>(`/tours/${id}/actions/set-status`, { status }))

export const useBlockOffs = (from: string, to: string) =>
  useQuery({ queryKey: ['tours', 'block-offs', from, to], queryFn: () => api.get<BlockOff[]>('/tours/block-offs', { from, to }) })

export const useAddBlockOff = () =>
  useEnquiryMutation((body: { staff_id: string | null; starts_at: string; ends_at: string; reason: string | null }) => api.post<BlockOff>('/tours/block-offs', body))

export const useDeleteBlockOff = () => useEnquiryMutation((id: string) => api.delete(`/tours/block-offs/${id}`))

export const useTasks = (done: boolean) => useInfiniteList<FollowUpTask>(['tasks'], '/follow-up-tasks', { done: done ? 'true' : undefined })

export const useCompleteTask = () => useEnquiryMutation((id: string) => api.post<FollowUpTask>(`/follow-up-tasks/${id}/actions/complete`))

// ── Settings and roles ──────────────────────────────────────────────────────

export const useCentreSettings = () => useQuery({ queryKey: ['centre-settings'], queryFn: () => api.get<CentreSettings>('/settings/centre') })

export function useSaveCentreSettings() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: CentreSettings) => api.put<CentreSettings>('/settings/centre', body),
    onSuccess: () => Promise.all(['centre-settings', 'invoices'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useRoles = (enabled = true) => useQuery({ queryKey: ['roles'], enabled, queryFn: () => api.get<RoleItem[]>('/roles') })

export const usePermissionCatalogue = (enabled = true) => useQuery({ queryKey: ['permission-catalogue'], enabled, queryFn: () => api.get<string[]>('/roles/permissions') })

export function useSaveRole(id?: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { display_name: Record<string, string>; description: string | null; permissions: string[] }) =>
      id ? api.patch<RoleItem>(`/roles/${id}`, body) : api.post<RoleItem>('/roles', body),
    onSuccess: () => client.invalidateQueries({ queryKey: ['roles'] }),
  })
}

export function useDeleteRole() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/roles/${id}`),
    onSuccess: () => client.invalidateQueries({ queryKey: ['roles'] }),
  })
}

// ── Calendar and tools ──────────────────────────────────────────────────────

export const useCalendarEvents = (from: string, to: string, roomId?: string) =>
  useQuery({ queryKey: ['calendar', 'events', from, to, roomId ?? ''], queryFn: () => api.get<CalendarOccurrence[]>('/calendar/events', { from, to, room_id: roomId }) })

export const useAgenda = (from: string, to: string, layers: string[], mine: boolean, enabled = true) =>
  useQuery({
    queryKey: ['calendar', 'agenda', from, to, layers.join(','), mine],
    enabled,
    queryFn: () => api.get<AgendaItem[]>('/calendar/agenda', { from, to, layers: layers.join(','), mine }),
  })

export const useSchedules = (enabled = true) => useQuery({ queryKey: ['calendar', 'schedules'], enabled, queryFn: () => api.get<ScheduleData>('/calendar/schedules') })

export const useCalendarEvent = (id: string | null) =>
  useQuery({ queryKey: ['calendar', 'event', id], enabled: !!id, queryFn: () => api.get<CalendarEventDetail>(`/calendar/events/${id}`) })

export interface CalendarEventInput {
  title: Record<string, string>
  description: Record<string, string> | null
  starts_at: string
  ends_at: string
  is_all_day: boolean
  type: string
  room_id: string | null
  visibility: string
  recurrence: string | null
}

export function useSaveCalendarEvent(id?: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: CalendarEventInput) => (id ? api.patch<CalendarEventDetail>(`/calendar/events/${id}`, body) : api.post<CalendarEventDetail>('/calendar/events', body)),
    onSuccess: () => client.invalidateQueries({ queryKey: ['calendar'] }),
  })
}

export function useDeleteCalendarEvent() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/calendar/events/${id}`),
    onSuccess: () => client.invalidateQueries({ queryKey: ['calendar'] }),
  })
}

export const useFeedLink = (enabled = true) => useQuery({ queryKey: ['calendar', 'feed-link'], enabled, queryFn: () => api.get<{ path: string }>('/calendar/feed-link') })

export function useRotateFeedLink() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<{ path: string }>('/calendar/feed-link/actions/rotate'),
    onSuccess: () => client.invalidateQueries({ queryKey: ['calendar', 'feed-link'] }),
  })
}

export const useIdCardTemplates = () => useQuery({ queryKey: ['id-card-templates'], queryFn: () => api.get<IdCardTemplate[]>('/tools/id-card-templates') })

// ── Forms, admissions and certificates ──────────────────────────────────────

export const useForms = (status: string) => useInfiniteList<FormItem>(['forms'], '/forms', { status: status || undefined })

export const useForm = (id: string | null) => useQuery({ queryKey: ['form', id], enabled: !!id, queryFn: () => api.get<FormDetail>(`/forms/${id}`) })

export const useFillableForm = (id: string | null) => useQuery({ queryKey: ['form-fill', id], enabled: !!id, queryFn: () => api.get<FillableForm>(`/forms/${id}/fill`) })

function useFormMutation<TBody>(run: (body: TBody) => Promise<unknown>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: () => Promise.all(['forms', 'form', 'form-fill', 'form-submissions', 'form-analytics', 'admission-steps', 'admission-status', 'student-admission'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export interface FormInput {
  name: Record<string, string>
  description: Record<string, string> | null
  type: string
  is_public: boolean
}

export const useCreateForm = () => useFormMutation((body: FormInput) => api.post<FormDetail>('/forms', body))

export const useUpdateForm = (id: string) => useFormMutation((body: FormInput) => api.patch<FormDetail>(`/forms/${id}`, body))

export const useSaveFields = (id: string) => useFormMutation((fields: FormFieldDef[]) => api.put<FormDetail>(`/forms/${id}/fields`, { fields }))

export const usePublishForm = (id: string) => useFormMutation(() => api.post<FormDetail>(`/forms/${id}/actions/publish`))

export const useArchiveForm = (id: string) => useFormMutation((archive: boolean) => api.post<FormDetail>(`/forms/${id}/actions/${archive ? 'archive' : 'unarchive'}`))

export const useStartForm = () => useMutation({ mutationFn: (id: string) => api.post<{ start_id: string }>(`/forms/${id}/starts`) })

export const useSubmitForm = (id: string) =>
  useFormMutation((body: { answers: Record<string, unknown>; student_id: string | null; start_id: string | null }) => api.post<{ id: string }>(`/forms/${id}/submissions`, body))

export const useSubmissions = (id: string | null) => useInfiniteList<FormSubmissionItem>(['form-submissions', id], `/forms/${id}/submissions`, {}, !!id)

export const useFormAnalytics = (id: string | null) => useQuery({ queryKey: ['form-analytics', id], enabled: !!id, queryFn: () => api.get<FormAnalytics>(`/forms/${id}/analytics`) })

export const useAdmissionSteps = (enabled = true) => useQuery({ queryKey: ['admission-steps'], enabled, queryFn: () => api.get<AdmissionStepItem[]>('/admissions/steps') })

export const useSaveStep = (id?: string) =>
  useFormMutation((body: { name: Record<string, string>; form_id: string | null; due_days: number }) => (id ? api.patch<AdmissionStepItem[]>(`/admissions/steps/${id}`, body) : api.post<AdmissionStepItem[]>('/admissions/steps', body)))

export const useDeleteStep = () => useFormMutation((id: string) => api.delete(`/admissions/steps/${id}`))

export const useReorderSteps = () => useFormMutation((orderedIds: string[]) => api.put<AdmissionStepItem[]>('/admissions/steps/order', { ordered_ids: orderedIds }))

export const useAdmissionStatus = (enabled = true) => useQuery({ queryKey: ['admission-status'], enabled, queryFn: () => api.get<AdmissionStatus>('/admissions/status') })

export const useStudentAdmission = (id: string | null) => useQuery({ queryKey: ['student-admission', id], enabled: !!id, queryFn: () => api.get<StudentAdmission>(`/admissions/students/${id}`) })

export const useStartAdmission = () => useFormMutation((studentId: string) => api.post<StudentAdmission>(`/admissions/students/${studentId}/actions/start`))

export const useStepAction = () =>
  useFormMutation(({ studentId, stepId, action }: { studentId: string; stepId: string; action: 'complete' | 'skip' }) => api.post<StudentAdmission>(`/admissions/students/${studentId}/steps/${stepId}/actions/${action}`))

export const useCertificateTemplates = () => useQuery({ queryKey: ['certificate-templates'], queryFn: () => api.get<CertificateTemplateItem[]>('/tools/certificate-templates') })

export const useCertificateHistory = () => useInfiniteList<IssuedCertificate>(['certificates'], '/tools/certificates', {})

// ── Report centre and analytics ─────────────────────────────────────────────

export const useReportDefinitions = () => useQuery({ queryKey: ['report-definitions'], queryFn: () => api.get<ReportDefinition[]>('/reports/center/definitions') })

export const useReportRuns = () =>
  useInfiniteQuery({
    queryKey: ['report-runs'],
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }): Promise<Page<ReportRun>> => api.page<ReportRun>('/reports/center/runs', { cursor: pageParam, limit: PAGE_SIZE }),
    getNextPageParam: (last) => (last.pagination.has_more ? (last.pagination.next_cursor ?? undefined) : undefined),
    // Reports are made in the background, so keep looking while any is still being made.
    refetchInterval: (query) => (query.state.data?.pages.some((p) => p.items.some((r) => r.status === 'pending' || r.status === 'running')) ? 2000 : false),
  })

export function useRequestReport() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { kind: string; format: string; parameters: Record<string, string>; language: string }) => api.post<ReportRun>('/reports/center/runs', body),
    onSuccess: () => client.invalidateQueries({ queryKey: ['report-runs'] }),
  })
}

export const useAnalytics = (from: string, to: string) =>
  useQuery({ queryKey: ['analytics', from, to], queryFn: () => api.get<Analytics>('/reports/analytics', { from, to }) })

// ── Newsletters and resources ───────────────────────────────────────────────

export const useNewsletters = (status: string, search: string) =>
  useInfiniteList<NewsletterItem>(['newsletters'], '/newsletters', { status: status || undefined, search: search || undefined })

export const useNewsletter = (id: string | null) =>
  useQuery({ queryKey: ['newsletter', id], enabled: !!id, queryFn: () => api.get<NewsletterDetail>(`/newsletters/${id}`) })

export const useNewsletterStats = (id: string | null) =>
  useQuery({ queryKey: ['newsletter-stats', id], enabled: !!id, queryFn: () => api.get<NewsletterStats>(`/newsletters/${id}/stats`) })

export const useMyNewsletters = (enabled = true) => useInfiniteList<MyNewsletter>(['my-newsletters'], '/newsletters/mine', {}, enabled)

export const useReadNewsletter = (id: string | null, language: string) =>
  useQuery({ queryKey: ['read-newsletter', id, language], enabled: !!id, queryFn: () => api.get<ReadNewsletter>(`/newsletters/${id}/read`, { language }) })

export interface NewsletterInput {
  title: Record<string, string>
  body: Record<string, string>
  audience_type: string
  audience_ids: string[]
}

function useNewsletterMutation<TBody>(run: (body: TBody) => Promise<unknown>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: () => Promise.all(['newsletters', 'newsletter', 'newsletter-stats', 'my-newsletters'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useSaveNewsletter = (id?: string) =>
  useNewsletterMutation((body: NewsletterInput) => (id ? api.patch<NewsletterDetail>(`/newsletters/${id}`, body) : api.post<NewsletterDetail>('/newsletters', body)))

export const useNewsletterAction = () =>
  useNewsletterMutation(({ id, action, at }: { id: string; action: 'publish' | 'unschedule' | 'archive' | 'schedule'; at?: string }) =>
    api.post<NewsletterDetail>(`/newsletters/${id}/actions/${action}`, action === 'schedule' ? { at } : undefined))

export const useDeleteNewsletter = () => useNewsletterMutation((id: string) => api.delete(`/newsletters/${id}`))

export const useDraftNewsletter = () => useMutation({ mutationFn: (language: string) => api.post<{ title: string; body: string; used_ai: boolean }>(`/newsletters/actions/draft?language=${language}`) })

export const useResourceFolders = () => useQuery({ queryKey: ['resource-folders'], queryFn: () => api.get<ResourceFolder[]>('/resources/folders') })

function useResourceMutation<TBody>(run: (body: TBody) => Promise<unknown>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: () => Promise.all(['resource-folders', 'resource-documents', 'resource-versions', 'resource-downloads'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useSaveFolder = (id?: string) =>
  useResourceMutation((body: { name: Record<string, string>; audience: string; parent_folder_id: string | null }) => (id ? api.patch<ResourceFolder[]>(`/resources/folders/${id}`, body) : api.post<ResourceFolder[]>('/resources/folders', body)))

export const useDeleteFolder = () => useResourceMutation((id: string) => api.delete(`/resources/folders/${id}`))

export const useResourceDocuments = (folderId: string, search: string) =>
  useInfiniteList<ResourceDocument>(['resource-documents'], '/resources/documents', { folder_id: folderId || undefined, search: search || undefined })

export const useUploadDocument = () =>
  useResourceMutation(({ file, ...fields }: { file: File; folder_id: string; title: string; description: string; tags: string }) => api.upload<ResourceDocument>('/resources/documents', file, fields))

export const useUploadVersion = () => useResourceMutation(({ id, file }: { id: string; file: File }) => api.upload<ResourceDocument>(`/resources/documents/${id}/versions`, file))

export const useUpdateDocument = (id: string) =>
  useResourceMutation((body: { title: string; description: string | null; tags: string | null; folder_id: string | null }) => api.patch<ResourceDocument>(`/resources/documents/${id}`, body))

export const useDeleteDocument = () => useResourceMutation((id: string) => api.delete(`/resources/documents/${id}`))

export const useResourceVersions = (id: string | null) => useQuery({ queryKey: ['resource-versions', id], enabled: !!id, queryFn: () => api.get<ResourceVersionItem[]>(`/resources/documents/${id}/versions`) })

export const useResourceDownloads = (id: string | null) => useQuery({ queryKey: ['resource-downloads', id], enabled: !!id, queryFn: () => api.get<ResourceDownloadItem[]>(`/resources/documents/${id}/downloads`) })

// ── Learning (curriculum and lessons) ───────────────────────────────────────

export const useFrameworks = () => useQuery({ queryKey: ['frameworks'], queryFn: () => api.get<FrameworkSummary[]>('/learning/frameworks') })

export const useFramework = (id: string) =>
  useQuery({ queryKey: ['framework', id], enabled: !!id, queryFn: () => api.get<FrameworkDetail>(`/learning/frameworks/${id}`) })

export function useSaveFramework() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { code: string; name: Record<string, string>; description?: Record<string, string> | null }) => api.post<FrameworkSummary>('/learning/frameworks', body),
    onSuccess: () => client.invalidateQueries({ queryKey: ['frameworks'] }),
  })
}

export function useImportFramework(id: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: unknown) => api.post<FrameworkDetail>(`/learning/frameworks/${id}/import`, body),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: ['frameworks'] }), client.invalidateQueries({ queryKey: ['framework', id] })]),
  })
}

export const useLessons = (filters: { frameworkId?: string; areaId?: string; ageBandId?: string; roomId?: string; search?: string; templates?: boolean }) =>
  useInfiniteList<Lesson>(['lessons'], '/learning/lessons', {
    templates: filters.templates ? 'true' : undefined,
    framework_id: filters.frameworkId || undefined, area_id: filters.areaId || undefined, age_band_id: filters.ageBandId || undefined,
    room_id: filters.roomId || undefined, search: filters.search || undefined,
  })

export function useSaveLesson(id?: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: LessonInput) => (id ? api.put<Lesson>(`/learning/lessons/${id}`, body) : api.post<Lesson>('/learning/lessons', body)),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: ['lessons'] }), client.invalidateQueries({ queryKey: ['frameworks'] })]),
  })
}

export function useDeleteLesson() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/learning/lessons/${id}`),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: ['lessons'] }), client.invalidateQueries({ queryKey: ['frameworks'] })]),
  })
}

export const useObservations = (filters: { studentId?: string; type?: string }) =>
  useInfiniteList<Observation>(['observations'], '/learning/observations', { student_id: filters.studentId || undefined, type: filters.type || undefined })

export function useSaveObservation() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: ObservationInput) => api.post<Observation>('/learning/observations', body),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: ['observations'] }), client.invalidateQueries({ queryKey: ['journey'] })]),
  })
}

export function useDeleteObservation() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/learning/observations/${id}`),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: ['observations'] }), client.invalidateQueries({ queryKey: ['journey'] })]),
  })
}

export const useAssessmentTemplates = () => useQuery({ queryKey: ['assessment-templates'], queryFn: () => api.get<AssessmentTemplate[]>('/learning/assessment-templates') })

export function useSaveTemplate() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: Record<string, string>; framework_id: string; age_band_id?: string | null; scoring_kind: string; milestone_ids: string[] }) => api.post<AssessmentTemplate>('/learning/assessment-templates', body),
    onSuccess: () => client.invalidateQueries({ queryKey: ['assessment-templates'] }),
  })
}

export const useAssessmentRuns = (filters: { studentId?: string }) => useInfiniteList<AssessmentRun>(['assessment-runs'], '/learning/assessments', { student_id: filters.studentId || undefined })

export const useAssessmentRun = (id: string) => useQuery({ queryKey: ['assessment-run', id], enabled: !!id, queryFn: () => api.get<AssessmentRun>(`/learning/assessments/${id}`) })

function useRunMutation<TBody>(run: (body: TBody) => Promise<AssessmentRun>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: (r) => {
      client.setQueryData(['assessment-run', r.id], r)
      return Promise.all([client.invalidateQueries({ queryKey: ['assessment-runs'] }), client.invalidateQueries({ queryKey: ['assessment-templates'] }), client.invalidateQueries({ queryKey: ['journey'] }), client.invalidateQueries({ queryKey: ['parent-reports'] })])
    },
  })
}

export const useStartRun = () => useRunMutation((body: { template_id: string; student_id: string; term: string }) => api.post<AssessmentRun>('/learning/assessments', body))
export const useSaveScores = (id: string) =>
  useRunMutation((body: { scores: { milestone_id: string; score: number; note?: string }[]; staff_summary?: Record<string, string>; parent_summary?: Record<string, string> }) => api.put<AssessmentRun>(`/learning/assessments/${id}/scores`, body))
export const useCompleteRun = (id: string) => useRunMutation(() => api.post<AssessmentRun>(`/learning/assessments/${id}/actions/complete`))
export const useApproveRun = (id: string) => useRunMutation(() => api.post<AssessmentRun>(`/learning/assessments/${id}/actions/approve`))

export const useJourney = (studentId: string) => useQuery({ queryKey: ['journey', studentId], enabled: !!studentId, queryFn: () => api.get<Journey>(`/learning/journey/${studentId}`) })
export const useParentReports = (studentId: string) => useQuery({ queryKey: ['parent-reports', studentId], enabled: !!studentId, queryFn: () => api.get<ParentReport[]>(`/learning/parent-reports/${studentId}`) })

export const useSystemOverview = () => useQuery({ queryKey: ['system-overview'], queryFn: () => api.get<SystemOverview>('/dashboard/system'), refetchInterval: 60_000 })

export const useSuggestMilestones = () =>
  useMutation({
    mutationFn: (q: { frameworkId: string; text: string; areaId?: string; ageBandId?: string }) =>
      api.get<MilestoneSuggestion[]>('/learning/suggest-milestones', { framework_id: q.frameworkId, text: q.text, area_id: q.areaId || undefined, age_band_id: q.ageBandId || undefined }),
  })

export const useLessonDraft = () =>
  useMutation({
    mutationFn: (q: { frameworkId: string; areaId?: string; ageBandId?: string; theme?: string; language: string }) =>
      api.get<LessonDraft>('/ai/lesson-draft', { framework_id: q.frameworkId, area_id: q.areaId || undefined, age_band_id: q.ageBandId || undefined, theme: q.theme || undefined, language: q.language }),
  })

export const useGroupOverview = (enabled = true) => useQuery({ queryKey: ['group-overview'], enabled, queryFn: () => api.get<GroupOverview>('/group/overview') })
export const useMyCentres = () => useQuery({ queryKey: ['my-centres'], queryFn: () => api.get<GroupCentre[]>('/group/my-centres') })
export const useGroupReport = (kind: string, lang: string) =>
  useQuery({ queryKey: ['group-report', kind, lang], queryFn: () => api.get<GroupReportTable>(`/group/reports/${kind}`, { lang }) })

export function useProvisionCentre() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; name_arabic?: string; slug: string }) => api.post<GroupCentre>('/group/centres', body),
    onSuccess: () => Promise.all(['group-overview', 'my-centres', 'group-report'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}

export const useAssignStaffToCentre = () =>
  useMutation({ mutationFn: (body: { email: string; centre_id: string; role: string }) => api.post<void>('/group/staff-assignments', body) })

export function useTransferStudent() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (v: { studentId: string; toCentreId: string; reason?: string }) => api.post<string>(`/group/students/${v.studentId}/transfer`, { to_centre_id: v.toCentreId, reason: v.reason }),
    onSuccess: () => Promise.all(['students', 'student-options', 'group-overview'].map((key) => client.invalidateQueries({ queryKey: [key] }))),
  })
}
