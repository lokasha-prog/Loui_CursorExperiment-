# 09 — Authentication & Security

> Keycloak realms, RBAC matrix, parent-child ownership enforcement, encryption, audit, and security best practices.

---

## 1. Identity Architecture

### 1.1 Identity Provider: Keycloak 24

NMS uses **Keycloak** as its identity provider (IdP). Keycloak handles:
- User registration (admin-provisioned for staff/parents; no self-service for parents in MVP).
- Authentication (username/password, MFA, social login if needed).
- Authorization (RBAC via realm roles → JWT claims).
- Session management (refresh token rotation, revocation).
- User federation (LDAP/AD for on-prem customers with existing IdP).
- Multi-realm support (one realm per centre in Phase 1, multi-realm per MCG in Phase 3).

### 1.2 Realm Strategy

**Phase 1 (single-centre deployment):**
- One realm per deployment.
- Realm name = tenant slug (e.g., `bright-futures-nursery`).
- All users (staff + parents) live in the same realm.

**Phase 3 (multi-centre SaaS with MCG):**
- One realm per Multi-Centre Group.
- Users can be assigned roles in multiple centres within the MCG.
- Cross-realm SSO: Group Owner logs in once, accesses all centres.

**On-prem:**
- Customer-managed Keycloak realm.
- Optional LDAP/AD federation (customer's existing IdP).
- Customer controls user lifecycle.

### 1.3 Client Configuration

Two clients per realm:

| Client | Type | Purpose |
|---|---|---|
| `nms-web` | Public (OIDC Authorization Code + PKCE) | Web frontend (React) |
| `nms-mobile` | Public (OIDC Authorization Code + PKCE) | Mobile app (React Native) |

For Phase 3 Open API:
| Client | Type | Purpose |
|---|---|---|
| `nms-api` | Confidential (Client Credentials) | Third-party API access |

### 1.4 Token Configuration

| Token type | Lifetime | Storage | Rotation |
|---|---|---|---|
| Access token (JWT) | 15 minutes | In-memory (web) / Secure Keychain (iOS) / EncryptedSharedPreferences (Android) | Refreshed via refresh token |
| Refresh token | 7 days | HttpOnly + Secure + SameSite=Strict cookie (web) / Secure Keychain (mobile) | Rotated on each use (detects theft) |
| ID token | 15 minutes | Same as access token | Same as access token |

### 1.5 JWT Claims

```json
{
  "sub": "uuid-of-user",
  "email": "layla@brightfutures.ae",
  "email_verified": true,
  "name": "Layla Ahmed",
  "given_name": "Layla",
  "family_name": "Ahmed",
  "preferred_language": "ar",
  "tenant_id": "uuid-of-tenant",
  "realm_access": {
    "roles": ["centre_admin"]
  },
  "resource_access": {
    "nms-web": {
      "roles": ["centre_admin"]
    }
  },
  "tenant_roles": [
    {
      "centre_id": "uuid",
      "role": "centre_admin"
    }
  ],
  "scope": "openid profile email",
  "iss": "https://keycloak.nms.ae/realms/bright-futures-nursery",
  "aud": ["nms-web"],
  "exp": 1716339000,
  "iat": 1716338100,
  "jti": "unique-token-id"
}
```

**Custom claims added by Keycloak mapper:**
- `tenant_id` — UUID of the tenant (centre).
- `preferred_language` — User's UI language preference.
- `tenant_roles` — Roles per centre (for multi-centre users in Phase 3).

---

## 2. Roles & Permissions

### 2.1 Standard Roles (8 system roles)

| Role | Keycloak role name | Description |
|---|---|---|
| **Super Admin** | `super_admin` | Full system owner. All permissions. MFA mandatory. |
| **Centre Admin** | `centre_admin` | Day-to-day admin. Attendance, enquiries, billing, communication, forms. |
| **Teacher / Key Person** | `teacher` | Classroom staff. Attendance, activities, learning journals, room-scoped view. |
| **Parent / Guardian** | `parent` | Read daily reports, send messages, view invoices, photos. Limited to own children. |
| **Receptionist** | `receptionist` | Front desk. Enquiries, calendar, visitor management. |
| **Accounts** | `accounts` | Billing, payment settings, financial system. |
| **IT** | `it` | All modules, settings, user management, billing (system administration). |
| **Marketing** | `marketing` | Enquiries and communication only. |

> Per Loui Okasha comment on source SRS, also include "Procurement" role for Phase 2+ (Inventory & Asset Management module). Not in Phase 1.

### 2.2 RBAC Permission Matrix

Permission format: `<module>.<action>` (e.g., `students.read`, `students.write`, `students.delete`).

| Permission | Super Admin | Centre Admin | Teacher | Parent | Receptionist | Accounts | IT | Marketing |
|---|---|---|---|---|---|---|---|---|
| `dashboard.view` | ✅ | ✅ | ✅ (room-scoped) | ✅ (child-scoped) | ✅ | ✅ | ✅ | ✅ |
| `students.read` | ✅ | ✅ | ✅ (room-scoped) | ✅ (own children) | ✅ | ❌ | ✅ | ❌ |
| `students.write` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `students.delete` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `staff.read` | ✅ | ✅ | ✅ (own room) | ❌ | ✅ | ✅ | ✅ | ❌ |
| `staff.write` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `attendance.read` | ✅ | ✅ | ✅ (room-scoped) | ✅ (own child) | ✅ | ❌ | ✅ | ❌ |
| `attendance.write` | ✅ | ✅ | ✅ (room-scoped) | ❌ | ✅ | ❌ | ✅ | ❌ |
| `attendance.override` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `activities.read` | ✅ | ✅ | ✅ (room-scoped) | ✅ (own child) | ❌ | ❌ | ✅ | ❌ |
| `activities.write` | ✅ | ✅ | ✅ (own + room) | ❌ | ❌ | ❌ | ✅ | ❌ |
| `activities.approve` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `messaging.read` | ✅ | ✅ | ✅ (room-scoped) | ✅ (own threads) | ✅ | ❌ | ✅ | ✅ |
| `messaging.write` | ✅ | ✅ | ✅ | ✅ (own threads) | ✅ | ❌ | ✅ | ✅ |
| `messaging.broadcast` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `billing.read` | ✅ | ✅ | ❌ | ✅ (own invoices) | ❌ | ✅ | ✅ | ❌ |
| `billing.write` | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| `invoices.generate` | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| `payments.record` | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| `enquiries.read` | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ✅ | ✅ |
| `enquiries.write` | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ✅ | ✅ |
| `tours.read` | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ |
| `tours.write` | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ |
| `calendar.read` | ✅ | ✅ | ✅ | ✅ (relevant) | ✅ | ❌ | ✅ | ✅ |
| `calendar.write` | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ✅ | ✅ |
| `forms.read` | ✅ | ✅ | ✅ | ✅ (parent forms) | ✅ | ❌ | ✅ | ❌ |
| `forms.write` | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ |
| `tools.use` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `reports.view` | ✅ | ✅ | ✅ (room-scoped) | ❌ | ✅ | ✅ | ✅ | ✅ |
| `reports.generate` | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| `settings.read` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `settings.write` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `audit_log.view` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `user_management.manage` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `notifications.preferences` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

### 2.3 Permission Enforcement Layers

1. **Keycloak (JWT claims)** — issues JWT with `realm_access.roles`. Cannot be tampered (signed by Keycloak).
2. **.NET API middleware** — `[Authorize]` attribute requires valid JWT. `[Authorize(Policy = "students.write")]` requires permission claim.
3. **.NET API handler** — explicit checks: `if (!await _authorizationService.AuthorizeAsync(user, student, "student.ownership")) return NotFound();`
4. **Database (RLS)** — defense-in-depth. Even if app layer bug, DB rejects cross-tenant queries.
5. **Frontend** — UI elements conditionally rendered based on permissions from `/auth/me`. **Never security-critical** — backend always enforces.

### 2.4 Parent-Child Ownership Enforcement

This is the **most critical** security rule in the system. Parents MUST NEVER see another parent's child data.

**Implementation:**

```csharp
// In StudentQueryHandler
public async Task<StudentDto> Handle(GetStudentByIdQuery request, CancellationToken ct)
{
    var student = await _dbContext.Students
        .FirstOrDefaultAsync(s => s.Id == request.Id, ct);
    
    if (student == null)
        return Result<StudentDto>.NotFound();
    
    // If user is a parent, verify ownership
    if (_currentUser.IsInRole("parent"))
    {
        var isParentOfStudent = await _dbContext.StudentGuardians
            .AnyAsync(sg => sg.StudentId == request.Id 
                         && sg.Guardian.AppUserId == _currentUser.UserId, ct);
        
        if (!isParentOfStudent)
            return Result<StudentDto>.NotFound();  // 404, NOT 403 — prevents ID enumeration
    }
    
    return Result<StudentDto>.Success(_mapper.Map<StudentDto>(student));
}
```

**Applied to every endpoint that returns child-specific data:**
- `/api/v1/students/{id}` — ownership check
- `/api/v1/students/{id}/activities` — ownership check
- `/api/v1/students/{id}/attendance` — ownership check
- `/api/v1/activities?studentId={id}` — filter to only owned children
- `/api/v1/threads` — only threads where parent is participant
- `/api/v1/invoices?studentId={id}` — only invoices for owned children
- `/api/v1/notifications` — only notifications addressed to current user

**Testing:**
- Unit tests: every endpoint tested with parent owning student A, attempting to access student B → 404.
- Integration tests: parent JWT from tenant X, attempt to access tenant Y → 404.
- Pen test: attempt ID enumeration + JWT tampering before each phase go-live.

---

## 3. Authentication Flows

### 3.1 Staff Login Flow (Web)

```
1. User navigates to https://app.nms.ae
2. App checks for valid JWT in memory
   - If valid → redirect to dashboard
   - If expired → attempt refresh via /api/v1/auth/refresh
   - If refresh fails → redirect to /login
3. User enters email + password on /login
4. App calls POST /api/v1/auth/login with credentials
5. Backend (Keycloak JS adapter):
   a. Authenticates against Keycloak
   b. Returns access_token + refresh_token (in HttpOnly cookie)
6. App stores access_token in memory, refresh_token in HttpOnly cookie (set by backend)
7. App fetches /api/v1/auth/me → gets user info + permissions
8. App redirects to role-specific landing page
```

### 3.2 Parent Login Flow (Web)

Same as staff, EXCEPT:
- Default landing page: child activity feed (not dashboard).
- Only sees parent-scoped data.
- Optionally: magic link login (no password) — email a one-time login link valid for 15 minutes.

### 3.3 Mobile Login Flow (React Native + Expo)

```
1. App checks Keychain/EncryptedSharedPreferences for stored refresh_token
   - If valid → use to get new access_token → skip to step 5
2. User enters email + password on login screen
3. App calls Keycloak token endpoint via expo-auth-session
   - Uses Authorization Code + PKCE flow
   - Keycloak returns authorization code
   - App exchanges code for access_token + refresh_token
4. App stores:
   - access_token in memory
   - refresh_token in Secure Keychain (iOS) / EncryptedSharedPreferences (Android)
5. App calls /api/v1/auth/me → gets user info + permissions + tenant_id
6. App navigates to role-specific landing page
7. App connects to SignalR hub with JWT
```

### 3.4 Refresh Token Flow

```
1. Access token expires (15 min)
2. App calls POST /api/v1/auth/refresh with HttpOnly cookie containing refresh_token
3. Backend:
   a. Validates refresh_token signature + expiry
   b. Checks if refresh_token is in revocation list (logout, password change, suspension)
   c. Issues new access_token + new refresh_token (rotation)
   d. Adds old refresh_token to revocation list (detected theft if reused)
4. App stores new tokens
```

**Replay detection**: If backend sees a refresh_token that's already been used (in revocation list), it immediately revokes ALL of that user's refresh tokens (assumes theft) and forces re-login.

### 3.5 Logout Flow

```
1. App calls POST /api/v1/auth/logout
2. Backend:
   a. Adds current refresh_token to revocation list
   b. Calls Keycloak logout endpoint to revoke Keycloak session
3. App clears local tokens (memory + Keychain)
4. App redirects to /login
```

### 3.6 Password Reset Flow

```
1. User clicks "Forgot password" on /login
2. User enters email
3. App calls POST /api/v1/auth/forgot-password
4. Backend:
   a. Looks up user by email (within tenant context)
   b. Generates password reset token (UUID, 1-hour expiry)
   c. Stores token hash in Keycloak (custom credential)
   d. Sends email with link: https://app.nms.ae/reset-password?token=...
5. User clicks link
6. App calls POST /api/v1/auth/reset-password with token + new password
7. Backend:
   a. Validates token
   b. Updates password in Keycloak
   c. Revokes all user's refresh tokens (force re-login)
   d. Sends confirmation email
```

### 3.7 Parent Invitation Flow

```
1. Centre Admin creates student with primary guardian email
2. Backend:
   a. Creates guardian record (app_user_id = NULL, status = 'invited')
   b. Generates invitation token (UUID, 7-day expiry)
   c. Sends email: "You've been invited to view <child>'s activities at <nursery>"
3. Parent clicks link: https://app.nms.ae/accept-invitation?token=...
4. App shows invitation acceptance form (set password, accept terms)
5. App calls POST /api/v1/auth/accept-invitation with token + new password
6. Backend:
   a. Validates token
   b. Creates app_user in Keycloak
   c. Links app_user to guardian record
   d. Sends welcome email
7. Parent logs in → sees child's activity feed
```

### 3.8 MFA

- **TOTP** (Time-based One-Time Password) via Keycloak built-in.
- **Mandatory for**: Super Admin, IT roles.
- **Optional for**: All other staff roles.
- **Not applicable for**: Parents (in Phase 1; consider SMS OTP for Phase 3 if security review requires).

Setup flow:
1. User navigates to Settings → Security → Enable MFA.
2. Backend generates TOTP secret + QR code (via Keycloak API).
3. User scans QR with authenticator app (Google Authenticator, Authy, etc.).
4. User enters 6-digit code to verify setup.
5. Backend stores backup codes (10 single-use codes) — user must save.

Login flow with MFA:
1. User enters email + password.
2. Backend validates credentials.
3. If MFA enabled, backend returns 200 with `mfa_required: true` (no tokens).
4. App shows MFA code input.
5. App calls POST /api/v1/auth/mfa-verify with TOTP code.
6. Backend validates → returns access + refresh tokens.

---

## 4. Data Security

### 4.1 Encryption at Rest

| Data | Encryption method |
|---|---|
| PostgreSQL (database) | AWS RDS encryption at rest (AWS-managed KMS key) |
| PostgreSQL (on-prem) | LUKS disk encryption (customer-managed) |
| S3 / MinIO (object storage) | Server-side encryption (S3: SSE-S3 or SSE-KMS; MinIO: SSE-S3) |
| Redis | Encrypted at rest (AWS ElastiCache at-rest encryption) |
| RabbitMQ | Encrypted at rest (EBS volume encryption) |
| Keycloak DB | Same as PostgreSQL |
| Backups | Encrypted (AWS KMS or customer-managed) |

### 4.2 Encryption in Transit

| Connection | Encryption |
|---|---|
| Client ↔ Load Balancer | TLS 1.2+ (TLS 1.3 preferred) |
| Load Balancer ↔ Backend pods | mTLS (via Istio service mesh in Phase 2+) or plain HTTP in same VPC (Phase 1) |
| Backend pods ↔ PostgreSQL | TLS 1.2+ (require sslmode=verify-full) |
| Backend pods ↔ Redis | TLS 1.2+ (rediss:// connection string) |
| Backend pods ↔ RabbitMQ | TLS 1.2+ (amqps://) |
| Backend pods ↔ S3 | TLS 1.2+ (HTTPS) |
| Backend pods ↔ Keycloak | TLS 1.2+ (HTTPS) |
| Backend pods ↔ External services (FCM, SES, etc.) | TLS 1.2+ (HTTPS) |
| Keycloak ↔ User browser | TLS 1.2+ (HTTPS) |

**TLS configuration**:
- Cipher suites: Mozilla intermediate compatibility list.
- HSTS: `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`.
- Certificate: Let's Encrypt (SaaS) or customer-supplied cert (on-prem).
- Certificate rotation: automated via cert-manager (K8s).

### 4.3 Column-Level Encryption

Sensitive fields encrypted at column level using PostgreSQL `pgcrypto` extension (additional layer beyond at-rest encryption):

```sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Encrypt on write
INSERT INTO student (
  id, tenant_id, first_name, ...,
  medical_notes,  -- This will be encrypted
  allergy_info,
  custody_notes,
  safeguarding_flags,
  passport_number,
  emirates_id
) VALUES (
  $1, $2, $3, ...,
  pgp_sym_encrypt($medical_notes, current_setting('app.encryption_key')),
  pgp_sym_encrypt($allergy_info, current_setting('app.encryption_key')),
  ...
);

-- Decrypt on read
SELECT 
  ...,
  pgp_sym_decrypt(medical_notes, current_setting('app.encryption_key')) AS medical_notes,
  pgp_sym_decrypt(allergy_info, current_setting('app.encryption_key')) AS allergy_info,
  ...
FROM student WHERE id = $1;
```

**Encrypted fields:**
- `student.medical_notes`
- `student.allergy_info`
- `student.dietary_requirements`
- `student.custody_notes`
- `student.safeguarding_flags`
- `student.passport_number`
- `student.emirates_id`
- `staff.salary_amount_fils`
- `staff.emirates_id`
- `staff.passport_number`
- `staff.visa_info`

**Key management:**
- Encryption key stored in AWS KMS (SaaS) or HashiCorp Vault (on-prem).
- Key rotation: annual, with re-encryption job.
- Key never in code, env vars, or config files — fetched at app startup from secrets manager.

**Access controls:**
- Decryption only in API layer (never in DB views or stored procedures).
- Audit log records every decryption (who, when, which field).
- Emergency report PDF generation triggers explicit audit event.

### 4.4 Media Security

- **Storage**: S3 / MinIO bucket, **private** (no public access).
- **Access**: Only via presigned URLs (5-min expiry for downloads, 60-sec for uploads).
- **CDN**: CloudFront / Cloudflare in front of S3, with signed URLs (CloudFront signed URLs or Cloudflare signed URLs).
- **Quarantine**: Failed-scan files moved to separate quarantine bucket. CDN NEVER serves from quarantine bucket — bucket policy blocks.
- **Watermarking** (Phase 2+): Optional, configurable per tenant — overlay staff member name + timestamp on photos for leakage attribution.

```csharp
// Presigned URL generation
public string GenerateDownloadUrl(Guid mediaId, string contentType)
{
    var media = await _mediaRepo.GetByIdAsync(mediaId);
    
    // Verify ownership / permission
    if (!await CanUserAccessMedia(_currentUser.UserId, mediaId))
        throw new ForbiddenException();
    
    // Verify scan status
    if (media.ScanStatus != "approved")
        throw new ForbiddenException("Media not approved for viewing");
    
    // Generate presigned URL (5 min expiry)
    var url = _s3Client.GetPresignedURL(new GetPresignedURLRequest
    {
        BucketName = _bucketName,
        Key = media.ObjectKey,
        Verb = HttpVerb.GET,
        Expires = DateTime.UtcNow.AddMinutes(5),
        ResponseContentType = media.ContentType
    });
    
    // Audit
    await _auditLogger.LogAsync(new AuditEvent
    {
        ActionType = "media_access",
        EntityType = "media",
        EntityId = mediaId,
        Details = new { accessedBy = _currentUser.UserId, expiry = "5min" }
    });
    
    return url;
}
```

### 4.5 Content Moderation Pipeline

Every media upload goes through content moderation BEFORE parent visibility:

```
1. Staff uploads photo (via presigned URL to S3)
2. API creates media record with scan_status = 'pending'
3. API publishes MediaUploaded event to RabbitMQ
4. Media Processing module consumes event:
   a. Downloads file from S3
   b. Runs ClamAV scan (malware)
   c. Calls AWS Rekognition (or Azure Content Safety) for inappropriate content:
      - DetectModerationLabels API
      - Thresholds: explicit, suggestive, violence, drugs
   d. Updates media.scan_status:
      - APPROVED if all checks pass
      - QUARANTINED if any check fails
   e. If QUARANTINED:
      - Moves file to quarantine bucket
      - Deletes from main bucket
      - Notifies uploading staff + Centre Admin
   f. Publishes MediaScanCompleted event
5. Parent Communication module consumes MediaScanCompleted:
   - If APPROVED: activity_post.approval_status → 'approved' → visible to parents
   - If QUARANTINED: activity_post.approval_status → 'rejected'
```

**Configuration:**
- Sensitivity thresholds configurable per tenant (Conservative / Standard / Permissive).
- False positive appeal flow: staff can request manual review by Centre Admin.
- Manual override: Centre Admin can manually approve/reject any media.

---

## 5. Rate Limiting & Brute Force Protection

### 5.1 API Rate Limiting

Per-user rate limiting (per IP fallback for unauthenticated):
- Authenticated: 100 req/min/user
- Unauthenticated: 10 req/min/IP
- Login attempts: 5 per minute, then 15-min lockout
- Password reset: 3 per hour per email

Implemented via Redis sliding window counter. Returns 429 with `Retry-After` header.

### 5.2 Account Lockout

- 5 failed login attempts → 15-min lockout
- 3 lockouts in 24h → 24h lockout
- Account locked → email sent to user (security notification)
- Super Admin can manually unlock

### 5.3 IP Blocking

- 100 failed requests in 5 min from same IP → 1h IP block
- IP blocked → logged in audit, surfaced in Grafana
- False positive appeal: Super Admin can whitelist IP

### 5.4 Bot Protection

- reCAPTCHA v3 on public forms (enquiry form, password reset)
- Honeypot fields on public forms
- User-Agent + IP fingerprinting for suspicious patterns

---

## 6. Input Validation

### 6.1 Validation Layers

1. **Frontend** — Zod schema validation before submit. UX feedback.
2. **Backend** — FluentValidation on every command/query. **Never trust the client.**
3. **Database** — Check constraints, NOT NULL, foreign keys, unique indexes. Last line of defense.

### 6.2 SQL Injection Prevention

- EF Core uses parameterized queries by default.
- Never use `FromSqlRaw` with string concatenation.
- For dynamic queries, use LINQ `IQueryable` composition.
- Audit any raw SQL usage in code review.

### 6.3 XSS Prevention

- React auto-escapes by default (don't use `dangerouslySetInnerHTML`).
- For rich text (newsletter, email templates), use DOMPurify on render.
- CSP headers: `default-src 'self'; script-src 'self' 'unsafe-inline'` (lock down further in production).

### 6.4 CSRF Protection

- API uses JWT in `Authorization` header (not cookies) for access tokens.
- Refresh token in HttpOnly cookie + SameSite=Strict → CSRF-resistant.
- For state-changing operations, require `X-Requested-With` header (custom header, CORS-restricted).

### 6.5 File Upload Validation

- File type validation via magic bytes (not just extension) — `file-type` library.
- File size validation at presign time + S3 bucket policy.
- Filename sanitization (no path traversal, no special chars).
- Virus scan via ClamAV.
- Image dimension validation (reject excessively large images that could DoS).

---

## 7. Audit Trail

### 7.1 What's Logged

Every mutation (CREATE, UPDATE, DELETE) on every entity. Plus:
- Login / logout / failed login
- Password change / reset
- MFA enable / disable
- Permission change
- Data export (audit log export, report export, emergency report)
- Media access (especially sensitive for emergency reports)
- Settings changes
- Failed authorization attempts (403, 404 on ownership denial)

### 7.2 Audit Log Record

```json
{
  "id": "uuid",
  "tenant_id": "uuid",
  "user_id": "uuid",
  "user_email": "user@example.com",  // Denormalized for forensic query
  "entity_type": "student",
  "entity_id": "uuid",
  "action": "update",
  "changed_fields": {
    "status": { "before": "active", "after": "on_hold" },
    "status_reason": { "before": null, "after": "Family travel" }
  },
  "ip_address": "192.168.1.1",
  "user_agent": "Mozilla/5.0 ...",
  "request_id": "req_abc123",
  "occurred_at": "2026-05-17T10:30:00.123Z"
}
```

### 7.3 Append-Only Enforcement

PostgreSQL trigger rejects UPDATE and DELETE on `audit_log`:

```sql
CREATE OR REPLACE FUNCTION prevent_audit_log_mutation()
RETURNS TRIGGER AS $$
BEGIN
  RAISE EXCEPTION 'audit_log is append-only. UPDATE and DELETE are not permitted.';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER audit_log_immutable
  BEFORE UPDATE OR DELETE ON audit_log
  FOR EACH ROW EXECUTE FUNCTION prevent_audit_log_mutation();
```

Even Super Admin cannot modify audit log via app. Only platform ops team with `service` role (BYPASSRLS) can access — and that access is itself logged at infrastructure level.

### 7.4 Audit Log Viewer (Super Admin)

UI in Settings → Audit Log:
- Filters: user, date range, entity type, action.
- Paginated list (cursor-based, 100 per page).
- Click entry → see full JSONB diff.
- Export to Excel (Super Admin only; export itself is audited).

### 7.5 Retention

- Audit log retained per legal requirement (proposed 7 years).
- After 1 year: partition detached from main table, moved to archive schema (still queryable, but doesn't slow down recent queries).
- After retention period: archived to S3 cold storage, partition dropped.

---

## 8. Session Management

### 8.1 Session Limits

- Max 5 concurrent sessions per user (5 devices).
- 6th login → oldest session revoked.
- Sessions viewable in user profile → can manually revoke.

### 8.2 Idle Timeout

- Web: 30-min idle → access token expires (15 min anyway) → refresh fails if no activity → redirect to login.
- Mobile: no idle timeout (tokens auto-refresh in background).

### 8.3 Forced Logout

Super Admin can force-logout any user (revokes all their refresh tokens). Used for:
- Suspected account compromise.
- Staff termination.
- Compliance investigation.

---

## 9. Secrets Management

### 9.1 Secrets Inventory

| Secret | Where stored | Rotation |
|---|---|---|
| Database password | AWS Secrets Manager / Vault | Every 90 days (auto-rotated) |
| Redis AUTH token | AWS Secrets Manager / Vault | Every 90 days |
| RabbitMQ password | AWS Secrets Manager / Vault | Every 90 days |
| S3 access key (if not using IAM role) | AWS Secrets Manager | Every 90 days |
| Keycloak client secrets | AWS Secrets Manager | Every 180 days |
| JWT signing key (Keycloak-managed) | Keycloak internal | Every 365 days (Keycloak auto-rotates) |
| FCM server key | AWS Secrets Manager | On-demand |
| SES SMTP credentials | AWS Secrets Manager | Every 180 days |
| Twilio auth token | AWS Secrets Manager | Every 180 days |
| AWS Rekognition access | IAM role (no secret) | N/A |
| OpenAI API key (Phase 2+) | AWS Secrets Manager | On-demand |
| pgcrypto encryption key | AWS KMS / Vault | Every 365 days (with re-encryption) |
| TLS private keys | cert-manager (K8s) | Auto-renewed (Let's Encrypt 90 days) |

### 9.2 Secret Access Pattern

```csharp
// Program.cs
builder.Services.AddSingleton<ISecretsProvider, AwsSecretsManagerProvider>();

// In service
public class DatabaseConnectionFactory
{
    private readonly ISecretsProvider _secrets;
    private string _cachedConnectionString;
    private DateTime _cacheExpiry = DateTime.MinValue;
    
    public async Task<string> GetConnectionStringAsync()
    {
        if (DateTime.UtcNow < _cacheExpiry)
            return _cachedConnectionString;
        
        var secret = await _secrets.GetSecretAsync("nms/prod/database");
        _cachedConnectionString = $"Host=...;Password={secret.Password};...";
        _cacheExpiry = DateTime.UtcNow.AddHours(1);  // Cache 1h, refresh before rotation
        return _cachedConnectionString;
    }
}
```

### 9.3 Forbidden Locations

Secrets MUST NEVER be in:
- Source code (caught by GitGuardian in pre-commit).
- Environment variables in production (only non-sensitive config in env vars).
- Config files committed to git.
- Log output (Serilog has Destructuring transformers to mask secrets).
- Error messages sent to clients.
- Database tables (unencrypted).

---

## 10. Security Hardening Checklist

### 10.1 Backend

- [ ] HTTPS only, HTTP redirects to HTTPS.
- [ ] HSTS header enabled.
- [ ] TLS 1.2+ only (TLS 1.0/1.1 disabled).
- [ ] Security headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `X-XSS-Protection: 1; mode=block`, `Referrer-Policy: strict-origin-when-cross-origin`.
- [ ] CSP header (production): `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self' wss:; font-src 'self' data:`.
- [ ] Rate limiting on all endpoints.
- [ ] JWT validation on every endpoint (no unauthenticated endpoints except login, password reset, public forms).
- [ ] Permission check on every endpoint.
- [ ] Tenant isolation via RLS on every query.
- [ ] Parent-child ownership check on every parent-facing endpoint.
- [ ] Input validation on every command.
- [ ] SQL injection prevention (parameterized queries only).
- [ ] XSS prevention (React auto-escape, DOMPurify for rich text).
- [ ] CSRF prevention (SameSite cookies + custom header requirement).
- [ ] File upload validation (magic bytes, size, scan).
- [ ] Secrets in secrets manager (never in code/env).
- [ ] Encryption at rest (DB, S3, Redis).
- [ ] Column-level encryption for sensitive fields.
- [ ] Audit log on every mutation.
- [ ] Logging of all auth events (login, logout, failed, MFA).
- [ ] Dependency scanning on every PR.
- [ ] Container image scanning on every build.
- [ ] SAST scan on every PR.
- [ ] DAST scan weekly on staging.

### 10.2 Frontend

- [ ] JWT in memory (not localStorage).
- [ ] Refresh token in HttpOnly + Secure + SameSite=Strict cookie.
- [ ] No sensitive data in URL params.
- [ ] React auto-escape used (no `dangerouslySetInnerHTML` except with DOMPurify).
- [ ] CSP-compliant (no inline scripts).
- [ ] Bundle size monitored (no accidental secret inclusion).
- [ ] Source maps not exposed in production.
- [ ] HTTPS only (HSTS preload).

### 10.3 Mobile

- [ ] Access token in memory.
- [ ] Refresh token in Secure Keychain (iOS) / EncryptedSharedPreferences (Android).
- [ ] Certificate pinning (Phase 2+ for high-security customers).
- [ ] Biometric unlock (Face ID / Touch ID / fingerprint) — optional, configurable.
- [ ] No sensitive data in URL params.
- [ ] App screenshots disabled in sensitive screens (e.g., emergency report).
- [ ] Root/jailbreak detection (warn user, optional block).

### 10.4 Infrastructure

- [ ] K8s RBAC enabled.
- [ ] Network policies (pods can only reach required services).
- [ ] Private subnets for DB, Redis, RabbitMQ.
- [ ] Public subnets only for load balancer + NAT gateway.
- [ ] WAF on load balancer (OWASP rules).
- [ ] DDoS protection (AWS Shield Standard on CloudFront + ALB).
- [ ] Security groups: deny by default, allow by explicit rule.
- [ ] IAM roles: least privilege.
- [ ] CloudTrail / Audit Log enabled (AWS account-level audit).
- [ ] Backup encryption.
- [ ] DR plan tested quarterly.

---

## 11. Compliance Alignment

### 11.1 UAE PDPL (Federal Decree-Law No. 45 of 2021)

| PDPL requirement | How we comply |
|---|---|
| Consent for data collection | Parent consent captured at invitation acceptance. Configurable per data category. |
| Data subject right: access | Parent can view all child's data via portal. Admin-only function for staff data. |
| Data subject right: correction | Parent can request correction via messaging. Admin can edit any record (audited). |
| Data subject right: deletion | Soft-delete for student record. Hard delete after retention period. Legal hold for safeguarding records. |
| Data minimization | Only collect what's needed for nursery operations. No marketing tracking without consent. |
| Purpose limitation | Data used only for nursery operations. Not shared with third parties without consent. |
| Data residency | UAE-hosted infrastructure (AWS me-central-1 or customer's UAE cloud). |
| Breach notification | Incident response runbook. Notify authorities within 72 hours of breach discovery. |
| Data Protection Officer (DPO) | Customer designates DPO. We provide tools (audit log, consent management) for DPO. |
| Children's data | Special protection: encryption, access controls, audit, retention limits. |
| Cross-border transfer | Not permitted without explicit consent + adequate protection. We host in UAE only (SaaS default). |

### 11.2 KHDA / ADEK Alignment

| Requirement | How we comply |
|---|---|
| Record retention | Configurable retention per record type (proposed: attendance 7y, medical 10y, assessment 5y). |
| Inspection readiness | Reports module generates KHDA-aligned reports (Phase 1 Sprint 13). Audit log accessible. |
| Staff qualifications tracking | `staff.qualifications` JSONB field. Reports for inspection. |
| Child safety | Ratio enforcement, emergency reports, safeguarding flags, audit trail. |
| Parent communication records | Activity feed + messaging + daily reports all retained per policy. |

Full compliance details: `17-COMPLIANCE-LEGAL.md`.

---

## 12. Penetration Testing

### 12.1 Schedule

- **Phase 1 go-live** (Sprint 17): External pen test by 3rd party, OWASP Top 10 + business logic.
- **Phase 2 go-live** (Sprint 26): External pen test, focus on new modules (Learning & Assessment, mobile app).
- **Phase 3 go-live** (Sprint 30): External pen test, focus on payment gateway + multi-tenancy + Open API.
- **Annual** (ongoing): Annual pen test by 3rd party.

### 12.2 Scope

- Web application (admin + parent portal).
- Mobile app (iOS + Android).
- API (all endpoints).
- Authentication (Keycloak, JWT, MFA).
- Multi-tenancy isolation (cross-tenant access attempts).
- Parent-child ownership (ID enumeration attempts).
- File upload (malicious file attempts).
- Infrastructure (K8s, network, secrets).

### 12.3 Remediation

- Critical findings: fix before go-live (block release).
- High findings: fix within 1 week.
- Medium findings: fix within 1 month.
- Low findings: fix in next sprint.

All findings tracked in GitHub Issues with security label. Reproducible test cases added to regression suite.

---

## 13. Incident Response

### 13.1 Incident Severity

| Severity | Definition | Response time |
|---|---|---|
| SEV-1 | Active data breach, child safety incident, production outage | 15 min |
| SEV-2 | Security vulnerability being exploited, major functionality broken | 1 hour |
| SEV-3 | Bug affecting many users, degraded service | 4 hours |
| SEV-4 | Minor bug, single user impact | 1 business day |

### 13.2 Incident Response Runbook

`infra/docs/incident-response.md` contains:
- On-call rotation.
- Escalation matrix.
- Communication templates (customer email, status page update).
- Forensic steps (snapshot DB, preserve logs, don't destroy evidence).
- Recovery steps per incident type.
- Post-mortem template.

### 13.3 Breach Notification

Per UAE PDPL:
- Notify UAE Data Office within 72 hours of breach discovery.
- Notify affected individuals "without undue delay" if high risk.
- Document breach: nature, scope, individuals affected, remediation.

### 13.4 Post-Mortem

After every SEV-1/SEV-2 incident:
- Blameless post-mortem within 5 business days.
- Root cause analysis (5 Whys).
- Action items with owners + due dates.
- Action items tracked to completion.
- Post-mortem document archived in `docs/post-mortems/`.

---

**Next file to read**: `10-MULTI-TENANCY.md` for the dual-mode (on-prem + SaaS) multi-tenancy architecture.
