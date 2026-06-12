using System.Text.Json.Serialization;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Request model representing parameters to update an existing lead with quotation estimate bounds.
/// </summary>
public class UpdateLeadEstimateRequest
{
    private string _leadId = string.Empty;

    public string LeadId 
    { 
        get => _leadId; 
        set => _leadId = value; 
    }

    [JsonPropertyName("lead_id")]
    public string LeadIdSnake 
    { 
        get => _leadId; 
        set => _leadId = value; 
    }

    private string _estimatedQuoteRange = string.Empty;

    public string EstimatedQuoteRange 
    { 
        get => _estimatedQuoteRange; 
        set => _estimatedQuoteRange = value; 
    }

    [JsonPropertyName("estimated_quote_range")]
    public string EstimatedQuoteRangeSnake 
    { 
        get => _estimatedQuoteRange; 
        set => _estimatedQuoteRange = value; 
    }

    private decimal _minimumEstimate;

    public decimal MinimumEstimate 
    { 
        get => _minimumEstimate; 
        set => _minimumEstimate = value; 
    }

    [JsonPropertyName("minimum_estimate")]
    public decimal MinimumEstimateSnake 
    { 
        get => _minimumEstimate; 
        set => _minimumEstimate = value; 
    }

    private decimal _maximumEstimate;

    public decimal MaximumEstimate 
    { 
        get => _maximumEstimate; 
        set => _maximumEstimate = value; 
    }

    [JsonPropertyName("maximum_estimate")]
    public decimal MaximumEstimateSnake 
    { 
        get => _maximumEstimate; 
        set => _maximumEstimate = value; 
    }
}
