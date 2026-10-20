# 21 — Delivery Checklist

> Go-live readiness + handover checklist for each phase. Print this. Check every box before signing off.

---

## 1. MVP Pilot Go-Live Checklist (Sprint 5)

### 1.1 Code Quality

- [ ] All Sprint 1–4 deliverables complete + tested.
- [ ] Backend test coverage ≥ 80%.
- [ ] Web test coverage ≥ 75%.
- [ ] Mobile test coverage ≥ 70%.
- [ ] 0 critical SonarCloud issues.
- [ ] 0 high SonarCloud issues.
- [ ] All security hotspots reviewed + marked "safe" or fixed.
- [ ] E2E tests for critical journeys pass: login, check-in, post activity, send message, generate daily report, view emergency report.
- [ ] Lighthouse Performance ≥ 90 on dashboard, attendance, parent feed.
- [ ] Lighthouse Accessibility ≥ 90 on parent portal.
- [ ] axe-core tests pass on all MVP screens.
- [ ] Visual regression tests pass in both LTR + RTL.

### 1.2 Infrastructure

- [ ] Production AWS ME Central account provisioned.
- [ ] Terraform applies cleanly to production.
- [ ] EKS cluster healthy (3+ worker nodes, multi-AZ).
- [ ] RDS PostgreSQL Multi-AZ + automated backups + WAL archiving.
- [ ] ElastiCache Redis Multi-AZ.
- [ ] Amazon MQ RabbitMQ Multi-AZ.
- [ ] S3 buckets created with correct policies (private, versioned, encrypted).
- [ ] CloudFront distribution configured with signed URLs.
- [ ] Route53 DNS records configured (app.nms.ae, api.nms.ae).
- [ ] KMS keys created for encryption.
- [ ] Secrets Manager populated with all secrets.
- [ ] WAF rules active on ALB.
- [ ] TLS certificates valid (Let's Encrypt via cert-manager).
- [ ] Helm charts deployed: nms-api, nms-web, Keycloak.
- [ ] HPA configured for nms-api (3-10 replicas).
- [ ] PodDisruptionBudgets configured.
- [ ] NetworkPolicies: default deny + explicit allow.
- [ ] Backups tested (restore to staging, verify data).

### 1.3 Observability

- [ ] Prometheus scraping all pods.
- [ ] Grafana dashboards imported (NMS Overview, API Performance, Business KPIs, Infrastructure, Database, Notifications).
- [ ] Alertmanager routes: PagerDuty (critical), Slack #ops-alerts (warning), email (info).
- [ ] Alerts configured: HTTP 5xx > 1%, p99 > 3s, DB pool > 80%, RabbitMQ queue > 500, pod restart loop, backup failed, cert expiring.
- [ ] Loki collecting logs from all pods.
- [ ] OpenTelemetry traces flowing.
- [ ] Sentry integrated (web + mobile).
- [ ] Synthetic checks running (login, dashboard, parent portal every 5 min).
- [ ] Status page configured (status.nms.ae).

### 1.4 Security

- [ ] OWASP Top 10 internal review complete.
- [ ] SAST (SonarCloud) clean.
- [ ] SCA (OWASP Dependency Check) clean.
- [ ] Container scan (Trivy) clean.
- [ ] IaC scan (tfsec + checkov) clean.
- [ ] Secrets scan (GitGuardian) clean.
- [ ] All secrets in Secrets Manager (none in code/env).
- [ ] RLS policies verified on all tenant tables.
- [ ] Parent-child ownership tests pass.
- [ ] Cross-tenant isolation tests pass.
- [ ] JWT validation enforced on all endpoints (except public).
- [ ] Rate limiting active (100 req/min/user).
- [ ] WAF rules active.
- [ ] TLS 1.2+ enforced.
- [ ] HSTS header set.
- [ ] CSP header set.
- [ ] All other security headers set (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`).

### 1.5 Compliance

- [ ] Privacy Policy published at nms.ae/privacy (EN + AR).
- [ ] Terms of Service published at nms.ae/terms (EN + AR).
- [ ] Parent consent flow reviewed by legal.
- [ ] Data retention schedule confirmed with legal counsel.
- [ ] UAE data residency confirmed (all personal data in AWS me-central-1).
- [ ] Sub-processor list documented (AWS, FCM, SES).
- [ ] Breach response runbook documented.
- [ ] DPA template ready for pilot nursery.
- [ ] KHDA alignment confirmed (basic — full reporting in Phase 1 Sprint 13).
- [ ] Legal counsel sign-off (written).

### 1.6 Pilot Nursery Readiness

- [ ] Pilot agreement signed (covers data ownership, pilot duration, support SLA, exit terms).
- [ ] Pilot nursery director + 2-3 staff identified as primary users.
- [ ] Pilot nursery staff trained (1-hour onboarding session).
- [ ] Pilot nursery data migrated from existing system (if any) — manual entry or scripted.
- [ ] Pilot nursery rooms, programs, groups configured in system.
- [ ] Pilot nursery staff accounts created + invitations sent.
- [ ] Pilot nursery student records created.
- [ ] Pilot nursery parent invitations sent (5-10 parents minimum).
- [ ] Barcode cards printed for students + staff.
- [ ] USB/Bluetooth barcode scanners confirmed working with web app.
- [ ] Pilot nursery WiFi stable (test in their actual environment).
- [ ] In-app feedback button tested.

### 1.7 Support + Incident Response

- [ ] On-call rotation defined (Tech Lead + 1 engineer).
- [ ] PagerDuty schedule configured.
- [ ] Incident response runbook documented (`infra/docs/incident-response.md`).
- [ ] Severity definitions documented (SEV-1 through SEV-4).
- [ ] Communication templates ready (customer email, status page update).
- [ ] Escalation matrix documented.
- [ ] Slack channel for ops alerts (#ops-alerts).
- [ ] Support email monitored (support@nms.ae).
- [ ] In-app support request flow tested.

### 1.8 Documentation

- [ ] Admin guide (Centre Admin + Super Admin) — `docs/user-guides/admin.md`.
- [ ] Teacher quick-start — `docs/user-guides/teacher.md`.
- [ ] Parent guide — `docs/user-guides/parent.md` (bilingual).
- [ ] API documentation (Scalar UI in staging).
- [ ] Runbooks for common ops tasks — `infra/docs/runbooks/`.
- [ ] Disaster recovery plan — `infra/docs/disaster-recovery.md`.
- [ ] Architecture overview — `docs/architecture.md` (summary of `06-ARCHITECTURE.md`).

### 1.9 Final Pre-Go-Live Checks

- [ ] Staging environment passes full UAT with internal team.
- [ ] Bug bash with entire team (1 day) — critical bugs fixed.
- [ ] Load test passed: 100 concurrent users, p95 < 2s.
- [ ] Smoke test in production after deployment (login, check-in, post activity, send message, generate report).
- [ ] DNS propagated (check from multiple locations).
- [ ] Pilot nursery director confirms ready.
- [ ] Tech Lead sign-off.
- [ ] Product Owner sign-off.

### 1.10 Go-Live Day

- [ ] Deploy to production (manual approval in CI/CD).
- [ ] Smoke test in production.
- [ ] Notify pilot nursery: "We're live!"
- [ ] Daily check-in with pilot nursery (Week 1: daily, Week 2: every other day).
- [ ] Monitor Grafana dashboards closely for first 48 hours.
- [ ] Ready to roll back if critical issues.
- [ ] Send pilot user survey link after Week 1.
- [ ] Pilot retrospective at end of Sprint 5.

---

## 2. Phase 1 Full Go-Live Checklist (Sprint 18)

### 2.1 All MVP Checklist Items (Re-Verified)

- [ ] All §1 items re-verified (some may have evolved).

### 2.2 Phase 1 Additional Modules

- [ ] Billing & Accounting module complete + tested.
- [ ] Enquiry Management module complete + tested.
- [ ] Tour Management module complete + tested.
- [ ] Calendar module complete + tested.
- [ ] Forms & Admissions module complete + tested.
- [ ] Tools (ID Card + Certificate generators) complete + tested.
- [ ] Reports & Analytics module complete + tested.
- [ ] Settings module full (all categories) complete + tested.
- [ ] Newsletter builder + Resource Centre complete + tested.
- [ ] SMS notifications (Critical tier) live with Unifonic or Twilio.

### 2.3 Mobile App Live

- [ ] iOS app submitted + approved by App Store.
- [ ] Android app submitted + approved by Google Play.
- [ ] iOS app available in UAE App Store.
- [ ] Android app available in UAE Google Play.
- [ ] EAS Build + OTA updates configured.
- [ ] Push notifications verified on iOS + Android.
- [ ] Mobile crash reporting (Sentry) active.
- [ ] Mobile analytics (PostHog) active.
- [ ] App Store screenshots in EN + AR.
- [ ] App Store description in EN + AR.
- [ ] App Store privacy labels completed.

### 2.4 Quality Gates

- [ ] Backend test coverage ≥ 80%.
- [ ] Web test coverage ≥ 75%.
- [ ] Mobile test coverage ≥ 70%.
- [ ] 0 critical + 0 high SonarCloud issues.
- [ ] E2E tests for all critical journeys across all modules.
- [ ] Visual regression tests for all screens in LTR + RTL.
- [ ] Load test: 500 concurrent users, p95 < 2s, p99 < 3s, error rate < 0.1%.
- [ ] WCAG 2.1 AA audit passed (external).
- [ ] Arabic RTL full regression (native Arabic speaker QA).
- [ ] External penetration test passed (OWASP Top 10 + business logic).
- [ ] All critical + high findings from pen test remediated.

### 2.5 Compliance

- [ ] PDPL compliance review complete (legal counsel sign-off).
- [ ] KHDA reporting alignment confirmed.
- [ ] ADEK reporting alignment confirmed (if Abu Dhabi customer).
- [ ] Data retention schedule legally confirmed.
- [ ] Privacy Policy updated for full Phase 1 features.
- [ ] Terms of Service updated.
- [ ] DPA template finalized.
- [ ] Sub-processor list updated (now includes SMS provider, content moderation service).

### 2.6 Commercial Readiness

- [ ] Pricing page published (nms.ae/pricing).
- [ ] Self-service trial signup flow tested.
- [ ] Stripe (or equivalent) billing integration for SaaS subscriptions.
- [ ] On-prem license agreement template finalized.
- [ ] Implementation services SOW template finalized.
- [ ] Customer success process documented.
- [ ] Support SLA documented (response times per severity).
- [ ] First 3 SaaS customers identified (pipeline).
- [ ] First 2 on-prem customers identified (pipeline).

### 2.7 Marketing + Sales

- [ ] Website live (nms.ae) with EN + AR versions.
- [ ] Product demo video recorded.
- [ ] Case study from pilot nursery (with permission).
- [ ] Sales deck ready.
- [ ] Demo environment ready (staging with seed data).
- [ ] Sales team trained (if applicable).
- [ ] Partner program documented (if applicable).

### 2.8 Final Sign-Off

- [ ] Tech Lead sign-off.
- [ ] Product Owner sign-off.
- [ ] Legal counsel sign-off.
- [ ] Security sign-off (pen test remediation verified).
- [ ] Compliance sign-off.
- [ ] Operations sign-off (runbooks tested, on-call ready).
- [ ] Sponsor sign-off.

---

## 3. Phase 2 Go-Live Checklist (Sprint 26)

### 3.1 All Phase 1 Items (Re-Verified)

- [ ] All §2 items re-verified.

### 3.2 Learning & Assessment Module

- [ ] EYFS framework fully implemented (7 development areas, all age bands, all milestones).
- [ ] Montessori framework fully implemented.
- [ ] Lessons module complete + tested.
- [ ] Observation records (formal, informal, concern) complete + tested.
- [ ] AI-assisted observation capture (voice-to-text) live + tested.
- [ ] Assessment templates + runs complete + tested.
- [ ] Dual report views (Staff + Parent) complete + tested.
- [ ] Parent report approval workflow complete + tested.
- [ ] Learning Journey view complete + tested.
- [ ] All 5 assessment report types complete + tested.
- [ ] L&A reports PDF generation works (QuestPDF).
- [ ] L&A module live in ≥3 pilot nurseries.
- [ ] 80%+ of pilot teachers using digital observations weekly.

### 3.3 Mobile App Maturity

- [ ] Mobile app: full offline support (attendance queue, activity post queue, messaging queue).
- [ ] Mobile app: Learning Journey view.
- [ ] Mobile app: observation capture with AI dictation.
- [ ] Mobile app: push notifications for all 3 tiers (Critical + Operational + Informational).
- [ ] Mobile app: 4.5+ star rating on iOS App Store.
- [ ] Mobile app: 4.5+ star rating on Google Play.
- [ ] Mobile app: crash-free rate > 99.5%.

### 3.4 Performance + Reliability

- [ ] p99 API latency < 500ms at 500 concurrent users.
- [ ] 99.5% uptime over 3-month measurement window.
- [ ] Database query performance reviewed (slow query log analyzed).
- [ ] Index optimization pass complete.
- [ ] Materialized view refresh tuned.
- [ ] Connection pool sizing reviewed.
- [ ] Redis memory usage reviewed.
- [ ] RabbitMQ queue depth monitored + alerts tuned.

### 3.5 Service Extraction Decision

- [ ] Notification Service extraction decision documented (ADR-015 if extracted).
- [ ] Media Processing Service extraction decision documented (ADR-016 if extracted).
- [ ] If extracted: new service deployed + tested + monitored.

### 3.6 Phase 2 Sign-Off

- [ ] Tech Lead sign-off.
- [ ] Product Owner sign-off.
- [ ] Pilot teacher feedback collected + analyzed (NPS).
- [ ] External pen test for new modules (L&A + mobile).
- [ ] Compliance review for AI features (OpenAI DPA, data anonymization).

---

## 4. Phase 3 Go-Live Checklist (Sprint 30)

### 4.1 All Phase 2 Items (Re-Verified)

- [ ] All §3 items re-verified.

### 4.2 Multi-Centre Group (MCG) Support

- [ ] Multi-realm Keycloak configured.
- [ ] MCG entity + parent_tenant_id hierarchy live.
- [ ] Group-level roles (group_owner, group_director, group_it) live.
- [ ] Cross-centre SSO verified (login once, access all centres).
- [ ] Tenant provisioning UI live (Group Owner can create new centres).
- [ ] MCG consolidated dashboard live.
- [ ] Cross-centre reports live.
- [ ] Student transfer between centres tested.
- [ ] Staff assignment to multiple centres tested.
- [ ] 1 MCG customer with 5+ centres live.

### 4.3 Payment Gateway Integration

- [ ] Gateway selected (Telr / PayTabs / Network International).
- [ ] Gateway integration tested (sandbox → production).
- [ ] Parent payment flow tested end-to-end.
- [ ] Webhook signature verification tested.
- [ ] Idempotent webhook handler tested.
- [ ] Refund flow tested.
- [ ] Saved cards (tokenization) tested.
- [ ] Apple Pay / Google Pay tested.
- [ ] Reconciliation job verified.
- [ ] PCI DSS scope confirmed (we're SAQ-A — gateway handles cardholder data).
- [ ] 100+ online payments processed in production.

### 4.4 ERP Integration

- [ ] At least 1 ERP adapter implemented (Xero / QuickBooks / Zoho Books).
- [ ] Chart of accounts mapping UI live.
- [ ] Sync: invoices → ERP verified.
- [ ] Sync: payments → ERP verified.
- [ ] Sync: expenses → ERP verified.
- [ ] Sync: payment status ← ERP verified.
- [ ] 1 customer with active ERP sync.

### 4.5 Open API

- [ ] Open API endpoints live (students, attendance, invoices, payments, reports).
- [ ] OAuth 2.0 client credentials flow tested.
- [ ] Rate limiting per application (1000 req/hour).
- [ ] Webhook publishing live (all major events).
- [ ] Webhook signing (HMAC-SHA256) verified.
- [ ] Webhook retry (exponential backoff, max 5 attempts over 24h) tested.
- [ ] Developer portal live (docs + sandbox + code samples).
- [ ] 1 third-party integration built by external developer.

### 4.6 Scale + Reliability

- [ ] 20 centres live.
- [ ] 500 concurrent users sustained (peak).
- [ ] 10,000 total users.
- [ ] 99.9% uptime over 3-month measurement window.
- [ ] p99 API latency < 3s at 500 concurrent + 20 centres.
- [ ] Multi-region DR tested (quarterly DR drill).

### 4.7 Extracted Services (If Applicable)

- [ ] Notification Service extracted (if ADR-015 decided yes).
- [ ] Notification Service: independently scalable, monitored.
- [ ] Media Processing Service extracted (if ADR-016 decided yes).
- [ ] Media Processing Service: independently scalable, monitored.
- [ ] Distributed tracing across all services (OpenTelemetry + Tempo).

### 4.8 Compliance + Security

- [ ] External pen test for Phase 3 (payment + multi-tenancy + Open API focus).
- [ ] All critical + high findings remediated.
- [ ] SOC 2 Type II audit initiated (target: 6 months post-launch).
- [ ] PCI DSS SAQ-A completed.
- [ ] Privacy Policy updated for Phase 3 features.
- [ ] Sub-processor list updated (payment gateway, ERP).
- [ ] Open API Terms of Service published.

### 4.9 Phase 3 Sign-Off

- [ ] Tech Lead sign-off.
- [ ] Product Owner sign-off.
- [ ] Legal counsel sign-off (payment + Open API + multi-tenant compliance).
- [ ] Security sign-off.
- [ ] Operations sign-off.
- [ ] Sponsor sign-off.
- [ ] SOC 2 audit kickoff scheduled.

---

## 5. Handover to Operations

After each phase go-live, formal handover to operations team:

### 5.1 Documentation Handover

- [ ] All runbooks reviewed + updated.
- [ ] Architecture diagrams updated.
- [ ] ADRs up to date.
- [ ] Operational metrics + SLOs documented.
- [ ] On-call runbook tested (dry run).
- [ ] Knowledge transfer sessions recorded.

### 5.2 Access Handover

- [ ] Ops team has AWS access (read + limited write).
- [ ] Ops team has K8s access (kubectl + Helm).
- [ ] Ops team has Grafana + Loki + Alertmanager access.
- [ ] Ops team has PagerDuty access.
- [ ] Ops team has Sentry access.
- [ ] Ops team has status page admin access.
- [ ] Secrets rotation schedule documented.
- [ ] Access review process documented (quarterly).

### 5.3 Process Handover

- [ ] Incident response process documented + tested.
- [ ] Change management process documented (who approves prod changes).
- [ ] Backup verification process documented.
- [ ] DR drill process documented (quarterly).
- [ ] Capacity planning process documented.
- [ ] Cost monitoring process documented.

### 5.4 Knowledge Transfer

- [ ] 2-hour knowledge transfer session with ops team.
- [ ] Q&A document from KT session.
- [ ] Ops team shadows dev team for 1 week (on-call).
- [ ] Ops team takes primary on-call (dev team secondary).
- [ ] Dev team available for escalation (Slack #eng-oncall).

---

## 6. Post-Launch Monitoring (First 30 Days)

For the first 30 days after each phase go-live:

### 6.1 Daily Checks (Tech Lead)

- [ ] Review Grafana dashboards (any anomalies?).
- [ ] Review Sentry errors (any new patterns?).
- [ ] Review customer support tickets.
- [ ] Review Slack #ops-alerts.
- [ ] Daily standup with team (15 min) — any issues?

### 6.2 Weekly Reviews

- [ ] Weekly customer feedback review (Product Owner).
- [ ] Weekly uptime + performance review.
- [ ] Weekly bug triage (new bugs from launch).
- [ ] Weekly retrospective (what went well, what didn't).

### 6.3 30-Day Retrospective

- [ ] Full team retrospective.
- [ ] Customer NPS survey results.
- [ ] Uptime report.
- [ ] Performance report (p50/p95/p99 latency over 30 days).
- [ ] Bug report (count by severity, time-to-resolve).
- [ ] Lessons learned document.
- [ ] Action items for next phase.

---

## 7. Quick Reference: Severity Definitions

| Severity | Definition | Response Time | Resolution Target | Notify |
|---|---|---|---|---|
| SEV-1 | Production down, data loss, security breach, child safety incident | 15 min | 4 hours | PagerDuty + Sponsor |
| SEV-2 | Major functionality broken, no workaround, >10 users affected | 1 hour | 24 hours | PagerDuty |
| SEV-3 | Feature partially broken, workaround exists, >1 user affected | 4 hours | 1 week | Slack |
| SEV-4 | Minor bug, single user impact | 1 business day | Next sprint | GitHub Issue |

---

## 8. Quick Reference: Go-Live Gates Summary

| Phase | Coverage | Performance | Uptime | Security | Compliance | Customers |
|---|---|---|---|---|---|---|
| MVP | 80/75/70 | p95 < 2s, 100 users | 99% | SAST + SCA clean | PDPL review | 1 pilot live |
| Phase 1 | 80/75/70 | p95 < 2s, 500 users | 99.5% | + External pen test | + KHDA + ADEK | 2 on-prem + 3 SaaS |
| Phase 2 | 80/75/70 | p99 < 500ms, 500 users | 99.5% | + Mobile pen test | + AI features review | 10 SaaS + 2 on-prem |
| Phase 3 | 80/75/70 | p99 < 3s, 500 users, 20 centres | 99.9% | + Payment + multi-tenant pen test | + SOC 2 kickoff | 20 SaaS, multi-centre groups |

---

## 9. Final Word

This checklist is exhaustive but not exhaustive enough. Use judgment. If something feels wrong, stop and investigate. Don't go-live with unresolved critical issues. Don't skip steps because "we're late." Late is better than broken.

**When in doubt, ask.** Tech Lead, Product Owner, Legal, Security — all available for go-live decisions.

Good luck. Build something parents + teachers + nursery owners love.

---

**End of NMS Prerequisites Pack.**

You now have everything needed to start building NMS with Cursor:
1. Read `00-MASTER-BRIEF.md` first.
2. Paste `18-CURSOR-RULES.md` contents into `.cursorrules`.
3. Follow `19-ROADMAP-SPRINTS.md` sprint by sprint.
4. Reference other files as needed.
5. Use `21-DELIVERY-CHECKLIST.md` before each go-live.

Welcome to the project.
