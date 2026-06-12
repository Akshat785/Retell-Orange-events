namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing the outcome of the Google Sheets setup operation.
/// </summary>
public sealed class SetupResult
{
    /// <summary>
    /// The ID of the configured Google Spreadsheet.
    /// </summary>
    public string SpreadsheetId { get; set; } = string.Empty;

    /// <summary>
    /// The direct browser URL to access the configured Google Spreadsheet.
    /// </summary>
    public string SpreadsheetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether a brand new spreadsheet was created.
    /// </summary>
    public bool IsNewSpreadsheet { get; set; }

    /// <summary>
    /// Indicates whether the spreadsheet was already configured and verified.
    /// </summary>
    public bool IsAlreadyConfigured { get; set; }
}
