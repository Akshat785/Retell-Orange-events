using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing the incoming event quotation request from Retell AI.
/// </summary>
public sealed class QuoteRequestModel
{
    [JsonPropertyName("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("mobile")]
    public string Mobile { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("event_date")]
    public string EventDate { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("guest_count")]
    public int GuestCount { get; set; }

    [JsonPropertyName("requirements")]
    public List<string> Requirements { get; set; } = new();
    
    [JsonPropertyName("call_id")]
    public string CallId { get; set; } = string.Empty;
}
