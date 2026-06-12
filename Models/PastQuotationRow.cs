using System;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing a historical quotation from the Past Quotations Google Sheet.
/// </summary>
public sealed class PastQuotationRow
{
    public string QuoteId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public string ServicesIncluded { get; set; } = string.Empty;
    public decimal FinalQuoteAmount { get; set; }
    public string Notes { get; set; } = string.Empty;

    public static PastQuotationRow FromRowValues(System.Collections.Generic.IList<object> row)
    {
        var model = new PastQuotationRow();
        if (row.Count > 0) model.QuoteId = row[0]?.ToString()?.Trim() ?? string.Empty;
        if (row.Count > 1) model.EventType = row[1]?.ToString()?.Trim() ?? string.Empty;
        if (row.Count > 2) model.Location = row[2]?.ToString()?.Trim() ?? string.Empty;
        
        if (row.Count > 3 && int.TryParse(row[3]?.ToString(), out var gc))
        {
            model.GuestCount = gc;
        }
        
        if (row.Count > 4) model.ServicesIncluded = row[4]?.ToString()?.Trim() ?? string.Empty;

        if (row.Count > 5 && decimal.TryParse(row[5]?.ToString(), out var amount))
        {
            model.FinalQuoteAmount = amount;
        }

        if (row.Count > 6) model.Notes = row[6]?.ToString()?.Trim() ?? string.Empty;

        return model;
    }
}
