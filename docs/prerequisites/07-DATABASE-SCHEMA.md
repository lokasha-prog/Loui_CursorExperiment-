# 07 — Database Schema

> PostgreSQL 16 schema design: entities, RLS for multi-tenancy, partitioning, JSONB i18n, audit log, indexing strategy.

---

## 1. Design Principles

### 1.1 Universal Columns

Every table includes:
- `id` — UUID v4 primary key (avoids sequential ID enumeration attacks, simplifies distributed ID generation).
- `tenant_id` — UUID, foreign key to `tenant` table, NEVER NULL (even on on-prem single-tenant — same value throughout).
- `created_at` — `timestamptz`, NEVER NULL, defaults to `now()`.
- `updated_at` — `timestamptz`, NEVER NULL, defaults to `now()`, updated via trigger on UPDATE.
- `created_by` — UUID, foreign key to `app_user`, NEVER NULL (set from JWT `sub` claim).
- `updated_by` — UUID, foreign key to `app_user`, NEVER NULL.
- `is_deleted` — boolean, default false (soft-delete for persistent entities).
- `deleted_at` — `timestamptz`, nullable (set when soft-deleted).

### 1.2 Soft Deletes

- **Soft-deleted** (have `is_deleted` + `deleted_at`): Student, Staff, Room, Program, Group, Guardian, FeePlan, Invoice, Activity, Message, Observation, Assessment, Form, FormSubmission, Enquiry, TourAppointment, CalendarEvent.
- **Hard-deleted** (no soft-delete): Session, RefreshToken, NotificationDelivery, AttendanceEvent (immutable, never deleted), AuditLog (immutable, never deleted), temporary data (PresignedUrl, UploadJob).
- EF Core global query filter: `modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted)` — applies automatically to all reads.

### 1.3 Multi-Tenancy via RLS

Every table has `tenant_id` column + RLS policy:

```sql
ALTER TABLE student ENABLE ROW LEVEL SECURITY;
ALTER TABLE student FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON student
  USING (tenant_id = current_setting('app.tenant_id')::uuid);
```

Application sets `app.tenant_id` per request via `SET LOCAL app.tenant_id = '<uuid>'` after extracting `tenant_id` from JWT.

EF Core global query filter adds defense-in-depth:
```csharp
modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
```

If a bug in EF Core filter allows a row to leak, RLS rejects it at the DB layer.

### 1.4 JSONB for i18n Fields

All user-facing translatable strings stored as JSONB:
```sql
display_name JSONB NOT NULL DEFAULT '{"en": "", "ar": ""}'
```

Read pattern:
```sql
SELECT display_name->>'en' as display_name_en,
       display_name->>'ar' as display_name_ar
FROM room WHERE id = $1;
```

Or dynamic per user's locale:
```sql
SELECT display_name->>$1 as display_name
FROM room WHERE id = $2;
```

Default fallback to English if locale key missing (handled in application layer).

### 1.5 Money as Integer Cents (AED Fils)

All monetary amounts stored as `bigint` integer fils (1 AED = 100 fils). NEVER `numeric`, `decimal`, or `float` — floating-point precision errors are unacceptable in financial data.

```sql
amount_fils BIGINT NOT NULL  -- e.g., 25000 = 250.00 AED
```

Display layer formats to decimal: `25000 / 100 = 250.00 AED`.

### 1.6 Immutable Records

`attendance_event` and `audit_log` are immutable:
- `attendance_event` — every check-in/out scan logged; never UPDATE or DELETE.
- `audit_log` — every mutation logged; enforced append-only via DB trigger.

```sql
CREATE OR REPLACE FUNCTION prevent_update_delete()
RETURNS TRIGGER AS $$
BEGIN
  RAISE EXCEPTION 'Table % is append-only. UPDATE and DELETE are not permitted.', TG_TABLE_NAME;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER audit_log_immutable
  BEFORE UPDATE OR DELETE ON audit_log
  FOR EACH ROW EXECUTE FUNCTION prevent_update_delete();
```

### 1.7 Partitioning

`attendance_record` and `audit_log` partitioned by month (PostgreSQL declarative range partitioning):

```sql
CREATE TABLE attendance_record (
  id UUID NOT NULL,
  tenant_id UUID NOT NULL,
  student_id UUID NOT NULL,
  -- ...
  check_in_at timestamptz NOT NULL,
  PRIMARY KEY (id, check_in_at)
) PARTITION BY RANGE (check_in_at);

CREATE TABLE attendance_record_2026_05 PARTITION OF attendance_record
  FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');

-- Future partitions created by scheduled job (pg_partman or custom)
```

Older partitions can be archived to cold storage (S3) after retention period.

---

## 2. Core Entities

### 2.1 Tenant & Identity

#### `tenant`
```sql
CREATE TABLE tenant (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name JSONB NOT NULL,  -- {"en": "Bright Futures Nursery", "ar": "..."}
  slug TEXT NOT NULL UNIQUE,  -- URL-friendly identifier, e.g., "bright-futures"
  deployment_mode TEXT NOT NULL CHECK (deployment_mode IN ('onprem', 'saas')),
  parent_tenant_id UUID NULL,  -- For multi-centre groups (Phase 3); MCG parent
  centre_type TEXT NULL,  -- 'single' | 'multi_centre_group'
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'suspended', 'archived')),
  subscription_tier TEXT NULL,  -- For SaaS: 'tier1' | 'tier2' | 'tier3' | 'tier4' | 'onprem'
  subscription_started_at timestamptz NULL,
  subscription_ends_at timestamptz NULL,
  branding JSONB NULL,  -- {"logo_url": "...", "primary_color": "#...", "secondary_color": "#..."}
  address JSONB NULL,  -- {"line1": "...", "city": "...", "country": "AE", ...}
  contact_email TEXT NULL,
  contact_phone TEXT NULL,
  khda_license_number TEXT NULL,  -- UAE-specific
  weekend_days TEXT[] NOT NULL DEFAULT '{"fri", "sat"}',  -- UAE default
  timezone TEXT NOT NULL DEFAULT 'Asia/Dubai',
  -- Universal columns
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by UUID NOT NULL,
  updated_by UUID NOT NULL,
  is_deleted boolean NOT NULL DEFAULT false,
  deleted_at timestamptz NULL
);
```

> Note: `tenant` table itself does NOT have `tenant_id` column (it IS the tenant).

#### `app_user` (mirror of Keycloak user)
```sql
CREATE TABLE app_user (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  keycloak_user_id TEXT NOT NULL UNIQUE,  -- Keycloak's user ID
  email TEXT NOT NULL,
  email_verified boolean NOT NULL DEFAULT false,
  first_name TEXT NULL,
  last_name TEXT NULL,
  display_name JSONB NULL,  -- If different from first+last (e.g., Arabic name)
  preferred_language TEXT NOT NULL DEFAULT 'en' CHECK (preferred_language IN ('en', 'ar')),
  phone TEXT NULL,
  phone_verified boolean NOT NULL DEFAULT false,
  avatar_url TEXT NULL,
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'suspended', 'invited')),
  last_login_at timestamptz NULL,
  -- Universal columns
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by UUID NOT NULL,
  updated_by UUID NOT NULL,
  is_deleted boolean NOT NULL DEFAULT false,
  deleted_at timestamptz NULL,
  UNIQUE (tenant_id, email)
);
```

#### `role` & `permission` & `role_permission`
Standard RBAC tables. Custom roles supported (Phase 1).

```sql
CREATE TABLE role (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  name TEXT NOT NULL,  -- 'super_admin' | 'centre_admin' | 'teacher' | 'parent' | 'receptionist' | 'accounts' | 'it' | 'marketing' | 'procurement' | custom
  display_name JSONB NOT NULL,
  description TEXT NULL,
  is_system_role boolean NOT NULL DEFAULT false,  -- System roles can't be deleted
  permissions JSONB NOT NULL DEFAULT '[]',  -- Array of permission strings
  -- Universal columns...
  UNIQUE (tenant_id, name)
);

CREATE TABLE app_user_role (
  app_user_id UUID NOT NULL REFERENCES app_user(id),
  role_id UUID NOT NULL REFERENCES role(id),
  tenant_id UUID NOT NULL,  -- Denormalized for RLS
  assigned_at timestamptz NOT NULL DEFAULT now(),
  assigned_by UUID NOT NULL,
  PRIMARY KEY (app_user_id, role_id)
);
```

### 2.2 Centre Management

#### `student`
```sql
CREATE TABLE student (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  first_name TEXT NOT NULL,
  last_name TEXT NOT NULL,
  display_name JSONB NULL,  -- Optional Arabic display name
  first_name_arabic TEXT NULL,
  last_name_arabic TEXT NULL,
  date_of_birth DATE NOT NULL,
  gender TEXT NULL CHECK (gender IN ('male', 'female', 'other', 'unspecified')),
  photo_media_id UUID NULL REFERENCES media(id),
  room_id UUID NULL REFERENCES room(id),
  group_id UUID NULL REFERENCES student_group(id),
  program_id UUID NULL REFERENCES program(id),
  key_person_staff_id UUID NULL REFERENCES staff(id),
  status TEXT NOT NULL DEFAULT 'signed_up'
    CHECK (status IN ('signed_up', 'active', 'on_hold', 'inactive', 'withdrawn', 'transferred')),
  status_changed_at timestamptz NOT NULL DEFAULT now(),
  status_reason TEXT NULL,
  start_date DATE NULL,
  end_date DATE NULL,  -- When withdrawn/transferred
  barcode TEXT NOT NULL UNIQUE,  -- For attendance scanning
  -- Sensitive (encrypted at column level via pgcrypto)
  medical_notes TEXT NULL,  -- Encrypted
  allergy_info TEXT NULL,  -- Encrypted
  dietary_requirements TEXT NULL,  -- Encrypted
  custody_notes TEXT NULL,  -- Encrypted
  safeguarding_flags JSONB NOT NULL DEFAULT '[]',  -- Encrypted
  nationality TEXT NULL,
  passport_number TEXT NULL,  -- Encrypted
  emirates_id TEXT NULL,  -- UAE-specific, encrypted
  religion TEXT NULL,
  primary_language TEXT NULL,
  -- Universal columns...
  UNIQUE (tenant_id, barcode)
);

CREATE INDEX idx_student_tenant_created ON student (tenant_id, created_at DESC);
CREATE INDEX idx_student_tenant_status ON student (tenant_id, status) WHERE is_deleted = false;
CREATE INDEX idx_student_room ON student (tenant_id, room_id) WHERE is_deleted = false;
```

#### `guardian`
```sql
CREATE TABLE guardian (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  app_user_id UUID NULL REFERENCES app_user(id),  -- NULL until they accept invitation
  first_name TEXT NOT NULL,
  last_name TEXT NOT NULL,
  display_name JSONB NULL,
  email TEXT NOT NULL,
  phone TEXT NULL,
  relationship TEXT NOT NULL CHECK (relationship IN ('mother', 'father', 'grandparent', 'legal_guardian', 'other')),
  is_primary_contact boolean NOT NULL DEFAULT false,
  can_pickup boolean NOT NULL DEFAULT true,
  receives_communications boolean NOT NULL DEFAULT true,
  nationality TEXT NULL,
  occupation TEXT NULL,
  employer TEXT NULL,
  -- Universal columns...
  UNIQUE (tenant_id, email)
);

CREATE TABLE student_guardian (
  student_id UUID NOT NULL REFERENCES student(id),
  guardian_id UUID NOT NULL REFERENCES guardian(id),
  tenant_id UUID NOT NULL,  -- Denormalized for RLS
  is_primary boolean NOT NULL DEFAULT false,
  PRIMARY KEY (student_id, guardian_id)
);
```

#### `staff`
```sql
CREATE TABLE staff (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  app_user_id UUID NULL REFERENCES app_user(id),
  first_name TEXT NOT NULL,
  last_name TEXT NOT NULL,
  display_name JSONB NULL,
  date_of_birth DATE NULL,
  gender TEXT NULL,
  photo_media_id UUID NULL REFERENCES media(id),
  email TEXT NOT NULL,
  phone TEXT NULL,
  job_title TEXT NULL,
  qualifications JSONB NOT NULL DEFAULT '[]',  -- [{name: "Cache Level 3", year: 2020, issued_by: "..."}]
  hire_date DATE NOT NULL,
  end_date DATE NULL,
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'on_leave', 'inactive', 'terminated')),
  barcode TEXT NOT NULL UNIQUE,
  -- Sensitive (encrypted)
  salary_amount_fils BIGINT NULL,  -- Encrypted
  emirates_id TEXT NULL,  -- Encrypted
  passport_number TEXT NULL,  -- Encrypted
  visa_info JSONB NULL,  -- Encrypted
  -- Universal columns...
  UNIQUE (tenant_id, barcode),
  UNIQUE (tenant_id, email)
);
```

#### `room` / `program` / `student_group` / `label`
```sql
CREATE TABLE room (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  name JSONB NOT NULL,  -- {"en": "Toddlers", "ar": "..."}
  capacity INT NOT NULL,
  min_age_months INT NOT NULL,
  max_age_months INT NOT NULL,
  ratio_staff_to_student INT NOT NULL,  -- e.g., 1:4 means 1 staff per 4 students
  is_active boolean NOT NULL DEFAULT true,
  -- Universal columns...
  UNIQUE (tenant_id, name)
);

CREATE TABLE program (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  name JSONB NOT NULL,
  description JSONB NULL,
  days_per_week INT NOT NULL,
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  age_group TEXT NOT NULL,
  fee_plan_id UUID NULL REFERENCES fee_plan(id),
  is_active boolean NOT NULL DEFAULT true,
  -- Universal columns...
);

CREATE TABLE student_group (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  name JSONB NOT NULL,
  room_id UUID NOT NULL REFERENCES room(id),
  -- Universal columns...
);

CREATE TABLE label (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL REFERENCES tenant(id),
  name JSONB NOT NULL,
  color TEXT NOT NULL,  -- Hex color
  -- Universal columns...
);

CREATE TABLE student_label (
  student_id UUID NOT NULL REFERENCES student(id),
  label_id UUID NOT NULL REFERENCES label(id),
  PRIMARY KEY (student_id, label_id)
);
```

#### `staff_room_assignment` (time-slot aware)
```sql
CREATE TABLE staff_room_assignment (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  staff_id UUID NOT NULL REFERENCES staff(id),
  room_id UUID NOT NULL REFERENCES room(id),
  day_of_week INT NOT NULL CHECK (day_of_week BETWEEN 0 AND 6),
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  effective_from DATE NOT NULL,
  effective_to DATE NULL,
  -- Universal columns...
);
```

### 2.3 Attendance

#### `attendance_record` (partitioned by month)
```sql
CREATE TABLE attendance_record (
  id UUID NOT NULL,
  tenant_id UUID NOT NULL,
  person_type TEXT NOT NULL CHECK (person_type IN ('student', 'staff')),
  person_id UUID NOT NULL,  -- student_id or staff_id (polymorphic, but typed)
  room_id UUID NULL,
  date DATE NOT NULL,
  check_in_at timestamptz NULL,
  check_in_by UUID NULL,  -- staff_id who checked them in
  check_in_method TEXT NULL CHECK (check_in_method IN ('barcode', 'manual', 'system_auto')),
  check_out_at timestamptz NULL,
  check_out_by UUID NULL,
  check_out_method TEXT NULL,
  check_out_guardian_id UUID NULL,  -- For students: who picked them up
  pickup_identity_verified boolean NULL,  -- Per SRS: verify pickup individual's identity
  status TEXT NOT NULL DEFAULT 'pending'
    CHECK (status IN ('pending', 'present', 'absent', 'late', 'early_leave', 'auto_absent')),
  notes TEXT NULL,
  -- Universal columns (no soft delete — attendance is immutable after 24h)
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by UUID NOT NULL,
  updated_by UUID NOT NULL,
  PRIMARY KEY (id, check_in_at)
) PARTITION BY RANGE (check_in_at);

CREATE INDEX idx_attendance_tenant_date ON attendance_record (tenant_id, date DESC);
CREATE INDEX idx_attendance_person ON attendance_record (tenant_id, person_type, person_id, date DESC);
CREATE INDEX idx_attendance_room_date ON attendance_record (tenant_id, room_id, date);
```

#### `attendance_event` (immutable scan log)
```sql
CREATE TABLE attendance_event (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  person_type TEXT NOT NULL,
  person_id UUID NULL,  -- NULL if scan failed (e.g., barcode not found)
  barcode_scanned TEXT NOT NULL,
  event_type TEXT NOT NULL CHECK (event_type IN ('check_in', 'check_out', 'failed_lookup', 'already_checked_in', 'already_checked_out')),
  scanned_at timestamptz NOT NULL DEFAULT now(),
  scanned_by UUID NOT NULL,  -- staff_id who scanned
  scanner_device TEXT NULL,  -- Device identifier
  ip_address INET NULL,
  outcome TEXT NOT NULL CHECK (outcome IN ('success', 'warning', 'error')),
  message TEXT NULL,
  -- No UPDATE/DELETE allowed (enforced by trigger)
  -- No soft delete
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_attendance_event_tenant_date ON attendance_event (tenant_id, scanned_at DESC);
CREATE INDEX idx_attendance_event_person ON attendance_event (tenant_id, person_id, scanned_at DESC);
```

#### `leave_request` & `leave_type`
```sql
CREATE TABLE leave_type (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name JSONB NOT NULL,  -- {"en": "Sick", "ar": "..."}
  applies_to TEXT NOT NULL CHECK (applies_to IN ('student', 'staff', 'both')),
  requires_approval boolean NOT NULL DEFAULT true,
  affects_attendance boolean NOT NULL DEFAULT true,  -- Auto-mark absent if true
  affects_ratio boolean NOT NULL DEFAULT false,  -- For staff: excluded from ratio if true
  -- Universal columns...
);

CREATE TABLE leave_request (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  person_type TEXT NOT NULL CHECK (person_type IN ('student', 'staff')),
  person_id UUID NOT NULL,
  leave_type_id UUID NOT NULL REFERENCES leave_type(id),
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  reason TEXT NULL,
  status TEXT NOT NULL DEFAULT 'submitted' CHECK (status IN ('submitted', 'approved', 'rejected', 'cancelled')),
  approved_by UUID NULL,
  approved_at timestamptz NULL,
  approval_notes TEXT NULL,
  -- Universal columns...
);

CREATE INDEX idx_leave_request_tenant_status ON leave_request (tenant_id, status) WHERE is_deleted = false;
CREATE INDEX idx_leave_request_person ON leave_request (tenant_id, person_type, person_id, start_date DESC);
```

### 2.4 Media & Activities

#### `media`
```sql
CREATE TABLE media (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  uploaded_by UUID NOT NULL REFERENCES app_user(id),
  bucket_name TEXT NOT NULL,
  object_key TEXT NOT NULL,  -- S3 key
  content_type TEXT NOT NULL,
  size_bytes BIGINT NOT NULL,
  width INT NULL,  -- For images/videos
  height INT NULL,
  duration_seconds INT NULL,  -- For videos/audio
  thumbnail_object_key TEXT NULL,
  scan_status TEXT NOT NULL DEFAULT 'pending'
    CHECK (scan_status IN ('pending', 'approved', 'quarantined', 'scan_failed')),
  scan_started_at timestamptz NULL,
  scan_completed_at timestamptz NULL,
  scan_result JSONB NULL,  -- {clamav: "clean", rekognition: {...}, reasons: [...]}
  quarantined_object_key TEXT NULL,  -- Moved to quarantine bucket if applicable
  -- Universal columns...
);

CREATE INDEX idx_media_tenant_status ON media (tenant_id, scan_status);
CREATE INDEX idx_media_uploaded_by ON media (tenant_id, uploaded_by, created_at DESC);
```

#### `activity_post` (parent communication)
```sql
CREATE TABLE activity_post (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  type TEXT NOT NULL CHECK (type IN ('announcement', 'photo', 'video', 'diary', 'food', 'toilet', 'medical', 'mood')),
  caption JSONB NULL,  -- {"en": "...", "ar": "..."}
  posted_by_staff_id UUID NOT NULL REFERENCES staff(id),
  room_id UUID NULL REFERENCES room(id),
  requires_approval boolean NOT NULL DEFAULT true,
  approval_status TEXT NOT NULL DEFAULT 'pending'
    CHECK (approval_status IN ('pending', 'approved', 'rejected', 'needs_manual_review')),
  approved_by UUID NULL,
  approved_at timestamptz NULL,
  rejection_reason TEXT NULL,
  published_at timestamptz NULL,  -- When visible to parents (after approval)
  -- For targeted posts
  is_room_wide boolean NOT NULL DEFAULT false,  -- True = all students in room; false = specific students
  -- Universal columns...
);

CREATE TABLE activity_post_student (
  activity_post_id UUID NOT NULL REFERENCES activity_post(id),
  student_id UUID NOT NULL REFERENCES student(id),
  tenant_id UUID NOT NULL,
  PRIMARY KEY (activity_post_id, student_id)
);

CREATE TABLE activity_post_media (
  activity_post_id UUID NOT NULL REFERENCES activity_post(id),
  media_id UUID NOT NULL REFERENCES media(id),
  sort_order INT NOT NULL DEFAULT 0,
  PRIMARY KEY (activity_post_id, media_id)
);

CREATE INDEX idx_activity_post_tenant_published ON activity_post (tenant_id, published_at DESC)
  WHERE is_deleted = false AND approval_status = 'approved';
CREATE INDEX idx_activity_post_student ON activity_post_student (student_id);
```

#### `daily_report`
```sql
CREATE TABLE daily_report (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  student_id UUID NOT NULL REFERENCES student(id),
  report_date DATE NOT NULL,
  generated_at timestamptz NOT NULL DEFAULT now(),
  pdf_media_id UUID NULL REFERENCES media(id),
  content JSONB NOT NULL,  -- {check_in: ..., check_out: ..., food: [...], toilet: [...], nap: ..., mood: ..., incidents: [...], activities: [...]}
  -- Universal columns...
  UNIQUE (tenant_id, student_id, report_date)
);
```

### 2.5 Messaging

#### `message_thread` & `message` & `message_read_receipt`
```sql
CREATE TABLE message_thread (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  thread_type TEXT NOT NULL CHECK (thread_type IN ('parent_staff', 'staff_staff', 'broadcast', 'group')),
  subject TEXT NULL,
  student_id UUID NULL REFERENCES student(id),  -- For parent-staff threads
  room_id UUID NULL REFERENCES room(id),
  is_archived boolean NOT NULL DEFAULT false,
  last_message_at timestamptz NOT NULL DEFAULT now(),
  -- Universal columns...
);

CREATE TABLE message (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  thread_id UUID NOT NULL REFERENCES message_thread(id),
  sender_id UUID NOT NULL REFERENCES app_user(id),
  sender_type TEXT NOT NULL CHECK (sender_type IN ('staff', 'parent', 'system')),
  body TEXT NOT NULL,  -- Plain text or markdown
  body_i18n JSONB NULL,  -- {en: ..., ar: ...} if sender wrote in multiple languages
  parent_message_id UUID NULL REFERENCES message(id),  -- For threaded replies
  -- Universal columns...
);

CREATE TABLE message_read_receipt (
  message_id UUID NOT NULL REFERENCES message(id),
  user_id UUID NOT NULL REFERENCES app_user(id),
  tenant_id UUID NOT NULL,
  read_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (message_id, user_id)
);

CREATE TABLE thread_participant (
  thread_id UUID NOT NULL REFERENCES message_thread(id),
  user_id UUID NOT NULL REFERENCES app_user(id),
  tenant_id UUID NOT NULL,
  joined_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (thread_id, user_id)
);

CREATE INDEX idx_message_thread_tenant_last_message ON message_thread (tenant_id, last_message_at DESC)
  WHERE is_deleted = false;
CREATE INDEX idx_message_thread_user ON thread_participant (user_id, thread_id);
```

### 2.6 Billing

#### `fee_plan` & `fee_component` & `invoice` & `invoice_payment`
```sql
CREATE TABLE fee_plan (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name JSONB NOT NULL,
  description JSONB NULL,
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'inactive')),
  period TEXT NOT NULL CHECK (period IN ('monthly', 'termly', 'annual', 'one_time')),
  program_id UUID NULL REFERENCES program(id),
  -- Universal columns...
);

CREATE TABLE fee_component (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  fee_plan_id UUID NOT NULL REFERENCES fee_plan(id),
  name JSONB NOT NULL,
  description JSONB NULL,
  amount_fils BIGINT NOT NULL CHECK (amount_fils >= 0),
  component_type TEXT NOT NULL CHECK (component_type IN ('tuition', 'meal', 'transport', 'activity', 'discount', 'tax', 'other')),
  tax_rate_percent NUMERIC(5,4) NULL,  -- NULL = no tax, otherwise 0.0500 for 5% VAT
  sort_order INT NOT NULL DEFAULT 0,
  -- Universal columns...
);

CREATE TABLE student_fee_plan (
  student_id UUID NOT NULL REFERENCES student(id),
  fee_plan_id UUID NOT NULL REFERENCES fee_plan(id),
  tenant_id UUID NOT NULL,
  effective_from DATE NOT NULL,
  effective_to DATE NULL,
  PRIMARY KEY (student_id, fee_plan_id, effective_from)
);

CREATE TABLE invoice (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  invoice_number TEXT NOT NULL,  -- e.g., "INV-2026-00001" — sequential per tenant
  student_id UUID NOT NULL REFERENCES student(id),
  fee_plan_id UUID NULL REFERENCES fee_plan(id),
  issue_date DATE NOT NULL,
  due_date DATE NOT NULL,
  period_start DATE NOT NULL,
  period_end DATE NOT NULL,
  subtotal_fils BIGINT NOT NULL,
  discount_fils BIGINT NOT NULL DEFAULT 0,
  tax_fils BIGINT NOT NULL DEFAULT 0,
  total_fils BIGINT NOT NULL,
  paid_fils BIGINT NOT NULL DEFAULT 0,
  status TEXT NOT NULL DEFAULT 'draft'
    CHECK (status IN ('draft', 'issued', 'partially_paid', 'paid', 'overdue', 'cancelled', 'void')),
  sent_at timestamptz NULL,
  pdf_media_id UUID NULL REFERENCES media(id),
  notes JSONB NULL,  -- {en: ..., ar: ...}
  -- Universal columns...
  UNIQUE (tenant_id, invoice_number)
);

CREATE TABLE invoice_line_item (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  invoice_id UUID NOT NULL REFERENCES invoice(id),
  description JSONB NOT NULL,
  quantity NUMERIC(10,2) NOT NULL DEFAULT 1,
  unit_price_fils BIGINT NOT NULL,
  discount_fils BIGINT NOT NULL DEFAULT 0,
  tax_fils BIGINT NOT NULL DEFAULT 0,
  total_fils BIGINT NOT NULL,
  fee_component_id UUID NULL REFERENCES fee_component(id),
  sort_order INT NOT NULL DEFAULT 0
);

CREATE TABLE invoice_payment (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  invoice_id UUID NOT NULL REFERENCES invoice(id),
  amount_fils BIGINT NOT NULL,
  payment_date DATE NOT NULL,
  method TEXT NOT NULL CHECK (method IN ('cash', 'cheque', 'bank_transfer', 'card', 'online', 'other')),
  reference TEXT NULL,  -- Cheque number, transaction ref, etc.
  gateway_transaction_id TEXT NULL,  -- Phase 3 payment gateway
  gateway_name TEXT NULL,
  recorded_by UUID NOT NULL REFERENCES app_user(id),
  notes TEXT NULL,
  -- Universal columns...
);

CREATE INDEX idx_invoice_tenant_status ON invoice (tenant_id, status) WHERE is_deleted = false;
CREATE INDEX idx_invoice_student ON invoice (tenant_id, student_id, issue_date DESC);
CREATE INDEX idx_invoice_due ON invoice (tenant_id, due_date) WHERE status IN ('issued', 'partially_paid');
```

#### `expense`
```sql
CREATE TABLE expense (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  date DATE NOT NULL,
  amount_fils BIGINT NOT NULL,
  category TEXT NOT NULL CHECK (category IN ('rent', 'utilities', 'salaries', 'supplies', 'food', 'marketing', 'maintenance', 'professional_services', 'other')),
  description TEXT NOT NULL,
  receipt_media_id UUID NULL REFERENCES media(id),
  recorded_by UUID NOT NULL REFERENCES app_user(id),
  -- Universal columns...
);

CREATE INDEX idx_expense_tenant_date ON expense (tenant_id, date DESC) WHERE is_deleted = false;
```

### 2.7 Notifications

#### `notification` & `notification_delivery`
```sql
CREATE TABLE notification (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  recipient_user_id UUID NOT NULL REFERENCES app_user(id),
  tier TEXT NOT NULL CHECK (tier IN ('critical', 'operational', 'informational')),
  category TEXT NOT NULL,  -- 'ratio_breach', 'new_message', 'invoice_created', etc.
  title JSONB NOT NULL,
  body JSONB NOT NULL,
  action_url TEXT NULL,  -- Deep link
  action_label JSONB NULL,
  related_entity_type TEXT NULL,
  related_entity_id UUID NULL,
  is_read boolean NOT NULL DEFAULT false,
  read_at timestamptz NULL,
  dismissed_at timestamptz NULL,
  expires_at timestamptz NULL,  -- Auto-purge old notifications
  -- Universal columns...
);

CREATE TABLE notification_delivery (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  notification_id UUID NOT NULL REFERENCES notification(id),
  channel TEXT NOT NULL CHECK (channel IN ('in_app', 'push', 'email', 'sms')),
  provider TEXT NULL,  -- 'fcm', 'ses', 'twilio', etc.
  provider_message_id TEXT NULL,
  status TEXT NOT NULL DEFAULT 'queued'
    CHECK (status IN ('queued', 'sent', 'delivered', 'failed', 'suppressed', 'cancelled')),
  attempt_count INT NOT NULL DEFAULT 0,
  last_attempt_at timestamptz NULL,
  delivered_at timestamptz NULL,
  failed_at timestamptz NULL,
  failure_reason TEXT NULL,
  provider_response JSONB NULL,
  -- Universal columns...
);

CREATE INDEX idx_notification_tenant_recipient ON notification (tenant_id, recipient_user_id, created_at DESC)
  WHERE is_deleted = false;
CREATE INDEX idx_notification_unread ON notification (tenant_id, recipient_user_id)
  WHERE is_read = false AND is_deleted = false;
CREATE INDEX idx_notification_delivery_status ON notification_delivery (tenant_id, status, last_attempt_at);
```

### 2.8 Audit Log (Append-Only, Partitioned)

```sql
CREATE TABLE audit_log (
  id UUID NOT NULL,
  tenant_id UUID NOT NULL,
  user_id UUID NULL,  -- NULL for system events
  user_email TEXT NULL,  -- Denormalized for forensic query
  entity_type TEXT NOT NULL,  -- 'student', 'invoice', etc.
  entity_id UUID NOT NULL,
  action TEXT NOT NULL CHECK (action IN ('create', 'update', 'delete', 'restore', 'export', 'login', 'logout', 'permission_change')),
  changed_fields JSONB NULL,  -- {field_name: {before: ..., after: ...}}
  ip_address INET NULL,
  user_agent TEXT NULL,
  request_id TEXT NULL,  -- Correlates to logging/tracing
  occurred_at timestamptz NOT NULL DEFAULT now(),
  -- No UPDATE or DELETE allowed (enforced by trigger)
  PRIMARY KEY (id, occurred_at)
) PARTITION BY RANGE (occurred_at);

CREATE INDEX idx_audit_log_tenant_date ON audit_log (tenant_id, occurred_at DESC);
CREATE INDEX idx_audit_log_user ON audit_log (tenant_id, user_id, occurred_at DESC);
CREATE INDEX idx_audit_log_entity ON audit_log (tenant_id, entity_type, entity_id, occurred_at DESC);
CREATE INDEX idx_audit_log_action ON audit_log (tenant_id, action, occurred_at DESC);
```

### 2.9 Enquiry & Tours

#### `enquiry`
```sql
CREATE TABLE enquiry (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  family_name TEXT NOT NULL,
  child_name TEXT NOT NULL,
  child_dob DATE NULL,
  contact_email TEXT NOT NULL,
  contact_phone TEXT NULL,
  preferred_contact_method TEXT NULL CHECK (preferred_contact_method IN ('email', 'phone', 'whatsapp')),
  desired_start_date DATE NULL,
  desired_program_id UUID NULL REFERENCES program(id),
  source TEXT NOT NULL CHECK (source IN ('walk_in', 'website', 'referral', 'social_media', 'phone', 'email', 'existing_parent', 'other')),
  source_detail TEXT NULL,  -- E.g., "Facebook ad campaign May 2026"
  assigned_staff_id UUID NULL REFERENCES staff(id),
  stage TEXT NOT NULL DEFAULT 'no_stage'
    CHECK (stage IN ('no_stage', 'interested', 'contacted', 'tour_appointment', 'toured', 'enrolled')),
  stage_changed_at timestamptz NOT NULL DEFAULT now(),
  notes JSONB NOT NULL DEFAULT '[]',  -- Array of {note: ..., author: ..., timestamp: ...}
  is_archived boolean NOT NULL DEFAULT false,
  archived_at timestamptz NULL,
  -- Universal columns...
);

CREATE TABLE enquiry_stage_log (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  enquiry_id UUID NOT NULL REFERENCES enquiry(id),
  from_stage TEXT NULL,
  to_stage TEXT NOT NULL,
  changed_by UUID NOT NULL REFERENCES app_user(id),
  reason TEXT NULL,
  changed_at timestamptz NOT NULL DEFAULT now()
  -- No UPDATE/DELETE allowed (enforced by trigger)
);

CREATE INDEX idx_enquiry_tenant_stage ON enquiry (tenant_id, stage) WHERE is_deleted = false;
CREATE INDEX idx_enquiry_assigned ON enquiry (tenant_id, assigned_staff_id, stage);
```

#### `tour_appointment`
```sql
CREATE TABLE tour_appointment (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  enquiry_id UUID NOT NULL REFERENCES enquiry(id),
  scheduled_at timestamptz NOT NULL,
  duration_minutes INT NOT NULL DEFAULT 60,
  assigned_staff_id UUID NOT NULL REFERENCES staff(id),
  tour_type TEXT NOT NULL DEFAULT 'general' CHECK (tour_type IN ('general', 'program_focus', 'birthday_booking')),
  num_adults INT NOT NULL DEFAULT 1,
  num_children INT NOT NULL DEFAULT 0,
  notes TEXT NULL,
  status TEXT NOT NULL DEFAULT 'scheduled'
    CHECK (status IN ('scheduled', 'completed', 'cancelled', 'no_show')),
  status_changed_at timestamptz NOT NULL DEFAULT now(),
  outcome_notes TEXT NULL,
  -- Universal columns...
);

CREATE TABLE staff_block_off (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  staff_id UUID NOT NULL REFERENCES staff(id),
  start_at timestamptz NOT NULL,
  end_at timestamptz NOT NULL,
  reason TEXT NULL,
  -- Universal columns...
);
```

### 2.10 Calendar

```sql
CREATE TABLE calendar_event (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  calendar_type TEXT NOT NULL CHECK (calendar_type IN ('school', 'admin', 'staff')),
  title JSONB NOT NULL,
  description JSONB NULL,
  event_type TEXT NOT NULL CHECK (event_type IN ('regular', 'holiday', 'staff_only', 'birthday', 'tour')),
  start_at timestamptz NOT NULL,
  end_at timestamptz NOT NULL,
  is_all_day boolean NOT NULL DEFAULT false,
  room_id UUID NULL,  -- NULL = all rooms
  staff_id UUID NULL,  -- For staff calendar events
  student_id UUID NULL,  -- For birthday events
  recurrence_rule JSONB NULL,  -- RFC 5545 RRULE format
  -- Universal columns...
);

CREATE INDEX idx_calendar_event_tenant_start ON calendar_event (tenant_id, start_at);
CREATE INDEX idx_calendar_event_type ON calendar_event (tenant_id, calendar_type, event_type);
```

### 2.11 Forms

```sql
CREATE TABLE form_definition (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name JSONB NOT NULL,
  description JSONB NULL,
  form_type TEXT NOT NULL CHECK (form_type IN ('enquiry', 'admission', 'check_in_out', 'incident', 'custom')),
  status TEXT NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'live', 'archived')),
  version INT NOT NULL DEFAULT 1,
  fields JSONB NOT NULL,  -- Array of field definitions
  is_public boolean NOT NULL DEFAULT false,  -- Public forms (e.g., enquiry) don't require auth
  success_message JSONB NULL,
  redirect_url TEXT NULL,
  -- Universal columns...
);

CREATE TABLE form_submission (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  form_definition_id UUID NOT NULL REFERENCES form_definition(id),
  submitted_by UUID NULL REFERENCES app_user(id),  -- NULL for public submissions
  submitted_at timestamptz NOT NULL DEFAULT now(),
  data JSONB NOT NULL,  -- Field values
  student_id UUID NULL REFERENCES student(id),
  enquiry_id UUID NULL REFERENCES enquiry(id),
  status TEXT NOT NULL DEFAULT 'submitted' CHECK (status IN ('submitted', 'reviewed', 'approved', 'rejected')),
  -- Universal columns...
);

CREATE TABLE admission_process (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  student_id UUID NOT NULL REFERENCES student(id),
  process_name JSONB NOT NULL,
  steps JSONB NOT NULL,  -- Array of {name, status, due_date, completed_at, form_definition_id}
  -- Universal columns...
);
```

### 2.12 Learning & Assessment (Phase 2)

```sql
CREATE TABLE curriculum_framework (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name TEXT NOT NULL,  -- 'eyfs' | 'montessori'
  display_name JSONB NOT NULL,
  is_active boolean NOT NULL DEFAULT true,
  -- Universal columns...
);

CREATE TABLE development_area (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  framework_id UUID NOT NULL REFERENCES curriculum_framework(id),
  code TEXT NOT NULL,  -- 'CAL', 'PD', etc.
  name JSONB NOT NULL,
  -- Universal columns...
);

CREATE TABLE age_band (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  framework_id UUID NOT NULL REFERENCES curriculum_framework(id),
  name TEXT NOT NULL,  -- '8_to_20_months'
  min_months INT NOT NULL,
  max_months INT NOT NULL,
  -- Universal columns...
);

CREATE TABLE milestone (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  framework_id UUID NOT NULL,
  development_area_id UUID NOT NULL REFERENCES development_area(id),
  age_band_id UUID NOT NULL REFERENCES age_band(id),
  code TEXT NOT NULL,
  description JSONB NOT NULL,
  -- Universal columns...
);

CREATE TABLE observation (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  student_id UUID NOT NULL REFERENCES student(id),
  observed_by_staff_id UUID NOT NULL REFERENCES staff(id),
  observed_at timestamptz NOT NULL,
  framework_id UUID NULL REFERENCES curriculum_framework(id),
  development_area_id UUID NULL REFERENCES development_area(id),
  age_band_id UUID NULL REFERENCES age_band(id),
  observation_type TEXT NOT NULL CHECK (observation_type IN ('formal', 'informal', 'concern')),
  notes JSONB NULL,  -- {en: ..., ar: ...}
  has_concern boolean NOT NULL DEFAULT false,
  concern_severity TEXT NULL CHECK (concern_severity IN ('low', 'medium', 'high')),
  -- Universal columns...
);

CREATE TABLE observation_milestone (
  observation_id UUID NOT NULL REFERENCES observation(id),
  milestone_id UUID NOT NULL REFERENCES milestone(id),
  achieved boolean NOT NULL DEFAULT true,
  notes TEXT NULL,
  PRIMARY KEY (observation_id, milestone_id)
);

CREATE TABLE assessment (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  student_id UUID NOT NULL REFERENCES student(id),
  template_id UUID NOT NULL REFERENCES assessment_template(id),
  assessed_by_staff_id UUID NOT NULL REFERENCES staff(id),
  assessment_date DATE NOT NULL,
  period_start DATE NOT NULL,
  period_end DATE NOT NULL,
  results JSONB NOT NULL,
  parent_view_approved boolean NOT NULL DEFAULT false,
  parent_view_approved_by UUID NULL,
  parent_view_approved_at timestamptz NULL,
  -- Universal columns...
);
```

---

## 3. Indexing Strategy Summary

| Table | Indexes |
|---|---|
| tenant | (slug) UNIQUE |
| app_user | (tenant_id, email) UNIQUE; (keycloak_user_id) UNIQUE |
| student | (tenant_id, created_at DESC); (tenant_id, status) WHERE is_deleted=false; (tenant_id, room_id); (tenant_id, barcode) UNIQUE |
| staff | (tenant_id, email) UNIQUE; (tenant_id, barcode) UNIQUE |
| attendance_record | (tenant_id, date DESC); (tenant_id, person_type, person_id, date DESC); (tenant_id, room_id, date) |
| audit_log | (tenant_id, occurred_at DESC); (tenant_id, user_id, occurred_at DESC); (tenant_id, entity_type, entity_id, occurred_at DESC) |
| invoice | (tenant_id, status) WHERE is_deleted=false; (tenant_id, student_id, issue_date DESC); (tenant_id, due_date) WHERE status IN ('issued','partially_paid') |
| activity_post | (tenant_id, published_at DESC) WHERE is_deleted=false AND approval_status='approved' |
| notification | (tenant_id, recipient_user_id, created_at DESC) WHERE is_deleted=false; (tenant_id, recipient_user_id) WHERE is_read=false |
| message_thread | (tenant_id, last_message_at DESC) WHERE is_deleted=false |

---

## 4. Materialized Views (Dashboard Aggregations)

```sql
CREATE MATERIALIZED VIEW mv_daily_attendance_summary AS
SELECT
  tenant_id,
  date,
  room_id,
  count(*) FILTER (WHERE status = 'present') AS present_count,
  count(*) FILTER (WHERE status = 'absent') AS absent_count,
  count(*) FILTER (WHERE status = 'pending') AS pending_count,
  count(*) FILTER (WHERE person_type = 'student') AS student_count,
  count(*) FILTER (WHERE person_type = 'staff') AS staff_count
FROM attendance_record
WHERE date = CURRENT_DATE
GROUP BY tenant_id, date, room_id
WITH DATA;

CREATE UNIQUE INDEX ON mv_daily_attendance_summary (tenant_id, date, room_id);

CREATE MATERIALIZED VIEW mv_live_ratio AS
SELECT
  ar.tenant_id,
  ar.room_id,
  count(*) FILTER (WHERE ar.person_type = 'student' AND ar.check_in_at IS NOT NULL AND ar.check_out_at IS NULL) AS students_present,
  count(*) FILTER (WHERE ar.person_type = 'staff' AND ar.check_in_at IS NOT NULL AND ar.check_out_at IS NULL) AS staff_present,
  r.ratio_staff_to_student AS required_ratio
FROM attendance_record ar
JOIN room r ON r.id = ar.room_id
WHERE ar.date = CURRENT_DATE
GROUP BY ar.tenant_id, ar.room_id, r.ratio_staff_to_student
WITH DATA;

CREATE UNIQUE INDEX ON mv_live_ratio (tenant_id, room_id);

CREATE MATERIALIZED VIEW mv_billing_dashboard AS
SELECT
  tenant_id,
  date_trunc('month', issue_date) AS month,
  sum(total_fils) AS total_billed,
  sum(paid_fils) AS total_collected,
  sum(total_fils - paid_fils) FILTER (WHERE status = 'overdue') AS overdue,
  sum(total_fils - paid_fils) FILTER (WHERE status IN ('issued','partially_paid')) AS outstanding
FROM invoice
WHERE is_deleted = false
GROUP BY tenant_id, date_trunc('month', issue_date)
WITH DATA;

CREATE UNIQUE INDEX ON mv_billing_dashboard (tenant_id, month);
```

**Refresh strategy:**
- On every attendance event (RabbitMQ-triggered): refresh `mv_daily_attendance_summary` + `mv_live_ratio` for affected tenant.
- Fallback scheduled refresh every 5 minutes via Hangfire.
- On invoice/payment event: refresh `mv_billing_dashboard`.
- Use `CONCURRENTLY` to avoid locking: `REFRESH MATERIALIZED VIEW CONCURRENTLY mv_live_ratio;`

---

## 5. Row Level Security (RLS) — Complete Policy Set

### 5.1 Universal RLS Setup

```sql
-- Function to set tenant context (called per request)
CREATE OR REPLACE FUNCTION set_tenant_context(p_tenant_id UUID)
RETURNS void AS $$
BEGIN
  PERFORM set_config('app.tenant_id', p_tenant_id::text, true);  -- true = local (transaction-scoped)
END;
$$ LANGUAGE plpgsql;

-- Function to get current tenant
CREATE OR REPLACE FUNCTION current_tenant_id()
RETURNS UUID AS $$
BEGIN
  RETURN COALESCE(
    NULLIF(current_setting('app.tenant_id', true), '')::UUID,
    '00000000-0000-0000-0000-000000000000'::UUID  -- Sentinel that matches nothing
  );
END;
$$ LANGUAGE plpgsql STABLE;
```

### 5.2 Apply RLS to All Tenant Tables

```sql
-- Apply to every table with tenant_id
DO $$
DECLARE
  t RECORD;
BEGIN
  FOR t IN
    SELECT table_name FROM information_schema.columns
    WHERE column_name = 'tenant_id'
      AND table_schema = 'public'
      AND table_name NOT IN ('tenant')  -- tenant table itself
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY;', t.table_name);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY;', t.table_name);
    EXECUTE format('
      CREATE POLICY tenant_isolation ON %I
        USING (tenant_id = current_tenant_id())
        WITH CHECK (tenant_id = current_tenant_id());
    ', t.table_name);
  END LOOP;
END $$;
```

### 5.3 Service Role Bypass

For migrations, admin tasks, and platform ops team:
```sql
-- Bypass RLS for service role (used by Hangfire jobs, migrations)
-- Application connects with `app` role (RLS enforced)
-- Service connects with `service` role (BYPASSRLS)
CREATE ROLE service LOGIN BYPASSRLS;
```

Platform ops team can use `service` role for forensic queries. All application queries use `app` role (RLS enforced).

---

## 6. Partitioning Maintenance

### 6.1 Future Partition Creation

Run as a daily Hangfire job:
```sql
-- Ensure next 3 months of partitions exist
CREATE TABLE IF NOT EXISTS attendance_record_2026_06 PARTITION OF attendance_record
  FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
CREATE TABLE IF NOT EXISTS audit_log_2026_06 PARTITION OF audit_log
  FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
-- ...etc
```

### 6.2 Old Partition Archival

After retention period:
- `attendance_record`: detach partition, export to Parquet on S3, drop partition.
- `audit_log`: retain per legal requirement (proposed 7 years). Detach from main table after 1 year, move to archive schema for query performance, retain indefinitely.

---

## 7. EF Core Configuration

### 7.1 DbContext

```csharp
public class NmsDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;

    public NmsDbContext(DbContextOptions<NmsDbContext> options, ITenantContext tenantContext, ICurrentUser currentUser)
        : base(options)
    {
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Staff> Staff => Set<Staff>();
    // ... etc

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NmsDbContext).Assembly);

        // Global query filter for tenant + soft delete
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(e => EF.Property<UUID>(e, "TenantId") == _tenantContext.TenantId);
            }
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(e => !EF.Property<bool>(e, "IsDeleted"));
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Set audit fields
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                    entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
                    entry.Entity.CreatedBy = _currentUser.UserId;
                    entry.Entity.UpdatedBy = _currentUser.UserId;
                    if (entry.Entity is ITenantEntity tenantEntity)
                        tenantEntity.TenantId = _tenantContext.TenantId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
                    entry.Entity.UpdatedBy = _currentUser.UserId;
                    break;
            }
        }

        // Set soft delete
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTimeOffset.UtcNow;
            }
        }

        // Audit log generation (via SaveChanges interceptor — see below)
        return await base.SaveChangesAsync(ct);
    }
}
```

### 7.2 Tenant Context Middleware

```csharp
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, NmsDbContext db)
    {
        // Extract tenant_id from JWT
        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            tenantContext.TenantId = tenantId;

            // Set RLS context
            await db.Database.ExecuteSqlRawAsync(
                $"SELECT set_config('app.tenant_id', '{tenantId}', true)");
        }

        await _next(context);
    }
}
```

### 7.3 Audit Log Interceptor

```csharp
public class AuditLogInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        var dbContext = eventData.Context as NmsDbContext;
        if (dbContext == null) return result;

        var auditEntries = new List<AuditEntry>();
        foreach (var entry in dbContext.ChangeTracker.Entries().Where(e =>
            e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
        {
            auditEntries.Add(new AuditEntry(entry));
        }

        if (auditEntries.Any())
        {
            var auditLogs = auditEntries.Select(ae => ae.ToAuditLog()).ToList();
            await dbContext.Set<AuditLog>().AddRangeAsync(auditLogs, ct);
            await dbContext.SaveChangesAsync(ct);
        }

        return result;
    }
}
```

---

## 8. Migration Strategy

- **Tool**: EF Core migrations (Code-First).
- **Versioning**: Each migration is timestamped + immutable. Never edit a published migration — create a new one.
- **CI/CD**: Migrations run automatically in CI/CD pipeline before app startup.
- **Production**: Migrations run as a separate init container in K8s, before app pods roll out.
- **Backup**: Always take DB snapshot before migration in production.
- **Rollback**: Migrations include `Up` and `Down` methods. Rollback tested in staging.

### 8.1 Migration Naming

```
20260517120000_CreateInitialSchema.cs
20260518100000_AddEmiratesIdToStudent.cs
20260601120000_AddPaymentGatewayColumns.cs
```

### 8.2 Data Migrations

For data changes (not schema):
- Separate migration class with `migrationBuilder.Sql("UPDATE ...")`.
- Test on staging with production data snapshot first.
- Idempotent — safe to re-run.

---

## 9. Backup Strategy

| Data | Frequency | Method | RPO | Retention |
|---|---|---|---|---|
| PostgreSQL (SaaS) | Daily snapshot + WAL archiving (5 min) | AWS RDS automated backups | 5 min | 35 days |
| PostgreSQL (on-prem) | Daily snapshot + WAL archiving | pgBackRest or customer's backup tool | 5 min | Customer-defined (min 35 days) |
| Object storage | Continuous cross-region replication (SaaS) | S3 CRR | 0 | Lifecycle policy |
| Object storage (on-prem) | Customer's responsibility | MinIO replication or backup tool | Customer-defined | Customer-defined |
| Redis | Not backed up (cache only) | N/A | N/A | N/A |
| RabbitMQ | Not backed up (transient) | N/A | N/A | N/A |
| Configuration / Terraform | Git version control + S3 state backend | Git + S3 | 0 | Indefinite (git history) |
| Container images | Retain last 20 per repo in registry | ECR lifecycle policy | 0 | 20 versions |

### 9.1 Restore Test

- Monthly: restore PostgreSQL snapshot to staging, verify schema + sample queries.
- Quarterly: full DR drill — restore everything to DR env, run smoke tests.

---

**Next file to read**: `08-API-SPEC.md` for REST conventions, OpenAPI, pagination, and SignalR.
