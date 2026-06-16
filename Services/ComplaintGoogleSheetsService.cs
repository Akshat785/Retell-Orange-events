using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetellIntegrationApi.Configuration;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service implementation that authenticates with Google Sheets API and logs customer complaints in a separate tab or spreadsheet.
/// Includes support for auto-verifying and dynamically creating sheet tabs with headers.
/// </summary>
public sealed class ComplaintGoogleSheetsService : IComplaintGoogleSheetsService
{
    private readonly GoogleSheetsOptions _googleSheetsOptions;
    private readonly ComplaintSheetsOptions _complaintOptions;
    private readonly ILogger<ComplaintGoogleSheetsService> _logger;
    private readonly string[] _scopes = { SheetsService.Scope.Spreadsheets };

    public ComplaintGoogleSheetsService(
        IOptions<GoogleSheetsOptions> googleSheetsOptions,
        IOptions<ComplaintSheetsOptions> complaintOptions,
        ILogger<ComplaintGoogleSheetsService> logger)
    {
        _googleSheetsOptions = googleSheetsOptions.Value;
        _complaintOptions = complaintOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AppendComplaintRowAsync(ComplaintSheetRow row)
    {
        // Resolve SpreadsheetId: Fallback to the main GoogleSheets:SpreadsheetId if ComplaintSheets:SpreadsheetId is empty
        var spreadsheetId = string.IsNullOrWhiteSpace(_complaintOptions.SpreadsheetId)
            ? _googleSheetsOptions.SpreadsheetId
            : _complaintOptions.SpreadsheetId;

        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId for complaints is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        var sheetName = string.IsNullOrWhiteSpace(_complaintOptions.SheetName) ? "Complaint Logs" : _complaintOptions.SheetName;

        try
        {
            var service = CreateSheetsService();

            // 1. Auto-detect if tab exists, and create it dynamically if missing
            await EnsureSheetExistsAsync(service, spreadsheetId, sheetName);

            // A:F matches our 6 columns (CustomerName, MobileNumber, ComplaintType, ComplaintDescription, Address, Timestamp)
            var range = $"'{sheetName}'!A:F";
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { row.ToRowValues() }
            };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, spreadsheetId, range);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            _logger.LogInformation("Appending complaint row to Google Sheet. SpreadsheetId: {SpreadsheetId}, Range: {Range}", spreadsheetId, range);
            
            var response = await appendRequest.ExecuteAsync();
            
            _logger.LogInformation("Successfully appended complaint row to Google Sheet. Range updated: {UpdatedRange}", response.Updates?.UpdatedRange);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append complaint row to Google Sheets spreadsheet '{SpreadsheetId}' in sheet '{SheetName}'.", spreadsheetId, sheetName);
            throw;
        }
    }

    /// <summary>
    /// Helper to verify sheet tab existence, auto-create it if missing, and initialize it with table headers.
    /// </summary>
    private async Task EnsureSheetExistsAsync(SheetsService service, string spreadsheetId, string sheetName)
    {
        try
        {
            _logger.LogInformation("Verifying if complaint sheet tab '{SheetName}' exists in spreadsheet '{SpreadsheetId}'...", sheetName, spreadsheetId);
            var spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
            
            foreach (var sheet in spreadsheet.Sheets)
            {
                if (sheet.Properties.Title.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Tab already exists!
                }
            }

            _logger.LogWarning("Complaint sheet tab '{SheetName}' was not found. Creating it dynamically...", sheetName);

            var batchUpdate = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        AddSheet = new AddSheetRequest
                        {
                            Properties = new SheetProperties
                            {
                                Title = sheetName
                            }
                        }
                    }
                }
            };
            
            await service.Spreadsheets.BatchUpdate(batchUpdate, spreadsheetId).ExecuteAsync();
            _logger.LogInformation("Complaint sheet tab '{SheetName}' created successfully. Appending headers...", sheetName);

            // Populate row 1 with headers matching the requirement
            var headerValues = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> { "Customer Name", "Mobile Number", "Complaint Type", "Complaint Description", "Address", "Timestamp" }
                }
            };

            var headerRange = $"'{sheetName}'!A1:F1";
            var updateRequest = service.Spreadsheets.Values.Update(headerValues, spreadsheetId, headerRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            
            await updateRequest.ExecuteAsync();
            _logger.LogInformation("Successfully initialized headers on complaint tab '{SheetName}'.", sheetName);
        }
        catch (Exception ex)
        {
            // Fall back gracefully so that writes are attempted even if read-only metadata scanning fails
            _logger.LogWarning(ex, "Could not check or auto-create complaint sheet tab '{SheetName}'. Proceeding to write directly.", sheetName);
        }
    }

    /// <summary>
    /// Loads service account credentials from file stream and instantiates the Google Sheets service.
    /// </summary>
    private SheetsService CreateSheetsService()
    {
        GoogleCredential credential;
        var envJson = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS_JSON");

        if (!string.IsNullOrWhiteSpace(envJson))
        {
            _logger.LogInformation("Loading Google credentials from environment variable.");
#pragma warning disable CS0618
            credential = GoogleCredential.FromJson(envJson).CreateScoped(_scopes);
#pragma warning restore CS0618
        }
        else
        {
            var credentialPath = _googleSheetsOptions.CredentialsJsonPath;
            if (string.IsNullOrWhiteSpace(credentialPath))
            {
                _logger.LogError("Google Sheets CredentialsJsonPath is not configured in settings.");
                throw new InvalidOperationException("Google credentials path is not configured.");
            }

            // Handle path expansion for relative paths or home directories
            if (!Path.IsPathRooted(credentialPath))
            {
                credentialPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, credentialPath);
            }

            if (!File.Exists(credentialPath))
            {
                _logger.LogError("Google Sheets Service Account credentials JSON file not found at: '{Path}'", credentialPath);
                throw new FileNotFoundException("Google credentials file not found.", credentialPath);
            }

            using (var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read))
            {
#pragma warning disable CS0618
                credential = GoogleCredential.FromStream(stream).CreateScoped(_scopes);
#pragma warning restore CS0618
            }
        }

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "RetellWebhookIntegration"
        });
    }
}
