using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetellIntegrationApi.Configuration;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service implementation that authenticates with Google APIs using a Service Account 
/// and appends data rows into a specific Google Sheet spreadsheet.
/// </summary>
public sealed class GoogleSheetsService : IGoogleSheetsService
{
    private readonly GoogleSheetsOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleSheetsService> _logger;
    private readonly string[] _scopes = { SheetsService.Scope.Spreadsheets };

    public GoogleSheetsService(
        IOptions<GoogleSheetsOptions> options,
        IConfiguration configuration,
        ILogger<GoogleSheetsService> logger)
    {
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AppendRowAsync(GoogleSheetRow row)
    {
        if (string.IsNullOrWhiteSpace(_options.SpreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        var sheetName = string.IsNullOrWhiteSpace(_options.SheetName) ? "Sheet1" : _options.SheetName;

        try
        {
            var service = CreateSheetsService();

            // Auto-detect if tab exists, and create it if missing
            await EnsureSheetExistsAsync(service, sheetName);

            // A:D matches our 4 columns (Timestamp, CallerNumber, Transcript, Duration)
            var range = $"'{sheetName}'!A:D";
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { row.ToRowValues() }
            };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, _options.SpreadsheetId, range);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            _logger.LogInformation("Appending row to Google Sheet. SpreadsheetId: {SpreadsheetId}, Range: {Range}", _options.SpreadsheetId, range);
            
            var response = await appendRequest.ExecuteAsync();
            
            _logger.LogInformation("Successfully appended row to Google Sheet. Range updated: {UpdatedRange}", response.Updates?.UpdatedRange);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append row to Google Sheets spreadsheet '{SpreadsheetId}' in sheet '{SheetName}'.", _options.SpreadsheetId, sheetName);
            throw;
        }
    }

    /// <summary>
    /// Helper to verify sheet tab existence, auto-create it if missing, and initialize it with table headers.
    /// </summary>
    private async Task EnsureSheetExistsAsync(SheetsService service, string sheetName)
    {
        try
        {
            _logger.LogInformation("Verifying if sheet tab '{SheetName}' exists...", sheetName);
            var spreadsheet = await service.Spreadsheets.Get(_options.SpreadsheetId).ExecuteAsync();
            
            foreach (var sheet in spreadsheet.Sheets)
            {
                if (sheet.Properties.Title.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Tab already exists!
                }
            }

            _logger.LogWarning("Sheet tab '{SheetName}' was not found. Creating it dynamically...", sheetName);

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
            
            await service.Spreadsheets.BatchUpdate(batchUpdate, _options.SpreadsheetId).ExecuteAsync();
            _logger.LogInformation("Sheet tab '{SheetName}' created successfully. Appending headers...", sheetName);

            // Populate row 1 with headers
            var headerValues = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> { "Timestamp", "Caller Number", "Transcript", "Duration" }
                }
            };

            var headerRange = $"'{sheetName}'!A1:D1";
            var updateRequest = service.Spreadsheets.Values.Update(headerValues, _options.SpreadsheetId, headerRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            
            await updateRequest.ExecuteAsync();
            _logger.LogInformation("Successfully initialized headers on tab '{SheetName}'.", sheetName);
        }
        catch (Exception ex)
        {
            // If checking/creating the sheet tab fails due to restrictions (e.g. read-only permissions on getting spreadsheet structure), 
            // log a warning but proceed with the normal append sequence so we don't break fallback workflows.
            _logger.LogWarning(ex, "Could not check or auto-create sheet tab '{SheetName}'. Proceeding to write directly.", sheetName);
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
            credential = GoogleCredential.FromJson(envJson).CreateScoped(_scopes);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_options.CredentialsJsonPath))
            {
                _logger.LogError("Google Sheets CredentialsJsonPath is not configured in settings.");
                throw new InvalidOperationException("Google credentials path is not configured.");
            }

            // Handle path expansion for relative paths or home directories
            var credentialPath = _options.CredentialsJsonPath;
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
                credential = GoogleCredential.FromStream(stream).CreateScoped(_scopes);
            }
        }

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "RetellWebhookIntegration"
        });
    }

    /// <inheritdoc />
    public async Task<List<PricingMatrixItem>> GetPricingMatrixAsync()
    {
        var spreadsheetId = _configuration["GoogleSheets:OrangeEventsSpreadsheetId"];
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            spreadsheetId = _options.SpreadsheetId;
        }

        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        const string sheetName = "Pricing Matrix";
        // Query A1:F100 to retrieve Row 1 (the headers) dynamically
        var range = $"'{sheetName}'!A1:F100";

        try
        {
            var service = CreateSheetsService();
            _logger.LogInformation("Reading Pricing Matrix from Google Sheets range '{Range}' inside Spreadsheet ID '{SpreadsheetId}'...", range, spreadsheetId);
            
            var response = await service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync();
            var values = response.Values;

            var list = new List<PricingMatrixItem>();
            if (values == null || values.Count == 0)
            {
                _logger.LogWarning("No data rows found in sheet '{SheetName}'.", sheetName);
                return list;
            }

            // Extract headers from Row 1
            var headers = values[0].Select(h => h?.ToString()?.Trim() ?? string.Empty).ToList();

            // Resolve column indices dynamically by matching header names
            var eventTypeIdx = headers.FindIndex(h => h.Equals("Event Type", StringComparison.OrdinalIgnoreCase));
            var locationIdx = headers.FindIndex(h => h.Equals("Location", StringComparison.OrdinalIgnoreCase));
            var serviceIdx = headers.FindIndex(h => h.Equals("Service", StringComparison.OrdinalIgnoreCase));
            var pricingTypeIdx = headers.FindIndex(h => h.Equals("Pricing Type", StringComparison.OrdinalIgnoreCase));
            var unitCostIdx = headers.FindIndex(h => h.Equals("Unit Cost", StringComparison.OrdinalIgnoreCase));
            var additionalChargesIdx = headers.FindIndex(h => h.Equals("Additional Charges", StringComparison.OrdinalIgnoreCase));

            // Iterate over actual data rows starting from Index 1
            for (int i = 1; i < values.Count; i++)
            {
                var row = values[i];
                if (row.Count == 0) continue;
                
                var item = new PricingMatrixItem();

                if (eventTypeIdx >= 0 && row.Count > eventTypeIdx)
                    item.EventType = row[eventTypeIdx]?.ToString()?.Trim() ?? string.Empty;

                if (locationIdx >= 0 && row.Count > locationIdx)
                    item.Location = row[locationIdx]?.ToString()?.Trim() ?? string.Empty;

                if (serviceIdx >= 0 && row.Count > serviceIdx)
                    item.Service = row[serviceIdx]?.ToString()?.Trim() ?? string.Empty;

                if (pricingTypeIdx >= 0 && row.Count > pricingTypeIdx)
                    item.PricingType = row[pricingTypeIdx]?.ToString()?.Trim() ?? string.Empty;
                
                if (unitCostIdx >= 0 && row.Count > unitCostIdx && decimal.TryParse(row[unitCostIdx]?.ToString(), out var cost))
                {
                    item.UnitCost = cost;
                }
                
                if (additionalChargesIdx >= 0 && row.Count > additionalChargesIdx && decimal.TryParse(row[additionalChargesIdx]?.ToString(), out var addCharges))
                {
                    item.AdditionalCharges = addCharges;
                }

                list.Add(item);
            }

            _logger.LogInformation("Successfully retrieved and dynamically mapped {Count} Pricing Matrix items.", list.Count);
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read Pricing Matrix from Google Sheets.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<PastQuotation>> GetPastQuotationsAsync()
    {
        var spreadsheetId = _configuration["GoogleSheets:OrangeEventsSpreadsheetId"];
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            spreadsheetId = _options.SpreadsheetId;
        }

        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        const string sheetName = "Past Quotations";
        var range = $"'{sheetName}'!A2:E1000"; // Read up to 1000 history records

        try
        {
            var service = CreateSheetsService();
            _logger.LogInformation("Reading Past Quotations from Google Sheets range '{Range}' inside Spreadsheet ID '{SpreadsheetId}'...", range, spreadsheetId);
            
            var response = await service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync();
            var values = response.Values;

            var list = new List<PastQuotation>();
            if (values == null || values.Count == 0)
            {
                _logger.LogWarning("No data rows found in sheet '{SheetName}'.", sheetName);
                return list;
            }

            foreach (var row in values)
            {
                if (row.Count == 0) continue;

                var item = new PastQuotation();
                if (row.Count > 0) item.EventType = row[0]?.ToString()?.Trim() ?? string.Empty;
                if (row.Count > 1) item.Location = row[1]?.ToString()?.Trim() ?? string.Empty;
                
                if (row.Count > 2 && int.TryParse(row[2]?.ToString(), out var gc))
                {
                    item.GuestCount = gc;
                }
                
                if (row.Count > 3) item.ServicesIncluded = row[3]?.ToString()?.Trim() ?? string.Empty;
                
                if (row.Count > 4 && decimal.TryParse(row[4]?.ToString(), out var finalQuote))
                {
                    item.FinalQuote = finalQuote;
                }

                list.Add(item);
            }

            _logger.LogInformation("Successfully retrieved and parsed {Count} Past Quotations historical records.", list.Count);
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read Past Quotations from Google Sheets.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> CreateLeadAsync(CreateLeadRequest request)
    {
        var spreadsheetId = _configuration["GoogleSheets:OrangeEventsSpreadsheetId"];
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            spreadsheetId = _options.SpreadsheetId;
        }

        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        var service = CreateSheetsService();
        int highestId = 0;
        const string sheetName = "Leads";

        try
        {
            _logger.LogInformation("Querying existing Leads sheet for sequential ID generation...");
            var idRange = $"'{sheetName}'!A2:A10000";
            var response = await service.Spreadsheets.Values.Get(spreadsheetId, idRange).ExecuteAsync();
            var values = response.Values;

            if (values != null)
            {
                foreach (var row in values)
                {
                    if (row.Count == 0) continue;
                    var cellVal = row[0]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(cellVal)) continue;

                    // Extract the numerical suffix from "LEAD-XXXXXX"
                    if (cellVal.StartsWith("LEAD-", StringComparison.OrdinalIgnoreCase))
                    {
                        var numPart = cellVal.Substring(5);
                        if (int.TryParse(numPart, out var idNum))
                        {
                            if (idNum > highestId)
                            {
                                highestId = idNum;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine highest existing Lead ID. Defaulting to fresh sequence.");
        }

        var nextId = highestId + 1;
        var leadId = $"LEAD-{nextId:D6}";
        _logger.LogInformation("Generated next sequential Lead ID: '{LeadId}'", leadId);

        // Build the 19 columns row payload
        var newRow = new List<object>
        {
            leadId,
            request.CustomerName,
            request.Mobile,
            request.Email,
            request.EventType,
            request.EventDate,
            request.Location,
            request.GuestCount,
            string.Join(", ", request.Requirements),
            request.CustomerBudget?.ToString() ?? string.Empty,
            string.Empty, // Estimated Quote Range
            string.Empty, // Sales Team Price
            string.Empty, // Final Price
            string.Empty, // Actual Final Price
            "New",        // Lead Status
            string.Empty, // Sales Owner
            string.Empty, // Retell Call Id
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), // Created Date
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")  // Last Updated Date
        };

        try
        {
            var appendRange = $"'{sheetName}'!A:S";
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { newRow }
            };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, spreadsheetId, appendRange);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            
            _logger.LogInformation("Appending new Lead '{LeadId}' into Google Sheets...", leadId);
            await appendRequest.ExecuteAsync();
            _logger.LogInformation("Successfully created Lead '{LeadId}' in Google Sheets.", leadId);

            return leadId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append new Lead '{LeadId}' to Google Sheets.", leadId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateLeadEstimateAsync(UpdateLeadEstimateRequest request)
    {
        var spreadsheetId = _configuration["GoogleSheets:OrangeEventsSpreadsheetId"];
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            spreadsheetId = _options.SpreadsheetId;
        }

        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        var service = CreateSheetsService();
        const string sheetName = "Leads";
        var readRange = $"'{sheetName}'!A1:S10000"; // Read up to 10000 rows

        try
        {
            _logger.LogInformation("Retrieving Leads rows for update lookup on Lead ID '{LeadId}'...", request.LeadId);
            var response = await service.Spreadsheets.Values.Get(spreadsheetId, readRange).ExecuteAsync();
            var values = response.Values;

            if (values == null || values.Count == 0)
            {
                _logger.LogError("Leads sheet is completely empty. Cannot update lead.");
                throw new KeyNotFoundException($"Leads sheet is empty. Lead with ID '{request.LeadId}' was not found.");
            }

            // Map header column indices dynamically
            var headers = values[0].Select(h => h?.ToString()?.Trim() ?? string.Empty).ToList();
            var leadIdIdx = headers.FindIndex(h => h.Equals("Lead Id", StringComparison.OrdinalIgnoreCase));
            var estRangeIdx = headers.FindIndex(h => h.Equals("Estimated Quote Range", StringComparison.OrdinalIgnoreCase));
            var leadStatusIdx = headers.FindIndex(h => h.Equals("Lead Status", StringComparison.OrdinalIgnoreCase));
            var lastUpdatedIdx = headers.FindIndex(h => h.Equals("Last Updated Date", StringComparison.OrdinalIgnoreCase));

            if (leadIdIdx < 0)
            {
                throw new InvalidOperationException("Leads sheet is missing 'Lead Id' header column.");
            }

            int matchedRowIndex = -1;
            IList<object> matchedRow = null;

            // Find matching row starting from index 1 (ignoring headers)
            for (int i = 1; i < values.Count; i++)
            {
                var row = values[i];
                if (row.Count > leadIdIdx && row[leadIdIdx]?.ToString()?.Trim().Equals(request.LeadId.Trim(), StringComparison.OrdinalIgnoreCase) == true)
                {
                    matchedRowIndex = i + 1; // 1-based index
                    matchedRow = row;
                    break;
                }
            }

            if (matchedRowIndex == -1 || matchedRow == null)
            {
                _logger.LogWarning("Lead ID '{LeadId}' was not found in Leads sheet.", request.LeadId);
                throw new KeyNotFoundException($"Lead with ID '{request.LeadId}' was not found in the Leads sheet.");
            }

            // Pad the row values array dynamically if cells are empty at the end
            var maxIndex = Math.Max(estRangeIdx, Math.Max(leadStatusIdx, lastUpdatedIdx));
            while (matchedRow.Count <= maxIndex)
            {
                matchedRow.Add(string.Empty);
            }

            // Update only specified columns dynamically
            if (estRangeIdx >= 0) matchedRow[estRangeIdx] = request.EstimatedQuoteRange;
            if (leadStatusIdx >= 0) matchedRow[leadStatusIdx] = "Quoted";
            if (lastUpdatedIdx >= 0) matchedRow[lastUpdatedIdx] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            var writeRange = $"'{sheetName}'!A{matchedRowIndex}";
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { matchedRow }
            };

            _logger.LogInformation("Updating Lead '{LeadId}' at sheet row {Row}...", request.LeadId, matchedRowIndex);
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, writeRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateRequest.ExecuteAsync();

            _logger.LogInformation("Successfully updated Lead '{LeadId}' in Google Sheets.", request.LeadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Lead '{LeadId}' in Google Sheets.", request.LeadId);
            throw;
        }
    }
}
