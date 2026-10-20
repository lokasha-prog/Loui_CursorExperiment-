# 01 — Product Vision

> Why this product exists, who it serves, what success looks like, and how it makes money.

---

## 1. Vision Statement

> **"Every UAE nursery — from a single 30-child neighborhood centre to a 20-branch group — runs on one trustworthy digital backbone that lets educators focus on children, parents feel close to their kids, and owners sleep at night knowing the books, ratios, and compliance are taken care of."**

The Nursery Management System (NMS) replaces fragmented manual and spreadsheet-driven workflows across UAE early childhood centres (ECCs) with a single, integrated, real-time platform. It is built for the **UAE regulatory and cultural context** first (PDPL, KHDA/ADEK, Arabic-English bilingual, AED billing) and designed to scale across the GCC.

We are **not** building a generic childcare SaaS. We are building the system that UAE nurseries choose because it understands their world: Ministry approval cycles, mixed Arabic/English parent populations, weekend-Friday scheduling expectations, AED fee plans, and KHDA inspection reporting.

## 2. Strategic Goals

| # | Goal | How we measure it |
|---|---|---|
| G1 | Replace manual nursery operations with a unified real-time platform | ≥80% of daily admin tasks digitized in pilot nurseries by end of Phase 1 |
| G2 | Give administrators live attendance, ratio, and child-safety visibility | Ratio breaches detected and alerted within 30 seconds; dashboard p95 < 2s |
| G3 | Give parents a portal they actually use | ≥70% of pilot parents active weekly (≥1 login or push interaction) |
| G4 | Automate billing and fee reminders | ≥90% of invoices auto-generated; ≥50% reduction in overdue invoices at 30 days |
| G5 | Support EYFS and Montessori curriculum with structured observation | Phase 2 milestone — observations captured digitally ≥80% of the time |
| G6 | Comply with UAE PDPL, KHDA/ADEK, GDPR-aligned standards | Zero unresolved compliance findings at go-live; legal sign-off before pilot |
| G7 | Build a horizontally scalable, multi-tenant-ready architecture | Phase 3 supports 20 centres, 500 concurrent users, p99 < 3s |
| G8 | Be commercially viable in both on-prem and SaaS modes | 2 on-prem pilot deployments + 1 SaaS tenant live by end of Phase 1 |

## 3. The Problem We Solve

### 3.1 Today's UAE Nursery Reality

A typical 100-child UAE nursery runs on:
- **WhatsApp groups** for parent communication (privacy risk, no audit trail, photos leak)
- **Excel sheets** for attendance, billing, and ratios (manual, error-prone, no real-time view)
- **Paper daily report cards** handed to parents at pickup (often skipped when staff are busy)
- **Manual invoice generation** in Word/PDF, sent individually via WhatsApp (hours of admin per week)
- **Sticky notes** for safeguarding concerns (lost, not actioned, no audit trail)
- **Phone calls** for emergency contact (parents unreachable, no log)
- **KHDA inspection panic** every year — scrambling to assemble records from 5 sources

### 3.2 What This Costs

| Cost | Quantified impact |
|---|---|
| Admin time | 15–20 hours/week per centre on billing, attendance, parent comms |
| Lost fees | 5–10% of monthly revenue from untracked overdue invoices |
| Compliance risk | KHDA/ADEK inspection findings; PDPL exposure via WhatsApp photo leaks |
| Parent dissatisfaction | Churn — parents switch nurseries when they feel "out of the loop" |
| Staff burnout | Teachers spend 30% of day on admin instead of children |
| Safety risk | Ratio breaches undetected; safeguarding notes lost; emergency contacts not on hand |

### 3.3 Why Existing Solutions Fall Short

| Competitor | Gap we close |
|---|---|
| **parent.app** | Strong UX baseline but limited UAE-specific localization, AED billing, KHDA reporting, Arabic RTL depth — we exceed their feature set and improve UX |
| **Famly** | UK/EU-focused, weak on Arabic, no on-prem option for sensitive nurseries |
| **Blossom Educational** | Limited billing automation, no SaaS+on-prem dual model |
| **Tapestry** | Learning-only, no operations, no billing |
| **Local UAE point solutions** | Often single-feature (attendance only, or billing only), no integrated platform |
| **Excel + WhatsApp** | Free but every problem above remains |

## 4. Personas

### 4.1 Primary Personas

#### Persona 1: **Layla — Centre Director** (Super Admin / Centre Admin)
- **Role**: Owner or hired director of a 60–150 child UAE nursery
- **Age**: 35–50, female (60% / 40% male)
- **Languages**: Arabic + English bilingual
- **Goals**: Keep ratios compliant, keep parents happy, pass KHDA inspections, grow enrolment
- **Pains**: Spends evenings on billing; worries about WhatsApp photo leaks; KHDA inspection panic
- **Tech comfort**: Moderate — uses WhatsApp, Excel, basic SaaS tools
- **Decision power**: Buys the software. Decides on-prem vs SaaS.

#### Persona 2: **Ahmed — Owner of 3 nurseries** (Super Admin, multi-centre)
- **Role**: Owns a small group of 2–5 nurseries across Dubai/Sharjah
- **Age**: 40–55, male or female
- **Goals**: Standardize operations across centres, see consolidated revenue, scale to 10 centres
- **Pains**: Each centre runs differently; no consolidated view; can't compare performance
- **Tech comfort**: High — wants dashboards, expects modern UX
- **Decision power**: Buys multi-centre SaaS subscription. Brand-sensitive.

#### Persona 3: **Mariam — Teacher / Key Person**
- **Role**: Classroom teacher, assigned as key person to 8–12 children
- **Age**: 25–40, female (90%)
- **Languages**: Arabic primary, English functional
- **Goals**: Spend time with children, not admin; share moments with parents; track development
- **Pains**: Hates end-of-day paperwork; forgets observations; phone camera roll full of child photos with privacy risk
- **Tech comfort**: High on mobile, moderate on web
- **Decision power**: Influencer — adoption lives or dies with teachers

#### Persona 4: **Fatima — Parent**
- **Role**: Working mother, child aged 2–4 in nursery
- **Age**: 28–40, female (primary app user; fathers secondary)
- **Languages**: Arabic or English (50/50 in UAE)
- **Goals**: Know her child is safe, fed, learning; feel connected during workday; pay fees easily
- **Pains**: Anxious when nursery doesn't communicate; WhatsApp group is noisy and not about her kid; paying fees is a chore
- **Tech comfort**: Very high on mobile
- **Decision power**: Adoption driver — if parents don't use the app, the nursery churns

#### Persona 5: **Sara — Receptionist / Front Desk**
- **Role**: Manages check-in/out, enquiries, tours, parent phone calls
- **Age**: 25–35, female
- **Languages**: Arabic + English
- **Goals**: Smooth check-in rush hour; convert enquiries to enrolments; never double-book a tour
- **Pains**: Barcode scanners don't work; paper sign-in sheets lost; enquiry follow-ups forgotten
- **Tech comfort**: Moderate
- **Decision power**: Influencer on operational UX

#### Persona 6: **Omar — Accounts Manager**
- **Role**: Handles billing, invoices, fee collection, expense tracking
- **Age**: 30–50, male
- **Languages**: English primary, Arabic functional
- **Goals**: Get invoices out on time, collect fees, track revenue, prepare monthly accounts
- **Pains**: Manual invoice generation in Excel; chasing parents for fees; reconciliation with bank
- **Tech comfort**: High on accounting tools, moderate on SaaS
- **Decision power**: Influencer on billing module

### 4.2 Secondary Personas (Phase 2+)

- **Khalid — IT Manager** (multi-centre group): Manages the SaaS tenant, user provisioning, integrations
- **Aisha — Marketing Lead**: Manages enquiries, newsletter, social media-sourced leads
- **Yousef — Procurement**: Inventory & asset management (Phase 2 scope)

## 5. Commercial Model

### 5.1 Dual Delivery Mode

NMS is sold in **two commercial modes** from a single codebase. The architecture must support both without forking.

#### Mode A: **On-Prem (Single-Tenant License)**
- **Customer**: Single nursery or small group (1–3 centres) that requires data sovereignty (e.g., government-affiliated, royal family nurseries, military nurseries)
- **Deployment**: Customer's own cloud account (AWS UAE / Azure UAE / local data centre) or on-prem servers
- **Tenant model**: Single tenant. `tenant_id` column exists but only one tenant row. RLS policies still enforced.
- **Pricing**: One-time license fee (AED 80,000–150,000) + annual maintenance (20% of license) + implementation (AED 20,000–50,000)
- **Identity**: Customer-managed Keycloak realm (or Active Directory federation)
- **Updates**: Customer controls update cadence. We provide LTS branches.
- **Contract**: Perpetual license + annual support

#### Mode B: **SaaS (Multi-Tenant Subscription)**
- **Customer**: Any nursery preferring low-capex, fast onboarding
- **Deployment**: Our managed AWS UAE Central (me-central-1) region
- **Tenant model**: Multi-tenant. Shared DB, RLS-enforced isolation. Each centre = one tenant.
- **Pricing**: Per-centre monthly subscription (AED 1,500–4,000/month depending on student count)
  - Tier 1: ≤50 students — AED 1,500/month
  - Tier 2: 51–100 students — AED 2,500/month
  - Tier 3: 101–200 students — AED 3,500/month
  - Tier 4: 201+ students — AED 4,000/month + custom
- **Identity**: Shared Keycloak instance, one realm per centre (Phase 1) → multi-realm (Phase 3)
- **Updates**: Continuous, we control cadence. Customer notified 7 days ahead.
- **Contract**: Monthly or annual subscription, cancel anytime

### 5.2 Add-On Revenue Streams

| Add-on | Phase | Price point |
|---|---|---|
| SMS notifications (above free quota) | Phase 1 | AED 0.30/SMS, billed monthly |
| Additional storage (above 50GB/centre) | Phase 1 | AED 50/100GB/month |
| Custom branding (white-label app) | Phase 1 on-prem, Phase 3 SaaS | AED 10,000 one-time |
| Payment gateway processing (transaction fee) | Phase 3 | 1.5–2.5% per transaction |
| Premium support (24/7, dedicated CSM) | All phases | +30% on subscription |
| Migration services (from Excel / competitor) | All phases | AED 5,000–25,000 one-time |
| Training (on-site, per centre) | All phases | AED 3,000/day |

### 5.3 Free Tier Strategy (SaaS only)

To drive adoption, a free 30-day trial per nursery (full features, no card required). Conversion target: 25% trial → paid.

## 6. Competitive Positioning

### 6.1 vs parent.app (the baseline)

parent.app is the **minimum acceptable feature bar**. We must match them on:
- Real-time newsfeed / activity feed
- Secure messaging
- Attendance tracking with ratio compliance
- Billing with online payments
- Multi-centre management
- Multi-language (they have 198 auto-translation; we deliver Arabic + English natively with proper RTL)
- AI-powered observation capture (Phase 2)

We must **exceed** them on:
- **UX/UI quality**: Modern, warm, premium feel — not the dated enterprise look many incumbents have
- **UAE-specific depth**: KHDA inspection reports, AED-native billing, Arabic-first RTL, Friday-weekend awareness
- **Dual deployment model**: On-prem option (parent.app is SaaS-only)
- **Real-time ratio enforcement**: Live WebSocket-driven alerts, not periodic refresh
- **Parent experience**: Thoughtful, mobile-first, with offline emergency info cache
- **Compliance auditability**: Append-only audit log with Super Admin UI viewer

### 6.2 Design Aspiration

Reference for design quality: **Linear, Notion, Stripe Dashboard, Vercel Dashboard, Arc Browser**. NOT 2010s enterprise SaaS. NOT a Material Design template.

The product must feel:
- **Calm**: Generous whitespace, soft colors, no clutter
- **Confident**: Strong typography, clear hierarchy, decisive interactions
- **Warm**: This is for children — soft pastels, friendly iconography, approachable tone
- **Trustworthy**: Professional, secure-feeling, no dark patterns
- **Fast**: Interactions < 200ms perceived, dashboard < 2s load

Full design system in `14-UX-UI-DESIGN.md`.

## 7. Roadmap Summary

| Phase | Duration | Outcome | Commercial milestone |
|---|---|---|---|
| **MVP Pilot** | Sprints 0–5 (~10 weeks) | Deploy to 1–2 pilot nurseries. Core: centre setup, attendance, ratios, parent comms, messaging, dashboard. | First pilot signed (free or discounted) |
| **Phase 1 Full** | Sprints 0–18 (~38 weeks) | All 13 Phase 1 modules. Web + mobile. Notifications. Emergency reports. KHDA-aligned reports. | First paid on-prem + first 3 SaaS tenants |
| **Phase 2** | Sprints 19–26 (~16 weeks) | Learning & Assessment (EYFS + Montessori). Full mobile maturity. Performance hardening. | 10 SaaS tenants, 2 on-prem sites |
| **Phase 3** | Sprints 27–30 (~8 weeks) | Multi-centre groups. Payment gateway. ERP integration. Open API. | 20 SaaS tenants, multi-centre groups |

Detailed sprint plan: `19-ROADMAP-SPRINTS.md`.

## 8. Success Metrics

### 8.1 MVP Pilot Exit Criteria (end of Sprint 5)

| Metric | Target |
|---|---|
| Pilot nurseries live | ≥1 (target: 2) |
| Active users (staff) | ≥10 |
| Active users (parents) | ≥30 |
| Daily check-ins logged in system | ≥80% of expected attendance |
| Critical bugs open | 0 |
| High bugs open | ≤3 |
| Parent portal load time p95 | < 2.5s |
| Ratio breach alert delivery | < 30s from breach to push notification |
| Pilot user NPS | ≥30 |
| Pilot user "would recommend" | ≥60% |

### 8.2 Phase 1 Commercial Targets

| Metric | Target by end of Phase 1 (Sprint 18) |
|---|---|
| On-prem deployments | 2 paid |
| SaaS tenants | 3 paid |
| ARR | AED 600,000 |
| Centres live | 5+ |
| Net Revenue Retention | ≥110% |
| Trial → paid conversion | ≥25% |
| Customer satisfaction (CSAT) | ≥4.5/5 |

### 8.3 Product Quality Targets (every phase)

| Metric | Target |
|---|---|
| Dashboard p95 load | < 2s |
| API p99 latency | < 500ms |
| Uptime SLA | 99.5% (Phase 1), 99.9% (Phase 3) |
| Test coverage (backend) | ≥80% |
| E2E test pass rate | ≥95% on main |
| Critical security findings open | 0 |
| Accessibility (parent portal) | WCAG 2.1 AA |
| RTL parity | 100% (no broken layouts in Arabic) |

## 9. Stakeholders

| Stakeholder | Interest | Engagement cadence |
|---|---|---|
| **Sponsor / Product Owner** | Commercial success, scope decisions | Weekly sprint review |
| **Centre Directors (pilot)** | Operational fit, daily usability | Weekly during pilot, monthly after |
| **Parents (pilot)** | App quality, communication value | Bi-weekly survey during pilot |
| **Legal counsel (UAE)** | PDPL compliance, data residency, contracts | Sprint 0, before go-live, Phase 3 kickoff |
| **KHDA / ADEK liaison** | Regulatory alignment | Sprint 0 consultation, pre-go-live review |
| **Cloud provider (AWS/Azure UAE)** | Infrastructure, compliance certifications | Sprint 0 setup, ongoing |
| **AI/Coding agent (Cursor)** | Builds the code per these specs | Continuous |

## 10. Non-Goals (What We Are NOT Building)

To prevent scope creep, these are explicitly out of scope for all 3 phases:

- **Payroll processing for staff** — nurseries use external payroll. We export attendance, they calculate.
- **HR management (recruitment, performance reviews)** — out of scope.
- **Inventory management for supplies** — Phase 2 consideration only if Procurement role activates.
- **Transportation / bus tracking** — out of scope for all phases.
- **Catering / meal planning** — food log is in scope (parent comms), but menu planning is not.
- **Custom CRM beyond enquiry pipeline** — not a sales CRM.
- **Marketing automation (email campaigns, social scheduling)** — newsletter builder is in scope, full marketing automation is not.
- **Website builder for nurseries** — out of scope. We integrate with their existing website for enquiry form.
- **Mobile app for parents in Phase 1** — parent experience is responsive web in Phase 1; native parent app considered in Phase 2.
- **AI chatbot for parents** — out of scope.
- **Biometric attendance (fingerprint, facial recognition)** — barcode only in Phase 1; biometrics deferred pending legal review.

## 11. Glossary Reminder

Full glossary in upstream SRS v1.2 §8. Key terms used in this pack:

- **NMS** — Nursery Management System (this product)
- **ECC** — Early Childhood Centre (preferred UAE term over "nursery")
- **EYFS** — Early Years Foundation Stage (UK statutory framework, used in UAE nurseries)
- **PDPL** — UAE Personal Data Protection Law (Federal Decree-Law No. 45 of 2021)
- **KHDA** — Knowledge and Human Development Authority (Dubai education regulator)
- **ADEK** — Abu Dhabi Department of Education and Knowledge
- **RTL** — Right-to-Left text direction (Arabic)
- **RLS** — Row Level Security (PostgreSQL feature for multi-tenancy)
- **Tenant** — A single nursery centre (SaaS) or the single deployment (on-prem)
- **Key Person** — Designated staff member primary-responsible for a specific child
- **Ratio** — Mandated student-to-staff ratio per age band (UAE regulatory)

---

**Next file to read**: `02-PRD-MVP.md` — the MVP pilot scope and acceptance criteria.
