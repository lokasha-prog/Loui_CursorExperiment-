# Nursery website

The public website of the nursery: a plain, fast, bilingual (English / Arabic, right-to-left) site with an enquiry form that goes straight into the NMS enquiry pipeline. No build step, no framework.

## Change it
- `config.js` is the only settings file: the centre's web name inside NMS (`centreSlug`), where the NMS server is (`apiBase`), optional phone, e-mail, address and `portalUrl`, the NMS web app sign-in page that the "Sign in" and "Parent sign-in" buttons open (parents, teachers and managers all sign in there; the website never handles passwords).
- The nursery name shown in the header and footer is read from NMS (Settings > centre profile), so it always matches the system.
- Wording lives in `app.js` (English and Arabic side by side). Look and feel is in `styles.css`.
- The programmes (names, ages, days, hours) and the "never more than N children per adult" badge come live from NMS (Centre structure > Programmes and Rooms), so they never go out of date. Nothing about fees is published.
- Optional in `config.js`: a notice bar, real parent comments, and real photos for the gallery. Comments and photos are left empty on purpose: add only real ones with permission (empty = the section is hidden / illustrated tiles stay).

## Run it here
```bash
node tools/serve-website.mjs        # http://localhost:5182 (forwards /api to the local NMS API on 5178)
```
Seed some website enquiries for testing: `powershell -File tools\load-website-enquiries.ps1`.

## Publish it
Put the folder's files on any web host (or a sub-domain such as `www.`). Two things are needed:
1. `/api/*` must reach the NMS server (reverse proxy), or set `apiBase` to the NMS address (then allow that site in the server's cross-site settings).
2. Send the security headers in the proxy: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and a content policy allowing only this site, Google Fonts and (if the captcha is on) Google reCAPTCHA.

## Protection against spam
A hidden field that people never see, a per-address rate limit on the server (a few sends a minute) and, when `Captcha:SiteKey` / `Captcha:SecretKey` are set in NMS, Google reCAPTCHA (the form shows it automatically).

## Checked
Accessibility scan (axe, WCAG 2.1 AA) clean in English and Arabic; works on phone widths.
