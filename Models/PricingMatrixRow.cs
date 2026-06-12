using System;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing a service and its pricing rules from the Pricing Matrix Google Sheet.
/// </summary>
public sealed class PricingMatrixRow
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public string PricingRules { get; set; } = string.Empty; // "PerGuest" or "FlatRate"
    public decimal AdditionalCharges { get; set; }

    public static PricingMatrixRow FromRowValues(System.Collections.Generic.IList<object> row)
    {
        var model = new PricingMatrixRow();
        if (row.Count > 0) model.ServiceName = row[0]?.ToString()?.Trim() ?? string.Empty;
        
        if (row.Count > 1 && decimal.TryParse(row[1]?.ToString(), out var cost))
        {
            model.UnitCost = cost;
        }

        if (row.Count > 2) model.PricingRules = row[2]?.ToString()?.Trim() ?? string.Empty;

        if (row.Count > 3 && decimal.TryParse(row[3]?.ToString(), out var addCharges))
        {
            model.AdditionalCharges = addCharges;
        }

        return model;
    }
}
