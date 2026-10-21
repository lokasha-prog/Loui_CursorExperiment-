# Accessibility audit (WCAG 2.1 AA) — status 2026-10-21

**Method.** axe-core 4.10 run in a real browser on the live screens (rules: WCAG 2.0 A/AA and 2.1 A/AA), in English and in Arabic (right-to-left). Dev dependency: `axe-core` in `web/`.

**Screens covered (25 runs):** login, dashboard, students, feed, messages, attendance, calendar, billing, leave, newsletters, resources, forms, notifications, settings, reports (English and Arabic where listed).

**Findings and fixes**
| Finding | Where | Fix |
|---|---|---|
| Colour contrast 2.9:1 (white text on coral buttons) | all primary buttons and tabs | primary colour darkened to 4.5:1 or better (`--primary`) |
| Colour contrast 4.49:1 on helper text | sub-titles under page headings | `--muted-foreground` darkened |
| Colour contrast 3.9:1 on link-style buttons | text buttons in lists | `--primary-dark` darkened |

**Result after fixes:** 0 violations on all screens above, English and Arabic.

**How to repeat.** Start the web app, sign in, open the browser console and run:
```js
await new Promise((ok) => { const s = document.createElement('script'); s.src = '/node_modules/axe-core/axe.min.js'; s.onload = ok; document.head.appendChild(s) })
const run = async () => (await axe.run(document, { runOnly: ['wcag2a','wcag2aa','wcag21a','wcag21aa'] })).violations
for (const route of ['/dashboard','/students','/billing']) { history.pushState({}, '', route); dispatchEvent(new PopStateEvent('popstate')); await new Promise(r => setTimeout(r, 1500)); console.log(route, await run()) }
```

**Not covered by automation (needs a person)**: screen-reader reading order on the print previews, keyboard-only walk through the form builder drag steps, zoom to 200% on the phone app, VoiceOver/TalkBack on the phone apps. These are in UAT script H and the pilot checklist.
