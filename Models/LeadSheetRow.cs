using System;
using System.Collections.Generic;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing a row of data inside the Leads Google Sheet.
/// </summary>
public sealed class LeadSheetRow
{
    public string LeadId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public string Requirements { get; set; } = string.Empty;
    public string EstimatedRange { get; set; } = string.Empty;
    public string RetellCallId { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;

    public IList<object> ToRowValues()
    {
        return new List<object>
        {
            LeadId,
            Name,
            Mobile,
            Email,
            EventType,
            EventDate,
            Location,
            GuestCount,
            Requirements,
            EstimatedRange,
            RetellCallId,
            CreatedDate
        };
    }

    public static List<string> GetHeaders()
    {
        return new List<string>
        {
            "Lead Id",
            "Name",
            "Mobile",
            "Email",
            "Event Type",
            "Event Date",
            "Location",
            "Guest Count",
            "Requirements",
            "Estimated Range",
            "Retell Call Id",
            "Created Date"
        };
    }
}
