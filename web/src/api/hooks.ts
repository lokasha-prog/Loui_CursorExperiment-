import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, authApi, type Page } from './client'
import type {
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
