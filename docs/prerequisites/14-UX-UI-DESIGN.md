# 14 — UX/UI Design System

> The design vision, principles, color system, typography, components, and patterns that elevate NMS beyond parent.app's baseline.

---

## 1. Design Vision

### 1.1 The Bar

**parent.app** is the minimum acceptable baseline. We must match them on features and **exceed them on UX/UI quality**.

parent.app's UX is functional but feels dated — enterprise SaaS aesthetic from 2015: cluttered tables, dense forms, inconsistent spacing, generic Material Design echoes, weak Arabic RTL.

NMS UX must feel like a **2026 product**: calm, confident, warm, modern. Reference quality: Linear, Notion, Stripe, Vercel, Arc Browser. NOT Salesforce, NOT Workday, NOT 2010s enterprise.

### 1.2 Design Principles

1. **Calm** — Generous whitespace, soft colors, no clutter. The product feels restful even in busy moments.
2. **Confident** — Strong typography, clear hierarchy, decisive interactions. Every screen knows what it's about.
3. **Warm** — This is for children. Soft pastels, friendly iconography, approachable tone. Never sterile.
4. **Trustworthy** — Professional, secure-feeling. No dark patterns. No surprise modals. Clear state changes.
5. **Fast** — Interactions under 200ms perceived. Dashboard under 2s load. Loading states everywhere.
6. **Bilingual-first** — Arabic and English are first-class. RTL is not an afterthought. Both languages feel native.
7. **Accessible** — WCAG 2.1 AA. Keyboard-navigable. Screen-reader-friendly. Color-contrast-compliant.
8. **Mobile-grade on web** — Even the web app feels touch-friendly. Big tap targets. No hover-dependent UI.

### 1.3 Tone of Voice

| Dimension | What it means | Example |
|---|---|---|
| Clear | Plain language, no jargon | "Check in Ahmed" not "Execute attendance recordation for entity: Ahmed" |
| Warm | Friendly, not corporate | "Ahmed checked in at 8:32 AM" not "Attendance record created: timestamp=08:32:00, entity=Ahmed" |
| Concise | Short, scannable | "3 students pending" not "There are currently 3 students whose attendance status is pending" |
| Active | Verb-first, decisive | "Save changes" not "Changes should be saved" |
| Inclusive | Addresses user as "you" | "You have 5 unread messages" not "5 unread messages exist" |

---

## 2. Brand Identity

### 2.1 Logo

- Custom wordmark: "NMS" in a modern geometric sans (Inter or similar).
- Optional: small icon (leaf, building, abstract shape) — TBD with brand designer.
- Used in: app header, login screen, email templates, invoices.

### 2.2 Brand Personality

| Trait | Expression |
|---|---|
| Trustworthy (parents trust us with their children) | Solid typography, restrained color palette, no flashy animations |
| Warm (children are involved) | Soft pastels, rounded corners, friendly illustrations |
| Modern (we're a 2026 product) | Generous whitespace, micro-interactions, fast feedback |
| Professional (this is enterprise software) | Consistent grid, predictable patterns, no decorative noise |

---

## 3. Color System

### 3.1 Primary Palette

```css
/* globals.css — CSS variables (work in both LTR and RTL) */
:root {
  /* Brand */
  --color-primary: #0066CC;          /* Trust blue */
  --color-primary-hover: #0052A3;
  --color-primary-active: #003D7A;
  --color-primary-soft: #E6F0FF;     /* Background tint */
  
  /* Accent (warm contrast — for highlights, badges) */
  --color-accent: #FF6B35;           /* Warm orange — energy */
  --color-accent-hover: #E55A2B;
  --color-accent-soft: #FFF0E6;
  
  /* Semantic */
  --color-success: #16A34A;          /* Green */
  --color-success-soft: #DCFCE7;
  --color-warning: #F59E0B;          /* Amber */
  --color-warning-soft: #FEF3C7;
  --color-danger: #DC2626;           /* Red */
  --color-danger-soft: #FEE2E2;
  --color-info: #0EA5E9;             /* Sky blue */
  --color-info-soft: #E0F2FE;
  
  /* Neutrals (warm gray, not cold) */
  --color-background: #FAFAF9;       /* Warm white */
  --color-surface: #FFFFFF;
  --color-surface-2: #F5F5F4;        /* Subtle gray */
  --color-border: #E7E5E4;
  --color-border-strong: #D6D3D1;
  
  /* Text */
  --color-text: #1C1917;             /* Stone-900 */
  --color-text-secondary: #57534E;   /* Stone-600 */
  --color-text-tertiary: #A8A29E;    /* Stone-400 */
  --color-text-on-primary: #FFFFFF;
  --color-text-on-accent: #FFFFFF;
  
  /* Status (for attendance) */
  --color-status-present: #16A34A;
  --color-status-absent: #DC2626;
  --color-status-pending: #F59E0B;
  --color-status-late: #8B5CF6;      /* Violet */
  --color-status-checked-in: #16A34A;
  --color-status-checked-out: #6B7280;  /* Gray */
  
  /* Tiers (for notifications) */
  --color-tier-critical: #DC2626;
  --color-tier-operational: #F59E0B;
  --color-tier-informational: #0EA5E9;
}

/* Dark theme (optional, Phase 2) */
.dark {
  --color-primary: #3B82F6;
  --color-background: #0C0A09;
  --color-surface: #1C1917;
  --color-surface-2: #292524;
  --color-border: #44403C;
  --color-text: #FAFAF9;
  --color-text-secondary: #D6D3D1;
  --color-text-tertiary: #78716C;
}
```

### 3.2 Color Usage Rules

- **Primary** — main actions (Save, Submit, primary CTAs), active nav item, links.
- **Accent** — sparingly, for warm highlights (badges, "new" indicators, AI feature accents).
- **Success** — completed state, approved, paid.
- **Warning** — pending, overdue, attention needed.
- **Danger** — destructive actions, errors, ratio breach.
- **Info** — informational badges, neutral highlights.
- **Neutrals** — backgrounds, borders, secondary text.

**Never**:
- More than 2 semantic colors in one component.
- Primary color for non-interactive elements.
- Red for non-danger elements.
- Pure black (#000) or pure white (#FFF) for text/bg — use the warm neutrals.

### 3.3 Accessibility (Contrast)

All color combinations meet WCAG 2.1 AA:
- Body text on background: #1C1917 on #FAFAF9 → 16.5:1 (AAA).
- Secondary text: #57534E on #FAFAF9 → 6.5:1 (AA).
- Tertiary text: #A8A29E on #FAFAF9 → 2.7:1 — **only for large text or non-essential info**.
- Primary button: #FFFFFF on #0066CC → 5.3:1 (AA).
- Danger button: #FFFFFF on #DC2626 → 5.0:1 (AA).

Tested with `axe-core` in CI.

---

## 4. Typography

### 4.1 Font Stack

**Latin (English)**:
```css
--font-sans: 'Inter', system-ui, -apple-system, 'Segoe UI', sans-serif;
--font-mono: 'JetBrains Mono', 'Cascadia Code', monospace;
```

**Arabic**:
```css
--font-sans-ar: 'IBM Plex Sans Arabic', 'Tajawal', 'Noto Sans Arabic', sans-serif;
```

Why:
- **Inter** — modern, neutral, excellent legibility at small sizes, free, supports Latin + Cyrillic + Greek.
- **IBM Plex Sans Arabic** — designed alongside IBM Plex (consistent visual language), excellent Arabic legibility, supports Arabic + Latin.
- **Tajawal** — fallback Arabic (open source, clean, modern).

**Both fonts loaded via `@fontsource/inter` and `@fontsource/ibm-plex-sans-arabic`** (self-hosted, no Google Fonts privacy issue).

### 4.2 Type Scale

```css
/* Tailwind config */
fontSize: {
  'xs': ['0.75rem', { lineHeight: '1rem' }],     /* 12px */
  'sm': ['0.875rem', { lineHeight: '1.25rem' }], /* 14px */
  'base': ['1rem', { lineHeight: '1.5rem' }],    /* 16px */
  'lg': ['1.125rem', { lineHeight: '1.75rem' }], /* 18px */
  'xl': ['1.25rem', { lineHeight: '1.75rem' }],  /* 20px */
  '2xl': ['1.5rem', { lineHeight: '2rem' }],     /* 24px */
  '3xl': ['1.875rem', { lineHeight: '2.25rem' }],/* 30px */
  '4xl': ['2.25rem', { lineHeight: '2.5rem' }],  /* 36px */
  '5xl': ['3rem', { lineHeight: '1' }],          /* 48px */
}

fontWeight: {
  regular: 400,
  medium: 500,
  semibold: 600,
  bold: 700,
}
```

### 4.3 Usage

| Element | Size | Weight | Notes |
|---|---|---|---|
| Page title (H1) | text-3xl (30px) | semibold | One per page |
| Section title (H2) | text-xl (20px) | semibold | |
| Card title (H3) | text-lg (18px) | semibold | |
| Body text | text-base (16px) | regular | Default for paragraphs |
| Secondary text | text-sm (14px) | regular | Helper text, metadata |
| Caption | text-xs (12px) | regular | Timestamps, badge labels |
| Button label | text-sm (14px) | semibold | |
| Form label | text-sm (14px) | medium | |
| Form input | text-base (16px) | regular | Never smaller (iOS zoom) |
| Table header | text-xs (12px) | medium | uppercase, tracking-wide |
| Table cell | text-sm (14px) | regular | |

### 4.4 Line Height

- Body text: 1.5x font size (Tailwind `leading-6` for `text-base`).
- Headings: 1.25-1.4x font size.
- Buttons/labels: 1x (single line).

### 4.5 Arabic Typography Notes

- Arabic script is denser — slightly larger font size (102-105%) sometimes needed for legibility.
- Use `font-feature-settings: "kern"` for proper Arabic kerning.
- Numbers: use Western Arabic numerals (1, 2, 3) by default for consistency with billing/attendance. Eastern Arabic numerals (١, ٢, ٣) optional per user preference.
- Dates: use Gregorian calendar by default. Hijri calendar optional display.

---

## 5. Spacing & Layout

### 5.1 Spacing Scale (Tailwind defaults + extensions)

```css
spacing: {
  0: '0',
  1: '0.25rem',    /* 4px */
  2: '0.5rem',     /* 8px */
  3: '0.75rem',    /* 12px */
  4: '1rem',       /* 16px */
  5: '1.25rem',    /* 20px */
  6: '1.5rem',     /* 24px */
  8: '2rem',       /* 32px */
  10: '2.5rem',    /* 40px */
  12: '3rem',      /* 48px */
  16: '4rem',      /* 64px */
  20: '5rem',      /* 80px */
  24: '6rem',      /* 96px */
}
```

### 5.2 Container

```css
--container-page: 1280px;   /* Max page width */
--container-narrow: 768px;  /* Forms, single column */
--container-wide: 1536px;   /* Dashboards, tables */
```

### 5.3 Grid

- 12-column grid for complex layouts (dashboards).
- 8px base unit (Tailwind default).
- Use `gap-4` (16px) for grid gaps.

### 5.4 Page Padding

- Desktop: `p-6` (24px) on all sides.
- Tablet: `p-4` (16px).
- Mobile: `p-3` (12px).

### 5.5 Card Spacing

```css
--card-padding: 1.5rem;       /* 24px */
--card-padding-sm: 1rem;      /* 16px */
--card-gap: 1.5rem;           /* 24px between cards */
--card-radius: 0.75rem;       /* 12px — generous rounding */
--card-border: 1px solid var(--color-border);
--card-shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
```

---

## 6. Component Specifications

### 6.1 Buttons

| Variant | Use case | Style |
|---|---|---|
| Primary | Main action (one per view) | bg-primary text-white hover:bg-primary-hover |
| Secondary | Alternative action | bg-surface text-primary border border-primary |
| Outline | Cancel, dismiss | bg-transparent border border-border-strong text-text |
| Ghost | Tertiary actions | bg-transparent text-primary hover:bg-primary-soft |
| Destructive | Delete, remove | bg-danger text-white hover:bg-danger/90 |
| Subtle | In-table actions | bg-transparent text-text-secondary hover:bg-surface-2 |

**Sizes**:
- `sm`: h-8 px-3 text-sm (32px height)
- `md`: h-10 px-4 text-sm (40px height) — default
- `lg`: h-12 px-6 text-base (48px height) — primary CTA on key pages

**Rules**:
- One primary button per view (the main action).
- Icon + label: icon on inline-start, label after.
- Loading state: spinner replaces icon, button disabled.
- Disabled: opacity-50, cursor-not-allowed.

### 6.2 Form Inputs

```css
.input {
  height: 2.5rem;          /* 40px — tap target */
  padding: 0 0.75rem;
  border: 1px solid var(--color-border-strong);
  border-radius: 0.5rem;   /* 8px */
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 1rem;         /* 16px — never smaller, prevents iOS zoom */
  transition: border-color 150ms;
}

.input:focus {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px var(--color-primary-soft);
}

.input:disabled {
  background: var(--color-surface-2);
  color: var(--color-text-tertiary);
  cursor: not-allowed;
}

.input.error {
  border-color: var(--color-danger);
}
```

**Labels**: above inputs, `text-sm font-medium text-text`, 4px gap to input.
**Helper text**: below input, `text-xs text-text-secondary`.
**Error text**: below input, `text-xs text-danger`, replaces helper text when error.

### 6.3 Cards

```css
.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 0.75rem;   /* 12px */
  padding: 1.5rem;          /* 24px */
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.card-title {
  font-size: 1.125rem;      /* 18px */
  font-weight: 600;
}

.card-description {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  margin-top: 0.25rem;
}
```

### 6.4 Tables

```css
.table {
  width: 100%;
  border-collapse: collapse;
}

.table-header {
  background: var(--color-surface-2);
  border-bottom: 1px solid var(--color-border);
}

.table-header-cell {
  padding: 0.75rem 1rem;
  text-align: start;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.table-row {
  border-bottom: 1px solid var(--color-border);
  transition: background 150ms;
}

.table-row:hover {
  background: var(--color-surface-2);
}

.table-cell {
  padding: 0.75rem 1rem;
  font-size: 0.875rem;
  color: var(--color-text);
}
```

**Patterns**:
- Sticky header (stays visible on scroll).
- Sortable columns (click to sort, arrow indicator).
- Row hover: subtle background change.
- Row click: navigate to detail.
- Empty state: full-width row with icon + message.
- Loading: skeleton rows.

### 6.5 Badges

| Type | Use case | Style |
|---|---|---|
| Status (Present/Absent) | Attendance status | Colored bg + text (e.g., bg-success-soft text-success) |
| Count | Unread count, pending count | bg-primary text-white rounded-full |
| New | Recently added | bg-accent text-white |
| Tier | Notification tier | Colored dot + label |

### 6.6 Modals/Dialogs

```css
.dialog-overlay {
  background: rgb(0 0 0 / 0.5);
  backdrop-filter: blur(2px);
}

.dialog-content {
  background: var(--color-surface);
  border-radius: 0.75rem;
  padding: 1.5rem;
  max-width: 90vw;
  max-height: 85vh;
  overflow-y: auto;
  box-shadow: 0 25px 50px -12px rgb(0 0 0 / 0.25);
}

.dialog-sizes: {
  sm: 'max-w-sm',   /* 400px — confirm dialogs */
  md: 'max-w-md',   /* 450px — simple forms */
  lg: 'max-w-2xl',  /* 672px — complex forms */
  xl: 'max-w-4xl',  /* 896px — multi-step wizards */
}
```

**Rules**:
- Always have a clear title.
- Always have a close button (X) in top-end corner.
- Footer with action buttons (Cancel + primary).
- Trap focus inside modal.
- Close on Esc + overlay click.
- Return focus to trigger element on close.

### 6.7 Toasts/Notifications

```css
.toast {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 0.5rem;
  padding: 0.75rem 1rem;
  box-shadow: 0 10px 15px -3px rgb(0 0 0 / 0.1);
  min-width: 300px;
  max-width: 400px;
}

.toast-success { border-inline-start: 4px solid var(--color-success); }
.toast-error { border-inline-start: 4px solid var(--color-danger); }
.toast-warning { border-inline-start: 4px solid var(--color-warning); }
.toast-info { border-inline-start: 4px solid var(--color-info); }
```

Position: top-end of viewport, stacked vertically with gap-2.
Auto-dismiss: 5s (success), 10s (info/warning), persistent (error — user must dismiss).

### 6.8 Avatar

```css
.avatar {
  border-radius: 9999px;
  background: var(--color-surface-2);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.avatar-sm { width: 1.5rem; height: 1.5rem; }   /* 24px */
.avatar-md { width: 2.5rem; height: 2.5rem; }   /* 40px — default */
.avatar-lg { width: 3.5rem; height: 3.5rem; }   /* 56px */
.avatar-xl { width: 5rem; height: 5rem; }       /* 80px — profile */
```

If no photo: show initials (first letter of first + last name) on primary-soft background.

### 6.9 Empty States

Every list view has a designed empty state:

```
[Icon (large, muted)]
[Title (medium weight)]
[Description (small, secondary)]
[Optional action button]
```

Example: "No students yet. Add your first student to get started." + [Add Student] button.

### 6.10 Loading States

| Pattern | When |
|---|---|
| Full-screen spinner | Initial page load |
| Skeleton loaders | Content area loading (table, card grid) |
| Inline spinner | Button submit, async action |
| Progress bar | Long-running operation (file upload, report generation) |
| Pulse animation | Background refresh |

Never block UI without feedback.

---

## 7. Layout Patterns

### 7.1 App Shell (Staff Web)

```
┌──────────────────────────────────────────────────────────────┐
│ [Logo]  [Search...]              [Lang]  [Notif]  [Avatar]   │  ← Header (h-14)
├────────────┬─────────────────────────────────────────────────┤
│            │                                                 │
│ Dashboard  │                                                 │
│ Students   │                                                 │
│ Attendance │                                                 │
│ Activities │           [Page Content]                        │
│ Messaging  │                                                 │
│ Billing    │                                                 │
│ Enquiries  │                                                 │
│ ...        │                                                 │
│            │                                                 │
└────────────┴─────────────────────────────────────────────────┘
   Sidebar           Main content area
   (w-60 or w-72)    (flex-1, p-6)
```

**Responsive**:
- Desktop (>1024px): full sidebar visible.
- Tablet (768-1024px): sidebar collapses to icons only (w-16).
- Mobile (<768px): sidebar hidden, hamburger menu.

### 7.2 Parent Layout (Web + Mobile)

Simpler — parents don't need the full sidebar. Tab-based navigation:

```
┌──────────────────────────────────────────────────────────────┐
│ [Logo]  [Child Selector ▼]            [Lang]  [Avatar]       │  ← Header
├──────────────────────────────────────────────────────────────┤
│                                                              │
│                                                              │
│                    [Page Content]                            │
│                                                              │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│   [Feed]    [Messages]    [Reports]    [Profile]             │  ← Bottom tab bar (mobile)
└──────────────────────────────────────────────────────────────┘
```

On mobile: bottom tab bar (native iOS/Android pattern).
On web: top tab bar or sidebar (depending on breakpoint).

### 7.3 Dashboard Layout

```
┌──────────────────────────────────────────────────────────────┐
│ Dashboard                                                     │
├──────────────────────────────────────────────────────────────┤
│ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐              │
│ │ Rooms 8 │ │Staff 24 │ │Stud 142 │ │ Prog 6  │  ← KPI cards │
│ └─────────┘ └─────────┘ └─────────┘ └─────────┘              │
├──────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────┐ ┌─────────────────────────┐  │
│ │ Live Ratio Monitor          │ │ Pending Actions         │  │
│ │                             │ │                         │  │
│ │ Room A  4:1  ✓              │ │ • 3 unapproved photos   │  │
│ │ Room B  3:1  ⚠ BREACH       │ │ • 2 leave requests      │  │
│ │ Room C  5:1  ✓              │ │ • 5 unread messages     │  │
│ │                             │ │                         │  │
│ └─────────────────────────────┘ └─────────────────────────┘  │
├──────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────┐  │
│ │ Today's Attendance Summary                              │  │
│ │ [Staff: 22/24 checked in] [Students: 138/142]          │  │
│ └─────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

Grid: 4 columns for KPIs, 2 columns for widgets (1 column on mobile).

### 7.4 List View Layout

```
┌──────────────────────────────────────────────────────────────┐
│ Students                                          [+ Add]     │
├──────────────────────────────────────────────────────────────┤
│ [Search...]  [Room ▼]  [Status ▼]  [More filters]            │
├──────────────────────────────────────────────────────────────┤
│ Name              Room      Program     Status   Actions     │
│ ─────────────────────────────────────────────────────────── │
│ Ahmed Ali         Toddlers  Full Day    Active   [⋯]         │
│ Layla Hassan      Babies    Half Day    Active   [⋯]         │
│ Omar Khalid       Pre-K     Full Day    On Hold  [⋯]         │
│ ...                                                          │
├──────────────────────────────────────────────────────────────┤
│                          [Load more]                         │
└──────────────────────────────────────────────────────────────┘
```

### 7.5 Detail View Layout

```
┌──────────────────────────────────────────────────────────────┐
│ ← Back   Ahmed Ali                              [Edit] [...] │
├──────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────┐ ┌──────────────────────┐ │
│ │ [Photo]                         │ │ Quick Actions         │ │
│ │ Ahmed Ali                       │ │ • Check In            │ │
│ │ Age 3y 2m                       │ │ • Mark Absent         │ │
│ │ Room: Toddlers                  │ │ • Post Activity       │ │
│ │ Program: Full Day               │ │ • Send Message        │ │
│ │ Status: Active                  │ │ • Emergency Report    │ │
│ │                                 │ │                       │ │
│ │ Key Person: Mariam              │ │                       │ │
│ │ Start Date: Sep 1, 2025         │ │                       │ │
│ └─────────────────────────────────┘ └──────────────────────┘ │
├──────────────────────────────────────────────────────────────┤
│ [Attendance] [Activities] [Guardians] [Billing] [Medical]    │ ← Tabs
├──────────────────────────────────────────────────────────────┤
│                                                              │
│                   [Tab content]                              │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

---

## 8. Iconography

### 8.1 Icon Library

**Lucide React** (https://lucide.dev/) — clean, consistent, MIT-licensed, tree-shakeable.

Used for:
- Nav items (sidebar, tab bar).
- Action buttons (save, edit, delete).
- Status indicators (check, alert, info).
- Feature icons (dashboard, attendance, billing).

### 8.2 Icon Sizing

| Use | Size | Stroke |
|---|---|---|
| Nav (sidebar) | 20px | 1.5 (default) |
| Button icon | 16px | 2 |
| Inline (form) | 16px | 2 |
| Status badge | 12px | 2.5 |
| Empty state hero | 48px | 1.5 |
| Page hero | 64px | 1.5 |

### 8.3 Directional Icons in RTL

Directional icons (chevron-left, arrow-right) must flip in RTL:

```tsx
import { ChevronRight } from 'lucide-react';

// Auto-flips via CSS
<ChevronRight className="rtl:rotate-180" />

// Or use direction-aware component
<DirectionalIcon name="chevron-right" />
```

---

## 9. Animation & Micro-interactions

### 9.1 Principles

- **Subtle** — animation enhances, never distracts.
- **Fast** — 150-200ms duration. Anything longer feels sluggish.
- **Purposeful** — every animation explains a state change.
- **Eased** — use `ease-out` for entrances, `ease-in` for exits.

### 9.2 Standard Transitions

```css
--transition-fast: 100ms ease-out;
--transition-default: 150ms ease-out;
--transition-slow: 250ms ease-out;

/* Hover states */
.button { transition: background-color var(--transition-fast); }
.card { transition: box-shadow var(--transition-default); }

/* Modal entrance */
@keyframes dialog-in {
  from { opacity: 0; transform: scale(0.95); }
  to { opacity: 1; transform: scale(1); }
}

.dialog-content { animation: dialog-in 150ms ease-out; }

/* Toast entrance */
@keyframes toast-in {
  from { opacity: 0; transform: translateX(100%); }
  to { opacity: 1; transform: translateX(0); }
}

/* RTL: toast slides in from inline-start, not right */
[dir='rtl'] .toast { animation-name: toast-in-rtl; }
@keyframes toast-in-rtl {
  from { opacity: 0; transform: translateX(-100%); }
  to { opacity: 1; transform: translateX(0); }
}
```

### 9.3 Reduced Motion

```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
```

Always respected — some users get motion sickness.

---

## 10. RTL (Right-to-Left) Patterns

### 10.1 The Cardinal Rules

1. **CSS logical properties only** — `margin-inline-start`, `padding-inline-end`, `text-align: start`, `border-inline-start`. Never `margin-left`, `padding-right`, etc.

2. **Tailwind logical utilities** — `ms-*` (margin-inline-start), `me-*` (margin-inline-end), `ps-*`, `pe-*`, `text-start`, `text-end`. Tailwind 3.3+ handles the RTL flip automatically when `dir="rtl"` is set.

3. **Flexbox auto-flips** — `flex-row` in LTR becomes effectively `flex-row-reverse` in RTL automatically. Don't manually reverse.

4. **Directional icons flip** — `ChevronRight` becomes `ChevronLeft` in RTL. Use `rtl:rotate-180` Tailwind utility.

5. **Absolute positioning** — `left-0` becomes `right-0` in RTL. Use `inset-inline-start-0` instead.

6. **Numbers + dates** — Same format in both LTR and RTL. Numbers don't flip. Use `Intl.NumberFormat` and `Intl.DateTimeFormat` with locale.

### 10.2 RTL Testing

- ESLint rule forbids physical properties (see `12-WEB-FRONTEND.md` §6.4).
- Playwright visual regression tests in both LTR + RTL.
- Storybook stories render in both directions.
- Every screen manually QA'd in Arabic by an Arabic-speaking tester before sign-off.

### 10.3 Common RTL Pitfalls

| Pitfall | Wrong | Right |
|---|---|---|
| Icon position in button | `mr-2` (always right of text) | `me-2` (inline-end of text, flips in RTL) |
| Dropdown menu position | `left-0` | `inset-inline-start-0` |
| Tooltip arrow | hardcoded left arrow | direction-aware arrow |
| Modal close button | `top-3 right-3` | `top-3 inset-inline-end-3` |
| Progress bar fill | `left to right` | `inline-start to inline-end` |
| Avatar next to name | `mr-3` | `me-3` |
| Breadcrumb separator | `ChevronRight` (always right-pointing) | `ChevronRight` with `rtl:rotate-180` |

---

## 11. Accessibility (WCAG 2.1 AA)

### 11.1 Color Contrast

- Normal text: 4.5:1 minimum.
- Large text (18px+ regular, 14px+ bold): 3:1 minimum.
- UI components (button bg, input border): 3:1 minimum.
- Tested with `axe-core` in CI.

### 11.2 Keyboard Navigation

- All interactive elements reachable via Tab.
- Logical tab order (matches visual order).
- Visible focus indicator (2px outline, never `outline: none`).
- Skip-to-content link at top of every page.
- Modal: focus trap, focus first element on open, return focus on close.
- Dropdown: arrow keys to navigate, Enter to select, Esc to close.

### 11.3 Screen Reader Support

- Semantic HTML (`nav`, `main`, `section`, `article`, `header`, `footer`).
- ARIA labels on icon-only buttons (`aria-label="Close"`).
- `aria-live="polite"` for toasts, `aria-live="assertive"` for errors.
- `aria-busy="true"` on loading containers.
- Form labels: `<label>` associated with `<input>` via `htmlFor`/`id`, never just placeholder text.
- Tables: `<th scope="col">` and `<th scope="row">`.
- Images: meaningful `alt`, decorative images `alt=""`.
- Dynamic content: `aria-live` regions announce updates.

### 11.4 Reduced Motion

See §9.3 above.

### 11.5 Other

- No color-only meaning (always include icon or text).
- Touch targets: minimum 44×44px (iOS HIG) / 48×48dp (Material).
- Form errors: never rely on color alone — include icon + text.
- Headings: hierarchical (don't skip levels).
- Language: `<html lang="en">` or `<html lang="ar" dir="rtl">`.

---

## 12. Mobile-Specific Patterns

### 12.1 Touch Targets

- Minimum 44×44px tap target.
- 8px minimum spacing between targets.
- Buttons full-width on mobile (stretch to viewport edges with 16px margin).

### 12.2 Gestures

- Swipe right/left on list items: reveal actions (archive, delete).
- Pull down to refresh (with native-feeling spinner).
- Pinch to zoom on photos.
- Long-press for context menu (mobile equivalent of right-click).

### 12.3 Native iOS Patterns (via React Native Paper)

- Bottom sheet modals (not centered dialogs).
- Tab bar at bottom (not sidebar).
- Large title navigation bar (collapses on scroll).
- Swipe-back gesture (iOS edge swipe to go back).

### 12.4 Native Android Patterns

- Material Design 3 app bar.
- Floating Action Button (FAB) for primary action.
- Bottom sheet modals.
- Ripple effect on tap.
- Snackbars (not toasts) for transient messages.

---

## 13. Illustrations & Empty States

### 13.1 Illustration Style

- Custom illustrations (not stock): simple, warm, geometric.
- Used for: empty states, onboarding, error pages.
- Style: flat with soft shadows, muted palette, friendly characters (children, parents, teachers).
- commissioned from illustrator (one-time investment, ~$3-5k).

### 13.2 Empty State Examples

**No students yet:**
```
[Illustration: parent + child walking]
"No students yet"
"Add your first student to start tracking attendance, activities, and parent communication."
[Add Student]
```

**No messages:**
```
[Icon: chat bubble, large]
"No messages"
"Start a conversation with parents or staff."
```

**No activities today:**
```
[Icon: photo, large]
"No activities posted today"
"Share a moment from class to brighten a parent's day."
[Post Activity]
```

---

## 14. parent.app Baseline Comparison

### 14.1 Where parent.app Meets the Bar

We must match them on:
- Activity feed (chronological posts with photos/videos).
- Daily reports.
- Secure messaging.
- Attendance tracking with ratio compliance.
- Billing with online payments (Phase 3).
- Multi-centre management (Phase 3).
- Multi-language (their 198 auto-translation is impressive; we deliver EN+AR natively with proper RTL — which is what UAE market actually needs).
- AI-powered observation capture (Phase 2).
- Multi-language auto-translation (Phase 2 consideration).

### 14.2 Where We Exceed parent.app

| Area | parent.app | NMS |
|---|---|---|
| Design quality | Functional, dated enterprise feel | Modern 2026 aesthetic (Linear/Notion-grade) |
| RTL support | Likely partial/auto-translation crutch | Native Arabic-first, RTL as first-class, all components RTL-tested |
| Real-time updates | Periodic refresh implied | WebSocket/SignalR real-time (sub-5s updates) |
| Ratio enforcement | Tracked but alerts may be slow | Live WebSocket-driven, <30s breach → push |
| Dual deployment | SaaS only | On-prem + SaaS, same codebase |
| Audit trail | Implied but UI unclear | Append-only audit log with Super Admin filterable viewer |
| Emergency reports | Likely exists | One-click PDF + offline cache on mobile |
| UAE-specific depth | Generic | KHDA reporting, AED billing, UAE weekend awareness, emirates_id field |
| Parent UX | Adequate | Premium, mobile-first, magic-link login |
| Onboarding | "Under 2 weeks" claim | Guided wizard, first pilot user active in <1 hour |
| Custom branding | Per-branch | Per-tenant + white-label enterprise option |

### 14.3 Where We Learn From parent.app

- 198-language auto-translation is impressive UX (we should evaluate in Phase 2 for parents who don't speak EN/AR).
- "Built by Educators, for Educators" tone is good — we should adopt similar warmth.
- "Measurable ROI within 30 days" promise is good marketing — we should measure + publish similar metrics.
- Their support rating (4.8) suggests investment in customer support — we should match this from day 1.

---

## 15. Design System Governance

### 15.1 Storybook

All components documented in Storybook with:
- Visual examples (all variants, all sizes, all states).
- Props table (auto-generated from TypeScript types).
- Usage guidelines.
- Accessibility notes.
- RTL variant (every story renders in both LTR + RTL).

### 15.2 Design Tokens

All design tokens (colors, typography, spacing) defined as CSS variables in `globals.css` and mirrored in:
- `tailwind.config.ts` (for Tailwind utility classes).
- Figma library (for designer-developer sync).
- React Native theme (for mobile).

Single source of truth: `tailwind.config.ts`. Changes here propagate everywhere.

### 15.3 Design Review

Every PR that touches UI requires:
- Screenshots in both LTR + RTL.
- Storybook story for new/changed components.
- Accessibility check (axe in test).
- Visual regression test (Playwright + Percy/Chromatic).
- Design review by Tech Lead or designated design reviewer.

---

**Next file to read**: `15-NOTIFICATIONS.md` for the 3-tier notification system, channels, and fallback logic.
