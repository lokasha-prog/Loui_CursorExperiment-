# 15 — Notification System

> Three-tier notification architecture, delivery channels, retry/fallback logic, no-acknowledgement policy.

---

## 1. Overview

NMS notifications are classified into three tiers with distinct delivery needs. Per the source SRS and planning doc §7: **no individual notification delivery requires recipient acknowledgement** — delivery attempts and available delivery status are recorded, but the system does not require users to confirm receipt.

### 1.1 Three Tiers

| Tier | Examples | Channels | Suppressible? | User acknowledgement |
|---|---|---|---|---|
| **Critical / Safety** | Ratio breach, unexpected absence, safeguarding flag | In-app banner (red, persistent) + Push + SMS (Sprint 13+) | No — mandatory | Not required (UX "Acknowledge" button is for dismissal only, not delivery confirmation) |
| **Operational** | Leave approved/rejected, invoice generated, message received, activity pending approval | In-app notification + Push | Per user preference (push on/off per type) | Not required |
| **Informational / Reminders** | Fee reminder emails, daily report sent, upcoming birthday, newsletter published | Email + optional in-app | Yes — configurable, unsubscribe supported | Not required |

### 1.2 Design Principles

1. **No recipient acknowledgement required** — per source planning doc §7. We record delivery attempts + available status, but don't wait for user confirmation.
2. **Every attempt traceable** — `notification_delivery` table logs every attempt with status, provider response, retries.
3. **Tiered fallback** — Critical notifications have multi-channel fallback (push fail → SMS → email). Operational has limited fallback. Informational has no fallback (batch send, accept failures).
4. **Configurable per tenant** — Critical tier SMS enabled/disabled per tenant (cost control). Operational push preferences per user. Informational unsubscribe per user.
5. **Idempotent** — duplicate events don't create duplicate notifications (deduplication via `Idempotency-Key` + event ID).

---

## 2. Notification Lifecycle

```
1. Event occurs (e.g., ratio breach detected by Attendance module)
2. Attendance module publishes RatioBreachedEvent to RabbitMQ
3. Notification module consumes event:
   a. Determines tier (Critical), category (ratio_breach), recipients (all admins + assigned staff)
   b. For each recipient: creates notification record (status=pending)
   c. Determines channels based on tier + tenant config + user preferences
   d. For each channel: creates notification_delivery record (status=queued)
4. Notification dispatcher (Hangfire job) processes queued deliveries:
   a. Picks up delivery records with status=queued
   b. Calls channel-specific sender (FCM, SES, Twilio, in-app)
   c. Updates notification_delivery.status (sent, delivered, failed)
   d. On failure: schedules retry with exponential backoff
   e. On final failure (max retries): triggers fallback for Critical tier
5. Fallback logic (Critical tier only):
   a. If push fails after 3 retries → try SMS
   b. If SMS fails after 2 retries → try email
   c. If all channels fail → log to alert monitoring (PagerDuty)
6. Client receives notification:
   a. Push: OS displays notification (FCM/APNs)
   b. In-app: SignalR pushes to client, client shows banner
   c. Email: arrives in user's inbox
7. User interaction (optional, not required):
   a. Taps notification → app opens to deep-linked screen
   b. Dismisses in-app banner → updates notification.dismissed_at
   c. Marks as read → updates notification.is_read=true, read_at=now
8. Notification expires (after configurable retention, default 90 days)
```

---

## 3. Data Model

### 3.1 `notification` Table

```sql
CREATE TABLE notification (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  recipient_user_id UUID NOT NULL REFERENCES app_user(id),
  tier TEXT NOT NULL CHECK (tier IN ('critical', 'operational', 'informational')),
  category TEXT NOT NULL,  -- 'ratio_breach', 'new_message', 'invoice_created', etc.
  title JSONB NOT NULL,    -- {"en": "Ratio breach in Toddlers Room", "ar": "..."}
  body JSONB NOT NULL,
  action_url TEXT NULL,    -- Deep link: "/attendance?room=toddlers"
  action_label JSONB NULL, -- {"en": "View Room", "ar": "..."}
  related_entity_type TEXT NULL,  -- 'room', 'student', 'invoice', etc.
  related_entity_id UUID NULL,
  idempotency_key TEXT NULL,  -- For deduplication
  is_read boolean NOT NULL DEFAULT false,
  read_at timestamptz NULL,
  dismissed_at timestamptz NULL,
  expires_at timestamptz NOT NULL DEFAULT (now() + interval '90 days'),
  -- Universal columns...
  UNIQUE (tenant_id, idempotency_key) WHERE idempotency_key IS NOT NULL
);

CREATE INDEX idx_notification_tenant_recipient ON notification (tenant_id, recipient_user_id, created_at DESC)
  WHERE is_deleted = false;
CREATE INDEX idx_notification_unread ON notification (tenant_id, recipient_user_id)
  WHERE is_read = false AND is_deleted = false;
CREATE INDEX idx_notification_expiry ON notification (tenant_id, expires_at) WHERE is_deleted = false;
```

### 3.2 `notification_delivery` Table

```sql
CREATE TABLE notification_delivery (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  notification_id UUID NOT NULL REFERENCES notification(id),
  channel TEXT NOT NULL CHECK (channel IN ('in_app', 'push', 'email', 'sms')),
  provider TEXT NULL,  -- 'fcm', 'ses', 'twilio', 'unifonic', 'in_app_signalr'
  provider_message_id TEXT NULL,
  status TEXT NOT NULL DEFAULT 'queued'
    CHECK (status IN ('queued', 'sent', 'delivered', 'failed', 'suppressed', 'cancelled', 'fallback_triggered')),
  attempt_count INT NOT NULL DEFAULT 0,
  max_attempts INT NOT NULL DEFAULT 3,
  next_attempt_at timestamptz NULL,
  last_attempt_at timestamptz NULL,
  delivered_at timestamptz NULL,
  failed_at timestamptz NULL,
  failure_reason TEXT NULL,
  provider_response JSONB NULL,
  fallback_triggered boolean NOT NULL DEFAULT false,
  fallback_delivery_id UUID NULL REFERENCES notification_delivery(id),  -- Link to fallback delivery
  -- Universal columns...
);

CREATE INDEX idx_notification_delivery_status ON notification_delivery (tenant_id, status, next_attempt_at)
  WHERE is_deleted = false;
CREATE INDEX idx_notification_delivery_notification ON notification_delivery (tenant_id, notification_id);
```

### 3.3 `notification_preference` Table (per-user)

```sql
CREATE TABLE notification_preference (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID NOT NULL REFERENCES app_user(id),
  category TEXT NOT NULL,  -- 'ratio_breach', 'new_message', etc.
  push_enabled boolean NOT NULL DEFAULT true,
  email_enabled boolean NOT NULL DEFAULT true,
  sms_enabled boolean NOT NULL DEFAULT true,  -- Only honored for Critical tier
  in_app_enabled boolean NOT NULL DEFAULT true,
  -- Universal columns...
  UNIQUE (tenant_id, user_id, category)
);
```

### 3.4 `notification_template` Table (per-tenant, per-category)

```sql
CREATE TABLE notification_template (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  category TEXT NOT NULL,  -- 'ratio_breach', etc.
  tier TEXT NOT NULL,
  title_template JSONB NOT NULL,  -- {"en": "Ratio breach in {{room_name}}", "ar": "..."}
  body_template JSONB NOT NULL,
  action_url_template TEXT NULL,  -- "/attendance?room={{room_id}}"
  action_label_template JSONB NULL,
  channels JSONB NOT NULL DEFAULT '["in_app", "push"]',  -- Default channels
  -- Universal columns...
  UNIQUE (tenant_id, category)
);
```

---

## 4. Tier-Specific Behavior

### 4.1 Critical / Safety

**Examples**: Ratio breach, unexpected absence, safeguarding flag, evacuation alert.

**Channels** (in order of attempt):
1. **In-app banner** — red, persistent, modal-style (can't be dismissed without "Acknowledge" click).
2. **Push notification** (FCM/APNs) — high priority, bypasses Do Not Disturb on iOS (with Critical Alert Entitlement).
3. **SMS** (Sprint 13+, configurable per tenant) — fallback if push fails.

**Behavior**:
- Cannot be suppressed by users (mandatory).
- All recipients must receive (all admins + assigned staff).
- 3 retry attempts per channel with exponential backoff (1s, 4s, 16s).
- Channel fallback: push fail → SMS (if enabled) → email (final fallback).
- If all channels fail → alert ops team via PagerDuty (this is a SEV-1).
- Logged in audit trail (security-sensitive).

**Recipients**:
- All users with role `super_admin`, `centre_admin`, or `it` in the tenant.
- Plus: staff assigned to the affected room (for ratio breach).
- Plus: designated safety officer (configurable per tenant).

**Template example**:
```json
{
  "category": "ratio_breach",
  "tier": "critical",
  "title_template": {
    "en": "Ratio breach in {{room_name}}",
    "ar": "تجاوز النسبة في {{room_name}}"
  },
  "body_template": {
    "en": "The student-to-staff ratio in {{room_name}} has fallen below the required {{required_ratio}}. Currently {{students_present}} students and {{staff_present}} staff. Please address immediately.",
    "ar": "انخفضت نسبة الطلاب إلى الموظفين في {{room_name}} عن النسبة المطلوبة {{required_ratio}}. يوجد حاليًا {{students_present}} طالبًا و {{staff_present}} موظفًا. يرجى المعالجة فورًا."
  },
  "action_url_template": "/attendance?room={{room_id}}",
  "action_label_template": {
    "en": "View Room",
    "ar": "عرض الغرفة"
  },
  "channels": ["in_app", "push", "sms"]
}
```

### 4.2 Operational

**Examples**: Leave approved/rejected, invoice generated, message received, activity pending approval.

**Channels**:
1. **In-app notification** — appears in notification bell + notification center.
2. **Push notification** — standard priority (respects Do Not Disturb).

**Behavior**:
- User can suppress push per category (in Settings → Notifications).
- Cannot suppress in-app (always recorded).
- 3 retry attempts for push, no fallback (if push fails, in-app is sufficient).
- Batched delivery NOT applied (sent immediately).

**Recipients**:
- Specific to event (e.g., leave approval → leave requester; new message → thread participants).

**Template example**:
```json
{
  "category": "leave_approved",
  "tier": "operational",
  "title_template": {
    "en": "Leave approved",
    "ar": "تمت الموافقة على الإجازة"
  },
  "body_template": {
    "en": "Your leave request for {{start_date}} to {{end_date}} has been approved by {{approver_name}}.",
    "ar": "تمت الموافقة على طلب الإجازة من {{start_date}} إلى {{end_date}} من قبل {{approver_name}}."
  },
  "action_url_template": "/leaves/{{leave_id}}",
  "channels": ["in_app", "push"]
}
```

### 4.3 Informational / Reminders

**Examples**: Fee reminder emails, daily report sent, upcoming birthday, newsletter published.

**Channels**:
1. **Email** — primary channel.
2. **In-app** — optional (user can disable).

**Behavior**:
- User can suppress both email and in-app (unsubscribe).
- Batched delivery (configurable frequency — immediate, daily digest, weekly digest).
- No retry beyond provider's own (SES retries internally).
- No fallback.

**Recipients**:
- Specific (e.g., fee reminder → invoice's parent).
- Or broad (e.g., newsletter → all parents with email enabled).

**Template example**:
```json
{
  "category": "fee_reminder",
  "tier": "informational",
  "title_template": {
    "en": "Invoice {{invoice_number}} due in {{days_until_due}} days",
    "ar": "الفاتورة {{invoice_number}} مستحقة خلال {{days_until_due}} يومًا"
  },
  "body_template": {
    "en": "Your invoice {{invoice_number}} for {{invoice_amount}} is due on {{due_date}}. Please pay before the due date to avoid late fees.",
    "ar": "فاتورتك {{invoice_number}} بمبلغ {{invoice_amount}} مستحقة في {{due_date}}. يرجى الدفع قبل تاريخ الاستحقاق لتجنب الرسوم المتأخرة."
  },
  "action_url_template": "/invoices/{{invoice_id}}",
  "channels": ["email"]
}
```

---

## 5. Channel Implementations

### 5.1 In-App (SignalR)

- **Delivery**: SignalR pushes `NotificationReceived` event to user's connection.
- **Display**: 
  - Critical: red banner overlay (full-width, dismissible only via "Acknowledge" button).
  - Operational: toast notification (top-end, auto-dismiss after 10s).
  - Informational: subtle indicator in notification bell.
- **Persistence**: stored in `notification` table, accessible via `/api/v1/notifications`.
- **No acknowledgement**: server doesn't wait for client to confirm receipt. SignalR delivery is best-effort.
- **Offline behavior**: if user is offline when notification sent, they'll see it in notification center when they reconnect. Critical tier also sent via push (which works offline).

```csharp
// In NotificationDispatcher.cs
private async Task SendInAppAsync(Notification notification, NotificationDelivery delivery)
{
    try
    {
        await _hubContext.Clients
            .Group($"tenant-{notification.TenantId}-user-{notification.RecipientUserId}")
            .SendAsync("NotificationReceived", new NotificationDto
            {
                Id = notification.Id,
                Tier = notification.Tier,
                Title = notification.Title,
                Body = notification.Body,
                ActionUrl = notification.ActionUrl,
            });
        
        await UpdateDeliveryStatus(delivery.Id, "delivered");
    }
    catch (Exception ex)
    {
        await UpdateDeliveryStatus(delivery.Id, "failed", ex.Message);
        await ScheduleRetry(delivery.Id);
    }
}
```

### 5.2 Push (FCM + APNs)

- **Provider**: Firebase Cloud Messaging (FCM) — handles both Android (FCM) and iOS (APNs bridge).
- **Setup**: 
  - Firebase project created.
  - Android: `google-services.json` in mobile app.
  - iOS: APNs key uploaded to Firebase.
- **Token management**:
  - App registers for push on launch (via `expo-notifications`).
  - Token sent to backend via `POST /api/v1/devices/register`.
  - Backend stores token in `user_device` table.
  - User can have multiple devices (phone + tablet).
- **Delivery**: backend calls FCM HTTP v1 API with message payload.
- **Status tracking**: FCM returns message ID. Delivery status not directly available (FCM doesn't confirm device receipt). We treat "FCM accepted" as "sent".
- **Retry**: 3 attempts with exponential backoff (1s, 4s, 16s).
- **Failure modes**:
  - Invalid token (user uninstalled app) → mark device as inactive, don't retry.
  - FCM service unavailable → retry.
  - Rate limited → retry with longer backoff.

```csharp
private async Task SendPushAsync(Notification notification, NotificationDelivery delivery)
{
    var devices = await _deviceRepository.GetActiveDevicesForUser(notification.RecipientUserId);
    
    if (!devices.Any())
    {
        await UpdateDeliveryStatus(delivery.Id, "failed", "No active devices");
        return;
    }
    
    var message = new Message
    {
        Notification = new FirebaseAdmin.Messaging.Notification
        {
            Title = notification.Title[notification.RecipientUser.PreferredLanguage],
            Body = notification.Body[notification.RecipientUser.PreferredLanguage],
        },
        Data = new Dictionary<string, string>
        {
            { "notificationId", notification.Id.ToString() },
            { "tier", notification.Tier },
            { "category", notification.Category },
            { "actionUrl", notification.ActionUrl ?? "" },
            { "relatedEntityType", notification.RelatedEntityType ?? "" },
            { "relatedEntityId", notification.RelatedEntityId?.ToString() ?? "" },
        },
        Android = new AndroidConfig
        {
            Priority = notification.Tier == "critical" ? Priority.High : Priority.Normal,
            Notification = new AndroidNotification
            {
                ChannelId = notification.Tier switch
                {
                    "critical" => "critical",
                    "operational" => "operational",
                    _ => "informational"
                },
                Tag = notification.Category,
            },
        },
        Apns = new ApnsConfig
        {
            Headers = notification.Tier == "critical"
                ? new Dictionary<string, string> { { "apns-priority", "10" }, { "apns-push-type", "alert" } }
                : new Dictionary<string, string> { { "apns-priority", "5" } },
            Aps = new Aps
            {
                Sound = notification.Tier == "critical" ? "critical_alert.wav" : "default",
                Badge = 1,
            },
        },
        Tokens = devices.Select(d => d.PushToken).ToList(),
    };
    
    try
    {
        var response = await FirebaseMessaging.DefaultInstance.SendMulticastAsync(message);
        
        if (response.FailureCount > 0)
        {
            // Log per-token failures
            for (int i = 0; i < response.Responses.Count; i++)
            {
                var resp = response.Responses[i];
                if (!resp.IsSuccess)
                {
                    var token = message.Tokens[i];
                    await LogPushFailure(notification.Id, token, resp.Exception.Message);
                    
                    // If token invalid, deactivate device
                    if (resp.Exception.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    {
                        await _deviceRepository.DeactivateDeviceByTokenAsync(token);
                    }
                }
            }
        }
        
        await UpdateDeliveryStatus(delivery.Id, response.SuccessCount > 0 ? "sent" : "failed");
    }
    catch (Exception ex)
    {
        await UpdateDeliveryStatus(delivery.Id, "failed", ex.Message);
        await ScheduleRetry(delivery.Id);
    }
}
```

### 5.3 Email (SES)

- **Provider**: AWS SES (SaaS) or customer SMTP (on-prem).
- **Templates**: rendered via FluentEmail with Razor templates.
- **Recipients**: verified email addresses (SES sandbox in dev/staging, production-ready in prod).
- **Delivery**:
  - Send via SES `SendEmailAsync`.
  - SES returns message ID.
  - SES sends delivery status via SNS → webhook → update `notification_delivery.status`.
- **Retry**: 3 attempts with backoff (10s, 60s, 5min).
- **Failure modes**:
  - Invalid email → mark as `suppressed` (don't retry).
  - Bounce → mark as `suppressed`, alert ops.
  - Complaint → mark as `suppressed`, alert ops.
  - SES throttled → retry.

```csharp
private async Task SendEmailAsync(Notification notification, NotificationDelivery delivery)
{
    var recipient = await _userRepository.GetByIdAsync(notification.RecipientUserId);
    
    if (string.IsNullOrEmpty(recipient.Email))
    {
        await UpdateDeliveryStatus(delivery.Id, "suppressed", "No email on file");
        return;
    }
    
    var email = Email
        .From("notifications@nms.ae", "NMS")
        .To(recipient.Email)
        .Subject(notification.Title[recipient.PreferredLanguage])
        .UsingTemplate(_emailTemplate, new NotificationEmailModel
        {
            RecipientName = recipient.DisplayName,
            Body = notification.Body[recipient.PreferredLanguage],
            ActionUrl = notification.ActionUrl,
            ActionLabel = notification.ActionLabel?[recipient.PreferredLanguage],
            UnsubscribeUrl = GenerateUnsubscribeUrl(recipient.Id, notification.Category),
            TenantBranding = await GetTenantBrandingAsync(notification.TenantId),
        });
    
    try
    {
        var response = await _emailSender.SendAsync(email);
        await UpdateDeliveryStatus(delivery.Id, "sent", providerMessageId: response.MessageId);
    }
    catch (Exception ex)
    {
        if (ex is EmailValidationException || ex.Message.Contains("bounce"))
        {
            await UpdateDeliveryStatus(delivery.Id, "suppressed", ex.Message);
        }
        else
        {
            await UpdateDeliveryStatus(delivery.Id, "failed", ex.Message);
            await ScheduleRetry(delivery.Id);
        }
    }
}
```

### 5.4 SMS (Twilio / Unifonic)

- **Phase**: Sprint 13+ (deferred from MVP).
- **Provider**: 
  - **Twilio** — international standard, good API, decent UAE coverage.
  - **Unifonic** — UAE-focused, better local delivery + Arabic support.
  - Final selection in Sprint 13 kickoff.
- **Recipients**: only users with verified phone numbers.
- **Critical tier only**: SMS is expensive + intrusive. Only used for Critical tier fallback.
- **Configurable per tenant**: tenant can disable SMS entirely (cost control).
- **Delivery**: send via provider API. Provider returns message ID. Delivery status via webhook.
- **Retry**: 2 attempts (SMS is fast to deliver or fail).
- **Failure modes**:
  - Invalid number → `suppressed`.
  - Provider throttled → retry.
  - Undeliverable → `failed`, trigger email fallback.

```csharp
private async Task SendSmsAsync(Notification notification, NotificationDelivery delivery)
{
    var recipient = await _userRepository.GetByIdAsync(notification.RecipientUserId);
    
    if (string.IsNullOrEmpty(recipient.Phone))
    {
        await UpdateDeliveryStatus(delivery.Id, "suppressed", "No phone on file");
        return;
    }
    
    // Check tenant SMS enabled
    var tenantSettings = await _tenantSettingsRepository.GetByTenantAsync(notification.TenantId);
    if (!tenantSettings.SmsEnabled)
    {
        await UpdateDeliveryStatus(delivery.Id, "suppressed", "SMS disabled for tenant");
        return;
    }
    
    var messageBody = $"{notification.Title[recipient.PreferredLanguage]}\n\n{notification.Body[recipient.PreferredLanguage]}";
    
    try
    {
        var response = await _smsProvider.SendAsync(recipient.Phone, messageBody);
        await UpdateDeliveryStatus(delivery.Id, "sent", providerMessageId: response.MessageId);
    }
    catch (Exception ex)
    {
        await UpdateDeliveryStatus(delivery.Id, "failed", ex.Message);
        await ScheduleRetry(delivery.Id);
    }
}
```

---

## 6. Retry & Fallback Logic

### 6.1 Retry Strategy

```csharp
public class NotificationRetryService
{
    private static readonly int[] RetryBackoffSeconds = { 1, 4, 16, 60, 300 };  // 1s, 4s, 16s, 1min, 5min
    
    public async Task ScheduleRetryAsync(Guid deliveryId)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(deliveryId);
        
        if (delivery.AttemptCount >= delivery.MaxAttempts)
        {
            // Max retries reached — trigger fallback for Critical tier
            await TriggerFallbackAsync(delivery);
            return;
        }
        
        var nextAttemptDelay = RetryBackoffSeconds[Math.Min(delivery.AttemptCount, RetryBackoffSeconds.Length - 1)];
        delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(nextAttemptDelay);
        delivery.AttemptCount++;
        
        await _deliveryRepository.UpdateAsync(delivery);
        
        // Schedule Hangfire job
        _backgroundJob.Schedule<NotificationDispatcherJob>(
            job => job.ProcessDeliveryAsync(deliveryId),
            TimeSpan.FromSeconds(nextAttemptDelay));
    }
    
    private async Task TriggerFallbackAsync(NotificationDelivery failedDelivery)
    {
        var notification = await _notificationRepository.GetByIdAsync(failedDelivery.NotificationId);
        
        if (notification.Tier != "critical")
        {
            // Only Critical tier has fallback
            failedDelivery.Status = "failed";
            failedDelivery.FailedAt = DateTime.UtcNow;
            await _deliveryRepository.UpdateAsync(failedDelivery);
            return;
        }
        
        // Critical tier fallback chain:
        // push fail → sms → email → ops alert
        string? fallbackChannel = failedDelivery.Channel switch
        {
            "push" => "sms",     // push failed → try SMS
            "sms" => "email",    // SMS failed → try email
            "email" => null,     // email failed → no more channels, alert ops
            _ => null
        };
        
        if (fallbackChannel == null)
        {
            // All channels exhausted — alert ops
            failedDelivery.Status = "failed";
            failedDelivery.FailedAt = DateTime.UtcNow;
            await _deliveryRepository.UpdateAsync(failedDelivery);
            
            _logger.LogCritical("All notification channels failed for Critical notification {NotificationId}. Recipient: {UserId}", 
                notification.Id, notification.RecipientUserId);
            
            // Alert ops via PagerDuty
            await _pagerDuty.TriggerIncidentAsync(new PagerDutyIncident
            {
                Title = $"Critical notification delivery failed: {notification.Category}",
                Description = $"Notification {notification.Id} to user {notification.RecipientUserId} failed on all channels.",
                Urgency = "high",
            });
            
            return;
        }
        
        // Create fallback delivery
        var fallbackDelivery = new NotificationDelivery
        {
            NotificationId = notification.Id,
            Channel = fallbackChannel,
            Status = "queued",
            MaxAttempts = fallbackChannel == "sms" ? 2 : 3,  // SMS: 2 attempts, email: 3
        };
        
        failedDelivery.FallbackTriggered = true;
        failedDelivery.FallbackDeliveryId = fallbackDelivery.Id;
        await _deliveryRepository.UpdateAsync(failedDelivery);
        await _deliveryRepository.CreateAsync(fallbackDelivery);
        
        // Process fallback immediately
        _backgroundJob.Enqueue<NotificationDispatcherJob>(
            job => job.ProcessDeliveryAsync(fallbackDelivery.Id));
    }
}
```

### 6.2 Fallback Decision Tree (Critical Tier)

```
Push attempt 1 → success? → DONE
                ↓ fail
Push attempt 2 (4s) → success? → DONE
                      ↓ fail
Push attempt 3 (16s) → success? → DONE
                       ↓ fail
                       ↓ (push exhausted)
SMS enabled for tenant?
  ↓ No
  → Skip SMS, go to email
  ↓ Yes
SMS attempt 1 → success? → DONE
               ↓ fail
SMS attempt 2 (10s) → success? → DONE
                    ↓ fail
                    ↓ (SMS exhausted)
Email attempt 1 → success? → DONE
                  ↓ fail
Email attempt 2 (60s) → success? → DONE
                       ↓ fail
Email attempt 3 (5min) → success? → DONE
                        ↓ fail
                        ↓ (all channels exhausted)
Alert ops via PagerDuty (SEV-1)
```

---

## 7. Categories Catalog

| Category | Tier | Trigger | Recipients | Channels |
|---|---|---|---|---|
| `ratio_breach` | Critical | Attendance module detects ratio below threshold | All admins + room staff + safety officer | In-app + Push + SMS |
| `unexpected_absence` | Critical | Student marked absent who wasn't expected to be | All admins + child's guardians | In-app + Push + SMS |
| `safeguarding_flag` | Critical | Observation flagged as concern (high severity) | Super Admin + Centre Admin | In-app + Push + SMS |
| `evacuation_alert` | Critical | Manual trigger by admin (Phase 2) | All staff + all parents | In-app + Push + SMS |
| `leave_approved` | Operational | Leave request approved | Leave requester | In-app + Push |
| `leave_rejected` | Operational | Leave request rejected | Leave requester | In-app + Push |
| `invoice_created` | Operational | Invoice generated | Invoice's parent | In-app + Push |
| `invoice_overdue` | Operational | Invoice past due date + grace | Invoice's parent | In-app + Push |
| `payment_received` | Operational | Payment recorded against invoice | Invoice's parent | In-app + Push |
| `message_received` | Operational | New message in thread | Thread participants (except sender) | In-app + Push |
| `activity_pending_approval` | Operational | Activity post needs manual approval | Centre Admin | In-app + Push |
| `activity_approved` | Operational | Activity post approved + visible to parent | Linked parents | In-app + Push |
| `media_quarantined` | Operational | Uploaded media failed content scan | Uploading staff + Centre Admin | In-app + Push |
| `tour_scheduled` | Operational | New tour appointment booked | Assigned staff + enquiry's family | In-app + Push |
| `tour_reminder` | Operational | Tour scheduled in 24 hours | Assigned staff | In-app + Push |
| `enquiry_assigned` | Operational | Enquiry assigned to staff member | Assigned staff | In-app + Push |
| `enquiry_stage_changed` | Operational | Enquiry moved to new stage | Assigned staff | In-app + Push |
| `admission_step_due` | Operational | Admission process step due in 3 days | Parent + assigned staff | In-app + Push |
| `form_submitted` | Operational | Public form submitted | Assigned staff (e.g., receptionist for enquiries) | In-app + Push |
| `daily_report_ready` | Informational | Daily report generated for student | Student's primary guardian | Email + In-app |
| `fee_reminder` | Informational | Invoice due in X days (per reminder schedule) | Invoice's parent | Email |
| `newsletter_published` | Informational | Newsletter sent | Subscribed parents | Email |
| `upcoming_birthday` | Informational | Student birthday in 7 days | Centre Admin + room staff | Email + In-app |
| `app_adoption_reminder` | Informational | Parent hasn't logged in for 14 days | Parent | Email |
| `weekly_summary` | Informational | Weekly digest of activity | Configurable per user | Email |

---

## 8. User Preferences

### 8.1 Preference UI

Settings → Notifications:

```
Notification Preferences

Critical Alerts (cannot be disabled)
☐ These are mandatory safety alerts. You will always receive them.

Operational Notifications
┌──────────────────────────────────────────────────────┐
│ Notification Type              Push    Email    SMS  │
├──────────────────────────────────────────────────────┤
│ Leave status changes            ☑       ☐       ☐   │
│ New invoice                     ☑       ☐       ☐   │
│ Payment confirmation            ☑       ☐       ☐   │
│ New message                     ☑       ☐       ☐   │
│ Activity pending approval       ☑       ☐       ☐   │
│ Tour reminders                  ☑       ☐       ☐   │
│ New enquiry assigned            ☑       ☐       ☐   │
│ Form submitted                  ☑       ☐       ☐   │
│ Admission step due              ☑       ☐       ☐   │
└──────────────────────────────────────────────────────┘

Informational Notifications
┌──────────────────────────────────────────────────────┐
│ Daily report ready              ☑       ☑           │
│ Fee reminders                   ☑       ☑           │
│ Newsletters                     ☐       ☑           │
│ Upcoming birthdays              ☐       ☑           │
│ Weekly summary                  ☐       ☑           │
└──────────────────────────────────────────────────────┘

Quiet Hours (push notifications silenced)
Start: [10:00 PM] End: [6:00 AM]
(Exception: Critical alerts always delivered)
```

### 8.2 Preference Storage

```sql
INSERT INTO notification_preference (tenant_id, user_id, category, push_enabled, email_enabled, sms_enabled, in_app_enabled)
VALUES 
  ('tenant-uuid', 'user-uuid', 'leave_approved', true, false, false, true),
  ('tenant-uuid', 'user-uuid', 'invoice_created', true, false, false, true),
  ...
;
```

Default preferences seeded for new users based on their role (e.g., parents get `daily_report_ready` email by default; staff get `message_received` push by default).

### 8.3 Unsubscribe (Email)

Every informational email includes unsubscribe link:
```
https://app.nms.ae/unsubscribe?token=...&category=fee_reminder
```

Token is signed JWT with `{ userId, category, expiresAt }`. Clicking → mark `email_enabled=false` for that user + category. Confirmation page shows "You've unsubscribed from fee reminder emails."

**Note**: Critical + Operational emails (rare) don't have unsubscribe (they're transactional, not marketing).

---

## 9. Batching & Digests (Informational Tier)

### 9.1 Batch Modes

| Mode | Description |
|---|---|
| Immediate | Send as soon as event occurs (default for most Informational) |
| Daily digest | Collect all day's notifications, send one email at 8 AM |
| Weekly digest | Collect all week's notifications, send one email on Monday 8 AM |

### 9.2 Implementation

```csharp
public class DailyDigestJob
{
    public async Task ExecuteAsync()
    {
        var usersWithDigestEnabled = await _userPreferenceRepository
            .GetUsersWithDigestAsync(DigestMode.Daily);
        
        foreach (var user in usersWithDigestEnabled)
        {
            var pendingNotifications = await _notificationRepository
                .GetPendingDigestForUserAsync(user.UserId, DateTime.UtcNow.Date);
            
            if (!pendingNotifications.Any()) continue;
            
            var digestEmail = await _digestEmailBuilder.BuildAsync(user, pendingNotifications);
            await _emailSender.SendAsync(digestEmail);
            
            // Mark notifications as delivered via digest
            await _notificationRepository.MarkAsDigestDeliveredAsync(
                pendingNotifications.Select(n => n.Id));
        }
    }
}

// Hangfire recurring job
RecurringJob.AddOrUpdate<DailyDigestJob>(
    "daily-digest",
    job => job.ExecuteAsync(),
    "0 8 * * *",  // Daily at 8 AM
    new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arabian Standard Time") });
```

---

## 10. Monitoring & Observability

### 10.1 Metrics

- Notification delivery rate by channel (sent / queued).
- Notification failure rate by channel + tier.
- Average delivery latency (queue → sent) by tier.
- Push token registration count (active devices).
- Email bounce + complaint rate.
- SMS delivery success rate.
- Fallback trigger rate (Critical tier).
- PagerDuty alert count (Critical failures).

### 10.2 Grafana Dashboard

`nms-notifications.json`:
- Delivery rate (success / failed / suppressed) by channel — last 24h.
- p95 delivery latency by tier.
- Top failure reasons.
- Active push tokens by platform (iOS / Android).
- Email bounce rate trend.
- Critical notification fallback rate (should be < 5%).

### 10.3 Alerts

- Push delivery failure rate > 10% for 5 min → Slack warning.
- Email bounce rate > 5% for 1 hour → Slack warning.
- SMS delivery failure rate > 20% for 5 min → Slack warning.
- Critical notification delivery failure (any single notification) → PagerDuty critical.
- Fallback triggered for Critical notification → Slack info (track pattern).

---

## 11. Tenant Configuration

### 11.1 Per-Tenant Settings

```sql
INSERT INTO setting (tenant_id, key, value)
VALUES 
  ('tenant-uuid', 'notifications.critical.sms_enabled', 'true'::jsonb),
  ('tenant-uuid', 'notifications.critical.sms_provider', '"unifonic"'::jsonb),
  ('tenant-uuid', 'notifications.operational.push_default', 'true'::jsonb),
  ('tenant-uuid', 'notifications.informational.digest_mode', '"immediate"'::jsonb),
  ('tenant-uuid', 'notifications.informational.digest_time', '"08:00"'::jsonb),
  ('tenant-uuid', 'notifications.email.from_address', '"notifications@brightfutures.ae"'::jsonb),
  ('tenant-uuid', 'notifications.email.from_name', '"Bright Futures Nursery"'::jsonb);
```

### 11.2 SMS Quota

Per-tenant SMS quota:
- Tier 1 (≤50 students): 100 SMS/month included, then AED 0.30/SMS.
- Tier 2 (51-100 students): 250 SMS/month included.
- Tier 3 (101-200 students): 500 SMS/month included.
- Tier 4 (201+ students): 1000 SMS/month included.
- On-prem: customer's SMS account (Twilio/Unifonic) — no quota from us.

Quota usage tracked + alerted at 80%, 100%. Over-quota SMS blocked (with admin alert) unless customer opts into overage billing.

---

## 12. Testing

### 12.1 Unit Tests

- Template rendering with various locales.
- Channel selection logic (tier + preferences + tenant config).
- Fallback chain (mock each channel, verify fallback order).
- Retry scheduling (verify backoff times).

### 12.2 Integration Tests

- End-to-end: trigger event → notification created → delivery attempted → status updated.
- FCM sandbox: send real push to test device.
- SES sandbox: send real email to verified address.
- Twilio test mode: send SMS to test number.

### 12.3 Load Testing

- 1000 notifications/minute for 10 minutes → verify no queue buildup.
- 100 concurrent Critical notifications → verify all delivered within 30s.
- 10,000 informational emails batched → verify SES throughput.

---

**Next file to read**: `16-TESTING-QUALITY.md` for the testing strategy, coverage gates, and quality bars.
