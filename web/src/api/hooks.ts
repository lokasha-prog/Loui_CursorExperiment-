import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, authApi, type Page } from './client'
import type {
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
