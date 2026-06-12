using System.Collections.Generic;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing the row structure to be appended to the Complaint Logs Google Sheet.
/// </summary>
public sealed class ComplaintSheetRow
{
    /// <summary>
    /// Name of the customer filing the complaint.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Validated 10-digit Indian mobile number of the customer.
    /// </summary>
    public string MobileNumber { get; set; } = string.Empty;

    /// <summary>
    /// Type/category of complaint (e.g. billing, technical, service).
    /// </summary>
    public string ComplaintType { get; set; } = string.Empty;

    /// <summary>
    /// Descriptive details of the complaint.
    /// </summary>
    public string ComplaintDescription { get; set; } = string.Empty;

    /// <summary>
    /// Physical address or billing location.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Date and time when the complaint was recorded.
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// Converts the properties of this complaint row into a list format acceptable by Google Sheets API.
    /// Columns: Customer Name | Mobile Number | Complaint Type | Complaint Description | Address | Timestamp
    /// </summary>
    public IList<object> ToRowValues()
    {
        return new List<object>
        {
            CustomerName,
            MobileNumber,
            ComplaintType,
            ComplaintDescription,
            Address,
            Timestamp
        };
    }
}
