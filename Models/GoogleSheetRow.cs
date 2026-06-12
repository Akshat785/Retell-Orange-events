using System.Collections.Generic;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing the row structure to be appended to Google Sheets.
/// </summary>
public sealed class GoogleSheetRow
{
    /// <summary>
    /// Timestamp of when the call ended/webhook was processed.
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// Caller's phone number.
    /// </summary>
    public string CallerNumber { get; set; } = string.Empty;

    /// <summary>
    /// Full transcript of the call.
    /// </summary>
    public string Transcript { get; set; } = string.Empty;

    /// <summary>
    /// Call duration formatted to a human-readable string (e.g. "2m 14s").
    /// </summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary>
    /// Converts the properties of this row into a list format acceptable by Google Sheets API.
    /// </summary>
    public IList<object> ToRowValues()
    {
        return new List<object>
        {
            Timestamp,
            CallerNumber,
            Transcript,
            Duration
        };
    }
}
