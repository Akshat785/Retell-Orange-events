# Retell AI Web API Integration & Google Sheets Logging

A complete, production-ready ASP.NET Core .NET 9 Web API project designed to securely receive, validate, and log Retell AI voice call webhook payloads (transcripts, durations, caller metadata) into a Google Sheet using Service Account Authentication.

---

## 🚀 Key Features

*   **Webhook Endpoint (`POST /api/retell/webhook`):** Receives real-time or end-of-call webhooks from Retell AI.
*   **API Key Middleware Securing Route:** Inspects the request header for `x-api-key` (or falling back to `api_key` in query parameters) and matches it against server settings to ensure only Retell AI triggers can reach your handler.
*   **Google Sheets Integration (`GoogleSheetsService`):** Loads a Google Cloud Service Account JSON credentials file, validates access scopes, and appends a row representing each completed call.
*   **Request Lifecycle Logging:** Dynamic custom middleware to print latency, status, HTTP verb, and incoming parameters for easy debugging in console or structured files.
*   **Global Exception Handling Middleware:** A catch-all middleware returning structured RFC-compliant JSON responses to callers, avoiding stack-trace exposure.
*   **Swagger OpenAPI Documentation:** Native Swagger UI with integrated XML comments describing models, validation rules, and built-in secure header definitions for direct browser-based testing.

---

## 📂 Codebase Folder Structure

The project has been structured cleanly following enterprise C# patterns:

```text
/RetellIntegrationApi
  ├── /Configuration
  │     ├── RetellOptions.cs          # Retell Api Key, Webhook API Key, and AgentId configurations
  │     └── GoogleSheetsOptions.cs    # Google Sheets SpreadsheetId, SheetName, and credentials path
  ├── /Controllers
  │     └── RetellWebhookController.cs # Handle Webhook POST requests, parse payload, trigger sheets
  ├── /Middleware
  │     ├── RetellApiKeyMiddleware.cs # Secure /api/retell/webhook path using API keys
  │     ├── RequestLoggingMiddleware.cs # Custom HTTP verb, path, latency, and status logger
  │     └── ExceptionHandlingMiddleware.cs # Catch-all middleware for global error JSON formatting
  ├── /Models
  │     ├── RetellWebhookRequest.cs   # Strongly-typed deserialization model for Retell payload
  │     └── GoogleSheetRow.cs         # Row model translating fields into Google sheets list format
  ├── /Services
  │     ├── IGoogleSheetsService.cs   # Interface definition for dependency injection mapping
  │     └── GoogleSheetsService.cs     # Sheets API credentials loader and dynamic appender
  ├── Program.cs                      # Service container configuration and HTTP pipeline assembly
  └── appsettings.json                # Secure app configuration file
```

---

## 🛠️ Step-by-Step Setup

### 1. Configure the Google Sheets API & Service Account

To write to a Google Sheet from a backend API, you must configure a Service Account:

1.  Go to the [Google Cloud Console](https://console.cloud.google.com/).
2.  Create a new Project (or select an existing one).
3.  Navigate to **APIs & Services > Library** and search for **Google Sheets API**. Click **Enable**.
4.  Navigate to **APIs & Services > Credentials**. Click **Create Credentials** and select **Service Account**.
5.  Provide a name (e.g. `retell-sheets-logger`) and finish the setup.
6.  Once created, click on the service account under the list, go to the **Keys** tab, click **Add Key > Create New Key**, choose **JSON**, and download the file.
7.  Rename the downloaded JSON file to `credentials.json` and place it in the root folder of this project (`/RetellIntegrationApi/credentials.json`).
8.  **Important:** Open the `credentials.json` file, copy the `client_email` value (e.g. `retell-sheets-logger@your-project.iam.gserviceaccount.com`).
9.  Open the target Google Sheet in your web browser. Click the **Share** button in the top right corner and invite the `client_email` with **Editor** permissions.

### 2. Map Configuration Values in `appsettings.json`

Open the `appsettings.json` file in the project root and fill in your keys:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Retell": {
    "ApiKey": "YOUR_RETELL_API_KEY",
    "WebhookApiKey": "YOUR_RETELL_WEBHOOK_API_KEY",
    "AgentId": "YOUR_RETELL_AGENT_ID"
  },
  "GoogleSheets": {
    "SpreadsheetId": "YOUR_GOOGLE_SPREADSHEET_ID_FROM_SHEET_URL",
    "SheetName": "Sheet1",
    "CredentialsJsonPath": "credentials.json"
  }
}
```

> [!TIP]
> The **SpreadsheetId** is the long string of letters and numbers in the URL of your Google Sheet:
> `https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID_HERE/edit`

---

## 🌐 Public Testing using ngrok

Retell AI webhooks require a publicly accessible HTTPS URL. You can use **ngrok** to expose your local port securely to the web:

### Step 1: Install ngrok
If you do not have ngrok installed, download it from [ngrok.com](https://ngrok.com/download) or install it via package managers:
```powershell
# Windows Choco
choco install ngrok

# macOS Homebrew
brew install ngrok
```

### Step 2: Expose Your Local Server
1. Run your ASP.NET Core API server (the default local ports are configured in `Properties/launchSettings.json`). Let's say it runs on `http://localhost:5000` (or `https://localhost:5001`).
2. Run ngrok pointing to your HTTP port:
   ```bash
   ngrok http 5000
   ```
3. Copy the **Forwarding** HTTPS URL provided by ngrok (e.g., `https://a1b2-34-56-78.ngrok-free.app`).

### Step 3: Register Webhook in Retell AI
1. Login to your **Retell AI Developer Dashboard**.
2. Go to your **Agent Configuration** or **Webhook Settings**.
3. Paste the ngrok URL and append the webhook path:
   `https://a1b2-34-56-78.ngrok-free.app/api/retell/webhook`
4. Make sure to specify the API key you set for `Retell:WebhookApiKey` in the `x-api-key` configuration headers.

---

## 🧪 Manual Verification & Swagger

This project includes fully configured **Swagger UI** to easily examine structures and manually execute requests without external clients:

1.  Run the application using `dotnet run`.
2.  Open your browser and navigate to: `http://localhost:5000/swagger/index.html` (or your HTTPS alternative).
3.  Click the **Authorize** lock button in the top right. Enter your secret webhook API key (matching `Retell:WebhookApiKey` in `appsettings.json`) and click authorize.
4.  Expand the `POST /api/retell/webhook` endpoint and select **Try it out**.
5.  The Swagger UI is loaded with a pre-populated production-like JSON payload containing structured call coordinates, transcript segments, and duration timings. Click **Execute** to run the request and observe the results.

### Sample Payload Format

Here is the format of the webhook payload sent by Retell AI (and supported by this API):

```json
{
  "event": "call_ended",
  "call": {
    "call_id": "call_a9e5b2c7d8f94a3b8c2d1e0f",
    "agent_id": "agent_3f2a8c7b9e0d1f4a5c6b7e8d",
    "call_status": "ended",
    "start_timestamp": 1716723200000,
    "end_timestamp": 1716723335000,
    "duration_ms": 135000,
    "user_number": "+15550199",
    "from_number": "+15550199",
    "to_number": "+15559876",
    "transcript": "[Agent]: Hello, thanks for calling. How can I help you today?\n[Caller]: Hi, I would like to check the status of my order, please.\n[Agent]: I can help with that. Could you please provide your order ID?\n[Caller]: Sure, it is order number 48291.\n[Agent]: Thank you. Checking... Yes, it has shipped and is out for delivery today!\n[Caller]: Great! Thanks so much.\n[Agent]: You're welcome! Have a wonderful day."
  }
}
```

### Result in Google Sheet
The backend will parse the payload and append the following row to your Google Sheet:
1.  **Timestamp:** The current date and time (e.g. `2026-05-26 16:52:00`).
2.  **Caller Number:** `+15550199`
3.  **Transcript:** The full conversation text block.
4.  **Duration:** `2m 15s` (parsed from `135000` ms).
