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
  photo_media_id: string | null
  id_photo_media_id: string | null
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
  emirates_id: string | null
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
  min_age_months: number
  max_age_months: number
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

export interface AttendanceInsights {
  summary: string
  days: number
  language: 'en' | 'ar'
  model: string
  generated_at: string
  shortage_events: number
  approved_leave_requests: number
}

export interface MonthlyRow {
  person_id: string
  name: string
  present: number
  late: number
  early_leave: number
  absent: number
  days_recorded: number
}

export interface MonthlyReport {
  month: string
  person_type: 'student' | 'staff'
  rows: MonthlyRow[]
}

export interface Forecast {
  date: string
  total_expected: number
  by_program: { program_id: string | null; program_name: Localized | null; expected: number; on_leave: number; enrolled: number }[]
}

export interface RatioBreach {
  id: string
  room_id: string
  room_name: Localized | null
  detected_at: string
  resolved_at: string | null
  students_present: number
  staff_present: number
  staff_required: number
  created_at: string
}

export type ActivityType = 'announcement' | 'photo' | 'video' | 'diary' | 'food' | 'toilet' | 'medical' | 'mood'

export interface ActivityPost {
  id: string
  type: ActivityType
  caption: Localized | null
  posted_by_user_id: string
  posted_by_name: string | null
  room_id: string | null
  student_ids: string[]
  media: { id: string; content_type: string }[]
  status: 'pending' | 'approved' | 'rejected'
  rejection_reason: string | null
  created_at: string
}

export interface MediaItem {
  id: string
  file_name: string
  content_type: string
  size_bytes: number
}

export interface DailyReport {
  id: string
  student_id: string
  student_name: string
  date: string
  activity_count: number
  check_in_at: string | null
  check_out_at: string | null
  summary: Localized
  created_at: string
}

export interface MessageThread {
  id: string
  subject: string
  student_id: string | null
  is_broadcast: boolean
  last_message_preview: string | null
  last_sender_name: string | null
  last_message_at: string
  unread: boolean
  created_at: string
}

export interface ThreadMessage {
  id: string
  sender_user_id: string
  sender_name: string | null
  from_staff: boolean
  body: string
  created_at: string
}

export interface ThreadDetail {
  id: string
  subject: string
  student_id: string | null
  is_broadcast: boolean
  can_reply: boolean
  messages: ThreadMessage[]
}

export interface AppNotification {
  id: string
  tier: 'critical' | 'operational' | 'informational'
  type: string
  title: Localized
  body: Localized
  link: string | null
  read: boolean
  created_at: string
}

export interface NotificationPreference {
  tier: 'critical' | 'operational' | 'informational'
  in_app: boolean
  email: boolean
}

export interface AuditEntry {
  id: string
  user_id: string | null
  user_email: string | null
  entity_type: string
  entity_id: string
  action: string
  changed_fields: Record<string, { before: unknown; after: unknown }> | null
  ip_address: string | null
  occurred_at: string
  created_at: string
}

export interface RetentionPolicy {
  retention_days: number
  auto_purge_enabled: boolean
  min_retention_days: number
  total_entries: number
  oldest_entry_at: string | null
  eligible_for_purge: number
  cutoff: string
  recent_runs: { run_at: string; mode: 'auto' | 'manual'; cutoff: string; rows_deleted: number }[]
}

export interface FeedbackItem {
  id: string
  user_id: string
  user_name: string | null
  kind: 'problem' | 'idea' | 'praise'
  message: string
  page: string | null
  rating: number | null
  status: 'new' | 'seen' | 'done'
  created_at: string
}
