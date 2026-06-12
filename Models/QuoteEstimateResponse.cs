namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing the estimated range and notes returned by the estimation endpoint.
/// </summary>
public class QuoteEstimateResponse
{
    public string EstimatedRange { get; set; } = string.Empty;

    public decimal MinimumEstimate { get; set; }

    public decimal MaximumEstimate { get; set; }

    public string Message { get; set; } = string.Empty;
}
