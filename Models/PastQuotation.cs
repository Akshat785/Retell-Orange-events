namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing a historical quotation record.
/// </summary>
public class PastQuotation
{
    public string EventType { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public int GuestCount { get; set; }

    public string ServicesIncluded { get; set; } = string.Empty;

    public decimal FinalQuote { get; set; }
}
