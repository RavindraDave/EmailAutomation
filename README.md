# Email Automation

A cross-platform desktop application using .NET 8 and Avalonia UI for bulk email automation utilizing the Gmail API. This application empowers non-technical users to quickly send batches of personalized emails with attachments easily and securely.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Gmail Account with API Access configured for OAuth.

## Setup & Configuration

To allow the application to access the Gmail API, you must place your Google OAuth credentials in the application directory:

1. Obtain a `credentials.json` file from your [Google Cloud Console](https://console.cloud.google.com/). Ensure the OAuth credentials are created for a "Desktop Application" and have the `https://www.googleapis.com/auth/gmail.send` scope.
2. Place `credentials.json` directly next to your published executable file.

Upon initial execution, a browser will open prompting you to sign in and grant the application permissions. A `token.json` file will then be generated for subsequent authentications automatically.

## How to Build & Run

### Building the Project Locally

Clone the repository and run the build command from the root directory:

```bash
dotnet build
```

### Creating a Self-Contained Deployment

If you want a standalone executable that doesn't require .NET to be installed globally on a target machine, you can publish a self-contained release:

**For Windows (x64):**
```bash
dotnet publish EmailAutomation.UI/EmailAutomation.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**For macOS (ARM64):**
```bash
dotnet publish EmailAutomation.UI/EmailAutomation.UI.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

**For Linux (x64):**
```bash
dotnet publish EmailAutomation.UI/EmailAutomation.UI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

### Running the Application

Navigate to the output publish directory and launch the `.exe` (or respective binary for Linux/Mac).
Example path:
`EmailAutomation.UI/bin/Release/net8.0/win-x64/publish/EmailAutomation.UI.exe`

## Step-by-Step Usage Guide

### 1. Template Management
Before starting a batch, define an email template:
1. Navigate to the **Template Management** tab using the side menu.
2. Click **New Template**.
3. Name your template, create a default subject, and compose your body text.
4. You can utilize placeholders matching your Excel headers wrapped in double curly braces (e.g. `Hello {{FirstName}}, ...`).
5. Click **Save** to store it in your local SQLite database.

### 2. Prepare the Excel File
The system imports from `.xlsx` files format with minimum required columns:
- **To** (Recipient Email Address)
- **Cc** (Optional)
- **Subject** (Optional, overrides your Template Subject per-row)
- **AttachmentPath** (Optional absolute file path)
- **IsEnabled** (Optional boolean `true`/`false`. Disables specific row processing)

Any other custom column created (e.g., `InvoiceNo`) will be usable as `{{InvoiceNo}}` within your templates.

### 3. Batch Execution
1. Navigate to the **Batch Execution** tab.
2. Click **Browse...** to select the prepared `.xlsx` input file.
3. Select the template you designed from the dropdown.
4. Click **Start** to begin sending out emails. The status will update the Progress Bar and write logs per-execution. Logs can be checked later in the `logs/emailautomation.log` directory and Dashboard.

## Features Currently Implemented (MVP)
- Send automated batch emails through the Gmail API directly.
- Template engine dynamic variable insertion powered by [Scriban](https://github.com/scriban/scriban).
- Local SQLite database ensuring template states persist.
- Graceful transient network error handling and retries enabled by [Polly](https://github.com/App-vNext/Polly).
