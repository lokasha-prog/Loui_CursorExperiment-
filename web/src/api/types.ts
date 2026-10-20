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

export interface FeeComponent {
  id: string
  name: Localized
  component_type: 'tuition' | 'meal' | 'transport' | 'activity' | 'discount' | 'other'
  amount_fils: number
  tax_rate_percent: number | null
  sort_order: number
}

export interface FeePlan {
  id: string
  name: Localized
  description: Localized | null
  status: 'active' | 'inactive'
  period: 'monthly' | 'termly' | 'annual' | 'one_time'
  program_id: string | null
  components: FeeComponent[]
  total_per_period_fils: number
  student_count: number
  created_at: string
}

export interface FeePlanDetail {
  plan: FeePlan
  students: { student_id: string; name: string; effective_from: string; effective_to: string | null }[]
}

export interface ComponentInput {
  name: Localized
  component_type: string
  amount_fils: number
  tax_rate_percent: number | null
}

export interface InvoiceSummary {
  id: string
  invoice_number: string
  student_id: string
  student_name: string | null
  issue_date: string
  due_date: string
  period_start: string
  period_end: string
  total_fils: number
  paid_fils: number
  status: 'draft' | 'issued' | 'partially_paid' | 'paid' | 'overdue' | 'cancelled'
  created_at: string
}

export interface InvoiceDetail {
  invoice: InvoiceSummary
  fee_plan_id: string | null
  subtotal_fils: number
  discount_fils: number
  tax_fils: number
  notes: Localized | null
  sent_at: string | null
  lines: { id: string; description: Localized; quantity: number; unit_price_fils: number; discount_fils: number; tax_fils: number; total_fils: number }[]
  payments: { id: string; amount_fils: number; payment_date: string; method: string; reference: string | null; notes: string | null; recorded_by_name: string | null; created_at: string }[]
}

export interface BillingSnapshot {
  month: string
  billed_fils: number
  collected_fils: number
  overdue_fils: number
  outstanding_fils: number
  expenses_fils: number
  net_fils: number
  new_invoices_today: number
  new_invoices_today_fils: number
  recent_payments: { invoice_id: string; invoice_number: string; student_name: string | null; amount_fils: number; payment_date: string; method: string }[]
  top_overdue: { student_id: string; student_name: string | null; outstanding_fils: number; invoices: number }[]
  trend: { month: string; billed_fils: number; collected_fils: number }[]
}

export interface Expense {
  id: string
  expense_date: string
  amount_fils: number
  category: string
  description: string
  receipt_media_id: string | null
  created_at: string
}

export interface ExpenseReport {
  month: string
  total_fils: number
  by_category: { category: string; total_fils: number; count: number }[]
  items: Expense[]
}

export interface RevenueReport {
  month: string
  billed_fils: number
  collected_fils: number
  expenses_fils: number
  net_fils: number
  by_method: { method: string; total_fils: number; count: number }[]
  trend: { month: string; billed_fils: number; collected_fils: number }[]
}

export interface OverdueReport {
  total_fils: number
  rows: { invoice_id: string; invoice_number: string; student_id: string; student_name: string | null; contact: string | null; due_date: string; days_late: number; outstanding_fils: number }[]
}

export interface BillingSettings {
  due_days: number
  reminders_enabled: boolean
  before_due_days: number[]
  after_due_days: number[]
  before_subject: Record<string, string>
  before_body: Record<string, string>
  overdue_subject: Record<string, string>
  overdue_body: Record<string, string>
}

export interface ReminderLogItem {
  id: string
  invoice_id: string
  invoice_number: string
  student_name: string | null
  kind: 'before_due' | 'overdue'
  offset_days: number
  recipients: number
  status: string
  created_at: string
}

export interface Enquiry {
  id: string
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
  assigned_staff_name: string | null
  stage: string
  enquiry_date: string
  last_contact_date: string | null
  notes: string | null
  is_archived: boolean
  student_id: string | null
  created_at: string
}

export interface EnquiryDetail {
  enquiry: Enquiry
  history: { from_stage: string | null; to_stage: string; reason: string | null; at: string }[]
  activities: { id: string; kind: string; summary: string; at: string }[]
}

export interface EnquirySummary {
  total: number
  by_stage: { stage: string; count: number }[]
  by_source: { source: string; total: number; enrolled: number }[]
  new_this_month: number
  enrolled: number
}

export interface Tour {
  id: string
  enquiry_id: string
  family_name: string
  child_name: string
  staff_id: string | null
  staff_name: string | null
  starts_at: string
  duration_minutes: number
  tour_type: string
  adults: number
  children: number
  notes: string | null
  status: string
}

export interface BlockOff {
  id: string
  staff_id: string | null
  staff_name: string | null
  starts_at: string
  ends_at: string
  reason: string | null
}

export interface FollowUpTask {
  id: string
  title: string
  due_date: string
  assigned_staff_id: string | null
  assigned_staff_name: string | null
  enquiry_id: string | null
  family_name: string | null
  is_done: boolean
  created_at: string
}

export interface PublicForm {
  name: Record<string, string>
  captcha_site_key: string | null
}

export interface RoleItem {
  id: string
  name: string
  display_name: Record<string, string>
  description: string | null
  is_system_role: boolean
  permissions: string[]
  user_count: number
}

export interface CentreSettings {
  name: Record<string, string>
  contact_email: string | null
  contact_phone: string | null
  khda_license_number: string | null
  weekend_days: string[]
  timezone: string
  address: string | null
  website: string | null
  brand_color: string | null
  logo_media_id: string | null
  late_grace_minutes: number
  early_leave_grace_minutes: number
  parents_see_invoices: boolean
  sms_enabled: boolean
  sms_monthly_quota: number
  sms_available: boolean
  sms_used_this_month: number
}

export interface CalendarOccurrence {
  event_id: string
  title: Record<string, string>
  description: Record<string, string> | null
  starts_at: string
  ends_at: string
  is_all_day: boolean
  type: string
  room_id: string | null
  visibility: string
  repeats: boolean
}

export interface CalendarEventDetail {
  id: string
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

export interface AgendaItem {
  kind: string
  label: string | null
  title: Record<string, string> | null
  start: string
  end: string
  ref_id: string | null
  detail: string | null
}

export interface ScheduleData {
  staff: { staff_id: string; name: string; shifts: { day_of_week: number; room_id: string; room_name: string | null }[] }[]
  programs: { program_id: string; name: Record<string, string>; start_time: string; end_time: string; days_per_week: number; students: number }[]
}

export interface IdCardTemplate {
  id: string
  scope: string
  name: Record<string, string>
  layout: string
  primary: string
  secondary: string
  accent: string
}

export interface FormOption {
  value: string
  label: Record<string, string>
}

export interface FormFieldDef {
  id: string
  type: string
  label: Record<string, string>
  help?: Record<string, string> | null
  required: boolean
  options: FormOption[]
  show_when_field_id: string | null
  show_when_equals: string | null
}

export interface FormItem {
  id: string
  name: Record<string, string>
  description: Record<string, string> | null
  type: string
  status: string
  is_public: boolean
  published_version: number
  submissions: number
  created_at: string
}

export interface FormDetail {
  form: FormItem
  editing_version: number
  has_unpublished_changes: boolean
  fields: FormFieldDef[]
  published_fields: FormFieldDef[]
}

export interface FillableForm {
  id: string
  name: Record<string, string>
  description: Record<string, string> | null
  type: string
  version: number
  fields: FormFieldDef[]
}

export interface FormSubmissionItem {
  id: string
  version: number
  student_id: string | null
  student_name: string | null
  enquiry_id: string | null
  answers: Record<string, unknown>
  duration_seconds: number | null
  created_at: string
}

export interface FormAnalytics {
  starts: number
  submissions: number
  completion_rate: number
  average_seconds: number
  drop_offs: { field_id: string; count: number }[]
}

export interface AdmissionStepItem {
  id: string
  name: Record<string, string>
  sort_order: number
  form_id: string | null
  form_name: string | null
  due_days: number
}

export interface StudentAdmission {
  student_id: string
  student_name: string
  done: number
  total: number
  steps: { step_id: string; name: Record<string, string>; form_id: string | null; status: string; due_date: string | null; completed_at: string | null; overdue: boolean }[]
}

export interface AdmissionStatus {
  steps: { step_id: string; name: Record<string, string>; current: number; overdue: number; done: number; total: number; next_due: string | null }[]
  children: { student_id: string; student_name: string; done: number; total: number; current_step: string | null; due_date: string | null; overdue: boolean }[]
}

export interface CertificateTemplateItem {
  id: string
  code: string
  name: Record<string, string>
  layout: string
  primary: string
  accent: string
  default_title: Record<string, string>
  default_body: Record<string, string>
}

export interface IssuedCertificate {
  id: string
  template_id: string
  number: string
  student_id: string
  student_name: string | null
  title: string
  issued_on: string
  created_at: string
}

export interface ReportDefinition {
  kind: string
  name: Record<string, string>
  formats: string[]
  filters: string[]
  permission: string
}

export interface ReportRun {
  id: string
  kind: string
  format: string
  status: 'pending' | 'running' | 'done' | 'failed'
  error: string | null
  file_name: string | null
  parameters: Record<string, string | null>
  created_at: string
  completed_at: string | null
  expires_at: string
}

export interface Analytics {
  from: string
  to: string
  attendance_rate: { label: string; value: number }[]
  attendance_by_status: { label: string; count: number }[]
  revenue: { month: string; billed_fils: number; collected_fils: number; expenses_fils: number }[] | null
  invoice_status: { label: string; count: number }[] | null
  enquiries_by_month: { label: string; count: number }[] | null
  enrolled_by_month: { label: string; count: number }[] | null
  enquiries_by_source: { label: string; count: number }[] | null
}
