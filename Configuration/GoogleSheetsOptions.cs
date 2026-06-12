namespace RetellIntegrationApi.Configuration;

/// <summary>
/// Configuration options for Google Sheets integration.
/// </summary>
public sealed class GoogleSheetsOptions
{
    public const string GoogleSheets = "GoogleSheets";

    public string SpreadsheetId { get; set; } = string.Empty;
    public string OrangeEventsSpreadsheetId { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public string CredentialsJsonPath { get; set; } = string.Empty;
}
