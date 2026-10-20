import type { components } from './schema'

/** Request bodies come straight from the generated schema (pnpm gen:api); responses are typed here because the API wraps them in an envelope. */
export type Schemas = components['schemas']
export type CreateStudentRequest = Schemas['CreateStudentCommand']
export type CreateStaffRequest = Schemas['CreateStaffCommand']
export type CreateRoomRequest = Schemas['CreateRoomCommand']
export type CreateProgramRequest = Schemas['CreateProgramCommand']
export type CreateGroupRequest = Schemas['CreateGroupCommand']
export type CreateLabelRequest = Schemas['CreateLabelCommand']
export type CreateUserRequest = Schemas['CreateUserCommand']
export type GuardianRequest = Schemas['GuardianFields']

export type Localized = Record<string, string>

export interface Pagination {
  limit: number
  has_more: boolean
  next_cursor: string | null
}

export interface Me {
  id: string
  email: string
  first_name: string | null
  last_name: string | null
  preferred_language: string
  roles: string[]
  permissions: string[]
  tenant: { id: string; slug: string; name: Localized }
}

export interface CentreChoice {
  id: string
  slug: string
  name: Localized
}

export interface Session {
  access_token: string
  expires_at: string
  me: Me
}

export interface StudentSummary {
  id: string
  first_name: string
  last_name: string
  first_name_arabic: string | null
  last_name_arabic: string | null
  date_of_birth: string
  gender: string | null
  room_id: string | null
  group_id: string | null
  program_id: string | null
  status: string
  barcode: string
  created_at: string
}

export interface StudentDetail extends Omit<StudentSummary, 'created_at'> {
  display_name: Localized | null
  key_person_staff_id: string | null
  status_changed_at: string
  status_reason: string | null
  start_date: string | null
  end_date: string | null
  nationality: string | null
  religion: string | null
  primary_language: string | null
  medical_notes: string | null
  allergy_info: string | null
  dietary_requirements: string | null
  custody_notes: string | null
  passport_number: string | null
  emirates_id: string | null
  label_ids: string[]
  created_at: string
}

export interface Guardian {
  id: string
  first_name: string
  last_name: string
  email: string
  phone: string | null
  relationship: string
  is_primary: boolean
  can_pickup: boolean
  receives_communications: boolean
  has_login: boolean
}

export interface StaffSummary {
  id: string
  first_name: string
  last_name: string
  email: string
  phone: string | null
  job_title: string | null
  status: string
  hire_date: string
  barcode: string
  created_at: string
}

export interface Room {
  id: string
  name: Localized
  capacity: number
  min_age_months: number
  max_age_months: number
  ratio_staff_to_student: number
  is_active: boolean
  student_count: number
  created_at: string
}

export interface Program {
  id: string
  name: Localized
  description: Localized | null
  days_per_week: number
  start_time: string
  end_time: string
  age_group: string
  is_active: boolean
  created_at: string
}

export interface Group {
  id: string
  name: Localized
  room_id: string
  created_at: string
}

export interface Label {
  id: string
  name: Localized
  color: string
  created_at: string
}

export interface UserAccount {
  id: string
  email: string
  first_name: string | null
  last_name: string | null
  preferred_language: string
  status: string
  roles: string[]
  last_login_at: string | null
  created_at: string
}

export interface AttendanceRow {
  id: string
  person_type: 'student' | 'staff'
  person_id: string
  person_name: string | null
  room_id: string | null
  date: string
  check_in_at: string | null
  check_in_method: string | null
  check_out_at: string | null
  check_out_method: string | null
  check_out_guardian_id: string | null
  status: 'pending' | 'present' | 'absent' | 'late' | 'early_leave'
  notes: string | null
  created_at: string
}

export interface RoomRatio {
  room_id: string
  name: Localized
  capacity: number
  ratio_staff_to_student: number
  students_present: number
  staff_present: number
  staff_required: number
  is_breach: boolean
}

export interface DashboardSummary {
  date: string
  active_students: number
  students_present: number
  students_absent: number
  students_not_yet_arrived: number
  students_on_leave: number
  active_staff: number
  staff_present: number
  rooms_in_breach: number
  pending_leave_requests: number
}

export interface LeaveType {
  id: string
  name: Localized
  applies_to: 'student' | 'staff' | 'both'
  requires_approval: boolean
  affects_attendance: boolean
  affects_ratio: boolean
  created_at: string
}

export interface LeaveRequest {
  id: string
  person_type: 'student' | 'staff'
  person_id: string
  person_name: string | null
  leave_type_id: string
  leave_type_name: Localized
  start_date: string
  end_date: string
  reason: string | null
  status: 'submitted' | 'approved' | 'rejected' | 'cancelled'
  approved_at: string | null
  approval_notes: string | null
  created_at: string
}

export interface ScanRequest {
  barcode?: string
  person_type?: 'student' | 'staff'
  person_id?: string
  room_id?: string
}

export interface CheckOutRequest extends ScanRequest {
  guardian_id?: string
  pickup_identity_verified?: boolean
}

export interface CreateLeaveRequest {
  person_type: 'student' | 'staff'
  person_id: string
  leave_type_id: string
  start_date: string
  end_date: string
  reason?: string
}

export interface CreateLeaveTypeRequest {
  name: Localized
  applies_to: 'student' | 'staff' | 'both'
  requires_approval: boolean
  affects_attendance: boolean
  affects_ratio: boolean
}
