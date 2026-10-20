# 17 — Compliance & Legal

> UAE PDPL, KHDA/ADEK regulatory alignment, data residency, retention policies, parent consent, breach response.

---

## 1. Compliance Scope

NMS operates in the UAE and must comply with:

| Regulation | Scope | Status |
|---|---|---|
| **UAE PDPL** (Federal Decree-Law No. 45 of 2021) | All personal data of UAE residents | Mandatory |
| **KHDA** (Dubai: Knowledge and Human Development Authority) | Nurseries in Dubai | Mandatory for Dubai customers |
| **ADEK** (Abu Dhabi: Department of Education and Knowledge) | Nurseries in Abu Dhabi | Mandatory for Abu Dhabi customers |
| **GDPR-aligned** (EU/UK data subjects) | If EU/UK parents enrolled | Optional, good practice |
| **UAE Federal Law No. 2 of 2016** (Cybercrime) | All UAE digital activity | Mandatory |
| **UAE Consumer Protection Law** | Commercial relationships | Mandatory |

This file is **not legal advice**. It's our compliance approach based on current understanding. **Legal counsel must review and sign off before each phase go-live.**

---

## 2. UAE PDPL Compliance

### 2.1 PDPL Overview

The UAE Personal Data Protection Law (Federal Decree-Law No. 45 of 2021) governs the processing of personal data in the UAE. Key obligations:

| Obligation | How NMS Complies |
|---|---|
| Lawful basis for processing | Contract (providing nursery services) + legal obligation (KHDA record retention) + consent (for non-essential processing) |
| Data subject rights (access, correction, deletion, portability) | Implemented as admin-only functions (per source SRS §5); parents can request via messaging |
| Consent management | Captured at parent invitation acceptance; configurable per data category |
| Data minimization | Only collect data needed for nursery operations |
| Purpose limitation | Data used only for nursery operations; not shared without consent |
| Data residency | UAE-hosted infrastructure (SaaS default); on-prem at customer's UAE infra |
| Cross-border transfer restrictions | No cross-border transfer without explicit consent + adequate protection |
| Breach notification | Notify UAE Data Office within 72 hours of breach discovery |
| Data Protection Officer (DPO) | Customer designates DPO; we provide tools (audit log, consent mgmt) |
| Children's data | Special protection: encryption, access controls, audit, retention limits |
| Sensitive data (medical, biometric) | Higher protection: column-level encryption, restricted access, audit |

### 2.2 Personal Data We Process

| Category | Examples | Lawful basis | Retention (proposed) |
|---|---|---|---|
| **Identity** | Name, DOB, gender, photo, nationality | Contract | 7 years after withdrawal |
| **Contact** | Email, phone, address | Contract | 7 years after withdrawal |
| **Government IDs** | Emirates ID, passport number | Legal obligation (KHDA) | 10 years |
| **Attendance** | Check-in/out times, absences | Contract | 7 years |
| **Medical** | Allergies, medications, conditions | Contract + vital interests | 10 years |
| **Safeguarding** | Custody notes, safeguarding flags | Legal obligation | 10 years (or longer if legal hold) |
| **Assessment** | Observations, milestones, progress | Contract | 5 years after withdrawal |
| **Billing** | Invoices, payments, expenses | Legal obligation (tax) | 7 years (UAE tax law) |
| **Communications** | Messages, activity posts | Contract | 3 years |
| **Audit log** | All system mutations | Legal obligation | 7 years |
| **Device/usage** | Push token, IP, user agent | Legitimate interest | 1 year |

### 2.3 Parent Consent

At parent invitation acceptance:
```
Welcome to NMS!

By creating your account, you consent to:

✓ Receiving daily reports about your child via the app and email
✓ Receiving notifications about your child's activities, attendance, and invoices
✓ Photos and videos of your child being posted by nursery staff for you to view
✓ Your child's data being stored securely on UAE-hosted servers

You can:
- Withdraw consent for non-essential processing at any time (Settings → Privacy)
- Request a copy of your child's data (Contact nursery admin)
- Request correction of inaccurate data (Contact nursery admin)
- Request deletion of data (subject to legal retention requirements)

View full Privacy Policy: https://nms.ae/privacy
View Terms of Service: https://nms.ae/terms

[Accept] [Decline]
```

### 2.4 Data Subject Rights Implementation

| Right | How user exercises | How we fulfill |
|---|---|---|
| Access | Email/message nursery admin → admin exports student record | Admin uses "Export Student Data" function (generates JSON + PDF of all data) |
| Correction | Email/message nursery admin → admin edits record | Admin edits via UI, audit logged |
| Deletion | Email/message nursery admin → admin soft-deletes | Soft-delete applied; hard delete after retention period (or with legal sign-off if earlier) |
| Portability | Email/message nursery admin → admin exports in machine-readable format | JSON export (full data) + PDF export (human-readable) |
| Objection | Settings → Privacy → Disable specific processing | E.g., disable photo sharing → no more activity posts visible to that parent |
| Withdraw consent | Settings → Privacy → Withdraw consent | Data processing stops; data retained per legal minimum; parent account deactivated |

### 2.5 Sensitive Data Handling

**Encrypted at column level** (via pgcrypto, see `09-AUTH-SECURITY.md` §4.3):
- Student medical notes
- Student allergy info
- Student dietary requirements
- Student custody notes
- Student safeguarding flags
- Student passport number
- Student Emirates ID
- Staff salary
- Staff Emirates ID
- Staff passport number
- Staff visa info

**Access restrictions**:
- Only Centre Admin+ can view medical/safeguarding fields.
- Only Accounts role can view salary.
- Access logged in audit log (who viewed what, when).

**Emergency access**: Teacher can view emergency-relevant fields (allergies, medical conditions, emergency contacts) for students in their room — but only via the Emergency Report flow, which is heavily audited.

---

## 3. KHDA Compliance (Dubai)

### 3.1 KHDA Requirements for Nurseries

| Requirement | How NMS Complies |
|---|---|
| Student enrollment records | Student entity with full lifecycle tracking |
| Attendance records | Daily attendance, immutable after 24h, retained 7+ years |
| Staff qualifications | Staff.qualifications JSONB field, reportable |
| Staff-to-student ratios per age band | Configurable per room, real-time enforcement, breach alerting |
| Child safety | Safeguarding flags, custody notes, emergency contacts, audit trail |
| Parent communication records | Activity feed + messaging + daily reports all retained |
| Inspection readiness | Reports module generates KHDA-aligned reports (Phase 1 Sprint 13) |
| Curriculum (EYFS) | Phase 2: full EYFS framework support |
| Incident reporting | Forms module (Incident Form type), audit logged |

### 3.2 KHDA-Aligned Reports (Phase 1 Sprint 13)

| Report | Purpose | Format |
|---|---|---|
| Student Enrollment Report | Active students by age, room, program | Excel |
| Staff Qualifications Report | Staff with their qualifications, certifications | Excel |
| Attendance Compliance Report | Daily attendance vs. expected, ratio compliance history | Excel + PDF |
| Incident Summary Report | Incidents logged in the period | Excel + PDF |
| Safeguarding Register | Active safeguarding flags + custody notes (redacted for non-admin) | PDF (admin-only) |
| Audit Trail Export (per KHDA request) | All mutations in date range | Excel + JSON |

### 3.3 Inspection Readiness

KHDA inspections happen annually (typically). NMS supports inspection via:
- **Audit log**: Append-only, filterable by entity/date/action. Inspector can verify any data change.
- **Reports**: Pre-built KHDA-aligned reports, one-click generation.
- **Document storage**: Resource Centre stores inspection-relevant documents (policies, certifications).
- **Read-only inspector access**: Super Admin can create a temporary "Inspector" role with read-only access to relevant modules (Sprint 14+).

---

## 4. ADEK Compliance (Abu Dhabi)

Similar to KHDA but for Abu Dhabi nurseries. Same reports + audit capabilities apply. ADEK-specific reporting templates added in Phase 1 Sprint 14 based on customer feedback.

---

## 5. Data Residency

### 5.1 SaaS Default

- **Region**: AWS ME Central (me-central-1) — Bahrain, but UAE-compliant under UAE PDPL (AWS has UAE data residency certification).
- **Alternative**: AWS UAE (when available, expected 2026-2027) — migrate when launched.
- **Data stored in region**:
  - PostgreSQL (RDS).
  - Object storage (S3) for media, documents, backups.
  - Redis (ElastiCache).
  - RabbitMQ (Amazon MQ).
  - Keycloak (EKS pod + RDS).
  - All backups.
  - All audit logs.
- **Data NOT stored in region** (but doesn't contain personal data):
  - CloudFront edge cache (cached at global edge PoPs, but content is signed URLs + already-encrypted media).
  - Container images (ghcr.io or ECR — public registry for open-source base images, private for our images).
  - Terraform state (S3 in same region).
  - GitHub Actions artifacts (US, but no PII).

### 5.2 On-Prem

Customer's responsibility to host in UAE:
- Customer chooses UAE cloud (AWS UAE, Azure UAE, local provider) or on-prem data center.
- We provide Terraform modules for AWS, Azure, generic K8s.
- We DON'T support non-UAE hosting for on-prem customers (per PDPL).

### 5.3 Cross-Border Transfer Restrictions

Per PDPL, personal data cannot leave UAE without:
- Explicit consent from data subject.
- Adequate protection in destination country.
- Approved transfer mechanism.

**NMS policy**: No cross-border transfer of personal data. All personal data stays in UAE region.

**Exception**: If customer explicitly requests international hosting (rare), we require:
- Written acknowledgement from customer's legal counsel.
- Written consent from all data subjects (parents).
- Documentation of adequate protection measures.

This exception is documented per customer in their data processing agreement.

---

## 6. Data Retention

### 6.1 Retention Schedule (Proposed — Confirm with Legal)

| Data type | Retention period | Rationale |
|---|---|---|
| Student attendance | 7 years after withdrawal | KHDA requirement |
| Student medical/safeguarding | 10 years after withdrawal | Legal protection (potential claims) |
| Student assessment | 5 years after withdrawal | Educational continuity |
| Student identity (name, DOB) | 7 years after withdrawal | KHDA + legal |
| Student government IDs (Emirates ID, passport) | 10 years | Legal obligation |
| Staff records | 7 years after termination | UAE labor law |
| Billing/invoices | 7 years | UAE tax law (Federal Decree-Law No. 28 of 2022) |
| Audit log | 7 years | Compliance |
| Activity posts | 3 years | Operational |
| Messages | 3 years | Operational |
| Notifications | 90 days | Operational |
| Device tokens | 1 year after last use | Operational |
| Backups | 35 days | Operational |

### 6.2 Retention Implementation

```csharp
// Background job (Hangfire, daily at 2 AM)
public class DataRetentionJob
{
    public async Task ExecuteAsync()
    {
        var retentionPolicies = await _settingRepository
            .GetRetentionPoliciesAsync();
        
        foreach (var policy in retentionPolicies)
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddYears(-policy.Years);
            
            switch (policy.EntityType)
            {
                case "attendance_record":
                    await _attendanceRepo.HardDeleteBeforeAsync(cutoffDate);
                    break;
                case "audit_log":
                    await _auditLogRepo.ArchiveBeforeAsync(cutoffDate);  // Move to archive partition
                    break;
                case "notification":
                    await _notificationRepo.HardDeleteExpiredAsync();
                    break;
                // ... etc
            }
            
            _logger.LogInformation("Retention policy applied: {EntityType} deleted before {Cutoff}", 
                policy.EntityType, cutoffDate);
        }
    }
}
```

### 6.3 Legal Hold

When a legal hold is placed (e.g., safeguarding investigation, litigation):
- Super Admin marks tenant or specific entities as "legal hold".
- Retention jobs skip entities on legal hold.
- Legal hold records the reason, start date, and requesting authority.
- Legal hold release requires Super Admin + documented legal sign-off.

---

## 7. Privacy Policy + Terms of Service

### 7.1 Privacy Policy (Summary)

Hosted at `https://nms.ae/privacy`. Covers:
- What data we collect (per §2.2 above).
- Why we collect it (lawful basis).
- Where it's stored (UAE).
- How long we keep it (retention schedule).
- Who can access it (nursery staff, our ops team, AWS as processor).
- Data subject rights.
- Cookie usage (minimal — auth + locale only, no tracking).
- Breach notification commitment.
- Contact info for privacy questions.

Bilingual (English + Arabic), reviewed by legal counsel before each phase go-live.

### 7.2 Terms of Service

Hosted at `https://nms.ae/terms`. Covers:
- Service description.
- Acceptable use policy.
- Payment terms (for SaaS).
- SLA commitments.
- Limitation of liability.
- Termination terms.
- Data ownership (customer owns their data; we're processor).
- Sub-processor disclosure (AWS, FCM, SES, etc.).
- Governing law (UAE).
- Dispute resolution (UAE courts or arbitration).

### 7.3 Data Processing Agreement (DPA)

Signed with each customer (especially on-prem and enterprise SaaS):
- Defines roles (customer = controller, we = processor).
- Permitted processing activities.
- Sub-processor list (with notification of changes).
- Security obligations.
- Breach notification timeline.
- Data return/deletion on termination.
- Audit rights.

Template: `docs/legal/dpa-template.pdf` (created by legal counsel).

---

## 8. Breach Response

### 8.1 Breach Definition

A "personal data breach" means any breach of security leading to the accidental or unlawful destruction, loss, alteration, unauthorized disclosure of, or access to, personal data.

Examples:
- Unauthorized access to database (e.g., SQL injection).
- Lost/stolen device with cached personal data.
- Misdirected email containing personal data.
- Insider data exfiltration.
- Ransomware attack.
- Accidental public S3 bucket.

### 8.2 Breach Response Plan

`infra/docs/incident-response.md` documents:

**Phase 1: Detection + Triage (0-1 hour)**
1. Anyone who suspects a breach immediately notifies Tech Lead + DPO (customer's or our internal).
2. Tech Lead assesses: Is this a real breach? What data is affected? How many users?
3. If confirmed: declare incident (SEV-1).

**Phase 2: Containment (1-4 hours)**
1. Stop the bleeding: revoke compromised credentials, block IP, take service offline if needed.
2. Preserve evidence: snapshot DB, capture logs, don't destroy anything.
3. Notify legal counsel.

**Phase 3: Assessment (4-24 hours)**
1. Determine scope: which records, which users, what data categories.
2. Determine root cause.
3. Determine if breach is "notifiable" per PDPL (likely yes if personal data involved).

**Phase 4: Notification (24-72 hours)**
1. Notify UAE Data Office within 72 hours of discovery.
2. Notify affected individuals "without undue delay" if high risk.
3. Customer notification (if customer is controller, they notify their users; we support).

**Phase 5: Recovery (ongoing)**
1. Implement fixes to prevent recurrence.
2. Restore service from clean backups if needed.
3. Monitor for further unauthorized access.

**Phase 6: Post-Mortem (5 business days)**
1. Blameless post-mortem document.
2. Root cause analysis (5 Whys).
3. Action items with owners + due dates.
4. Update security controls + runbooks.

### 8.3 Breach Notification Template

```
Subject: Security Incident Notification — [Date]

Dear [Customer/User],

We are writing to inform you of a security incident that may have affected your personal data.

What Happened:
[Description of incident]

When It Happened:
[Date/time of incident and discovery]

What Data Was Affected:
[List of data categories]

What We Are Doing:
[Remediation actions]

What You Should Do:
[Recommended actions for user, if any]

For More Information:
[Contact details]

We sincerely apologize for this incident and are committed to...

Sincerely,
[Name]
[Title]
```

---

## 9. Sub-Processors

We use the following sub-processors (all UAE-region or with adequate protection):

| Sub-processor | Purpose | Region | DPA status |
|---|---|---|---|
| **AWS (Amazon Web Services)** | Hosting, DB, storage, cache, queue | UAE (me-central-1) | Standard AWS DPA |
| **Google Firebase (FCM)** | Push notifications | Global (no PII stored) | Google Cloud DPA |
| **AWS SES** | Transactional email | UAE | Standard AWS DPA |
| **Twilio / Unifonic** | SMS (Critical tier) | UAE (Unifonic) / Global (Twilio) | Provider DPA |
| **AWS Rekognition / Azure Content Safety** | Content moderation | UAE or adjacent | Provider DPA |
| **OpenAI** (Phase 2+) | AI features (newsletter, observation transcription) | Global — anonymized data only | OpenAI DPA |
| **Sentry** | Error monitoring | EU — anonymized data only | Sentry DPA |
| **GitHub** | Source code, CI/CD | US — no PII | GitHub DPA |
| **Stripe / Telr / PayTabs** (Phase 3) | Payment gateway | UAE | Provider DPA |

Customers notified 30 days before adding new sub-processors. Customer can object (rare).

---

## 10. Cookies + Tracking

### 10.1 Cookies Used

| Cookie | Purpose | Lifetime | Type |
|---|---|---|---|
| `nms_refresh_token` | Auth refresh token | 7 days | HttpOnly, Secure, SameSite=Strict |
| `nms_language` | User's preferred language | 1 year | SameSite=Strict |
| `nms_theme` | UI theme preference | 1 year | SameSite=Strict |

### 10.2 What We Don't Use

- ❌ Google Analytics (privacy-invasive).
- ❌ Facebook Pixel.
- ❌ Third-party tracking cookies.
- ❌ Advertising cookies.
- ❌ Cross-site tracking.

### 10.3 Analytics (Privacy-Preserving)

We use **PostHog** (self-hosted or EU region) for product analytics:
- Anonymous usage patterns (which features are used).
- No PII collected.
- No cross-site tracking.
- Opt-out available in Settings → Privacy.

---

## 11. Compliance Audit

### 11.1 Internal Audits

Quarterly internal audit:
- Review access controls (who has access to what).
- Review audit log for suspicious patterns.
- Review data retention compliance.
- Review DPA compliance with sub-processors.
- Review breach response readiness (DR drill).

### 11.2 External Audits

- Annual external compliance audit (third-party firm).
- SOC 2 Type II (target: Phase 3).
- ISO 27001 (target: Phase 3+, if customer demand).

### 11.3 Customer Audit Rights

Enterprise customers (on-prem + Tier 4 SaaS) have audit rights per DPA:
- Annual audit (customer bears cost).
- Or: rely on our SOC 2 report (we provide).

---

## 12. International Expansion Considerations (Phase 4+)

When expanding beyond UAE (Saudi Arabia, Qatar, etc.):

| Country | Additional compliance |
|---|---|
| Saudi Arabia | SAMA + PDPL (KSA) — separate data residency |
| Qatar | PDPL (Qatar) |
| Kuwait | Pending data protection law |
| Bahrain | PDPL (Bahrain) |
| Oman | PDPL (Oman) |

Each country requires:
- Local data residency.
- Local language support (Arabic dialects).
- Local payment gateway.
- Local regulatory alignment (e.g., Saudi VAT 15% vs UAE 5%).

**Architecture must support multi-region deployment** (Phase 4+). Current architecture (single-region SaaS) is fit for UAE-only Phase 1-3.

---

## 13. Legal Sign-Off Checklist (Before Each Phase Go-Live)

### Phase 1 Go-Live

- [ ] Privacy Policy reviewed + updated.
- [ ] Terms of Service reviewed + updated.
- [ ] DPA template finalized.
- [ ] Retention schedule confirmed with legal counsel.
- [ ] Breach response plan tested (tabletop exercise).
- [ ] Sub-processor list confirmed.
- [ ] KHDA reporting alignment confirmed.
- [ ] ADEK reporting alignment confirmed (if Abu Dhabi customer).
- [ ] Parent consent flow reviewed.
- [ ] Data subject rights procedures documented.
- [ ] Data residency confirmed (UAE region).
- [ ] Legal counsel sign-off (written).

### Phase 2 Go-Live

- [ ] All Phase 1 items maintained.
- [ ] Learning & Assessment data retention confirmed.
- [ ] AI features (observation transcription, newsletter generation) — OpenAI DPA in place.
- [ ] Mobile app privacy policy section added.
- [ ] App Store + Play Store privacy labels completed.

### Phase 3 Go-Live

- [ ] All Phase 2 items maintained.
- [ ] Payment gateway PCI DSS scope confirmed (we're SAQ-A — gateway handles cardholder data).
- [ ] ERP integration data flow reviewed (what data syncs to ERP).
- [ ] Open API terms of service published.
- [ ] Multi-centre group data isolation verified.
- [ ] SOC 2 Type II audit initiated (target: 6 months post-launch).

---

**Next file to read**: `18-CURSOR-RULES.md` — paste this into `.cursorrules` to give the Cursor agent the project context.
