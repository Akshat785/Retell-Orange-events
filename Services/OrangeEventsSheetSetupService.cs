using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetellIntegrationApi.Configuration;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service that automates the setup, validation, and bootstrapping of Google Sheets 
/// for the Orange Events data ecosystem. Completely safe to run multiple times.
/// </summary>
public sealed class OrangeEventsSheetSetupService : IOrangeEventsSheetSetupService
{
    private readonly GoogleSheetsOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<OrangeEventsSheetSetupService> _logger;

    private readonly string[] _scopes = { SheetsService.Scope.Spreadsheets };

    // Thread-safe lock to prevent overlapping setup operations
    private static readonly SemaphoreSlim _setupLock = new(1, 1);

    private const string LeadsSheetName = "Leads";
    private const string PricingMatrixSheetName = "Pricing Matrix";
    private const string PastQuotesSheetName = "Past Quotations";

    public OrangeEventsSheetSetupService(
        IOptions<GoogleSheetsOptions> options,
        IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment,
        ILogger<OrangeEventsSheetSetupService> logger)
    {
        _options = options.Value;
        _configuration = configuration;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SetupResult> SetupSpreadsheetAsync()
    {
        // Acquire lock
        await _setupLock.WaitAsync();
        try
        {
            _logger.LogInformation("Starting Orange Events Google Sheets setup process...");

            var service = CreateSheetsService();

            // 1. Resolve spreadsheet ID (check configuration/IConfiguration first for dynamic updates)
            var spreadsheetId = _configuration["GoogleSheets:OrangeEventsSpreadsheetId"];
            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                spreadsheetId = _options.OrangeEventsSpreadsheetId;
            }

            bool exists = false;
            Spreadsheet? spreadsheet = null;

            if (!string.IsNullOrWhiteSpace(spreadsheetId))
            {
                try
                {
                    _logger.LogInformation("Verifying configured Spreadsheet ID: '{SpreadsheetId}'", spreadsheetId);
                    spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
                    exists = true;
                }
                catch (Exception ex) when (ex.GetType().Name == "GoogleJsonResponseException" 
                                           || ex.Message.Contains("404") 
                                           || ex.Message.Contains("400") 
                                           || ex.Message.Contains("403") 
                                           || ex.Message.Contains("Forbidden") 
                                           || ex.Message.Contains("NotFound") 
                                           || ex.Message.Contains("BadRequest") 
                                           || ex.Message.Contains("permission"))
                {
                    _logger.LogWarning(ex, "Configured Spreadsheet ID '{SpreadsheetId}' is invalid, does not exist, or is inaccessible due to permission restrictions. A new spreadsheet will be created.", spreadsheetId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch spreadsheet metadata for '{SpreadsheetId}'. Recreating...", spreadsheetId);
                }
            }

            if (exists && spreadsheet != null)
            {
                _logger.LogInformation("Spreadsheet '{SpreadsheetId}' exists. Verifying sheets and tabs.", spreadsheetId);

                // Check missing tabs
                var requiredSheets = new[] { LeadsSheetName, PricingMatrixSheetName, PastQuotesSheetName };
                var existingSheets = spreadsheet.Sheets.Select(s => s.Properties.Title).ToList();
                var missingTabs = requiredSheets.Except(existingSheets, StringComparer.OrdinalIgnoreCase).ToList();

                if (missingTabs.Count > 0)
                {
                    _logger.LogInformation("Creating missing sheet tabs dynamically: {MissingTabs}", string.Join(", ", missingTabs));
                    var requests = missingTabs.Select(tabName => new Request
                    {
                        AddSheet = new AddSheetRequest
                        {
                            Properties = new SheetProperties { Title = tabName }
                        }
                    }).ToList();

                    var batchRequest = new BatchUpdateSpreadsheetRequest { Requests = requests };
                    await service.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync();
                    _logger.LogInformation("Missing sheet tabs created successfully.");
                }

                // Verify and initialize headers and data for all required sheets
                foreach (var sheetName in requiredSheets)
                {
                    await EnsureSheetInitializedAsync(service, spreadsheetId, sheetName);
                }

                return new SetupResult
                {
                    SpreadsheetId = spreadsheetId,
                    SpreadsheetUrl = spreadsheet.SpreadsheetUrl,
                    IsNewSpreadsheet = false,
                    IsAlreadyConfigured = true
                };
            }
            else
            {
                _logger.LogInformation("Creating a brand new spreadsheet named 'Orange Events AI Data'...");

                // Create spreadsheet with pre-defined tabs directly in the create request (avoids dynamic "Sheet1" cleanup!)
                var newSpreadsheet = new Spreadsheet
                {
                    Properties = new SpreadsheetProperties
                    {
                        Title = "Orange Events AI Data"
                    },
                    Sheets = new List<Sheet>
                    {
                        new Sheet { Properties = new SheetProperties { Title = LeadsSheetName } },
                        new Sheet { Properties = new SheetProperties { Title = PricingMatrixSheetName } },
                        new Sheet { Properties = new SheetProperties { Title = PastQuotesSheetName } }
                    }
                };

                var createdSpreadsheet = await service.Spreadsheets.Create(newSpreadsheet).ExecuteAsync();
                var newSpreadsheetId = createdSpreadsheet.SpreadsheetId;
                _logger.LogInformation("New Spreadsheet created successfully. ID: '{SpreadsheetId}'", newSpreadsheetId);

                // Populate headers and data for all sheets (they are all newly created and empty)
                await EnsureSheetInitializedAsync(service, newSpreadsheetId, LeadsSheetName);
                await EnsureSheetInitializedAsync(service, newSpreadsheetId, PricingMatrixSheetName);
                await EnsureSheetInitializedAsync(service, newSpreadsheetId, PastQuotesSheetName);

                // Persist new spreadsheet ID to local config files (appsettings.json / appsettings.Development.json)
                await SaveSpreadsheetIdToConfigurationAsync(newSpreadsheetId);

                return new SetupResult
                {
                    SpreadsheetId = newSpreadsheetId,
                    SpreadsheetUrl = createdSpreadsheet.SpreadsheetUrl,
                    IsNewSpreadsheet = true,
                    IsAlreadyConfigured = false
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure during Orange Events spreadsheet setup.");
            throw;
        }
        finally
        {
            _setupLock.Release();
        }
    }

    /// <summary>
    /// Checks if a sheet tab is empty and, if so, populates its headers and optional sample data.
    /// </summary>
    private async Task EnsureSheetInitializedAsync(SheetsService service, string spreadsheetId, string sheetName)
    {
        try
        {
            _logger.LogInformation("Checking if sheet '{SheetName}' is empty...", sheetName);
            // Fetch A1:Z10 range to check for presence of data
            var range = $"'{sheetName}'!A1:Z10";
            var response = await service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync();
            var values = response.Values;

            if (values == null || values.Count == 0)
            {
                _logger.LogInformation("Sheet '{SheetName}' is empty. Initializing headers and default values...", sheetName);
                var defaultData = GetDefaultDataForSheet(sheetName);

                var valueRange = new ValueRange
                {
                    Values = defaultData
                };

                var writeRange = $"'{sheetName}'!A1";
                var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, writeRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await updateRequest.ExecuteAsync();

                _logger.LogInformation("Successfully initialized sheet '{SheetName}'.", sheetName);
            }
            else
            {
                _logger.LogInformation("Sheet '{SheetName}' is not empty. Preserving existing data to ensure idempotency.", sheetName);
                
                if (sheetName.Equals(LeadsSheetName, StringComparison.OrdinalIgnoreCase))
                {
                    await MigrateLeadsHeadersAsync(service, spreadsheetId, values);
                }
                else if (sheetName.Equals(PricingMatrixSheetName, StringComparison.OrdinalIgnoreCase))
                {
                    await MigratePricingMatrixHeadersAsync(service, spreadsheetId, values);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify or initialize sheet tab '{SheetName}'.", sheetName);
            throw;
        }
    }

    /// <summary>
    /// Checks for missing Pricing Matrix sheet columns and appends them to the end of Row 1.
    /// Preserves existing column positions and data intact.
    /// </summary>
    private async Task MigratePricingMatrixHeadersAsync(SheetsService service, string spreadsheetId, IList<IList<object>> currentValues)
    {
        try
        {
            if (currentValues == null || currentValues.Count == 0) return;

            // Extract existing headers from Row 1
            var existingHeaders = currentValues[0].Select(h => h?.ToString()?.Trim() ?? string.Empty).ToList();
            
            // Get required 6 columns
            var requiredData = GetDefaultDataForSheet(PricingMatrixSheetName);
            if (requiredData == null || requiredData.Count == 0) return;
            var requiredHeaders = requiredData[0].Select(h => h?.ToString()?.Trim() ?? string.Empty).ToList();

            var updatedHeaders = new List<object>(existingHeaders.Cast<object>());
            var addedAny = false;

            // Append missing headers to the end, leaving existing columns and order untouched
            foreach (var reqHeader in requiredHeaders)
            {
                var exists = updatedHeaders.Any(eh => eh?.ToString()?.Trim().Equals(reqHeader, StringComparison.OrdinalIgnoreCase) == true);
                if (!exists)
                {
                    _logger.LogWarning("Missing header column found: '{Header}'. Appending dynamically on the right of Pricing Matrix sheet Row 1.", reqHeader);
                    updatedHeaders.Add(reqHeader);
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                _logger.LogInformation("Updating Pricing Matrix sheet header row with newly added columns...");
                var valueRange = new ValueRange
                {
                    Values = new List<IList<object>> { updatedHeaders }
                };

                var writeRange = $"'{PricingMatrixSheetName}'!A1";
                var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, writeRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await updateRequest.ExecuteAsync();
                
                _logger.LogInformation("Pricing Matrix sheet header row updated successfully. All pricing columns are active.");
            }
            else
            {
                _logger.LogInformation("All required Pricing Matrix sheet columns are present. No migration needed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform dynamic idempotent Pricing Matrix sheet header columns migration.");
        }
    }

    /// <summary>
    /// Checks for missing Leads sheet columns and appends them to the end of Row 1.
    /// Preserves existing column positions and data intact.
    /// </summary>
    private async Task MigrateLeadsHeadersAsync(SheetsService service, string spreadsheetId, IList<IList<object>> currentValues)
    {
        try
        {
            if (currentValues == null || currentValues.Count == 0) return;

            // Extract existing headers from Row 1
            var existingHeaders = currentValues[0].Select(h => h?.ToString()?.Trim() ?? string.Empty).ToList();
            
            // Get required 19 columns
            var requiredData = GetDefaultDataForSheet(LeadsSheetName);
            if (requiredData == null || requiredData.Count == 0) return;
            var requiredHeaders = requiredData[0].Select(h => h?.ToString()?.Trim() ?? string.Empty).ToList();

            var updatedHeaders = new List<object>(existingHeaders.Cast<object>());
            var addedAny = false;

            // Append missing headers to the end, leaving existing columns and order untouched
            foreach (var reqHeader in requiredHeaders)
            {
                var exists = updatedHeaders.Any(eh => eh?.ToString()?.Trim().Equals(reqHeader, StringComparison.OrdinalIgnoreCase) == true);
                if (!exists)
                {
                    _logger.LogWarning("Missing header column found: '{Header}'. Appending dynamically on the right of Leads sheet Row 1.", reqHeader);
                    updatedHeaders.Add(reqHeader);
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                _logger.LogInformation("Updating Leads sheet header row with newly added lifecycle columns...");
                var valueRange = new ValueRange
                {
                    Values = new List<IList<object>> { updatedHeaders }
                };

                var writeRange = $"'{LeadsSheetName}'!A1";
                var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, writeRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await updateRequest.ExecuteAsync();
                
                _logger.LogInformation("Leads sheet header row updated successfully. All lifecycle columns are active.");
            }
            else
            {
                _logger.LogInformation("All required Leads sheet columns are present. No migration needed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform dynamic idempotent Leads sheet header columns migration.");
        }
    }

    /// <summary>
    /// Generates the default headers and sample rows for each respective sheet tab.
    /// </summary>
    private List<IList<object>> GetDefaultDataForSheet(string sheetName)
    {
        var data = new List<IList<object>>();

        switch (sheetName)
        {
            case LeadsSheetName:
                data.Add(new List<object>
                {
                    "Lead Id",
                    "Customer Name",
                    "Mobile",
                    "Email",
                    "Event Type",
                    "Event Date",
                    "Location",
                    "Guest Count",
                    "Requirements",
                    "Customer Budget",
                    "Estimated Quote Range",
                    "Sales Team Price",
                    "Final Price",
                    "Actual Final Price",
                    "Lead Status",
                    "Sales Owner",
                    "Retell Call Id",
                    "Created Date",
                    "Last Updated Date"
                });
                break;

            case PricingMatrixSheetName:
                data.Add(new List<object> { "Event Type", "Location", "Service", "Pricing Type", "Unit Cost", "Additional Charges" });
                data.Add(new List<object> { "Wedding", "Delhi", "Catering", "PerGuest", 1200, 0 });
                data.Add(new List<object> { "Wedding", "Noida", "Catering", "PerGuest", 1000, 0 });
                data.Add(new List<object> { "Wedding", "Delhi", "Decoration", "FlatRate", 75000, 0 });
                data.Add(new List<object> { "Corporate", "Delhi", "Catering", "PerGuest", 800, 0 });
                data.Add(new List<object> { "Birthday", "Noida", "Decoration", "FlatRate", 25000, 0 });
                data.Add(new List<object> { "", "", "DJ", "FlatRate", 15000, 0 });
                data.Add(new List<object> { "", "", "Photography", "FlatRate", 30000, 0 });
                break;

            case PastQuotesSheetName:
                data.Add(new List<object> { "Event Type", "Location", "Guest Count", "Services Included", "Final Quote" });
                data.Add(new List<object> { "Wedding", "Delhi", 200, "Catering,Decoration", 450000 });
                data.Add(new List<object> { "Wedding", "Noida", 250, "Catering,Decoration,DJ", 520000 });
                break;

            default:
                throw new ArgumentException($"Unknown sheet tab name: '{sheetName}'", nameof(sheetName));
        }

        return data;
    }

    /// <summary>
    /// Writes the newly created spreadsheet ID into local appsettings configuration files.
    /// </summary>
    private async Task SaveSpreadsheetIdToConfigurationAsync(string spreadsheetId)
    {
        var filesToUpdate = new List<string> { "appsettings.json" };
        
        if (_webHostEnvironment.IsDevelopment())
        {
            filesToUpdate.Add("appsettings.Development.json");
        }

        foreach (var fileName in filesToUpdate)
        {
            var filePath = Path.Combine(_webHostEnvironment.ContentRootPath, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Configuration file '{FileName}' was not found at '{Path}'. Skipping.", fileName, filePath);
                continue;
            }

            try
            {
                _logger.LogInformation("Updating SpreadsheetId in configuration file '{FileName}'...", fileName);

                var jsonString = await File.ReadAllTextAsync(filePath);
                var rootNode = System.Text.Json.Nodes.JsonNode.Parse(jsonString);

                if (rootNode is System.Text.Json.Nodes.JsonObject rootObject)
                {
                    if (rootObject.TryGetPropertyValue("GoogleSheets", out var sheetsNode) && sheetsNode is System.Text.Json.Nodes.JsonObject sheetsObject)
                    {
                        sheetsObject["OrangeEventsSpreadsheetId"] = spreadsheetId;
                    }
                    else
                    {
                        // Add section dynamically if missing
                        var newSheetsObject = new System.Text.Json.Nodes.JsonObject
                        {
                            ["OrangeEventsSpreadsheetId"] = spreadsheetId
                        };
                        rootObject["GoogleSheets"] = newSheetsObject;
                    }

                    var writeOptions = new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    var updatedJson = rootObject.ToJsonString(writeOptions);
                    await File.WriteAllTextAsync(filePath, updatedJson);
                    _logger.LogInformation("Successfully persisted OrangeEventsSpreadsheetId in '{FileName}'.", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save dynamic configuration spreadsheet ID inside '{FileName}'.", fileName);
            }
        }
    }

    /// <summary>
    /// Instantiates and authenticates the SheetsService using service account keys.
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
                _logger.LogError("Google credentials file path is not configured.");
                throw new InvalidOperationException("Google credentials path is not configured.");
            }

            if (!Path.IsPathRooted(credentialPath))
            {
                credentialPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, credentialPath);
            }

            if (!File.Exists(credentialPath))
            {
                _logger.LogError("Google credentials JSON file not found at: '{Path}'", credentialPath);
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
