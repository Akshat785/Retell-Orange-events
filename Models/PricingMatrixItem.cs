namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing a pricing definition for a service.
/// </summary>
public class PricingMatrixItem
{
    public string EventType { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Service { get; set; } = string.Empty;

    public string PricingType { get; set; } = string.Empty;

    public decimal UnitCost { get; set; }

    public decimal AdditionalCharges { get; set; }
}
