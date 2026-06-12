using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
/// Service implementation that handles authentication and CRUD operations with Google Sheets
/// for the Orange Events data ecosystem (Leads, Pricing Matrix, Past Quotations).
/// </summary>
public sealed class EventSheetsService : IEventSheetsService
{
    private readonly GoogleSheetsOptions _options;
    private readonly ILogger<EventSheetsService> _logger;
    private readonly string[] _scopes = { SheetsService.Scope.Spreadsheets };
    
    // Thread-safe semaphore to prevent out-of-order concurrent write collisions in Google Sheets
    private static readonly SemaphoreSlim _writeLock = new(1, 1);

    private const string LeadsSheetName = "Leads";
    private const string PastQuotesSheetName = "Past Quotations";
    private const string PricingMatrixSheetName = "Pricing Matrix";

    public EventSheetsService(
        IOptions<GoogleSheetsOptions> options,
        ILogger<EventSheetsService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<PricingMatrixRow>> GetPricingMatrixAsync()
    {
        var spreadsheetId = _options.SpreadsheetId;
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        try
        {
            var service = CreateSheetsService();
            var range = $"'{PricingMatrixSheetName}'!A2:D100"; // Read up to 100 pricing items
            
            _logger.LogInformation("Reading Pricing Matrix from range: {Range}", range);
            var response = await service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync();
            var values = response.Values;

            var list = new List<PricingMatrixRow>();
            if (values == null || values.Count == 0)
            {
                _logger.LogWarning("No data found in Pricing Matrix sheet.");
                return list;
            }

            foreach (var row in values)
            {
                if (row.Count == 0) continue;
                list.Add(PricingMatrixRow.FromRowValues(row));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read Pricing Matrix from Google Sheets.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<PastQuotationRow>> GetPastQuotationsAsync()
    {
        var spreadsheetId = _options.SpreadsheetId;
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        try
        {
            var service = CreateSheetsService();
            var range = $"'{PastQuotesSheetName}'!A2:G1000"; // Read up to 1000 historical records
            
            _logger.LogInformation("Reading Past Quotations from range: {Range}", range);
            var response = await service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync();
            var values = response.Values;

            var list = new List<PastQuotationRow>();
            if (values == null || values.Count == 0)
            {
                _logger.LogWarning("No data found in Past Quotations sheet.");
                return list;
            }

            foreach (var row in values)
            {
                if (row.Count == 0) continue;
                list.Add(PastQuotationRow.FromRowValues(row));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read Past Quotations from Google Sheets.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AppendLeadAsync(LeadSheetRow lead)
    {
        var spreadsheetId = _options.SpreadsheetId;
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogError("Google Sheets SpreadsheetId is not configured.");
            throw new InvalidOperationException("SpreadsheetId is not configured in settings.");
        }

        // Acquire thread-safe lock to prevent overlapping write operations
        await _writeLock.WaitAsync();
        try
        {
            var service = CreateSheetsService();
            
            // Auto-verify and create the Leads sheet if missing
            await EnsureLeadsSheetExistsAsync(service, spreadsheetId);

            var range = $"'{LeadsSheetName}'!A:L"; // A to L maps 12 columns
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { lead.ToRowValues() }
            };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, spreadsheetId, range);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            _logger.LogInformation("Appending Lead to sheet. Range: {Range}", range);
            await appendRequest.ExecuteAsync();
            _logger.LogInformation("Successfully recorded lead for customer '{Name}' in Google Sheets.", lead.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append Lead to Google Sheets for customer '{Name}'.", lead.Name);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Helper to verify sheet tab existence, auto-create it if missing, and initialize it with table headers.
    /// </summary>
    private async Task EnsureLeadsSheetExistsAsync(SheetsService service, string spreadsheetId)
    {
        try
        {
            var spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
            foreach (var sheet in spreadsheet.Sheets)
            {
                if (sheet.Properties.Title.Equals(LeadsSheetName, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Already exists
                }
            }

            _logger.LogWarning("Leads sheet tab '{Name}' not found. Dynamic creation initiated...", LeadsSheetName);

            var batchUpdate = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        AddSheet = new AddSheetRequest
                        {
                            Properties = new SheetProperties { Title = LeadsSheetName }
                        }
                    }
                }
            };
            
            await service.Spreadsheets.BatchUpdate(batchUpdate, spreadsheetId).ExecuteAsync();

            // Populate headers
            var headerValues = new ValueRange
            {
                Values = new List<IList<object>> { LeadSheetRow.GetHeaders().ConvertAll(h => (object)h) }
            };

            var headerRange = $"'{LeadsSheetName}'!A1:L1";
            var updateRequest = service.Spreadsheets.Values.Update(headerValues, spreadsheetId, headerRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateRequest.ExecuteAsync();
            
            _logger.LogInformation("Initialized Leads sheet headers successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check or create Leads sheet. Writing directly.");
        }
    }

    /// <summary>
    /// Instantiates SheetsService using Service Account JSON keys.
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
            var credentialPath = _options.CredentialsJsonPath;
            if (string.IsNullOrWhiteSpace(credentialPath))
            {
                throw new InvalidOperationException("Google credentials path is not configured.");
            }

            if (!Path.IsPathRooted(credentialPath))
            {
                credentialPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, credentialPath);
            }

            if (!File.Exists(credentialPath))
            {
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
