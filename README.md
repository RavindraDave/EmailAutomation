# Email Automation

A cross-platform desktop application (Windows and macOS) for bulk email automation, built with .NET 10 and Avalonia UI. Send personalized batches of email from an Excel spreadsheet via SMTP (Gmail App Password) or the Gmail API, with a safety-first workflow: preview before you send, throttle to avoid provider rate limits, resume an interrupted batch without duplicate sends, and export a report afterward.

## Installing

Download the build for your platform (v1.1.0), or build it yourself - see [Building & Packaging](#building--packaging) below. All releases are listed on the [Releases](../../releases) page.

- **Windows (x64)**: [EmailAutomation-win-x64.zip](https://github.com/RavindraDave/EmailAutomation/releases/download/v1.1.1/EmailAutomation-win-x64.zip) - unzip and run `EmailAutomation.UI.exe`. No .NET installation required (it's self-contained).
- **macOS (Apple Silicon)**: [EmailAutomation-macOS-arm64.zip](https://github.com/RavindraDave/EmailAutomation/releases/download/v1.1.1/EmailAutomation-macOS-arm64.zip) - unzip `EmailAutomation.app` and drag it into `Applications`, or run it in place.
  - Because this build isn't code-signed/notarized, macOS Gatekeeper blocks the first launch. **Right-click the app and choose "Open"**, then confirm in the dialog that appears. (Alternatively, run `xattr -dr com.apple.quarantine /path/to/EmailAutomation.app` in Terminal.) You only need to do this once.

For a longer, screenshot-illustrated walkthrough, see the [User Guide](https://app.notion.com/p/3aa94476928181968eaad7eff3bf4981).

All of your data - settings, the local database, and logs - lives outside the install folder in your per-user application data directory (`%APPDATA%\EmailAutomation` on Windows, `~/Library/Application Support/EmailAutomation` on macOS), so updating the app in place never touches your templates or history.

## First-Time Setup

1. Launch the app and open the **Settings** tab.
2. Choose your provider:
   - **SMTP (recommended)** - works with any Gmail account. You'll need a Google **App Password**, not your normal password:
     1. Go to your Google Account → **Security**.
     2. Enable **2-Step Verification** if it isn't already on.
     3. Search for **App passwords** and create one (e.g. named "Email Automation").
     4. Enter your Gmail address and the 16-character app password into the Settings screen.
   - **Gmail API (OAuth)** - for advanced use. Requires a `credentials.json` OAuth client file from the [Google Cloud Console](https://console.cloud.google.com/) with the `gmail.send` scope; point Settings at its path. The first Test Connection or send opens a browser to sign in, then caches a token.
3. Set your **sending limits**: a delay between emails (a second or two is plenty) and a daily send cap (Gmail consumer accounts are limited to roughly 500/day - staying under that avoids temporary account locks).
4. Click **Test Connection** to confirm everything works before you rely on it.
5. Click **Save**. Your password is encrypted using your operating system's credential store (Keychain on macOS, DPAPI on Windows) - it is never written to disk in plain text.

## Step-by-Step Usage Guide

### 1. Get the Excel template

On the **Batch Execution** tab, click **Download Sample Template** to save a ready-to-fill-in `.xlsx` with the columns this app expects:

| Column | Required? | Purpose |
|---|---|---|
| `To` | Yes | Recipient email address |
| `Cc` | No | Optional CC address |
| `Subject` | No | Overrides the template's default subject for this row only |
| `AttachmentPath` | No | Absolute path to a file to attach |
| `IsEnabled` | No | `TRUE`/`FALSE` - set `FALSE` to skip a row without deleting it |

Any other column you add (e.g. `FirstName`, `InvoiceNo`) becomes usable in your templates as `{{ColumnName}}`.

### 2. Create a template

In **Template Management**, click **New Template**, give it a name, write a default subject, and compose the body using `{{ColumnName}}` placeholders that match your spreadsheet's headers. The body is HTML, so formatting - bold, colors, tables - is sent exactly as written; two buttons help with this:

- **Load HTML File...** - import an existing `.html` file (e.g. exported from Word or another editor) straight into the body, instead of hand-typing markup.
- **Preview in Browser** - opens the current body, rendered with placeholders left blank, in your default browser so you can check formatting and tables before sending.

Click **Save**.

### 3. Preview before you send

Back in **Batch Execution**, select your Excel file and template, then click **Preview / Validate**. This checks every row - without sending anything - for invalid email addresses, missing attachments, and placeholders that don't match any column (the most common mistake, since an unmatched placeholder silently sends a blank field rather than an error). Fix anything flagged, then re-run Preview. **Start** is disabled until validation passes clean.

### 4. Send, pause, or stop

Click **Start**. The progress bar and status line update per row as it sends. **Pause** holds the batch in place (nothing more is sent until you **Resume**); **Stop** halts after the current email finishes. Progress is saved continuously, so if you stop, close the app, or it's interrupted, relaunching the same file and template offers to **resume** - already-sent rows are skipped, not re-sent.

### 5. Review results

The **Dashboard** shows overall sent/failed totals and a history of recent runs. Select a run and click **Export Report (CSV)** to save a per-recipient log (status, attempts, error messages, timestamps) for your records.

## Building & Packaging

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

```bash
dotnet build     # build everything
dotnet test      # run the test suite
```

### Windows (x64)

```bash
./packaging/windows/publish-windows.sh
```

Produces a single self-contained `EmailAutomation.UI.exe` at `publish/win-x64/` - no separate .NET install needed on the target machine.

### macOS (Apple Silicon)

```bash
./packaging/macos/build-app-bundle.sh
```

Publishes a self-contained build and assembles it into a double-clickable `publish/EmailAutomation.app`, including an icon generated from `EmailAutomation.UI/Assets/avalonia-logo.ico`. This bundle is unsigned - see the Gatekeeper note under [Installing](#installing) before distributing it. Proper code signing (an Apple Developer ID, $99/yr) plus notarization is the natural next step if you're distributing to non-technical users at scale; it's out of scope for this build.

Both scripts can be run from any OS, since .NET's publish supports cross-compiling to another platform's runtime identifier.

## Architecture

- `EmailAutomation.Domain` - plain models (`EmailJob`, `EmailTemplate`, `BatchRun`, `EmailLog`, `AppSettings`, ...), no dependencies.
- `EmailAutomation.Application` - business logic and the interfaces Infrastructure implements: `BatchExecutionService` (runs a batch with throttling/pause/cancel/resume), `BatchValidationService` (dry-run checks), `IRepository`, `ISettingsService`, etc.
- `EmailAutomation.Infrastructure` - concrete implementations: SQLite persistence (Dapper), Excel I/O (ClosedXML), the Scriban template engine, SMTP/Gmail senders (MailKit/Google APIs, with Polly retries), and OS-native credential storage.
- `EmailAutomation.UI` - the Avalonia desktop app (MVVM, dependency-injected).
- `EmailAutomation.Tests` - xUnit test suite.

## Features

- Send batch emails via SMTP or the Gmail API, with retry on transient failures.
- HTML email bodies with full formatting (styles, tables, images) preserved as-authored; import an existing `.html` file or preview the rendered body in your browser before sending.
- Dry-run validation before sending: bad addresses, missing attachments, and unmatched template placeholders are all caught up front.
- Configurable throttling and a daily send cap to stay within provider rate limits.
- Pause, resume, and stop mid-batch; an interrupted run can be resumed later without re-sending already-delivered emails.
- Per-run history and CSV export from the Dashboard.
- Credentials encrypted via the OS credential store (Keychain / DPAPI), never stored in plain text.
- Local SQLite database for templates, run history, and per-recipient logs.
