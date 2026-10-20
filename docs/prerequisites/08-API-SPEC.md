# 08 — API Specification

> REST API conventions, OpenAPI 3.0, pagination, error handling, SignalR real-time, and the endpoint catalog.

---

## 1. API Design Principles

1. **RESTful** — Resources are nouns, HTTP methods are verbs. URL structure reflects domain model.
2. **Versioned** — URI-based versioning: `/api/v1/`. v2 introduced only for breaking changes, with parallel support period.
3. **Consistent envelope** — All responses wrapped in `{ success, data, meta, errors }` for predictability.
4. **Cursor pagination** — All list endpoints use cursor-based pagination. No OFFSET.
5. **Idempotency** — All mutating operations support `Idempotency-Key` header for safe retries.
6. **Stateless** — Server stores no per-request state. JWT in `Authorization` header for auth.
7. **OpenAPI-first** — OpenAPI 3.0 spec is the source of truth. Frontend/mobile clients generated from it.
8. **HTTPS only** — No HTTP. TLS 1.2+ enforced at load balancer.
9. **UTF-8 everywhere** — All requests/responses UTF-8 encoded.
10. **ISO 8601 dates** — All dates in UTC with timezone offset: `2026-05-17T10:30:00+04:00`.

---

## 2. URL Structure

```
Base URL (SaaS):   https://api.nms.ae/api/v1
Base URL (On-prem): https://api.<nursery-domain>/api/v1
```

Resource URLs follow this pattern:

| Operation | Method | URL |
|---|---|---|
| List | GET | `/api/v1/students` |
| Create | POST | `/api/v1/students` |
| Get one | GET | `/api/v1/students/{id}` |
| Replace | PUT | `/api/v1/students/{id}` |
| Partial update | PATCH | `/api/v1/students/{id}` |
| Delete (soft) | DELETE | `/api/v1/students/{id}` |
| Sub-resource list | GET | `/api/v1/students/{id}/guardians` |
| Action (verb) | POST | `/api/v1/students/{id}/actions/check-in` |
| Async action | POST | `/api/v1/reports/attendance/monthly` → 202 + job_id |

---

## 3. Request/Response Envelope

### 3.1 Success Response

```json
{
  "success": true,
  "data": { /* the resource or array of resources */ },
  "meta": {
    "pagination": {
      "cursor": "eyJpZCI6IjEyMyIsImNyZWF0ZWRfYXQiOiIyMDI2LTA1LTE3VDEwOjMwOjAwWiJ9",
      "next_cursor": "eyJpZCI6IjEyNCIsImNyZWF0ZWRfYXQiOiIyMDI2LTA1LTE3VDEwOjMxOjAwWiJ9",
      "has_more": true,
      "limit": 50
    },
    "request_id": "req_abc123",
    "timestamp": "2026-05-17T10:30:00.123Z"
  }
}
```

### 3.2 Error Response

```json
{
  "success": false,
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "message": "The field 'email' is required.",
      "field": "email",
      "details": { "locale": "en" }
    }
  ],
  "meta": {
    "request_id": "req_abc123",
    "timestamp": "2026-05-17T10:30:00.123Z"
  }
}
```

### 3.3 Async Response (202 Accepted)

```json
{
  "success": true,
  "data": {
    "job_id": "job_xyz789",
    "status": "queued",
    "status_url": "/api/v1/jobs/job_xyz789"
  },
  "meta": {
    "request_id": "req_abc123",
    "timestamp": "2026-05-17T10:30:00.123Z"
  }
}
```

Client polls `status_url` until status is `completed` or `failed`. Or subscribes via SignalR for `JobCompleted` event.

---

## 4. HTTP Status Codes

| Code | Meaning | When used |
|---|---|---|
| 200 OK | Success | GET, PATCH, PUT, DELETE success |
| 201 Created | Resource created | POST that creates a new resource |
| 202 Accepted | Async job accepted | POST that triggers long-running work |
| 204 No Content | Success, no body | DELETE success |
| 400 Bad Request | Validation error | Invalid input, schema mismatch |
| 401 Unauthorized | Not authenticated | Missing or invalid JWT |
| 403 Forbidden | Not authorized | Authenticated but lacks permission |
| 404 Not Found | Resource doesn't exist | ID not found (also used for parent-child ownership denial — prevents ID enumeration) |
| 409 Conflict | Duplicate or state conflict | E.g., email already exists |
| 410 Gone | Resource permanently gone | E.g., expired invitation token |
| 422 Unprocessable Entity | Semantic validation error | E.g., trying to check out a student not checked in |
| 429 Too Many Requests | Rate limited | Rate limit exceeded |
| 500 Internal Server Error | Server bug | Unhandled exception |
| 503 Service Unavailable | Temporary | Maintenance, dependency outage |

---

## 5. Error Codes

| Code | HTTP | Meaning |
|---|---|---|
| `VALIDATION_ERROR` | 400 | Field validation failed |
| `MISSING_FIELD` | 400 | Required field missing |
| `INVALID_FORMAT` | 400 | Field format invalid (e.g., bad UUID) |
| `UNAUTHORIZED` | 401 | JWT missing or expired |
| `INSUFFICIENT_PERMISSION` | 403 | User lacks required role/permission |
| `RESOURCE_NOT_FOUND` | 404 | Resource doesn't exist |
| `PARENT_CHILD_OWNERSHIP_DENIED` | 404 | Parent tried to access another parent's child (404 not 403 to prevent enumeration) |
| `DUPLICATE_RESOURCE` | 409 | Unique constraint violation |
| `INVALID_STATE_TRANSITION` | 422 | E.g., checking out a student who isn't checked in |
| `IMMUTABLE_RECORD` | 422 | Trying to update attendance_event or audit_log |
| `RATE_LIMIT_EXCEEDED` | 429 | Too many requests |
| `TENANT_SUSPENDED` | 403 | Tenant's subscription lapsed |
| `EXTERNAL_SERVICE_ERROR` | 502 | Upstream service (FCM, SES, etc.) failed |
| `INTERNAL_ERROR` | 500 | Unhandled server error |

Each error includes a `message` (English) and may include `message_i18n` with translations when relevant. Validation errors include `field` for form binding.

---

## 6. Pagination

All list endpoints use cursor-based pagination.

### 6.1 Request Parameters

| Param | Type | Default | Description |
|---|---|---|---|
| `cursor` | string (base64) | (none) | Cursor from previous response's `next_cursor` |
| `limit` | int | 50 | Page size (max 100) |
| `sort` | string | `created_at:desc` | Sort field + direction |

### 6.2 Cursor Format

Cursor is base64-encoded JSON containing the sort field value + ID of the last item:
```json
{
  "id": "uuid-of-last-item",
  "created_at": "2026-05-17T10:30:00Z"
}
```

Server decodes cursor, queries `WHERE (created_at, id) < (cursor_created_at, cursor_id) ORDER BY created_at DESC, id DESC LIMIT N+1`. If N+1 rows returned, `has_more=true`.

### 6.3 Example

Request:
```
GET /api/v1/students?limit=50&cursor=eyJpZCI6...
```

Response:
```json
{
  "success": true,
  "data": [ /* 50 student objects */ ],
  "meta": {
    "pagination": {
      "limit": 50,
      "has_more": true,
      "next_cursor": "eyJpZCI6..."
    }
  }
}
```

Final page: `has_more=false`, `next_cursor=null`.

---

## 7. Filtering

List endpoints support filtering via query params. Conventions:

- **Exact match**: `?status=active`
- **Multiple values**: `?status=active&status=on_hold` (OR logic)
- **Range**: `?created_after=2026-05-01&created_before=2026-06-01`
- **Search**: `?q=ahmed` (server decides which fields to search)
- **Nested**: `?room_id=uuid-1&program_id=uuid-2`

Filters are documented per endpoint in OpenAPI spec.

---

## 8. Sorting

`?sort=field:direction` where direction is `asc` or `desc`.

- Multiple sort fields: `?sort=last_name:asc,first_name:asc`
- Allowed sort fields documented per endpoint.
- Default sort: `created_at:desc`.

---

## 9. Authentication

### 9.1 JWT Bearer Token

```
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6...
```

- Issued by Keycloak.
- Access token: 15-minute lifetime.
- Refresh token: 7-day lifetime (stored in HttpOnly cookie for web, Secure Keychain for iOS, EncryptedSharedPreferences for Android).
- Claims include: `sub` (user_id), `tenant_id`, `realm_access.roles`, `email`, `name`, `preferred_language`.

### 9.2 Token Refresh

```
POST /api/v1/auth/refresh
Cookie: refresh_token=<refresh-jwt>
```

Returns new access token. Refresh token rotated on each use (detects theft).

### 9.3 Token Revocation

- Logout: revoke refresh token immediately.
- Password change: revoke all user's refresh tokens.
- Account suspension: revoke all user's refresh tokens.

### 9.4 Service-to-Service (Phase 3+)

For Open API + extracted services:
- OAuth 2.0 client credentials flow.
- Separate client_id + client_secret per integration.
- Tokens have scopes (e.g., `students:read`, `invoices:write`).

---

## 10. Idempotency

All mutating operations (POST, PUT, PATCH, DELETE) support `Idempotency-Key` header:

```
POST /api/v1/students
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{ "first_name": "Ahmed", ... }
```

- Server stores Idempotency-Key + response for 24 hours.
- Same key + same request body → server returns cached response (no duplicate side effects).
- Same key + different request body → 422 Unprocessable Entity (idempotency conflict).
- Different key → normal processing.

Implemented via `IdempotencyMiddleware` in .NET pipeline.

---

## 11. Localization

### 11.1 Accept-Language Header

```
Accept-Language: ar
```

- Server returns translated content for static messages (error messages, validation messages).
- Resource fields (e.g., `room.name`) return JSONB with both `en` and `ar` keys — client picks.
- Default: `en` if no Accept-Language header.

### 11.2 Resource Localization

For resources with i18n fields, response includes full JSONB:
```json
{
  "name": { "en": "Toddlers Room", "ar": "غرفة الأطفال" }
}
```

Client displays based on user's preferred language, falling back to English if missing.

### 11.3 DateTime Localization

All DateTimes returned in ISO 8601 with UTC + offset. Client formats per user's locale + timezone.

For dates displayed to users (e.g., "May 17, 2026"), client uses `Intl.DateTimeFormat` with locale + timezone.

---

## 12. Rate Limiting

### 12.1 Per-User Limits

| Endpoint type | Limit | Window |
|---|---|---|
| Authenticated API | 100 requests | 1 minute |
| Unauthenticated API (e.g., public enquiry form) | 10 requests | 1 minute |
| Login endpoint | 5 attempts | 1 minute (then 15-min lockout) |
| Password reset | 3 requests | 1 hour |
| File upload presign | 20 requests | 1 minute |

### 12.2 Implementation

- Redis-based sliding window counter.
- Headers returned: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`.
- 429 response includes `Retry-After` header (seconds until reset).
- Blocked IPs logged + surfaced in Grafana dashboard.

### 12.3 Tenant-Level Limits (SaaS Phase 3)

Each tenant has a daily API call quota based on subscription tier:
- Tier 1: 10,000 calls/day
- Tier 2: 25,000 calls/day
- Tier 3: 50,000 calls/day
- Tier 4: 100,000 calls/day
- On-prem: unlimited

Quota exceeded → 429 with upgrade prompt.

---

## 13. SignalR Real-Time

### 13.1 Hub URL

```
wss://api.nms.ae/hubs/tenant
```

### 13.2 Authentication

JWT passed as query string on connect (SignalR requirement):
```
wss://api.nms.ae/hubs/tenant?access_token=<jwt>
```

Server validates JWT on connection, extracts `tenant_id` + `user_id` + `roles`, adds to groups.

### 13.3 Client → Server Methods

| Method | Payload | Description |
|---|---|---|
| `JoinRoom` | `{ roomId: "uuid" }` | Subscribe to room-specific updates |
| `LeaveRoom` | `{ roomId: "uuid" }` | Unsubscribe |
| `MarkNotificationRead` | `{ notificationId: "uuid" }` | Acknowledge notification (UX only, not delivery ack) |
| `Ping` | `{ timestamp: "..." }` | Keep-alive |

### 13.4 Server → Client Methods

| Method | Payload | Description |
|---|---|---|
| `AttendanceUpdated` | `{ personId, personType, status, roomId, timestamp }` | Check-in/out event |
| `RatioUpdated` | `{ roomId, studentsPresent, staffPresent, requiredRatio, isBreaching }` | Ratio change |
| `NotificationReceived` | `{ notification: NotificationDto }` | New notification for user |
| `MessageReceived` | `{ threadId, message: MessageDto }` | New message in thread |
| `ActivityPosted` | `{ activityPost: ActivityPostDto }` | New approved activity (parents) |
| `DashboardRefreshed` | `{ widget: "kpi_card" | "ratio" | "pending_actions", data: {...} }` | Dashboard widget update |
| `BillingUpdated` | `{ invoiceId, status, amountPaidFils }` | Billing event |
| `JobCompleted` | `{ jobId, status, resultUrl? }` | Async job finished |
| `Connected` | `{ connectionId, groups: [...] }` | Connection confirmation |
| `Error` | `{ message, code }` | Connection error |

### 13.5 Reconnection

Client uses `@microsoft/signalr` automatic reconnect:
- Retry delays: 0, 2, 10, 30 seconds (then stop).
- On reconnect: client re-fetches state for changed widgets (server doesn't replay missed events).
- Refresh token flow before reconnect if JWT expired.

---

## 14. File Uploads

### 14.1 Presigned URL Flow

Direct-to-S3 upload bypasses API server for binary data:

1. **Client requests presigned URL**:
   ```
   POST /api/v1/media/presign
   {
     "files": [
       { "contentType": "image/jpeg", "sizeBytes": 1234567 },
       { "contentType": "video/mp4", "sizeBytes": 23456789 }
     ],
     "purpose": "activity_post"  // or "student_photo", "document", "invoice_pdf", etc.
   }
   ```

2. **Server validates + returns presigned URLs**:
   ```json
   {
     "success": true,
     "data": [
       {
         "mediaId": "uuid-1",
         "uploadUrl": "https://s3.me-central-1.amazonaws.com/nms-media/uploads/uuid-1?X-Amz-...",
         "expiresAt": "2026-05-17T10:31:00Z",
         "objectKey": "uploads/uuid-1"
       },
       { ... }
     ]
   }
   ```

3. **Client uploads directly to S3**:
   ```
   PUT <uploadUrl>
   Content-Type: image/jpeg
   x-amz-server-side-encryption: AES256
   
   <binary data>
   ```

4. **Client references media in subsequent API call**:
   ```
   POST /api/v1/activities
   {
     "type": "photo",
     "mediaIds": ["uuid-1", "uuid-2"],
     "caption": { "en": "First day!", "ar": "..." },
     "studentIds": ["uuid-3"]
   }
   ```

5. **Server creates activity_post + media records, triggers scan**:
   - `media.scan_status = "pending"`
   - RabbitMQ event `MediaUploaded` published
   - Media Processing module scans file → updates `scan_status`

### 14.2 File Constraints

| Purpose | Allowed types | Max size |
|---|---|---|
| Student/staff photo | image/jpeg, image/png, image/heic | 5 MB |
| Activity post photo | image/jpeg, image/png, image/heic | 10 MB |
| Activity post video | video/mp4, video/quicktime | 100 MB |
| Document (resource centre, form upload) | application/pdf, application/vnd.openxmlformats-*, application/vnd.ms-excel, image/* | 25 MB |
| Invoice PDF (system-generated) | application/pdf | 5 MB |
| Newsletter attachment | application/pdf, image/* | 10 MB |

Server enforces type + size in `/media/presign` endpoint. S3 bucket policy also enforces (defense-in-depth).

---

## 15. Endpoint Catalog (Phase 1 MVP)

> Complete OpenAPI spec generated from code annotations. Below is the human-readable catalog of MVP endpoints.

### 15.1 Auth & Identity

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/api/v1/auth/login` | Login (returns access + refresh tokens) | Public |
| POST | `/api/v1/auth/refresh` | Refresh access token | Authenticated (refresh cookie) |
| POST | `/api/v1/auth/logout` | Logout (revoke refresh token) | Authenticated |
| POST | `/api/v1/auth/forgot-password` | Request password reset email | Public |
| POST | `/api/v1/auth/reset-password` | Reset password with token | Public |
| POST | `/api/v1/auth/verify-email` | Verify email with token | Public |
| POST | `/api/v1/auth/bootstrap-super-admin` | First Super Admin creation (one-time) | Public (with token) |
| GET | `/api/v1/auth/me` | Get current user + permissions | Authenticated |
| PATCH | `/api/v1/auth/me` | Update own profile (language, avatar, etc.) | Authenticated |
| POST | `/api/v1/auth/change-password` | Change password | Authenticated |
| POST | `/api/v1/auth/invite-parent` | Send parent invitation (creates guardian with `invited` status) | Centre Admin+ |
| POST | `/api/v1/auth/accept-invitation` | Accept parent invitation (creates app_user + links guardian) | Public (with token) |

### 15.2 Students

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/students` | List students (filter, paginate) | Staff (room-scoped), Parent (own children only) |
| POST | `/api/v1/students` | Create student | Centre Admin+ |
| GET | `/api/v1/students/{id}` | Get student details | Staff (room-scoped), Parent (own child) |
| PATCH | `/api/v1/students/{id}` | Update student | Centre Admin+ |
| DELETE | `/api/v1/students/{id}` | Soft-delete student | Centre Admin+ |
| POST | `/api/v1/students/{id}/actions/transition-status` | Transition status (signed_up→active→on_hold→withdrawn) | Centre Admin+ |
| GET | `/api/v1/students/{id}/guardians` | List guardians | Staff (room-scoped), Parent (own child) |
| POST | `/api/v1/students/{id}/guardians` | Add guardian | Centre Admin+ |
| PATCH | `/api/v1/students/{id}/guardians/{guardianId}` | Update guardian | Centre Admin+ |
| DELETE | `/api/v1/students/{id}/guardians/{guardianId}` | Remove guardian | Centre Admin+ |
| GET | `/api/v1/students/{id}/emergency-report` | Generate emergency child report PDF | Staff+ |
| GET | `/api/v1/students/{id}/activities` | List activities for student | Staff (room-scoped), Parent (own child) |
| GET | `/api/v1/students/{id}/attendance` | Get student attendance history | Staff (room-scoped), Parent (own child) |

### 15.3 Staff

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/staff` | List staff | Centre Admin+ |
| POST | `/api/v1/staff` | Create staff | Centre Admin+ |
| GET | `/api/v1/staff/{id}` | Get staff details | Centre Admin+ (or self) |
| PATCH | `/api/v1/staff/{id}` | Update staff | Centre Admin+ (or self for own profile) |
| DELETE | `/api/v1/staff/{id}` | Soft-delete staff | Centre Admin+ |
| GET | `/api/v1/staff/{id}/room-assignments` | List room assignments | Centre Admin+ |
| POST | `/api/v1/staff/{id}/room-assignments` | Assign staff to room (with time slot) | Centre Admin+ |

### 15.4 Rooms / Programs / Groups / Labels

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/rooms` | List rooms | Staff+ |
| POST | `/api/v1/rooms` | Create room | Centre Admin+ |
| GET | `/api/v1/rooms/{id}` | Get room with current ratio | Staff+ |
| PATCH | `/api/v1/rooms/{id}` | Update room | Centre Admin+ |
| DELETE | `/api/v1/rooms/{id}` | Soft-delete room | Centre Admin+ |
| GET | `/api/v1/programs` | List programs | Staff+ |
| POST | `/api/v1/programs` | Create program | Centre Admin+ |
| PATCH | `/api/v1/programs/{id}` | Update program | Centre Admin+ |
| GET | `/api/v1/groups` | List groups | Staff+ |
| POST | `/api/v1/groups` | Create group | Centre Admin+ |
| GET | `/api/v1/labels` | List labels | Staff+ |
| POST | `/api/v1/labels` | Create label | Centre Admin+ |

### 15.5 Attendance

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/attendance` | List attendance records (filter by date, room, person_type) | Staff+ |
| POST | `/api/v1/attendance/actions/check-in` | Check in student/staff (barcode or manual) | Staff+ |
| POST | `/api/v1/attendance/actions/check-out` | Check out student/staff | Staff+ |
| POST | `/api/v1/attendance/actions/mark-absent` | Mark student absent | Staff+ |
| PATCH | `/api/v1/attendance/{id}` | Edit attendance (supervisor override after 24h) | Centre Admin+ |
| GET | `/api/v1/attendance/ratio/live` | Get live ratios for all rooms | Staff+ |
| GET | `/api/v1/attendance/ratio/history` | Get ratio breach history | Centre Admin+ |
| GET | `/api/v1/attendance/forecast` | Get attendance forecast by program | Staff+ |
| GET | `/api/v1/attendance/export` | Export sign-in sheet PDF/Excel | Staff+ |
| GET | `/api/v1/attendance/report/monthly` | Monthly attendance report | Centre Admin+ |

### 15.6 Leaves

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/leaves` | List leave requests | Staff+ |
| POST | `/api/v1/leaves` | Create leave request | Staff+ (for self or students in room) |
| GET | `/api/v1/leaves/{id}` | Get leave request | Staff+ |
| POST | `/api/v1/leaves/{id}/actions/approve` | Approve leave | Centre Admin+ |
| POST | `/api/v1/leaves/{id}/actions/reject` | Reject leave | Centre Admin+ |
| POST | `/api/v1/leaves/{id}/actions/cancel` | Cancel leave (requester) | Staff+ |

### 15.7 Activity Posts (Parent Communication)

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/activities` | List activity posts (filter by room, student, type, status) | Staff (room-scoped), Parent (own child) |
| POST | `/api/v1/activities` | Create activity post (requires approval before parent visibility) | Staff+ |
| GET | `/api/v1/activities/{id}` | Get activity post | Staff (room-scoped), Parent (if linked) |
| PATCH | `/api/v1/activities/{id}` | Update activity post (only if not yet approved) | Staff+ |
| DELETE | `/api/v1/activities/{id}` | Soft-delete activity post | Staff+ (own) or Centre Admin+ |
| POST | `/api/v1/activities/{id}/actions/approve` | Approve for parent visibility | Centre Admin+ (or auto by content moderation) |
| POST | `/api/v1/activities/{id}/actions/reject` | Reject with reason | Centre Admin+ |
| GET | `/api/v1/activities/daily-report/{studentId}/{date}` | Get daily report for student | Staff (room-scoped), Parent (own child) |
| POST | `/api/v1/activities/daily-report/generate` | Manually trigger daily report generation (auto at 3:30 PM) | Centre Admin+ |

### 15.8 Messaging

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/threads` | List message threads (inbox) | Staff+, Parent |
| POST | `/api/v1/threads` | Create thread (parent-staff, staff-staff) | Staff+, Parent |
| GET | `/api/v1/threads/{id}` | Get thread with messages (paginated) | Staff+, Parent (participant) |
| POST | `/api/v1/threads/{id}/messages` | Send message | Staff+, Parent (participant) |
| POST | `/api/v1/threads/{id}/actions/mark-read` | Mark thread read for current user | Staff+, Parent (participant) |
| POST | `/api/v1/threads/{id}/actions/archive` | Archive thread | Staff+ |
| POST | `/api/v1/threads/broadcast` | Send broadcast announcement | Centre Admin+ |

### 15.9 Media

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/api/v1/media/presign` | Get presigned upload URLs | Authenticated |
| GET | `/api/v1/media/{id}/download-url` | Get signed download URL (5-min expiry) | Authenticated (with ownership check) |
| GET | `/api/v1/media/{id}` | Get media metadata | Authenticated (with ownership check) |

### 15.10 Notifications

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/notifications` | List current user's notifications | Authenticated |
| POST | `/api/v1/notifications/{id}/actions/mark-read` | Mark notification read | Authenticated |
| POST | `/api/v1/notifications/{id}/actions/dismiss` | Dismiss notification | Authenticated |
| POST | `/api/v1/notifications/actions/mark-all-read` | Mark all read | Authenticated |
| GET | `/api/v1/notifications/unread-count` | Get unread count (for badge) | Authenticated |
| PATCH | `/api/v1/notifications/preferences` | Update notification preferences | Authenticated |

### 15.11 Dashboard

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/dashboard/kpis` | Get KPI cards data | Staff+ |
| GET | `/api/v1/dashboard/attendance-summary` | Today's attendance summary | Staff+ |
| GET | `/api/v1/dashboard/pending-actions` | Pending actions count | Staff+ |
| GET | `/api/v1/dashboard/billing-snapshot` | Billing snapshot widget data | Centre Admin+, Accounts |
| GET | `/api/v1/dashboard/enquiry-pipeline` | Enquiry pipeline widget data | Centre Admin+ |

### 15.12 Settings & Audit Log

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/api/v1/settings` | Get all settings | Centre Admin+ |
| PATCH | `/api/v1/settings` | Update settings | Centre Admin+ |
| GET | `/api/v1/settings/roles` | List roles | Super Admin |
| POST | `/api/v1/settings/roles` | Create custom role | Super Admin |
| PATCH | `/api/v1/settings/roles/{id}` | Update role permissions | Super Admin |
| GET | `/api/v1/settings/audit-log` | Query audit log | Super Admin |
| GET | `/api/v1/settings/audit-log/export` | Export audit log to Excel | Super Admin |

---

## 16. Endpoint Catalog (Phase 1 Full — Beyond MVP)

The following endpoints are added in Phase 1 sprints after MVP. Full specs generated from code.

### 16.1 Billing (Sprint 7–8)

```
GET    /api/v1/fee-plans
POST   /api/v1/fee-plans
GET    /api/v1/fee-plans/{id}
PATCH  /api/v1/fee-plans/{id}
DELETE /api/v1/fee-plans/{id}
POST   /api/v1/fee-plans/{id}/components
PATCH  /api/v1/fee-plans/{id}/components/{componentId}

GET    /api/v1/invoices
POST   /api/v1/invoices                  # Manual creation (auto-gen uses /api/v1/invoices/actions/generate)
GET    /api/v1/invoices/{id}
PATCH  /api/v1/invoices/{id}
POST   /api/v1/invoices/{id}/actions/cancel
POST   /api/v1/invoices/{id}/actions/send  # Email invoice to parent
POST   /api/v1/invoices/{id}/payments     # Record manual payment
GET    /api/v1/invoices/{id}/payments
POST   /api/v1/invoices/actions/generate  # Trigger batch generation (Hangfire job)

GET    /api/v1/expenses
POST   /api/v1/expenses
PATCH  /api/v1/expenses/{id}
DELETE /api/v1/expenses/{id}

GET    /api/v1/billing/dashboard

# Templates
GET    /api/v1/email-templates
POST   /api/v1/email-templates
PATCH  /api/v1/email-templates/{id}
```

### 16.2 Enquiries & Tours (Sprint 9–10)

```
GET    /api/v1/enquiries
POST   /api/v1/enquiries
GET    /api/v1/enquiries/{id}
PATCH  /api/v1/enquiries/{id}
POST   /api/v1/enquiries/{id}/actions/transition-stage
POST   /api/v1/enquiries/{id}/actions/convert-to-student
POST   /api/v1/enquiries/{id}/actions/archive
GET    /api/v1/enquiries/{id}/stage-history
GET    /api/v1/enquiries/analytics/conversion-rate

GET    /api/v1/tours
POST   /api/v1/tours
GET    /api/v1/tours/{id}
PATCH  /api/v1/tours/{id}
POST   /api/v1/tours/{id}/actions/complete
POST   /api/v1/tours/{id}/actions/cancel
POST   /api/v1/tours/{id}/actions/no-show
GET    /api/v1/tours/calendar  # Calendar view
GET    /api/v1/tours/print     # PDF export

GET    /api/v1/staff/{id}/block-offs
POST   /api/v1/staff/{id}/block-offs
DELETE /api/v1/staff/{id}/block-offs/{blockOffId}
```

### 16.3 Calendar (Sprint 11–12)

```
GET    /api/v1/calendar/school?month=2026-05
GET    /api/v1/calendar/admin?month=2026-05
GET    /api/v1/calendar/staff/{staffId}?month=2026-05
GET    /api/v1/calendar/birthdays?month=2026-05&type=student|staff
GET    /api/v1/calendar/staff-leaves?month=2026-05
GET    /api/v1/calendar/student-schedule?studentId=...&month=...

POST   /api/v1/calendar/events
GET    /api/v1/calendar/events/{id}
PATCH  /api/v1/calendar/events/{id}
DELETE /api/v1/calendar/events/{id}
POST   /api/v1/calendar/actions/import-holidays  # ICS import
GET    /api/v1/calendar/ics-feed  # iCal subscription URL (per-user)
```

### 16.4 Forms & Admissions (Sprint 11–12)

```
GET    /api/v1/forms
POST   /api/v1/forms
GET    /api/v1/forms/{id}
PATCH  /api/v1/forms/{id}
POST   /api/v1/forms/{id}/actions/publish
POST   /api/v1/forms/{id}/actions/archive
GET    /api/v1/forms/{id}/analytics

# Public form submission (no auth)
POST   /api/v1/public/forms/{slug}/submit

# Authenticated form submission
GET    /api/v1/forms/{id}/submissions
GET    /api/v1/forms/{id}/submissions/{submissionId}

# Admissions
GET    /api/v1/admissions/processes
POST   /api/v1/admissions/processes
PATCH  /api/v1/admissions/processes/{id}
POST   /api/v1/admissions/processes/{id}/actions/complete-step
```

### 16.5 Tools (Sprint 11–12)

```
GET    /api/v1/tools/id-card-templates
POST   /api/v1/tools/id-cards/actions/generate  # Bulk PDF generation (async)
GET    /api/v1/tools/certificate-templates
POST   /api/v1/tools/certificates/actions/generate  # Bulk PDF generation (async)
```

### 16.6 Reports & Analytics (Sprint 13–14)

```
POST   /api/v1/reports/attendance/monthly          # Async generation
POST   /api/v1/reports/billing/summary
POST   /api/v1/reports/enquiry/conversion
POST   /api/v1/reports/app-adoption/parent
POST   /api/v1/reports/app-adoption/staff
POST   /api/v1/reports/audit-log/export
POST   /api/v1/reports/sign-in-sheet

GET    /api/v1/reports/history  # Recently generated reports
GET    /api/v1/reports/{jobId}/download
```

### 16.7 Newsletter & Resource Centre (Sprint 5–6 full)

```
GET    /api/v1/newsletters
POST   /api/v1/newsletters
GET    /api/v1/newsletters/{id}
PATCH  /api/v1/newsletters/{id}
POST   /api/v1/newsletters/{id}/actions/schedule
POST   /api/v1/newsletters/{id}/actions/publish
POST   /api/v1/newsletters/{id}/actions/ai-generate-draft  # AI-assisted content

GET    /api/v1/resource-centre/folders
POST   /api/v1/resource-centre/folders
PATCH  /api/v1/resource-centre/folders/{id}
GET    /api/v1/resource-centre/documents
POST   /api/v1/resource-centre/documents
GET    /api/v1/resource-centre/documents/{id}
PATCH  /api/v1/resource-centre/documents/{id}
GET    /api/v1/resource-centre/documents/{id}/versions
GET    /api/v1/resource-centre/documents/{id}/download-log
```

---

## 17. Endpoint Catalog (Phase 2 — Learning & Assessment)

```
# Curriculum
GET    /api/v1/curriculum/frameworks
GET    /api/v1/curriculum/frameworks/{id}/development-areas
GET    /api/v1/curriculum/frameworks/{id}/age-bands
GET    /api/v1/curriculum/milestones?frameworkId=...&areaId=...&ageBandId=...

# Lessons
GET    /api/v1/lessons
POST   /api/v1/lessons
GET    /api/v1/lessons/{id}
PATCH  /api/v1/lessons/{id}
DELETE /api/v1/lessons/{id}

# Observations
GET    /api/v1/observations?studentId=...
POST   /api/v1/observations
GET    /api/v1/observations/{id}
PATCH  /api/v1/observations/{id}
POST   /api/v1/observations/{id}/actions/flag-concern
POST   /api/v1/observations/actions/ai-transcribe  # Voice-to-text dictation

# Assessments
GET    /api/v1/assessment-templates
POST   /api/v1/assessments
GET    /api/v1/assessments/{id}
PATCH  /api/v1/assessments/{id}
POST   /api/v1/assessments/{id}/actions/approve-parent-view
GET    /api/v1/assessments/{id}/staff-view
GET    /api/v1/assessments/{id}/parent-view
POST   /api/v1/assessments/{id}/actions/generate-pdf

# Learning Journey
GET    /api/v1/students/{id}/learning-journey?from=...&to=...
GET    /api/v1/students/{id}/learning-journey/export  # PDF

# Reports
POST   /api/v1/reports/assessment/progress
POST   /api/v1/reports/assessment/snapshot
POST   /api/v1/reports/assessment/comparison
POST   /api/v1/reports/assessment/class-milestone
POST   /api/v1/reports/assessment/student-milestone
```

---

## 18. Endpoint Catalog (Phase 3 — Multi-Centre, Payments, ERP, Open API)

```
# Multi-centre (MCG)
GET    /api/v1/mcg                          # Multi-centre group info
GET    /api/v1/mcg/centres                  # List centres in group
POST   /api/v1/mcg/centres                  # Provision new centre
PATCH  /api/v1/mcg/centres/{centreId}
POST   /api/v1/mcg/centres/{centreId}/actions/suspend
GET    /api/v1/mcg/dashboard                # Consolidated dashboard
GET    /api/v1/mcg/reports/consolidated-attendance
GET    /api/v1/mcg/reports/consolidated-revenue

# Payments
POST   /api/v1/payments/initiate            # Create payment session with gateway
GET    /api/v1/payments/{id}/status
POST   /api/v1/payments/webhook             # Gateway webhook receiver (signed)
POST   /api/v1/payments/{id}/actions/refund
GET    /api/v1/payments/methods             # List saved payment methods (per parent)

# ERP Integration
GET    /api/v1/integrations/erp/status
POST   /api/v1/integrations/erp/sync        # Manual sync trigger
GET    /api/v1/integrations/erp/chart-of-accounts-mapping
PATCH  /api/v1/integrations/erp/chart-of-accounts-mapping

# Open API (third-party)
POST   /api/v1/oauth/token                  # Client credentials flow
GET    /api/v1/open-api/students            # Same as /students but with OAuth scope check
GET    /api/v1/open-api/invoices
POST   /api/v1/open-api/webhooks            # Register webhook URL
DELETE /api/v1/open-api/webhooks/{id}
```

---

## 19. OpenAPI Generation

### 19.1 .NET OpenAPI Setup

```csharp
// Program.cs
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "NMS API",
            Version = "v1",
            Description = "Nursery Management System API",
            Contact = new OpenApiContact { Email = "api@nms.ae" }
        };
        document.Components.SecuritySchemes["bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        return Task.CompletedTask;
    });
});

// In production
if (app.Environment.IsProduction())
{
    app.MapOpenApi("/openapi.json");  // Served at /openapi.json
}
```

### 19.2 Frontend Client Generation

```bash
# Generate TypeScript client from OpenAPI
pnpm exec openapi-typescript-codegen --input http://localhost:5000/openapi.json --output src/api/generated --client axios
```

Run as a script in `web/package.json`:
```json
{
  "scripts": {
    "generate:api": "openapi-typescript-codegen --input http://localhost:5000/openapi.json --output src/api/generated --client axios"
  }
}
```

Run after every API change. Commit generated code.

### 19.3 API Documentation UI

Use Scalar (modern, beautiful alternative to Swagger UI):

```csharp
app.MapScalarApiReference("/api-docs");
```

Available at `/api-docs` in dev + staging. Not exposed in production.

---

## 20. Versioning & Deprecation

### 20.1 Versioning Strategy

- URI-based: `/api/v1/`, `/api/v2/`.
- v2 introduced only for breaking changes (removed fields, changed semantics).
- v1 + v2 run in parallel for at least 6 months.
- Deprecation: `Sunset` HTTP header on deprecated endpoints with deprecation date.
- Internal API + mobile API versioned together.
- Open API (Phase 3) versioned independently.

### 20.2 Backward Compatibility

The following changes are non-breaking (no version bump):
- Adding new endpoint.
- Adding optional field to request body.
- Adding new field to response body.
- Adding new enum value (clients should handle unknown values gracefully).
- Changing field order in response.
- Adding optional query parameter.

The following ARE breaking (require version bump):
- Removing endpoint.
- Removing/renaming field in request or response.
- Changing field type.
- Changing field semantics.
- Making optional field required.
- Changing error response structure.

---

## 21. Caching Headers

| Endpoint type | Cache-Control | Notes |
|---|---|---|
| GET list (mutable) | `no-cache` | Always fresh |
| GET list (immutable reference data, e.g., rooms) | `private, max-age=60` | 1 min cache |
| GET single (mutable) | `no-cache` | Always fresh |
| GET single (immutable, e.g., audit log entry) | `private, max-age=300` | 5 min cache |
| Generated PDFs / reports | `private, max-age=3600` | 1 hour (signed URLs anyway) |
| Static assets (images, JS, CSS) | `public, max-age=31536000, immutable` | 1 year (filename hashed) |

All responses include `ETag` for conditional requests (`If-None-Match` → 304 Not Modified).

---

## 22. Health & Diagnostic Endpoints

```
GET /health/live     # 200 if process is alive (no DB check)
GET /health/ready    # 200 if can serve traffic (DB + Redis + RabbitMQ connected)
GET /health/startup  # 200 if startup completed (used by K8s startup probe)
GET /metrics         # Prometheus metrics (auth: admin only, or mTLS)
GET /version         # { "version": "1.0.0", "commit": "abc123", "build_time": "..." }
```

---

**Next file to read**: `09-AUTH-SECURITY.md` for Keycloak realms, RBAC matrix, and security details.
