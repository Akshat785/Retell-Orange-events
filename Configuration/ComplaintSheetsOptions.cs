namespace RetellIntegrationApi.Configuration;

/// <summary>
/// Configuration options for Complaint Google Sheets integration.
/// </summary>
public sealed class ComplaintSheetsOptions
{
    public const string ComplaintSheets = "ComplaintSheets";

    /// <summary>
    /// Google Spreadsheet ID for logging complaints. If empty, falls back to the main GoogleSheets:SpreadsheetId.
    /// </summary>
    public string SpreadsheetId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the tab in the spreadsheet where complaints will be logged.
    /// </summary>
    public string SheetName { get; set; } = string.Empty;
}
