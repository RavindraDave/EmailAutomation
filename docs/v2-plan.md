# Email Automation v2 - Product & Engineering Plan

> **Status:** Draft for review. **Scope:** a ground-up rebuild of the desktop app on Electron + TypeScript, replacing the Avalonia UI while keeping v1 working until v2 reaches parity.
> Items marked **[Decision]** need an owner's call before the phase that depends on them starts. See [Open decisions](#18-open-decisions).

---

## Table of contents

1. [Vision & goals](#1-vision--goals)
2. [Who we are building for](#2-who-we-are-building-for)
3. [Scope: v2.0 vs later](#3-scope-v20-vs-later)
4. [UX principles & design system](#4-ux-principles--design-system)
5. [Key user journeys & screens](#5-key-user-journeys--screens)
6. [Architecture](#6-architecture)
7. [Data model & settings inheritance](#7-data-model--settings-inheritance)
8. [Feature specifications](#8-feature-specifications)
9. [Sending engine](#9-sending-engine)
10. [Security](#10-security)
11. [Privacy & compliance](#11-privacy--compliance)
12. [Code quality & engineering practices](#12-code-quality--engineering-practices)
13. [Testing strategy](#13-testing-strategy)
14. [CI/CD, packaging & releases](#14-cicd-packaging--releases)
15. [Observability, support & performance](#15-observability-support--performance)
16. [Migration from v1](#16-migration-from-v1)
17. [Roadmap & phases](#17-roadmap--phases)
18. [Open decisions](#18-open-decisions)
19. [Risks & mitigations](#19-risks--mitigations)
20. [Definition of Done](#20-definition-of-done)

---

## 1. Vision & goals

**Vision:** anyone who can use email and Excel can send professional, personalized emails to a list of people safely, confidently, and without help.

| Goal | How we'll measure it |
|---|---|
| **Easy for non-technical users** | In usability tests, 4 of 5 first-time users connect an account, design a template and send a test batch **without help in under 15 minutes**. |
| **Professional-looking emails** | Designer output renders correctly in Gmail (web/Android/iOS), Outlook (desktop/web), and Apple Mail, as checked against a test matrix every release. |
| **Safe by default** | No accidental double-sends. Always a test-send and confirmation step. No secrets stored in plain text. |
| **Runs everywhere** | Windows 10+, macOS 12+ (Intel and Apple Silicon), and mainstream Linux, all built from one codebase. |
| **Trustworthy engineering** | ≥ 85% test coverage on core logic, green CI on all 3 OSes, signed builds, automatic updates. |

**Non-goals for v2.0:** hosted/cloud service, marketing automation (drip campaigns, A/B tests), open/click tracking, team collaboration. Some of these may come later (see §3).

---

## 2. Who we are building for

| Persona | Description | What they need |
|---|---|---|
| **Office admin "Asha"** (primary) | Sends invoices, reminders and notices to 50-500 people from an Excel sheet. Comfortable with Word and Excel; doesn't know what SMTP is. | Step-by-step guidance, plain language, can't break anything, reassurance before sending. |
| **Small business owner "Rahul"** | Several brands or roles (sales@, support@, personal). Wants branded emails. | Multiple sender identities, brand kits, reusable templates. |
| **Power user "Priya"** | Already has HTML templates, wants control. | HTML import, code view, advanced settings behind a toggle. |

Design for **Asha first**. Priya's needs sit behind "Advanced" and must never complicate Asha's path.

---

## 3. Scope: v2.0 vs later

**v2.0 (MVP, parity with v1 plus the new core):**
- Guided first-run setup
- Multiple **Email Accounts** with provider presets (Gmail, Outlook.com/M365 via SMTP, Yahoo, Zoho, iCloud, Other) and a test email
- Multiple **Sender Profiles** (From name/address, reply-to, default CC/BCC, signature, brand kit)
- **Settings inheritance** (App → Account → Sender Profile → Template → Send)
- **Visual template designer** (drag-and-drop blocks, merge fields, conditional blocks, device preview, preview with real data, version history, starter gallery)
- **HTML import** and code view (advanced)
- **Send wizard** (recipients → template → review → send), with validation, test send, pause/resume/stop, crash-safe resume, and a daily limit per account
- **Suppression list** ("Do not email")
- History, per-recipient results, CSV export
- Light/dark theme, keyboard accessibility, English UI (ready for translation)
- Signed installers and auto-update on all 3 OSes
- Import of v1 data

**v2.x (next):**
- OAuth sign-in for Gmail and Microsoft 365 (no app passwords)
- Saved contact lists (if chosen, see [Decision D3](#18-open-decisions))
- Scheduled sends
- More languages (Hindi first?)
- Optional encrypted database
- Template sharing (export/import `.eatemplate` files)

**Later / maybe:** team workspaces, cloud sync, analytics, an optional hosted web version.

---

## 4. UX principles & design system

### 4.1 Principles (apply to every screen and PR)

1. **Plain language.** No jargon on the main path. "Connect your email", not "Configure SMTP". "Wait between emails: 2 seconds", not "DelayMs: 2000".
2. **One primary action per screen.** One visually dominant button; everything else is secondary.
3. **Progressive disclosure.** Technical fields live under **Advanced ▸**, collapsed by default and remembered per user.
4. **Guide, don't block.** Wizards with clear steps; each problem says *what's wrong*, *why it matters*, and has a **Fix it** action.
5. **Forgiving.** Autosave, undo/redo, version history, a confirmation step before anything irreversible (sending, deleting), and "Undo" toasts after soft deletes.
6. **Always show state.** Progress, what's happening now, what happens next, time remaining.
7. **Show where values come from.** Every inherited setting shows its source ("from *Office Gmail* account") and an **Override** toggle.
8. **Safe defaults.** Conservative sending limits, test-send first, escape merge values, never auto-resend uncertain emails.
9. **Consistency.** One design system and one wording glossary (see 4.4), with no one-off components.

### 4.2 Design system

- **Component library:** Mantine (accessible, themeable, and it includes Stepper, Notifications, Modals, Forms and Spotlight search) on top of **design tokens** (color, spacing, radius, typography, elevation, motion) defined once in `theme.ts`.
- **Themes:** light, dark and "follow system". High-contrast check on both.
- **Typography:** Inter (bundled, no network), base 15px, with a user text-size setting (90-130%).
- **Icons:** Tabler Icons (MIT, matches Mantine). Icons always come with a text label on navigation and primary actions.
- **Layout:** left sidebar navigation (Home, Send, Templates, Senders & Accounts, History, Settings, Help) that highlights the active page. Content max width ~1100px. Minimum window 1024×680.
- **Component inventory (built once, reused):** PageHeader, EmptyState (illustration + one action), StatusBadge, InheritedField (value + source + override), FieldHelp (ⓘ popover), ConfirmDialog, ProblemList (grouped, with Fix-it actions), ProgressPanel, FilePicker/DropZone, MergeFieldChip.
- **Storybook** hosts every shared component with light/dark and accessibility checks. It is the living design reference.

### 4.3 Accessibility (target: WCAG 2.2 AA)

- Everything works with the keyboard; visible focus rings; logical tab order; skip-to-content.
- Screen-reader labels on all controls; live regions announce send progress and errors.
- Color contrast ≥ 4.5:1 for text; never use color as the only signal (icons and text go with red/green).
- Respect OS "reduce motion"; no timing-based interactions.
- Automated axe checks in component and E2E tests, plus a manual screen-reader pass (NVDA on Windows, VoiceOver on macOS) every release.

### 4.4 Content & microcopy

- A **glossary** of fixed terms: *Email Account*, *Sender*, *Template*, *Recipients*, *Send* (not "Campaign/Batch/Job" in the UI).
- Error-message pattern: **what happened → why → what to do**, plus a button. Example: *"Gmail didn't accept the password. Gmail needs an 'app password' for apps like this one. [Show me how]"*
- All strings live in i18n resource files (i18next) from day one, even though only English ships in v2.0.
- Date, number and plural formatting via `Intl`.

### 4.5 Usability validation

- **Clickable prototype** (Figma or a Storybook-based flow) tested with 3-5 target users during Phase 1, **before** the designer and send wizard are built.
- A usability round at the end of Phases 3, 4 and 5 with 5 users each. Track task success, time on task and error count, plus the SUS questionnaire (target ≥ 80).
- Findings logged as issues labelled `ux-research`.

---

## 5. Key user journeys & screens

### 5.1 First run (setup wizard)
1. **Welcome.** One sentence on what the app does, and privacy reassurance ("everything stays on this computer").
2. **Which email do you use?** Big provider tiles (Gmail, Outlook, Yahoo, Zoho, iCloud, Other).
3. **Connect.** Provider-specific instructions with screenshots (e.g. creating a Gmail app password), with the host/port filled in automatically from the preset.
4. **Who are you?** Display name, plus an optional logo and brand color (this creates the first Sender Profile and Brand Kit).
5. **Send yourself a test.** One click, then "Did it arrive? Yes / No, help me".
6. **Done.** Go to Home, where a checklist appears: ✓ Connect email, ☐ Make your first template, ☐ Send your first emails.

The wizard can be skipped and re-opened any time from Settings or the Home checklist.

### 5.2 Home
Checklist (until complete) · big **Send emails** button · recent sends with status and a "Send again" action · "Emails left today" per account · template shortcuts.

### 5.3 Send wizard (the core flow)
Stepper: **1. Recipients → 2. Template & Sender → 3. Check → 4. Send**

1. **Recipients.** Drop an Excel/CSV file or browse. A download link for the sample spreadsheet. A table preview of the first 20 rows. Pick the sheet if the file has several. Map columns (auto-detects "To/Email/E-mail"). Summary: "248 people, 3 rows skipped (disabled)".
2. **Template & Sender.** Template gallery cards (thumbnail + name). Sender defaults to the template's sender, which can be changed for this send only. Subject line with merge fields.
3. **Check.** Automatic validation grouped by severity:
   - ⛔ **Must fix:** invalid addresses, missing required fields, missing attachments, daily limit exceeded.
   - ⚠️ **Worth checking:** empty merge values, duplicate recipients, people on the "Do not email" list (excluded automatically), attachments outside the usual folders.
   - Each issue can be expanded to see the affected rows and has a **Fix it** action (skip rows, change the mapping, open the file).
   - **Preview panel:** step through recipients (◀ 7 of 248 ▶) on desktop or phone view.
   - **Send a test to me** (required unless the user explicitly skips it).
4. **Send.** Confirmation: "Send 245 emails from *Office Gmail* as *Asha (Accounts)*? Estimated time: 9 minutes." Then a live progress panel (sent/failed/remaining, current recipient, ETA) with Pause / Resume / Stop. Safe to minimize; there's an OS notification when done.
   - **Result screen:** summary, "Retry failed (3)", "Export report", "View details".

### 5.4 Templates
Grid of cards with thumbnails, search and categories, and a "New from gallery". Opening a card shows the designer (§8.3).

### 5.5 Senders & Accounts
Two tabs: **Email Accounts** (connection, limits, status and "last tested") and **Senders** (identity, signature, brand kit, linked account). Each item has an edit drawer and a "Used by N templates" indicator. Deleting something that's in use explains the impact and offers to reassign.

### 5.6 History
List of sends (date, template, sender, counts, status). Detail view: per-recipient table with filter, search and error reasons in plain language. Buttons: Export CSV, Retry failed, Duplicate as new send.

### 5.7 Settings
General (theme, text size, language, confirm-before-send), Sending defaults (App-level inherited values), Data (backup/restore, history retention, open data folder, clear history), Privacy (crash reports opt-in), Updates (channel, check now), About/diagnostics.

### 5.8 Help
Searchable in-app help articles (bundled Markdown), contextual ⓘ links from every screen to the relevant article, "Re-run setup wizard", "Export diagnostics", and a link to the online guide.

---

## 6. Architecture

### At a glance

```
┌─────────────────────────────── Electron app ───────────────────────────────┐
│                                                                            │
│  Renderer (sandboxed, no Node)          Preload (contextBridge)            │
│  React + Mantine + TanStack Query  ──►  window.api.* (typed, minimal) ─┐   │
│  GrapesJS designer (in-page)                                           │   │
│  Preview <iframe sandbox> (no scripts)                                 │   │
│                                                                        ▼   │
│  Main process (Node)                                                       │
│   ├─ IPC router: zod-validate every request → call use case               │
│   ├─ core (pure TS): settings resolution, validation, merge, rules         │
│   ├─ services: TemplateCompiler (MJML), Excel/CSV reader, Secrets,         │
│   │            Repository (SQLite), Updater, Logger                        │
│   └─ spawns ──► Utility process: SendingEngine                             │
│                  (queue, throttle, retries, Nodemailer / Gmail API)        │
│                                                                            │
│  Storage: SQLite (app data dir) · secrets in OS keychain via safeStorage   │
└────────────────────────────────────────────────────────────────────────────┘
```

### 6.1 Technology choices

| Concern | Choice | Notes |
|---|---|---|
| Shell | **Electron** (latest stable, upgraded within 4 weeks of each release) | Bundled Chromium gives identical rendering on every OS. |
| Build tooling | **electron-vite** + Vite | Fast dev server, HMR, separate main/preload/renderer builds. |
| Language | **TypeScript** (`strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`) | |
| UI | **React 19** + **Mantine** + Tabler Icons | |
| Server state | **TanStack Query** over IPC | Caching, loading/error states, invalidation. |
| UI state | **Zustand** (small, local stores) | Only where React state isn't enough. |
| Routing | React Router (hash/memory router) | |
| Forms & validation | Mantine Form + **zod** | The same zod schemas are reused at the IPC boundary. |
| Email designer | **GrapesJS + grapesjs-mjml** | Phase 0 spike also evaluates **EmailBuilder.js** (simpler, MIT); choose one ([D6](#18-open-decisions)). |
| Email HTML | **mjml** (official) in the main process | Also generates plain text (html-to-text). |
| Merge fields | **LiquidJS** (escape output by default, strict filters, no code execution) | Supports `{% if %}` for conditional blocks. |
| HTML sanitizing | **sanitize-html** (main) / **DOMPurify** (renderer) | Strips scripts, event handlers and `javascript:` URLs from imported HTML. |
| Excel/CSV | **ExcelJS** + **Papa Parse** | Streaming for large files. |
| Database | **SQLite** via **better-sqlite3** + **Kysely** (type-safe query builder + migrator) | WAL mode, foreign keys on. |
| Sending | **Nodemailer** (SMTP), **googleapis** (Gmail API), **@azure/msal-node** (v2.x) | |
| Secrets | Electron **safeStorage** | Keychain / DPAPI / libsecret. |
| Logging | **electron-log** + redaction layer | Rotating files in the app data folder. |
| i18n | **i18next** + react-i18next | |
| Packaging/updates | **electron-builder** + **electron-updater** (GitHub Releases) | |
| Package manager | **pnpm** (workspaces, strict, lockfile) | |

Every major choice gets a short **Architecture Decision Record** in `docs/adr/` (context → decision → consequences).

### 6.2 Process model & responsibilities

- **Renderer:** presentation only. No Node, no filesystem, no network except the app's own bundle. Talks to main only through `window.api`.
- **Preload:** exposes a small, typed API through `contextBridge`. No business logic.
- **Main:** owns the database, secrets, files, template compilation and orchestration. Every IPC request is validated with zod and authorized. Is the requested file one the user picked in a dialog? Does the entity exist?
- **Utility process (sending engine):** long-running sends run in `utilityProcess` so the UI never freezes and a crash can't corrupt UI state. It communicates with main over a typed message port.

### 6.3 Repository layout (pnpm workspace)

```
apps/desktop/
  src/main/          # app lifecycle, windows, IPC router, services wiring
  src/preload/       # contextBridge API
  src/renderer/      # React app: routes/, features/<feature>/, components/, theme/
  src/sending/       # utility-process entry for the sending engine
  resources/         # icons, bundled help, sample spreadsheet
  e2e/               # Playwright tests
packages/
  core/              # PURE TypeScript domain + use cases (no Electron, no fs): entities,
                     # settings resolution, validation rules, merge rendering, send planning
  contracts/         # IPC channel names + zod schemas + inferred types (shared main/preload/renderer)
  db/                # Kysely schema, migrations, repositories
  email/             # MJML compile, text version, transport adapters (SMTP, Gmail API)
  ui/                # shared React components + Storybook
docs/
  adr/  v2-plan.md  user-guide/
legacy/              # (optional) v1 .NET solution moved here once v2 ships, or kept on a branch
```

Organize the renderer **by feature** (`features/templates`, `features/send`, `features/accounts`…), each with its components, hooks, queries and tests together.

### 6.4 Architectural rules (enforced with lint)

- `packages/core` imports nothing from Electron, Node `fs`/`net`, React, or the DB. It must run in plain Node and in tests with no mocks of the platform.
- The renderer may import only `contracts`, `ui` and its own code, never `db`, `email` or `main`.
- Dependencies point inward (UI → contracts → core; infrastructure implements core interfaces).
- Enforced with `eslint-plugin-boundaries` (or dependency-cruiser) in CI.

### 6.5 IPC contract

- One file per feature in `packages/contracts`, e.g.
  `templates.save: { input: SaveTemplateInput (zod), output: Template }`.
- Channel names are constants; no stringly-typed `ipcRenderer.invoke('whatever')` in the app code.
- Errors cross the boundary as typed `AppError { code, messageKey, details? }`. The renderer maps `messageKey` to friendly, translated text. Stack traces never reach the UI.
- Events from main to renderer (send progress) use a typed subscription API with unsubscribe.

---

## 7. Data model & settings inheritance

### 7.1 Tables (SQLite)

| Table | Key columns | Notes |
|---|---|---|
| `app_settings` | key, value_json | App-level defaults (delay, daily cap, theme, retention…). |
| `email_accounts` | id, name, provider_preset, auth_type (`password`/`oauth`), host, port, security (`starttls`/`tls`), username, secret_ref, daily_cap?, delay_ms?, last_tested_at, last_test_ok, created_at, updated_at | The secret itself lives in the keychain-backed secrets store; `secret_ref` points to it. `NULL` limits mean "inherit". |
| `brand_kits` | id, name, logo_asset_id, primary_color, secondary_color, font_family, footer_html | |
| `sender_profiles` | id, name, email_account_id (FK), from_name, from_address, reply_to?, default_cc?, default_bcc?, signature_json, brand_kit_id?, delay_ms?, daily_cap? | `from_address` is validated against the account (warn if the provider may reject it). |
| `templates` | id, name, category, subject, design_json, design_schema_version, compiled_html, compiled_text, default_sender_profile_id?, required_fields_json, thumbnail_asset_id, created_at, updated_at, deleted_at | Soft delete. `design_json` is the source of truth; the compiled output is a cache. |
| `template_versions` | id, template_id, version_no, design_json, subject, created_at, note | Snapshot kept on each manual save; keeps the last 50. |
| `template_attachments` | template_id, asset_id | Default attachments. |
| `assets` | id, sha256, mime, size, bytes (or file path), created_at | Images/logos, de-duplicated by hash. Embedded in emails as CID. |
| `sends` | id, template_id, template_version_id, sender_profile_id, email_account_id, resolved_settings_json, source_file_name, source_file_sha256, column_mapping_json, status, created_at, started_at, finished_at | `resolved_settings_json` freezes exactly what was used. |
| `send_recipients` | id, send_id, row_no, to, cc, bcc, subject_rendered?, data_json, status (`pending`/`sending`/`sent`/`failed`/`skipped`/`uncertain`), attempts, message_id?, error_code?, error_message?, updated_at | The snapshot of source data makes resume and history exact. |
| `suppression_list` | email (unique, lowercase), reason, added_at | "Do not email". Checked on every send. |
| `daily_usage` | email_account_id, date_utc, count | Enforces daily caps across all sends. |
| `schema_migrations` | (managed by Kysely migrator) | |

Indexes on foreign keys, `send_recipients(send_id, status)`, and `suppression_list(email)`.

### 7.2 Settings inheritance

Resolution order (first defined value wins, from most to least specific):

```
Send override → Template → Sender Profile → Email Account → App defaults
```

| Setting | App | Account | Sender | Template | Send |
|---|:-:|:-:|:-:|:-:|:-:|
| Delay between emails | ✓ | ✓ | ✓ | | ✓ |
| Daily limit | ✓ | ✓ (hard cap) | | | |
| Sender profile | | | | ✓ (default) | ✓ |
| From name / reply-to / CC / BCC | | | ✓ | | ✓ |
| Signature / brand kit | | | ✓ | ✓ (override) | |
| Attachments | | | | ✓ | ✓ (add) |
| Require test send | ✓ | | | | ✓ (skip with confirm) |

- Implemented as one pure function in `core`: `resolveSettings(layers) → { value, source }` for each key. Property-based tests cover it.
- The UI's `InheritedField` component shows the value and its source and has an override toggle.
- A **daily limit** is a hard ceiling: the stricter of the account cap and the provider's known limit always wins, and overrides can only go lower.

### 7.3 Migrations & data safety

- Versioned, forward-only migrations (Kysely migrator), each tested against a fixture of the previous schema.
- **Automatic backup** of the DB file before every migration and daily (keeps the last 7), plus manual **Backup / Restore** in Settings. Backups exclude secrets.
- `PRAGMA integrity_check` on startup after a crash; offer restore from backup if it fails.
- Template `design_json` carries `design_schema_version`, and a design migrator upgrades old designs when they're opened.

---

## 8. Feature specifications

### 8.1 Email Accounts
- **Provider presets** (JSON in resources): host, port, security, known daily limit, help article, app-password instructions, and "requires app password" flag.
- **Add account flow:** choose provider → credentials → **Test** (connect + authenticate, optional test email) → save only after a successful test (or explicit "Save anyway").
- **Status:** ✓ Working / ⚠ Not tested recently / ✗ Failed, with the last error in plain language.
- **Error translation table:** map SMTP codes and Nodemailer error codes (`EAUTH`, `ECONNECTION`, `ETIMEDOUT`, 535, 534, 550, 552, 421…) to friendly messages and help links.
- **OAuth (v2.x):** system browser + loopback redirect + PKCE; tokens stored via safeStorage; refresh handled in main.

### 8.2 Sender Profiles & Brand Kits
- A profile links to exactly one account; one account can serve many profiles.
- Signature editor uses the same block editor (a reduced block set).
- A brand kit applies to templates as theme variables (colors/fonts/logo). Changing the kit updates all templates that use it (with a preview of affected templates).

### 8.3 Template designer
- **Layout:** left panel = blocks and layers; center = canvas (desktop/phone toggle, zoom); right panel = properties of the selected block; top bar = name, subject, sender, **Preview**, **Send test**, **Save**, status ("Saved ✓ 2s ago").
- **Blocks:** Section/Columns (1-4), Heading, Text (rich text: bold, italic, underline, links, lists, alignment, color), Image (upload, alt text required, link), Button, Divider, Spacer, Table (rows/cols, header, zebra), Logo header, Footer (with address and optional unsubscribe line), Signature, Social icons, Personalization field, Custom HTML (advanced; sanitized).
- **Merge fields:** "Insert field ▾" lists the columns from the last-used spreadsheet (or ones declared manually). Fields appear as chips in text; fallback value is supported (`{{ FirstName | default: "there" }}`). Unknown fields are highlighted.
- **Conditional blocks:** "Show this block only if [field] [is not empty / equals …]". Compiles to a Liquid `{% if %}`.
- **Preview:** desktop / phone / dark-mode simulation, and **preview with real data** by stepping through spreadsheet rows. Rendered in a sandboxed iframe with no scripts.
- **Quality checks (live "Checklist" panel):** missing alt text, images too large (>1MB), missing subject, subject >78 chars, unmatched merge fields, too-small text, low-contrast colors, broken links (format check), Gmail's ~102KB clipping limit.
- **Safety:** autosave (debounced, stored as a draft), undo/redo, **version history** with restore, and a leave-page guard.
- **Import HTML:** Advanced option. Sanitized, then imported as a Custom HTML block (or parsed into blocks where possible). **Code view** is read-only by default, editable in Advanced.
- **Starter gallery:** 8-10 professionally designed, tested templates (Invoice, Payment reminder, Announcement, Newsletter, Event invite, Thank you, Welcome, Plain letter).

### 8.4 Recipients (data sources)
- Excel (.xlsx) and CSV (UTF-8/UTF-16, auto-detect delimiter). Choose the sheet; header row detection.
- Column mapping with auto-detection (To/Email/E-mail/Mail, Cc, Bcc, Subject, Attachment, Enabled) and a remembered mapping per template.
- Multiple addresses per cell (`;` or `,`) supported, as in v1.1.1.
- Data is **snapshotted** into `send_recipients` when the send is created, so later file edits don't affect a running or resumed send.
- Row limits: warn above 2,000, hard block above the remaining daily limit (offer "send the first N today, the rest tomorrow").

### 8.5 History & reports
- Per-send and per-recipient views, a filter by status, plain-language error reasons.
- **CSV export** with formula-injection protection (§10.4). Optional XLSX export.
- **Retry failed** creates a new send with only the failed recipients (never re-sends successful ones).
- **Retention setting:** keep recipient-level data for 30/90/365 days/forever (default 90); after that, keep only aggregate counts.

---

## 9. Sending engine

Runs in an Electron **utility process**. Design goals: never double-send, never freeze the UI, respect provider limits, and survive crashes.

- **State machine per recipient:** `pending → sending → sent | failed | skipped`. Before contacting the server, the row is marked `sending` and committed. After a success, it's `sent` with the provider's `message_id`.
- **Crash safety:** on restart, any row left in `sending` becomes **`uncertain`**. These are **never auto-resent**; the user is asked ("3 emails may or may not have been sent. Send them again / Skip them").
- **Resume:** a paused or interrupted send continues from `pending` rows only.
- **Throttling:** per-account token bucket (delay from resolved settings) plus `daily_usage` enforcement across all sends (rolling per-day count). Reaching the cap auto-pauses with a clear message and a "continue tomorrow" option.
- **Retries:** only for transient failures (network errors, timeouts, SMTP 4xx such as 421/450/451/452), with exponential backoff and jitter, max 3 attempts. Permanent failures (5xx: bad address, rejected) fail immediately with a friendly reason. A circuit breaker pauses the send after N consecutive connection or auth failures ("Your email account stopped accepting logins. Check your password.").
- **Connection reuse:** pooled SMTP connection per account; reconnect on drop.
- **Message building:** Liquid render (escaped) → MJML compile (cached per template version) → inline CID images → attachments → plain-text part → headers (Message-ID, Date, optional `List-Unsubscribe`). Line breaks are rejected in any header value coming from data.
- **Pause/Stop semantics:** Pause takes effect after the in-flight email; Stop does the same and marks the send `stopped` (resumable).
- **Progress events:** throttled to ≤ 4 per second to the UI, with counts, current recipient and ETA.
- **Background behaviour:** keeps running when the window is minimized; OS notification when done; on quit during a send, a confirm dialog ("Stop after the current email and quit?").
- **Sleep/wake:** listen for `powerMonitor` suspend/resume; pause on suspend, reconnect on resume.

---

## 10. Security

### 10.1 Threat model (summary)

| Asset | Threats | Primary controls |
|---|---|---|
| Email account secrets | Theft from disk, logs, memory dumps, or a compromised renderer | safeStorage, never in the renderer, redacted logs, short-lived plaintext in main only |
| User's machine | Code execution via malicious template HTML or files | Sandboxed renderer, CSP, sanitization, no Node in the renderer, sandboxed preview iframe |
| Recipient data (PII) | Disclosure from a lost device or exported reports | OS disk encryption guidance, retention limits, optional DB encryption (v2.x), CSV hardening |
| User's reputation / account | Accidental mass-send, double-send, sending sensitive attachments | Confirmation, test send, idempotent engine, attachment review, suppression list, daily caps |
| Update channel | Malicious update | Signed builds, electron-updater signature verification, HTTPS only |
| Build pipeline | Supply-chain compromise | Pinned deps, lockfile, audits, least-privilege CI, SBOM, provenance |

A fuller STRIDE threat model lives in `docs/security/threat-model.md` and is reviewed each phase.

### 10.2 Electron hardening checklist (all required, verified by tests)

- `contextIsolation: true`, `sandbox: true`, `nodeIntegration: false`, `webSecurity: true`, no `enableRemoteModule`, `allowRunningInsecureContent: false`.
- Strict **CSP**: `default-src 'self'; script-src 'self'; object-src 'none'; frame-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'` (inline styles needed by the editor). Refine during the Phase 0 spike.
- The app loads only from a custom `app://` protocol or `file://` bundle, **never remote URLs**.
- `will-navigate` blocked; `setWindowOpenHandler` denies everything, and http(s) links open in the default browser via `shell.openExternal` after validation.
- Deny all `session.setPermissionRequestHandler` requests (camera, mic, notifications through the web API, etc.).
- **Electron Fuses:** RunAsNode off, NodeOptions off, NodeCliInspect off, EnableEmbeddedAsarIntegrityValidation on, OnlyLoadAppFromAsar on, CookieEncryption on.
- Preload exposes only named functions; **no generic `invoke(channel, …)` passthrough**.
- Every IPC handler validates the sender frame (`event.senderFrame.url` is our app) and the input (zod).
- File access: the renderer never passes arbitrary paths. Main hands out **opaque tokens** for files the user selected in a native dialog or dropped, and only those are readable.
- Template previews render in `<iframe sandbox>` (no `allow-scripts`, no `allow-same-origin`) using `srcdoc`.
- Electron kept on a supported version (the latest 3 majors); a Renovate/Dependabot PR on every release.

### 10.3 Secrets
- Stored via `safeStorage.encryptString`. The encrypted blob is kept in SQLite; decryption happens only in main or the utility process, just in time.
- **Linux:** if `safeStorage.getSelectedStorageBackend()` is `basic_text`, show a clear warning and offer "don't remember password" (ask each session).
- Secrets are never sent to the renderer (the UI shows "•••• saved" and "Replace").
- Logger redaction: passwords, tokens, auth headers and email bodies; recipient addresses are partially masked in logs (`a***@gmail.com`).

### 10.4 Email-specific safety
- **Merge values are HTML-escaped by default**; raw insertion only via an explicit, advanced "raw" filter.
- **Header injection:** reject CR/LF in To/Cc/Bcc/Subject/Reply-To from data; validate addresses with a strict parser.
- **Attachments:** the review step lists every attachment; files outside user-approved folders are flagged; sensitive patterns (`.ssh`, `.pem`, `.key`, keychain/browser profile paths) are blocked unless explicitly allowed; max size per email configurable.
- **Imported HTML:** sanitized (scripts, event handlers, `javascript:` URLs, forms, iframes, objects removed).
- **TLS:** STARTTLS or TLS required; `rejectUnauthorized` always true; no "insecure" option in the UI.
- **CSV exports:** cells starting with `= + - @ \t \r` are prefixed with `'` to prevent formula injection.

### 10.5 Supply chain & build
- pnpm with a committed lockfile, `pnpm install --frozen-lockfile` in CI, `onlyBuiltDependencies` allowlist (install scripts blocked by default).
- Minimal dependencies. A new dependency requires a PR note (why, maintenance status, license, size).
- Automated: Dependabot/Renovate (grouped weekly), `pnpm audit` gate (high/critical), **GitHub CodeQL**, **dependency-review** action, secret scanning with push protection, and **license check** (allowlist: MIT, BSD, Apache-2.0, ISC, MPL-2.0).
- CI uses least-privilege `GITHUB_TOKEN` permissions and pinned action SHAs. Signing secrets live only in the protected `release` environment with required reviewers.
- **SBOM** (CycloneDX) and build provenance attestation attached to each release.

### 10.6 Security process
- `SECURITY.md` with a reporting address and supported versions.
- Security review checklist in the PR template for changes touching IPC, file access, sending, or secrets.
- Periodic review: the Electron security checklist plus a run of **Electronegativity** in CI.
- A third-party or community security review before the public v2.0 release (recommended).

---

## 11. Privacy & compliance

- **Local-first:** no data leaves the machine except the emails the user sends and (opt-in) crash reports.
- **No telemetry by default.** Crash reporting (e.g. Sentry) is **opt-in**, with PII scrubbing and no email content or addresses.
- **Data retention** controls and **Clear all history** (§8.5); **Delete everything** in Settings (DB, secrets, logs).
- **Anti-spam guidance:** the app is for sending to people who expect the email. An optional footer block has an unsubscribe line (mailto) and an optional `List-Unsubscribe` header; the suppression list is honoured automatically; a friendly notice about CAN-SPAM / GDPR / India's DPDP Act links to a help article. This is not legal advice, only guidance.
- A privacy policy page is bundled and in the README.

---

## 12. Code quality & engineering practices

- **TypeScript strict** everywhere; `any` is banned by lint (except in typed adapters with a comment).
- **ESLint** (typescript-eslint strict + stylistic, react, react-hooks, jsx-a11y, import, boundaries, security) and **Prettier**; `lint-staged` + `husky` pre-commit.
- **Conventional Commits**; **Changesets** for versioning and changelog.
- **Branching:** trunk-based on `main`, short-lived feature branches, PRs required, squash merge, branch protection (CI green + 1 review + up-to-date).
- **PR template:** what/why, screenshots or GIF for UI, test evidence, security checklist, accessibility checklist, docs updated.
- **CODEOWNERS** for `packages/core`, `contracts`, the security-sensitive main-process code, and CI.
- **Error handling:** typed `Result`/`AppError` in core; no swallowed errors; every user-facing error has a message key.
- **Documentation:** TSDoc on public core APIs; ADRs for decisions; `CONTRIBUTING.md` (setup, scripts, conventions); `docs/architecture.md` kept current.
- **Definition of Ready** for issues: user story, acceptance criteria, UX (design/wireframe) for UI work, test notes.
- **Code review focus:** correctness, security, accessibility, test adequacy, naming and readability. Small PRs (< ~400 lines changed where practical).
- **Tooling:** Node LTS pinned via `.nvmrc`/`packageManager`; EditorConfig; VS Code recommended extensions and debug configurations for main and renderer.

---

## 13. Testing strategy

### 13.1 Test pyramid

| Level | Tools | What's covered | Target |
|---|---|---|---|
| **Unit** | Vitest | `core`: settings resolution, validation rules, merge rendering, send planning, error translation, CSV escaping; utility functions | **≥ 90% lines/branches** on `core`; property-based tests (fast-check) for settings resolution, address parsing, CSV escaping |
| **Component** | Vitest + Testing Library + axe | Every shared component and feature screen: states (loading/empty/error/success), keyboard interaction, a11y violations = 0 | ≥ 80% on `ui` and renderer features |
| **Integration** | Vitest in Node | `db` (real SQLite in a temp dir, migrations up from every past version), `email` (MJML compile, **fake SMTP server** via `smtp-server` or Mailpit in Docker: TLS, auth failure, 4xx/5xx, dropped connection), Excel/CSV parsing fixtures, IPC handlers with zod validation | Every repository, every transport path, every IPC channel |
| **Sending engine** | Vitest + fake transport + fake clock | Throttle timing, daily caps, retries/backoff, circuit breaker, pause/resume/stop, **crash mid-send → `uncertain` rows**, no double-send (invariant tests) | 100% of state transitions |
| **E2E** | Playwright (`_electron`) | Journeys: first-run wizard, add account (against Mailpit), create template from gallery, send wizard end-to-end, pause/resume, resume after forced kill, history export, v1 import | Runs on Windows, macOS and Linux in CI |
| **Visual regression** | Playwright screenshots (Storybook + key screens), light and dark | Unintended UI changes | Key screens and all shared components |
| **Email rendering** | MJML output snapshots + HTML validation + size checks; **manual matrix** (Gmail web/iOS/Android, Outlook 365/desktop, Apple Mail) per release, optionally Litmus/Email on Acid | Starter templates and block types render correctly | Per release |
| **Security tests** | Custom tests + Electronegativity | Fuses and webPreferences asserted, CSP present, `window.open`/navigation blocked, IPC rejects invalid input and foreign frames, preview iframe cannot run scripts, sanitizer strips payloads (OWASP XSS cheat-sheet vectors), header injection rejected, CSV injection escaped | All must pass to release |
| **Performance** | Scripted benchmarks | Startup time, 10k-row import, 5k-recipient send with fake transport (memory flat, UI responsive) | Budgets in §15.3 |
| **Usability** | Moderated sessions | Task success on the core journeys | §4.5 |

### 13.2 Practices
- Tests live next to code (`*.test.ts[x]`); E2E in `apps/desktop/e2e`.
- **Test data builders** and fixtures (`packages/core/test/builders.ts`), including sample spreadsheets (edge cases: empty rows, unicode names, multiple addresses, formulas, merged cells, 10k rows).
- No network in unit or integration tests; Mailpit only in the integration/E2E jobs.
- Flaky-test policy: a flaky test is a bug. Fix or revert within 2 days; **never** skip to get green.
- Coverage gates in CI (per package) and a coverage report posted on PRs.
- **Mutation testing** (Stryker) on `packages/core` monthly to check test strength (target score ≥ 70%).

---

## 14. CI/CD, packaging & releases

### 14.1 CI (GitHub Actions)
- **On every PR:** install (frozen) → lint → typecheck → unit + component + integration (with coverage gates) → build → E2E on a **Windows / macOS / Linux matrix** → Storybook visual tests → CodeQL → dependency review → license check → bundle size report.
- **Nightly:** full E2E + performance benchmarks + mutation tests (weekly) + `pnpm audit`.
- Caching for pnpm store and Electron binaries; artifacts (installers) uploaded for every PR for manual testing.

### 14.2 Packaging

| OS | Formats | Signing |
|---|---|---|
| Windows | NSIS installer (per-user, no admin) + portable zip | Code-signing certificate (OV, or Azure Trusted Signing) **[D4]**. Unsigned builds trigger SmartScreen warnings. |
| macOS | DMG + ZIP, universal or separate arm64/x64 | Apple Developer ID + **notarization** (hardened runtime, entitlements minimal) **[D4]** |
| Linux | AppImage + .deb (+ .rpm optional) **[D1]** | GPG-signed checksums |

### 14.3 Releases & updates
- **Channels:** `beta` (opt-in in Settings) and `stable`.
- **electron-updater** via GitHub Releases: background download, "Restart to update" prompt, never interrupting an active send. Staged rollout (e.g. 20% → 100%) for stable.
- **Versioning:** SemVer; Changesets generate the changelog; release notes are written in plain language for users.
- **Release checklist:** CI green on all OSes, manual smoke test on each OS, email rendering matrix, a11y spot check, security tests, migration test from the previous 2 versions, rollback plan (previous release stays available).

---

## 15. Observability, support & performance

### 15.1 Logging
- Structured JSON logs (electron-log), rotating (5 × 5MB), in the app data folder; levels configurable in Advanced.
- Redaction layer (§10.3) with unit tests.
- Correlation id per send, so a whole send's log lines can be followed.

### 15.2 Support
- **Help → Export diagnostics:** a zip of redacted logs, app/OS versions, settings (no secrets) and a DB schema version. The user reviews it before sharing.
- "Something went wrong" screens (React error boundaries) with Reload and Export diagnostics.
- Optional, opt-in crash reporting (§11).

### 15.3 Performance budgets

| Metric | Budget |
|---|---|
| Cold start to interactive | < 3s on a mid-range laptop |
| Navigation between screens | < 150ms |
| Designer opens an existing template | < 1s |
| Preview re-render on change | < 300ms |
| Import 10k-row spreadsheet | < 5s, UI stays responsive |
| Memory during a 5k-recipient send | Flat (no growth per recipient); < 400MB total |
| Installer size | < 150MB |

---

## 16. Migration from v1

- On first launch, v2 detects the v1 data folder (`%APPDATA%\EmailAutomation`, `~/Library/Application Support/EmailAutomation`) and offers **Import from previous version**:
  - v1 settings → one Email Account + one Sender Profile (the password is re-entered, because v1's DPAPI/Keychain blobs are app-specific; the import is verified with a test).
  - v1 templates (HTML bodies) → templates with a single Custom HTML block (`{{Placeholder}}` syntax is compatible with Liquid).
  - v1 batch history → read-only History entries.
- v1 data is **never modified** (read-only import), so users can keep v1 during the transition.
- **v1 maintenance track (in parallel):** ship a v1.1.2 fixing the attachment-path and CSV-injection issues identified during planning. After v2.0 stable plus ~3 months, v1 becomes unsupported and the .NET code moves to a `legacy/` folder or a `v1` branch.

---

## 17. Roadmap & phases

Estimates assume one developer working with AI assistance; they're rough and get revisited at each phase exit.

| Phase | Duration (est.) | Deliverables | Exit criteria |
|---|---|---|---|
| **0. Foundations & spike** | 2 weeks | Monorepo, tooling (TS strict, lint, Prettier, Vitest, Playwright, Storybook), CI matrix, Electron hardening baseline + security tests, unsigned installers on 3 OSes. **Spikes:** GrapesJS+MJML vs EmailBuilder.js in the sandboxed renderer; MJML compile in main; Nodemailer test email against Mailpit and real Gmail; safeStorage on each OS; auto-update to a test release. ADRs 001-006. | Spike report + chosen editor ([D6]); CI green on 3 OSes; hardening tests pass |
| **1. Core & data** | 2-3 weeks | `core` domain + settings resolution, DB schema + migrations + backups, repositories, IPC contract layer, app shell (sidebar, routing, theme, i18n, error boundaries), design system components in Storybook, UX prototype tested with users | Core ≥ 90% coverage; prototype usability round done |
| **2. Accounts & senders** | 2 weeks | Provider presets, add/test account, secrets, error translation, sender profiles, brand kits, inherited-field UI, first-run wizard (accounts part) | E2E: first-run → test email via Mailpit on 3 OSes |
| **3. Template designer** | 4-5 weeks | Designer with all v2.0 blocks, merge fields, conditionals, preview with data, checklist panel, versions, autosave, HTML import, starter gallery (8-10 templates), rendering test matrix | Rendering matrix passes; usability round 2 |
| **4. Send wizard & engine** | 3-4 weeks | Recipients import & mapping, validation + Fix-it, test send, sending engine in utility process (throttle, caps, retries, crash safety), progress UI, notifications, suppression list | Engine invariant tests pass; E2E send/pause/resume/kill-resume on 3 OSes; usability round 3 |
| **5. History, polish & help** | 2-3 weeks | History & reports, retry failed, retention, backup/restore, v1 import, in-app help, diagnostics export, accessibility pass (NVDA/VoiceOver), performance budgets met | WCAG AA audit clean; budgets met |
| **6. Release hardening** | 2 weeks | Code signing + notarization, auto-update channels, security review, SBOM/provenance, user guide + screenshots, beta program (5-10 real users) | Beta feedback triaged; no open high/critical issues |
| **v2.0 stable** | - | Public release | Release checklist (§14.3) complete |

**Total:** roughly 19-23 weeks to v2.0 stable. Phases 2 and 3 can overlap once Phase 1 is done.

**Milestone demos** at the end of each phase: a short recorded walk-through plus the installers from CI.

---

## 18. Open decisions

| # | Decision | Options | Recommendation | Needed by |
|---|---|---|---|---|
| D1 | Linux formats | AppImage only / + .deb / + .rpm | AppImage + .deb | Phase 6 |
| D2 | Providers in v2.0 | SMTP presets only / + Gmail OAuth / + Microsoft 365 OAuth | SMTP presets in v2.0, OAuth in v2.1 (Microsoft needs an Azure app registration; Google needs OAuth consent verification for public distribution) | Phase 2 |
| D3 | Recipients source | Files only / + saved contact lists | Files only in v2.0, lists in v2.x | Phase 4 |
| D4 | Code-signing budget | Apple Developer ID ($99/yr); Windows OV cert or Azure Trusted Signing (~$10/mo) | Both, before public release | Phase 6 |
| D5 | UI component library | Mantine / shadcn/ui (Radix + Tailwind) | Mantine (more built in: Stepper, Forms, Notifications) | Phase 0 |
| D6 | Email editor | GrapesJS + MJML / EmailBuilder.js / commercial (Unlayer, Beefree) | Decide from the Phase 0 spike (ease of use for Asha is the main criterion; commercial SDKs cost money and some require network access) | End of Phase 0 |
| D7 | Crash reporting | None / opt-in Sentry | Opt-in Sentry | Phase 5 |
| D8 | License of the app | Private / open source (MIT/GPL) | Owner's choice. Affects which editor licenses are acceptable | Phase 0 |
| D9 | Second UI language | Hindi / other / none | Decide after beta | v2.x |

---

## 19. Risks & mitigations

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Email designer too complex for non-technical users | High | Medium | Phase 0 spike on 2 editors; reduced block set by default; starter gallery; usability rounds |
| Designer output renders badly in Outlook | High | Medium | MJML (Outlook-safe tables); rendering matrix every release; restrict blocks to MJML-compatible features |
| Gmail/Outlook tighten app-password or SMTP access | High | Medium | OAuth in v2.1; provider abstraction in `email` package; clear help articles |
| Account locks from sending too fast | Medium | Medium | Conservative defaults, daily caps, circuit breaker |
| Electron security misconfiguration | High | Low | Hardening checklist enforced by automated tests; Electronegativity; review |
| npm supply-chain compromise | High | Low | Minimal deps, lockfile, install scripts blocked, audits, Renovate with review |
| Unsigned builds scare users | Medium | High (until D4) | Budget signing before public release; clear install instructions for beta |
| Linux keyring missing | Medium | Medium | Detect `basic_text`; warn; "don't remember password" mode |
| Scope creep | Medium | High | Fixed v2.0 scope (§3); new ideas go to the v2.x backlog |
| Single-developer bus factor | Medium | Medium | ADRs, docs, CONTRIBUTING, high test coverage, CI as the source of truth |

---

## 20. Definition of Done

A feature is **done** when:

- [ ] Acceptance criteria met and demoed
- [ ] Unit/component/integration tests written; coverage gates pass; E2E updated for user-visible flows
- [ ] Lint, typecheck and all CI jobs green on Windows, macOS and Linux
- [ ] Accessibility: keyboard-only walkthrough done; axe shows zero violations; light and dark checked
- [ ] Security checklist reviewed (IPC input validated, no secrets in renderer or logs, file access via tokens)
- [ ] All strings in i18n files; copy follows the glossary and error pattern
- [ ] Empty, loading and error states designed and implemented
- [ ] Help article or tooltip added/updated; user guide screenshots refreshed if the UI changed
- [ ] ADR written if an architectural decision was made
- [ ] Reviewed and approved in a PR; changeset added
